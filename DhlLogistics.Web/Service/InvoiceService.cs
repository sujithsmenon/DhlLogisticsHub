namespace DhlLogistics.Web.Service;

using System.Text.RegularExpressions;
using DhlLogistics.Shared.Models;
using DhlLogistics.Web.Database;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.EntityFrameworkCore;
using iText.Kernel.Pdf;
using iText.IO.Font.Constants;
using iText.IO.Image;
using iText.Kernel.Font;
using iText.Layout;
using iText.Layout.Element;
using iText.Layout.Properties;
using iText.Layout.Borders;
using iText.Kernel.Colors;

/// <summary>
/// Invoice layer over the existing <see cref="Bill"/>. The Bill IS the ERP invoice —
/// this service only (a) marks a Bill as issued + records invoice metadata, (b) generates
/// the customer-invoice PDF from Bill data, and (c) stores/serves uploaded documents
/// (vendor invoices, credit / debit notes). No duplicate charges / totals / GST / accounting.
/// </summary>
public partial class InvoiceService
{
    private readonly AppDbContext _db;
    private readonly AuthenticationStateProvider _auth;
    private readonly IWebHostEnvironment _env;
    private readonly DashboardState _dash;
    private readonly ILogger<InvoiceService> _log;
    private readonly CompanyDetailsService _companyDetails;

    public InvoiceService(AppDbContext db, AuthenticationStateProvider auth,
                          IWebHostEnvironment env, DashboardState dash,
                          ILogger<InvoiceService> log, CompanyDetailsService companyDetails)
    {
        _db = db;
        _auth = auth;
        _env = env;
        _dash = dash;
        _log = log;
        _companyDetails = companyDetails;
    }

    private string StorageRoot => Path.Combine(_env.ContentRootPath, "App_Data", "invoices");

    private async Task<string> CurrentUserAsync()
    {
        var s = await _auth.GetAuthenticationStateAsync();
        return s.User?.Identity?.Name ?? "system";
    }

    // ── Queries ──────────────────────────────────────────────────────────────

    public Task<List<InvoiceDocument>> GetDocumentsAsync(long billId) =>
        _db.InvoiceDocuments.Where(d => d.BillId == billId)
            .OrderByDescending(d => d.UploadedDate).ToListAsync();

    public Task<InvoiceDocument?> GetDocumentAsync(long docId) =>
        _db.InvoiceDocuments.FirstOrDefaultAsync(d => d.Id == docId);

    /// <summary>Reads a stored document's bytes for download / inline display.
    /// Source of truth is the DB (<see cref="InvoiceDocument.Content"/>); falls back to the
    /// legacy on-disk file only for rows created before content was stored in the DB.</summary>
    public async Task<(byte[] Bytes, string ContentType, string FileName)?> GetFileAsync(long docId)
    {
        var doc = await GetDocumentAsync(docId);
        if (doc is null) return null;

        if (doc.Content is { Length: > 0 })
            return (doc.Content, doc.ContentType, doc.OriginalFileName);

        // Legacy fallback: file stored on this host's disk (pre-DB-content rows).
        if (!string.IsNullOrEmpty(doc.FilePath))
        {
            var full = Path.Combine(StorageRoot, doc.FilePath);
            if (File.Exists(full))
                return (await File.ReadAllBytesAsync(full), doc.ContentType, doc.OriginalFileName);
        }
        _log.LogWarning("Invoice document {DocId} has no DB content and no disk file ({Path}).", docId, doc.FilePath);
        return null;
    }

    // ── Issue (generate customer invoice from the Bill) ───────────────────────

    /// <summary>
    /// Issues the Bill as a customer invoice: sets invoice metadata, generates the PDF
    /// from Bill data, and records a <see cref="InvoiceDocumentType.CustomerInvoice"/> document.
    /// Re-issuing supersedes the previous customer invoice and bumps the version.
    /// Only an <see cref="BillStatus.Approved"/> (or later) Bill can be issued.
    /// </summary>
    public async Task<Bill> IssueInvoiceAsync(long billId, string? paymentTerms = null,
                                              string? remarks = null, string? actor = null)
    {
        var bill = await _db.Bills
            .Include(b => b.BillingClient)
            .Include(b => b.Branch)
            .Include(b => b.Currency)
            .Include(b => b.JobOrder).ThenInclude(j => j!.Shipper)
            .Include(b => b.JobOrder).ThenInclude(j => j!.Consignee)
            .Include(b => b.JobOrder).ThenInclude(j => j!.LoadPort)
            .Include(b => b.JobOrder).ThenInclude(j => j!.DischargePort)
            .Include(b => b.JobOrder).ThenInclude(j => j!.Commodity)
            .Include(b => b.Charges).ThenInclude(c => c.Sac)
            .Include(b => b.Charges).ThenInclude(c => c.ChargeCode)
            .Include(b => b.InvoiceDocuments)
            .Include(b => b.CustomerInvoice)          // for the double-invoice guard's message
            .FirstOrDefaultAsync(b => b.Id == billId)
            ?? throw new InvalidOperationException("Bill not found.");

        if (bill.Status is BillStatus.Draft or BillStatus.Submitted or BillStatus.Verified)
            throw new InvalidOperationException("Only an approved bill can be issued as an invoice.");
        if (bill.TotalAmount <= 0)
            throw new InvalidOperationException("Cannot issue an invoice for a zero-value bill.");
        // Double-invoice guard: this bill is already consolidated onto a CustomerInvoice. Issuing it again
        // on its own would bill the customer twice for the same charges. (CustomerInvoiceService.CancelAsync
        // releases the bill if the consolidated invoice is cancelled.)
        if (bill.CustomerInvoiceId is not null)
            throw new InvalidOperationException(
                $"Bill {bill.BillNo} is already included in customer invoice " +
                $"{bill.CustomerInvoice?.InvoiceNo ?? bill.CustomerInvoiceId.ToString()} and cannot be issued separately.");

        var user = actor ?? await CurrentUserAsync();

        // Invoice metadata (reuses the bill's own FY/number — INV mirrors the bill number).
        bill.InvoiceNumber = ToInvoiceNumber(bill.BillNo);
        bill.InvoiceDate   = DateTime.UtcNow.Date;
        bill.IssueDate     = DateTime.UtcNow.Date;
        bill.IsIssued      = true;
        bill.InvoiceStatus = InvoiceStatus.Issued;
        if (!string.IsNullOrWhiteSpace(paymentTerms)) bill.PaymentTerms = paymentTerms;
        if (remarks is not null)                       bill.InvoiceRemarks = remarks;
        bill.DueDate = ComputeDueDate(bill.IssueDate.Value, bill.PaymentTerms);

        // Supersede any previous active customer invoice.
        var prior = bill.InvoiceDocuments
            .Where(d => d.DocumentType == InvoiceDocumentType.CustomerInvoice && d.IsActive)
            .ToList();
        foreach (var p in prior) p.IsActive = false;
        var nextVersion = bill.InvoiceDocuments
            .Where(d => d.DocumentType == InvoiceDocumentType.CustomerInvoice)
            .Select(d => d.Version).DefaultIfEmpty(0).Max() + 1;

        // Generate the PDF into memory — the bytes are the source of truth (stored in the DB),
        // so issuing never depends on a writable/persistent local filesystem (EB is neither).
        var storedName   = $"{Guid.NewGuid():N}.pdf";
        var friendlyName = $"Invoice-{bill.InvoiceNumber?.Replace('/', '-')}-v{nextVersion}.pdf";
        var relPath      = $"{bill.Id}/{storedName}";
        var company      = await _companyDetails.GetOrCreateAsync();
        var pdfBytes     = GeneratePdf(bill, company);
        bill.InvoicePdfPath = relPath;

        // Best-effort local copy (nice for on-box inspection; failure is non-fatal — the DB has it).
        TryWriteToDisk(relPath, pdfBytes);

        _db.InvoiceDocuments.Add(new InvoiceDocument
        {
            BillId           = bill.Id,
            DocumentType     = InvoiceDocumentType.CustomerInvoice,
            FileName         = storedName,
            OriginalFileName = friendlyName,
            FilePath         = relPath,
            Content          = pdfBytes,
            ContentType      = "application/pdf",
            UploadedBy       = user,
            UploadedDate     = DateTime.UtcNow,
            Version          = nextVersion,
            IsActive         = true,
        });

        await _db.SaveChangesAsync();
        _log.LogInformation("Issued invoice {InvoiceNo} for bill {BillNo} ({Bytes} bytes stored in DB).",
            bill.InvoiceNumber, bill.BillNo, pdfBytes.Length);
        _dash.NotifyChanged();
        return bill;
    }

    // ── Upload (vendor invoice / credit / debit note) ─────────────────────────

    public async Task<InvoiceDocument> UploadDocumentAsync(long billId, Stream content,
        string originalFileName, string contentType, InvoiceDocumentType type, string? actor = null)
    {
        var bill = await _db.Bills.FirstOrDefaultAsync(b => b.Id == billId)
            ?? throw new InvalidOperationException("Bill not found.");
        var user = actor ?? await CurrentUserAsync();

        // Read the upload fully into memory → stored in the DB (env-agnostic, like invoices).
        using var ms = new MemoryStream();
        await content.CopyToAsync(ms);
        var bytes = ms.ToArray();

        var ext        = Path.GetExtension(originalFileName);
        var storedName = $"{Guid.NewGuid():N}{ext}";
        var relPath    = $"{bill.Id}/{storedName}";
        TryWriteToDisk(relPath, bytes);   // best-effort local copy

        var nextVersion = await _db.InvoiceDocuments
            .Where(d => d.BillId == billId && d.DocumentType == type)
            .Select(d => d.Version).DefaultIfEmpty(0).MaxAsync() + 1;

        var doc = new InvoiceDocument
        {
            BillId           = bill.Id,
            DocumentType     = type,
            FileName         = storedName,
            OriginalFileName = originalFileName,
            FilePath         = relPath,
            Content          = bytes,
            ContentType      = string.IsNullOrWhiteSpace(contentType) ? "application/octet-stream" : contentType,
            UploadedBy       = user,
            UploadedDate     = DateTime.UtcNow,
            Version          = nextVersion,
            IsActive         = true,
        };
        _db.InvoiceDocuments.Add(doc);
        await _db.SaveChangesAsync();
        return doc;
    }

    public async Task DeactivateDocumentAsync(long docId)
    {
        var doc = await GetDocumentAsync(docId);
        if (doc is null) return;
        doc.IsActive = false;
        await _db.SaveChangesAsync();
    }

    /// <summary>Best-effort local copy of a document. Never throws — the DB holds the
    /// authoritative bytes, so a read-only/locked-down app filesystem (EB) is harmless.</summary>
    private void TryWriteToDisk(string relPath, byte[] bytes)
    {
        try
        {
            var full = Path.Combine(StorageRoot, relPath);
            Directory.CreateDirectory(Path.GetDirectoryName(full)!);
            File.WriteAllBytes(full, bytes);
        }
        catch (Exception ex)
        {
            _log.LogDebug(ex, "Local invoice copy skipped for {Path} (non-fatal; DB has the bytes).", relPath);
        }
    }

    /// <summary>One-time migration for legacy rows: copies any on-disk PDF into the DB
    /// <see cref="InvoiceDocument.Content"/> column so documents become environment-independent.
    /// Idempotent — only touches rows with null content and a readable file on THIS host.</summary>
    public async Task<int> BackfillContentFromDiskAsync()
    {
        var legacy = await _db.InvoiceDocuments
            .Where(d => d.Content == null && d.FilePath != "")
            .ToListAsync();
        int migrated = 0;
        foreach (var d in legacy)
        {
            var full = Path.Combine(StorageRoot, d.FilePath);
            if (!File.Exists(full)) continue;
            d.Content = await File.ReadAllBytesAsync(full);
            migrated++;
        }
        if (migrated > 0) await _db.SaveChangesAsync();
        return migrated;
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static string ToInvoiceNumber(string billNo)
    {
        if (string.IsNullOrWhiteSpace(billNo)) return "INV";
        var slash = billNo.IndexOf('/');
        return slash > 0 ? "INV" + billNo[slash..] : "INV/" + billNo;
    }

    private static DateTime? ComputeDueDate(DateTime issueDate, string? paymentTerms)
    {
        if (string.IsNullOrWhiteSpace(paymentTerms)) return null;
        var m = Regex.Match(paymentTerms, @"\d+");
        return m.Success ? issueDate.AddDays(int.Parse(m.Value)) : (DateTime?)null;
    }

    // ── Company header details (issuer) — sourced from the CompanyDetails master. ──
    // Composes the single-line header/footer strings the layout expects from the record's fields.
    private static string CoAddrLine(CompanyDetails c) => string.Join(", ", new[]
        { c.AddressLine1, c.AddressLine2, c.City, c.State, c.Pincode, c.Country }
        .Where(s => !string.IsNullOrWhiteSpace(s)));

    private static string CoContactLine(CompanyDetails c)
    {
        var parts = new List<string>();
        var tel = string.Join(" / ", new[] { c.Phone, c.Mobile }.Where(s => !string.IsNullOrWhiteSpace(s)));
        if (!string.IsNullOrWhiteSpace(tel))         parts.Add("Tel: " + tel);
        if (!string.IsNullOrWhiteSpace(c.Email))     parts.Add("Email: " + c.Email);
        if (!string.IsNullOrWhiteSpace(c.Website))   parts.Add(c.Website!);
        return string.Join("  ·  ", parts);
    }

    private static string CoGstinLine(CompanyDetails c)
    {
        var parts = new List<string>();
        if (!string.IsNullOrWhiteSpace(c.GSTIN)) parts.Add("GSTIN: " + c.GSTIN);
        if (!string.IsNullOrWhiteSpace(c.PAN))   parts.Add("PAN: "   + c.PAN);
        return string.Join("  ·  ", parts);
    }

    /// <summary>Builds the professional customer-invoice PDF from Bill (+ its Job) data,
    /// returning the bytes (stored in the DB — no dependency on the local filesystem).
    /// Issuer/branding text comes from the <see cref="CompanyDetails"/> master.</summary>
    private byte[] GeneratePdf(Bill bill, CompanyDetails co)
    {
        var CoName    = co.CompanyName;
        var CoTagline = co.Tagline ?? string.Empty;
        var CoAddr    = CoAddrLine(co);
        var CoContact = CoContactLine(co);
        var CoGstin   = CoGstinLine(co);
        var CoFooter  = string.IsNullOrWhiteSpace(co.InvoiceFooter) ? "This is a computer-generated invoice." : co.InvoiceFooter!;
        var CoSignatory = string.IsNullOrWhiteSpace(co.AuthorisedSignatory) ? "Authorised Signatory" : co.AuthorisedSignatory!;
        var bold   = PdfFontFactory.CreateFont(StandardFonts.HELVETICA_BOLD);
        var normal = PdfFontFactory.CreateFont(StandardFonts.HELVETICA);
        var teal   = new DeviceRgb(25, 135, 129);
        var navy   = new DeviceRgb(14, 42, 71);
        var light  = new DeviceRgb(240, 244, 247);
        var line   = new SolidBorder(new DeviceRgb(210, 216, 224), 0.8f);
        var cur    = bill.Currency?.CurrencyCode ?? "INR";

        using var ms     = new MemoryStream();
        using var writer = new PdfWriter(ms);
        using var pdf    = new PdfDocument(writer);
        var doc          = new Document(pdf);
        doc.SetMargins(34, 34, 34, 34);

        // ── Header: logo (left) + company details (right) ──────────────────────
        var header = new Table(UnitValue.CreatePercentArray(new float[] { 1.1f, 2f })).UseAllAvailableWidth();
        var logoCell = new Cell().SetBorder(Border.NO_BORDER).SetVerticalAlignment(VerticalAlignment.MIDDLE);
        // Use the profile's uploaded logo when present; otherwise fall back to the bundled logo.
        var webRoot  = _env.WebRootPath ?? "";
        var logoPath = Path.Combine(webRoot, "img", "pvgt-logo.png");
        if (!string.IsNullOrWhiteSpace(co.LogoPath))
        {
            var uploaded = Path.Combine(webRoot, co.LogoPath.Replace('/', Path.DirectorySeparatorChar));
            if (File.Exists(uploaded)) logoPath = uploaded;
        }
        if (File.Exists(logoPath))
            logoCell.Add(new Image(ImageDataFactory.Create(logoPath)).ScaleToFit(150, 70));
        else
            logoCell.Add(new Paragraph(CoName).SetFont(bold).SetFontSize(16).SetFontColor(navy));
        header.AddCell(logoCell);
        header.AddCell(new Cell().SetBorder(Border.NO_BORDER).SetTextAlignment(TextAlignment.RIGHT)
            .Add(new Paragraph(CoName).SetFont(bold).SetFontSize(13).SetFontColor(navy).SetMarginBottom(1))
            .Add(new Paragraph(CoTagline).SetFont(normal).SetFontSize(7.5f).SetMarginBottom(2))
            .Add(new Paragraph(CoAddr).SetFont(normal).SetFontSize(8).SetMarginBottom(1))
            .Add(new Paragraph(CoContact).SetFont(normal).SetFontSize(8).SetMarginBottom(1))
            .Add(new Paragraph(CoGstin).SetFont(bold).SetFontSize(8)));
        doc.Add(header);

        // ── Title band ─────────────────────────────────────────────────────────
        doc.Add(new Paragraph("TAX INVOICE").SetFont(bold).SetFontSize(14).SetFontColor(ColorConstants.WHITE)
            .SetBackgroundColor(teal).SetTextAlignment(TextAlignment.CENTER).SetPadding(5).SetMarginTop(10));

        // ── Bill-To (left) + invoice meta (right) ──────────────────────────────
        var client = bill.BillingClient;
        var info = new Table(UnitValue.CreatePercentArray(new float[] { 1.3f, 1f })).UseAllAvailableWidth().SetMarginTop(8);
        var billTo = new Cell().SetBorder(line).SetPadding(7)
            .Add(new Paragraph("BILL TO").SetFont(bold).SetFontSize(8).SetFontColor(teal).SetMarginBottom(2))
            .Add(new Paragraph(client?.CompanyName ?? "-").SetFont(bold).SetFontSize(10))
            .Add(new Paragraph(client?.Address ?? "").SetFont(normal).SetFontSize(8.5f));
        if (!string.IsNullOrWhiteSpace(client?.Phone))
            billTo.Add(new Paragraph("Phone: " + client!.Phone).SetFont(normal).SetFontSize(8.5f));
        if (!string.IsNullOrWhiteSpace(client?.ContactEmail))
            billTo.Add(new Paragraph("Email: " + client!.ContactEmail).SetFont(normal).SetFontSize(8.5f));
        info.AddCell(billTo);

        var metaTbl = new Table(UnitValue.CreatePercentArray(new float[] { 1, 1.2f })).SetBorder(line);
        MetaRow(metaTbl, bold, normal, "Invoice No", bill.InvoiceNumber ?? "-");
        // Customer's own invoice reference — printed beneath the legal Invoice No, only when present.
        if (!string.IsNullOrWhiteSpace(bill.CustomerInvoiceNumber))
            MetaRow(metaTbl, bold, normal, "Customer Invoice", bill.CustomerInvoiceNumber!);
        MetaRow(metaTbl, bold, normal, "Invoice Date", bill.InvoiceDate?.ToString("dd-MMM-yyyy") ?? "-");
        MetaRow(metaTbl, bold, normal, "Due Date", bill.DueDate?.ToString("dd-MMM-yyyy") ?? "-");
        MetaRow(metaTbl, bold, normal, "Bill No", bill.BillNo);
        MetaRow(metaTbl, bold, normal, "Job No", bill.JobOrder?.JobOrderNo ?? "-");
        MetaRow(metaTbl, bold, normal, "Branch", bill.Branch?.BranchName ?? "-");
        info.AddCell(new Cell().SetBorder(Border.NO_BORDER).SetPaddingLeft(6).Add(metaTbl));
        doc.Add(info);

        // ── Shipment details (from the linked Job) ─────────────────────────────
        var job = bill.JobOrder;
        if (job is not null)
        {
            var ship = new Table(UnitValue.CreatePercentArray(new float[] { 1, 2, 1, 2 })).UseAllAvailableWidth()
                .SetMarginTop(8).SetBorder(line);
            ship.AddCell(SpanHead("SHIPMENT DETAILS", bold, teal));
            ShipCell(ship, bold, normal, "Mode / Type", $"{job.ShipmentMode} / {job.ShipmentType} ({job.CargoType})");
            ShipCell(ship, bold, normal, "Commodity", job.Commodity?.CommodityName ?? "-");
            ShipCell(ship, bold, normal, "Shipper", job.Shipper?.CompanyName ?? "-");
            ShipCell(ship, bold, normal, "Consignee", job.Consignee?.CompanyName ?? "-");
            ShipCell(ship, bold, normal, "Load Port", job.LoadPort?.PortName ?? "-");
            ShipCell(ship, bold, normal, "Discharge Port", job.DischargePort?.PortName ?? "-");
            ShipCell(ship, bold, normal, "Gross Wt (kg)", job.GrossWeightKg?.ToString("N2") ?? "-");
            ShipCell(ship, bold, normal, "Volume (CBM)", job.VolumeCbm?.ToString("N3") ?? "-");
            ShipCell(ship, bold, normal, "Est. Cargo Value", job.EstimatedValue.HasValue ? $"{job.EstimatedValue.Value:N2} {cur}" : "-");
            ShipCell(ship, bold, normal, "Currency", job.Currency?.CurrencyCode ?? cur);
            if (!string.IsNullOrWhiteSpace(job.Remarks))
            {
                ship.AddCell(new Cell().SetBorder(Border.NO_BORDER).SetPadding(3)
                    .Add(new Paragraph("Remarks").SetFont(bold).SetFontSize(7.5f).SetFontColor(new DeviceRgb(90, 101, 115))));
                ship.AddCell(new Cell(1, 3).SetBorder(Border.NO_BORDER).SetPadding(3)
                    .Add(new Paragraph(job.Remarks).SetFont(normal).SetFontSize(8.5f)));
            }
            doc.Add(ship);
        }

        // ── Charge lines ───────────────────────────────────────────────────────
        var t = new Table(UnitValue.CreatePercentArray(new float[] { 0.5f, 3.6f, 1.2f, 1f, 1.3f, 1f, 1.4f, 1.6f }))
            .UseAllAvailableWidth().SetMarginTop(10);
        foreach (var h in new[] { "#", "Description", "SAC", "Qty", "Rate", "GST%", "GST Amt", "Net Amount" })
            t.AddHeaderCell(new Cell().Add(new Paragraph(h).SetFont(bold).SetFontSize(8.5f).SetFontColor(ColorConstants.WHITE))
                .SetBackgroundColor(navy).SetPadding(5));

        // Presentation only: charges are grouped under their originating operation (Customs Clearance,
        // Freight, Transportation, …) using the OperationName snapshot copied onto each line at bill
        // generation. Lines with no operation fall under a single "General Charges" band. Grouping keeps
        // first-appearance order and per-line DisplayOrder — it does not change any amount, GST or total.
        int n = 0;
        var groups = bill.Charges
            .OrderBy(x => x.DisplayOrder)
            .GroupBy(x => string.IsNullOrWhiteSpace(x.OperationName) ? "" : x.OperationName!);

        foreach (var g in groups)
        {
            var groupLabel = string.IsNullOrWhiteSpace(g.Key) ? "General Charges" : g.Key;
            t.AddCell(new Cell(1, 8)
                .Add(new Paragraph(groupLabel).SetFont(bold).SetFontSize(8.5f).SetFontColor(navy))
                .SetBackgroundColor(light).SetPadding(4).SetBorder(line));

            foreach (var c in g)
            {
                var desc = string.IsNullOrWhiteSpace(c.Description) ? (c.ChargeCode?.ChargeName ?? "-") : c.Description;
                t.AddCell(BodyCell(normal, (++n).ToString(), TextAlignment.CENTER));
                t.AddCell(BodyCell(normal, desc, TextAlignment.LEFT));
                t.AddCell(BodyCell(normal, c.Sac?.SacCode ?? "", TextAlignment.LEFT));
                t.AddCell(BodyCell(normal, c.Quantity.ToString("N2"), TextAlignment.RIGHT));
                t.AddCell(BodyCell(normal, c.Rate.ToString("N2"), TextAlignment.RIGHT));
                t.AddCell(BodyCell(normal, c.GstRate.ToString("N2"), TextAlignment.RIGHT));
                t.AddCell(BodyCell(normal, c.GstAmount.ToString("N2"), TextAlignment.RIGHT));
                t.AddCell(BodyCell(normal, c.NetAmount.ToString("N2"), TextAlignment.RIGHT));
            }
        }
        doc.Add(t);

        // ── Amount in words (left) + GST summary / grand total (right) ─────────
        var summary = new Table(UnitValue.CreatePercentArray(new float[] { 1.6f, 1f })).UseAllAvailableWidth().SetMarginTop(8);
        summary.AddCell(new Cell().SetBorder(line).SetPadding(7).SetVerticalAlignment(VerticalAlignment.TOP)
            .Add(new Paragraph("Amount in Words").SetFont(bold).SetFontSize(8).SetFontColor(teal))
            .Add(new Paragraph(AmountInWords(bill.TotalAmount, cur)).SetFont(bold).SetFontSize(9)));

        var totals = new Table(UnitValue.CreatePercentArray(new float[] { 1.2f, 1f })).SetBorder(Border.NO_BORDER);
        AddTotal(totals, bold, normal, "Sub Total", $"{bill.SubTotal:N2}", light, false);
        AddTotal(totals, bold, normal, "GST", $"{bill.GstAmount:N2}", light, false);
        AddTotal(totals, bold, normal, $"Grand Total ({cur})", $"{bill.TotalAmount:N2}", teal, true);
        summary.AddCell(new Cell().SetBorder(Border.NO_BORDER).SetPadding(0).Add(totals));
        doc.Add(summary);

        // ── Payment terms + remarks ────────────────────────────────────────────
        var notes = new Table(UnitValue.CreatePercentArray(new float[] { 1, 1 })).UseAllAvailableWidth().SetMarginTop(8);
        notes.AddCell(new Cell().SetBorder(line).SetPadding(6)
            .Add(new Paragraph("Payment Terms").SetFont(bold).SetFontSize(8).SetFontColor(teal))
            .Add(new Paragraph(string.IsNullOrWhiteSpace(bill.PaymentTerms) ? "Due on receipt" : bill.PaymentTerms!)
                .SetFont(normal).SetFontSize(8.5f)));
        notes.AddCell(new Cell().SetBorder(line).SetPadding(6)
            .Add(new Paragraph("Remarks").SetFont(bold).SetFontSize(8).SetFontColor(teal))
            .Add(new Paragraph(string.IsNullOrWhiteSpace(bill.InvoiceRemarks) ? "-" : bill.InvoiceRemarks!)
                .SetFont(normal).SetFontSize(8.5f)));
        doc.Add(notes);

        // ── Authorised signature ───────────────────────────────────────────────
        var sign = new Table(UnitValue.CreatePercentArray(new float[] { 1.4f, 1f })).UseAllAvailableWidth().SetMarginTop(20);
        sign.AddCell(new Cell().SetBorder(Border.NO_BORDER)
            .Add(new Paragraph(CoFooter).SetFont(normal).SetFontSize(7.5f).SetFontColor(ColorConstants.GRAY)));
        sign.AddCell(new Cell().SetBorder(Border.NO_BORDER).SetTextAlignment(TextAlignment.CENTER).SetPaddingTop(26)
            .Add(new Paragraph("_______________________").SetFont(normal).SetFontSize(9))
            .Add(new Paragraph(CoSignatory).SetFont(bold).SetFontSize(8.5f))
            .Add(new Paragraph("for " + CoName).SetFont(normal).SetFontSize(7.5f)));
        doc.Add(sign);

        doc.Add(new Paragraph($"Generated on {DateTime.Now:dd-MMM-yyyy HH:mm}")
            .SetFont(normal).SetFontSize(7).SetFontColor(ColorConstants.GRAY).SetTextAlignment(TextAlignment.CENTER).SetMarginTop(10));

        doc.Close();               // flushes writer → ms
        return ms.ToArray();       // valid after close; DB stores these bytes
    }

    // ── PDF cell helpers ───────────────────────────────────────────────────────
    private static void MetaRow(Table t, PdfFont bold, PdfFont normal, string label, string value)
    {
        t.AddCell(new Cell().SetBorder(Border.NO_BORDER).SetPadding(2)
            .Add(new Paragraph(label).SetFont(bold).SetFontSize(8).SetFontColor(new DeviceRgb(90, 101, 115))));
        t.AddCell(new Cell().SetBorder(Border.NO_BORDER).SetPadding(2)
            .Add(new Paragraph(value).SetFont(bold).SetFontSize(8.5f)));
    }

    private static Cell SpanHead(string text, PdfFont bold, Color teal) =>
        new Cell(1, 4).SetBorder(Border.NO_BORDER).SetBackgroundColor(new DeviceRgb(240, 244, 247)).SetPadding(4)
            .Add(new Paragraph(text).SetFont(bold).SetFontSize(8).SetFontColor(teal));

    private static void ShipCell(Table t, PdfFont bold, PdfFont normal, string label, string value)
    {
        t.AddCell(new Cell().SetBorder(Border.NO_BORDER).SetPadding(3)
            .Add(new Paragraph(label).SetFont(bold).SetFontSize(7.5f).SetFontColor(new DeviceRgb(90, 101, 115))));
        t.AddCell(new Cell().SetBorder(Border.NO_BORDER).SetPadding(3)
            .Add(new Paragraph(value).SetFont(normal).SetFontSize(8.5f)));
    }

    private static Cell BodyCell(PdfFont font, string text, TextAlignment align) =>
        new Cell().Add(new Paragraph(text).SetFont(font).SetFontSize(8).SetTextAlignment(align)).SetPadding(4);

    private static void AddTotal(Table t, PdfFont bold, PdfFont normal, string label, string value, Color bg, bool strong)
    {
        var lf = strong ? bold : normal;
        t.AddCell(new Cell().Add(new Paragraph(label).SetFont(lf).SetFontSize(9).SetTextAlignment(TextAlignment.RIGHT))
            .SetBackgroundColor(bg).SetFontColor(strong ? ColorConstants.WHITE : ColorConstants.BLACK).SetPadding(5).SetBorder(Border.NO_BORDER));
        t.AddCell(new Cell().Add(new Paragraph(value).SetFont(bold).SetFontSize(9).SetTextAlignment(TextAlignment.RIGHT))
            .SetBackgroundColor(bg).SetFontColor(strong ? ColorConstants.WHITE : ColorConstants.BLACK).SetPadding(5).SetBorder(Border.NO_BORDER));
    }

    // ── Amount in words (Indian numbering: Crore / Lakh / Thousand) ─────────────
    private static readonly string[] _ones =
        { "", "One","Two","Three","Four","Five","Six","Seven","Eight","Nine","Ten","Eleven","Twelve",
          "Thirteen","Fourteen","Fifteen","Sixteen","Seventeen","Eighteen","Nineteen" };
    private static readonly string[] _tens =
        { "", "", "Twenty","Thirty","Forty","Fifty","Sixty","Seventy","Eighty","Ninety" };

    private static string TwoDigits(int nn) =>
        nn < 20 ? _ones[nn] : (_tens[nn / 10] + (nn % 10 > 0 ? " " + _ones[nn % 10] : ""));

    private static string IntToIndianWords(long num)
    {
        if (num == 0) return "Zero";
        var parts = new List<string>();
        long crore = num / 10000000; num %= 10000000;
        long lakh  = num / 100000;   num %= 100000;
        long thou  = num / 1000;      num %= 1000;
        long hund  = num / 100;       int rest = (int)(num % 100);
        if (crore > 0) parts.Add(IntToIndianWords(crore) + " Crore");
        if (lakh  > 0) parts.Add(TwoDigits((int)lakh) + " Lakh");
        if (thou  > 0) parts.Add(TwoDigits((int)thou) + " Thousand");
        if (hund  > 0) parts.Add(_ones[hund] + " Hundred");
        if (rest  > 0) parts.Add(TwoDigits(rest));
        return string.Join(" ", parts);
    }

    private static string AmountInWords(decimal amount, string currency)
    {
        var word = currency == "INR" ? "Rupees" : currency;
        long whole = (long)Math.Floor(amount);
        int  frac  = (int)Math.Round((amount - whole) * 100m);
        var sb = new System.Text.StringBuilder();
        sb.Append(word).Append(' ').Append(IntToIndianWords(whole));
        if (frac > 0) sb.Append(" and ").Append(IntToIndianWords(frac)).Append(" Paise");
        return sb.Append(" Only").ToString();
    }
}
