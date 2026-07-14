namespace DhlLogistics.Web.Service;

using DhlLogistics.Shared.Models;
using Microsoft.EntityFrameworkCore;
using iText.Kernel.Pdf;
using iText.Kernel.Pdf.Navigation;
using iText.IO.Font.Constants;
using iText.IO.Image;
using iText.Kernel.Font;
using iText.Layout;
using iText.Layout.Element;
using iText.Layout.Properties;
using iText.Layout.Borders;
using iText.Kernel.Colors;

/// <summary>
/// Consolidated customer-invoice PDF — one invoice over the Bills of a Billing Group.
///
/// <para>Reuses the EXISTING invoice layout: same fonts, same teal/navy palette, same header, Bill-To block,
/// charge table, totals block, signature and footer, and the same cell helpers (MetaRow / BodyCell / SpanHead /
/// AddTotal / AmountInWords) from <see cref="InvoiceService"/>. Nothing is redesigned; sections are ADDED
/// (Included Bills, Invoice Summary, Service Breakdown, Linked References, Bank Details).</para>
///
/// <para><b>No accounting, ever.</b> This file only reads bills and draws a document. A/R was posted per Bill
/// at approval; generating this PDF creates no voucher, no ledger line and no journal entry. Totals are the
/// arithmetic SUM of the selected bills' own stored totals — GST is never recomputed, so the consolidated
/// grand total is identical to the sum of the bills by construction.</para>
/// </summary>
public partial class InvoiceService
{
    // ── Public API ───────────────────────────────────────────────────────────

    /// <summary>Builds the consolidated PDF WITHOUT persisting anything — the popup's "Preview" path.
    /// The invoice number shows as PREVIEW because no CI number is consumed until the user generates.</summary>
    public async Task<byte[]> BuildConsolidatedPreviewAsync(IReadOnlyCollection<long> billIds)
    {
        var bills = await LoadBillsForInvoiceAsync(billIds);
        if (bills.Count == 0) throw new InvalidOperationException("Select at least one bill to preview.");

        var anchor = bills.OrderBy(b => b.Id).First();
        var draft = new CustomerInvoice
        {
            InvoiceNo             = "PREVIEW",
            CustomerInvoiceNumber = anchor.CustomerInvoiceNumber ?? "-",
            InvoiceDate           = DateTime.UtcNow.Date,
            BillingClientId       = anchor.BillingClientId,
            BillingClient         = anchor.BillingClient,
            Branch                = anchor.Branch,
            Currency              = anchor.Currency,
            SubTotal              = bills.Sum(b => b.SubTotal),
            GstAmount             = bills.Sum(b => b.GstAmount),
            TotalAmount           = bills.Sum(b => b.TotalAmount),
        };

        var co = await _companyDetails.GetOrCreateAsync();
        return GenerateConsolidatedPdf(draft, bills, co, await CurrentUserAsync(), preview: true);
    }

    /// <summary>
    /// Generates and STORES the consolidated PDF for an already-raised <see cref="CustomerInvoice"/>.
    /// Reuses the existing InvoiceDocument store: BillId points at the anchor bill (so every existing
    /// by-BillId query keeps working) and CustomerInvoiceId marks it as the consolidated document.
    /// </summary>
    public async Task<InvoiceDocument> IssueConsolidatedAsync(long customerInvoiceId, string? actor = null)
    {
        var invoice = await _db.CustomerInvoices
            .Include(i => i.BillingClient)
            .Include(i => i.Branch)
            .Include(i => i.Currency)
            .FirstOrDefaultAsync(i => i.Id == customerInvoiceId)
            ?? throw new InvalidOperationException("Customer invoice not found.");

        var billIds = await _db.Bills.Where(b => b.CustomerInvoiceId == customerInvoiceId)
                                     .Select(b => b.Id).ToListAsync();
        var bills = await LoadBillsForInvoiceAsync(billIds);
        if (bills.Count == 0)
            throw new InvalidOperationException($"Customer invoice {invoice.InvoiceNo} has no bills.");

        var user   = actor ?? await CurrentUserAsync();
        var co     = await _companyDetails.GetOrCreateAsync();
        var bytes  = GenerateConsolidatedPdf(invoice, bills, co, user, preview: false);
        var anchor = bills.OrderBy(b => b.Id).First();

        // Supersede any previous consolidated PDF for THIS invoice (regeneration bumps the version).
        var prior = await _db.InvoiceDocuments
            .Where(d => d.CustomerInvoiceId == customerInvoiceId
                     && d.DocumentType == InvoiceDocumentType.CustomerInvoice && d.IsActive)
            .ToListAsync();
        foreach (var p in prior) p.IsActive = false;
        var nextVersion = prior.Select(d => d.Version).DefaultIfEmpty(0).Max() + 1;

        var storedName   = $"{Guid.NewGuid():N}.pdf";
        var friendlyName = $"Invoice-{invoice.InvoiceNo.Replace('/', '-')}-v{nextVersion}.pdf";
        var relPath      = $"ci/{invoice.Id}/{storedName}";
        TryWriteToDisk(relPath, bytes);   // best-effort; the DB holds the authoritative bytes

        var doc = new InvoiceDocument
        {
            BillId            = anchor.Id,           // keeps every existing by-BillId query working
            CustomerInvoiceId = invoice.Id,          // …and marks this as the consolidated document
            DocumentType      = InvoiceDocumentType.CustomerInvoice,
            FileName          = storedName,
            OriginalFileName  = friendlyName,
            FilePath          = relPath,
            Content           = bytes,
            ContentType       = "application/pdf",
            UploadedBy        = user,
            UploadedDate      = DateTime.UtcNow,
            Version           = nextVersion,
            IsActive          = true,
        };
        _db.InvoiceDocuments.Add(doc);

        // Timeline entry — reuses the existing audit store (no separate event table).
        _db.WorkflowAuditLogs.Add(new WorkflowAuditLog
        {
            Kind       = WorkflowLogKind.Activity,
            Module     = "Billing",
            EntityType = "CustomerInvoice",
            EntityId   = invoice.Id,
            EntityRef  = invoice.InvoiceNo,
            Operation  = WorkflowOperationType.Update,
            Summary    = nextVersion == 1 ? "PDF Generated" : "PDF Regenerated",
            Details    = $"{friendlyName} (v{nextVersion}, {bytes.Length:N0} bytes) over {bills.Count} bill(s).",
            Actor      = user,
            At         = DateTime.UtcNow,
        });

        await _db.SaveChangesAsync();

        _log.LogInformation("Consolidated invoice {InvoiceNo} PDF generated over {Count} bill(s) ({Bytes} bytes).",
            invoice.InvoiceNo, bills.Count, bytes.Length);
        _dash.NotifyChanged();
        return doc;
    }

    /// <summary>One query graph for every bill on the invoice — charges, SAC, charge code, and the originating
    /// job (for the Linked References + shipment counts). Loaded once, not per bill.</summary>
    private Task<List<Bill>> LoadBillsForInvoiceAsync(IReadOnlyCollection<long> billIds) =>
        _db.Bills.AsNoTrackingWithIdentityResolution()
            .Include(b => b.BillingClient)
            .Include(b => b.Branch)
            .Include(b => b.Currency)
            .Include(b => b.JobOrder)
            .Include(b => b.Charges).ThenInclude(c => c.Sac)
            .Include(b => b.Charges).ThenInclude(c => c.ChargeCode)
            .Where(b => billIds.Contains(b.Id))
            .OrderBy(b => b.Mode).ThenBy(b => b.Id)
            .ToListAsync();

    // ── Service Breakdown labels ─────────────────────────────────────────────

    /// <summary>
    /// Label for a charge category. Derived from the <see cref="ChargeCategory"/> enum itself, so a category
    /// added later automatically appears in the Service Breakdown with a sensible name and NO code change
    /// here. A value outside the enum (a category from a future/unknown source) falls through to
    /// "Other Charges" rather than being dropped — no charge is ever lost from the breakdown.
    /// </summary>
    public static string CategoryLabel(ChargeCategory category) =>
        Enum.IsDefined(typeof(ChargeCategory), category)
            ? category switch
              {
                  // Only where the enum name alone would read oddly on an invoice.
                  ChargeCategory.General       => "General Charges",
                  ChargeCategory.Miscellaneous => "Other Charges",
                  ChargeCategory.Tax           => "Tax Charges",
                  _                            => $"{category} Charges",   // Freight → "Freight Charges", …
              }
            : "Other Charges";

    // ── The document ─────────────────────────────────────────────────────────

    private byte[] GenerateConsolidatedPdf(CustomerInvoice inv, List<Bill> bills, CompanyDetails co,
                                           string generatedBy, bool preview)
    {
        // Everything below reuses the single-bill invoice's styling verbatim.
        var CoName      = co.CompanyName;
        var CoTagline   = co.Tagline ?? string.Empty;
        var CoAddr      = CoAddrLine(co);
        var CoContact   = CoContactLine(co);
        var CoGstin     = CoGstinLine(co);
        var CoFooter    = string.IsNullOrWhiteSpace(co.InvoiceFooter) ? "This is a computer-generated invoice." : co.InvoiceFooter!;
        var CoSignatory = string.IsNullOrWhiteSpace(co.AuthorisedSignatory) ? "Authorised Signatory" : co.AuthorisedSignatory!;

        var bold   = PdfFontFactory.CreateFont(StandardFonts.HELVETICA_BOLD);
        var normal = PdfFontFactory.CreateFont(StandardFonts.HELVETICA);
        var teal   = new DeviceRgb(25, 135, 129);
        var navy   = new DeviceRgb(14, 42, 71);
        var light  = new DeviceRgb(240, 244, 247);
        var line   = new SolidBorder(new DeviceRgb(210, 216, 224), 0.8f);
        var cur    = inv.Currency?.CurrencyCode ?? bills.FirstOrDefault()?.Currency?.CurrencyCode ?? "INR";

        using var ms     = new MemoryStream();
        using var writer = new PdfWriter(ms);
        using var pdf    = new PdfDocument(writer);
        var doc          = new Document(pdf);
        doc.SetMargins(34, 34, 34, 34);

        var outlines = pdf.GetOutlines(false);   // (A) internal bookmarks

        // ── Header: logo (left) + company details (right) — all from Company Master ──
        var header   = new Table(UnitValue.CreatePercentArray(new float[] { 1.1f, 2f })).UseAllAvailableWidth();
        var logoCell = new Cell().SetBorder(Border.NO_BORDER).SetVerticalAlignment(VerticalAlignment.MIDDLE);
        var webRoot  = _env.WebRootPath ?? "";
        var logoPath = Path.Combine(webRoot, "img", "pvgt-logo.png");
        if (!string.IsNullOrWhiteSpace(co.LogoPath))
        {
            var uploaded = Path.Combine(webRoot, co.LogoPath.Replace('/', Path.DirectorySeparatorChar));
            if (File.Exists(uploaded)) logoPath = uploaded;
        }
        // (D) No logo → company name in its place. The layout never breaks on a missing file.
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

        doc.Add(new Paragraph(preview ? "TAX INVOICE — PREVIEW" : "TAX INVOICE")
            .SetFont(bold).SetFontSize(14).SetFontColor(ColorConstants.WHITE)
            .SetBackgroundColor(teal).SetTextAlignment(TextAlignment.CENTER).SetPadding(5).SetMarginTop(10));

        // ── Bill-To + invoice meta. BOTH numbers shown prominently. ─────────────
        var client = inv.BillingClient ?? bills[0].BillingClient;
        var info   = new Table(UnitValue.CreatePercentArray(new float[] { 1.3f, 1f })).UseAllAvailableWidth().SetMarginTop(8);
        var billTo = new Cell().SetBorder(line).SetPadding(7)
            .Add(new Paragraph("BILL TO").SetFont(bold).SetFontSize(8).SetFontColor(teal).SetMarginBottom(2))
            .Add(new Paragraph(client?.CompanyName ?? "-").SetFont(bold).SetFontSize(10))
            .Add(new Paragraph(client?.Address ?? "").SetFont(normal).SetFontSize(8.5f));
        if (!string.IsNullOrWhiteSpace(client?.Phone))
            billTo.Add(new Paragraph("Phone: " + client!.Phone).SetFont(normal).SetFontSize(8.5f));
        if (!string.IsNullOrWhiteSpace(client?.ContactEmail))
            billTo.Add(new Paragraph("Email: " + client!.ContactEmail).SetFont(normal).SetFontSize(8.5f));
        info.AddCell(billTo);

        var minDate = bills.Min(b => b.BillDate);
        var maxDate = bills.Max(b => b.BillDate);

        var metaTbl = new Table(UnitValue.CreatePercentArray(new float[] { 1, 1.2f })).SetBorder(line);
        MetaRow(metaTbl, bold, normal, "Customer Invoice No", inv.CustomerInvoiceNumber);   // the business reference
        MetaRow(metaTbl, bold, normal, "System Invoice No", inv.InvoiceNo);                 // CI/YY-YY/000001
        MetaRow(metaTbl, bold, normal, "Customer", client?.CompanyName ?? "-");
        MetaRow(metaTbl, bold, normal, "Branch", inv.Branch?.BranchName ?? bills[0].Branch?.BranchName ?? "-");
        MetaRow(metaTbl, bold, normal, "Billing Period",
            minDate == maxDate ? minDate.ToString("dd-MMM-yyyy")
                               : $"{minDate:dd-MMM-yyyy} → {maxDate:dd-MMM-yyyy}");
        MetaRow(metaTbl, bold, normal, "Currency", cur);
        MetaRow(metaTbl, bold, normal, "Generated Date", inv.InvoiceDate.ToString("dd-MMM-yyyy"));
        info.AddCell(new Cell().SetBorder(Border.NO_BORDER).SetPaddingLeft(6).Add(metaTbl));
        doc.Add(info);

        // ── (4) Included Bills ─────────────────────────────────────────────────
        AddOutline(outlines, pdf, "Included Bills");
        var incl = new Table(UnitValue.CreatePercentArray(new float[] { 1.6f, 1.3f, 1.2f, 1.4f, 1.1f }))
            .UseAllAvailableWidth().SetMarginTop(10);
        foreach (var h in new[] { "Bill Number", "Bill Type", "Bill Date", "Bill Amount", "Status" })
            incl.AddHeaderCell(new Cell().Add(new Paragraph(h).SetFont(bold).SetFontSize(8.5f).SetFontColor(ColorConstants.WHITE))
                .SetBackgroundColor(navy).SetPadding(5));
        foreach (var b in bills)
        {
            incl.AddCell(BodyCell(bold,   b.BillNo, TextAlignment.LEFT));
            incl.AddCell(BodyCell(normal, b.Mode.ToString(), TextAlignment.LEFT));
            incl.AddCell(BodyCell(normal, b.BillDate.ToString("dd-MMM-yyyy"), TextAlignment.LEFT));
            incl.AddCell(BodyCell(normal, b.TotalAmount.ToString("N2"), TextAlignment.RIGHT));
            incl.AddCell(BodyCell(normal, b.Status.ToString(), TextAlignment.LEFT));
        }
        doc.Add(incl);

        // ── (5) Merged charge lines: Bill Type → Operation → Charge ────────────
        // Presentation only. Every line's Qty/Rate/GST%/GST/Net is printed EXACTLY as stored on the bill —
        // no amount is recomputed, so tax logic is untouched.
        var t = new Table(UnitValue.CreatePercentArray(new float[] { 0.5f, 3.4f, 1.1f, 0.9f, 1.2f, 0.9f, 1.3f, 1.5f }))
            .UseAllAvailableWidth().SetMarginTop(10);
        foreach (var h in new[] { "#", "Description", "SAC", "Qty", "Rate", "GST%", "GST Amt", "Net Amount" })
            t.AddHeaderCell(new Cell().Add(new Paragraph(h).SetFont(bold).SetFontSize(8.5f).SetFontColor(ColorConstants.WHITE))
                .SetBackgroundColor(navy).SetPadding(5));

        int n = 0;
        foreach (var typeGroup in bills.GroupBy(b => b.Mode).OrderBy(g => g.Key))   // level 1: Bill Type
        {
            t.AddCell(new Cell(1, 8)
                .Add(new Paragraph($"{typeGroup.Key.ToString().ToUpperInvariant()} BILLS")
                    .SetFont(bold).SetFontSize(9).SetFontColor(ColorConstants.WHITE))
                .SetBackgroundColor(teal).SetPadding(4).SetBorder(line));

            foreach (var b in typeGroup)                                            // level 2: the bill itself
            {
                t.AddCell(new Cell(1, 8)
                    .Add(new Paragraph($"{b.BillNo}  ·  {b.BillDate:dd-MMM-yyyy}"
                                     + (b.JobOrder is not null ? $"  ·  Job {b.JobOrder.JobOrderNo}" : ""))
                        .SetFont(bold).SetFontSize(8.5f).SetFontColor(navy))
                    .SetBackgroundColor(light).SetPadding(3).SetBorder(line));

                foreach (var opGroup in b.Charges                                   // level 3: Operation
                             .OrderBy(c => c.DisplayOrder)
                             .GroupBy(c => string.IsNullOrWhiteSpace(c.OperationName) ? "" : c.OperationName!))
                {
                    var opLabel = string.IsNullOrWhiteSpace(opGroup.Key) ? "General Charges" : opGroup.Key;
                    t.AddCell(new Cell(1, 8)
                        .Add(new Paragraph("   " + opLabel).SetFont(bold).SetFontSize(8).SetFontColor(new DeviceRgb(90, 101, 115)))
                        .SetPadding(2).SetBorder(Border.NO_BORDER));

                    foreach (var c in opGroup)                                      // level 4: the charge
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
            }
        }
        doc.Add(t);

        // ── (8/F/G) Service Breakdown — presentation summary only ──────────────
        // Categories come from the charges themselves, so a NEW ChargeCategory appears here automatically
        // with no code change. The category totals sum to the same Grand Total; nothing is recomputed.
        AddOutline(outlines, pdf, "Service Breakdown");
        var byCategory = bills.SelectMany(b => b.Charges)
            .GroupBy(c => CategoryLabel(c.Category))
            .Select(g => new { Category = g.Key, Total = g.Sum(c => c.NetAmount) })
            .OrderByDescending(x => x.Total)
            .ToList();

        var svc = new Table(UnitValue.CreatePercentArray(new float[] { 3f, 1.4f })).UseAllAvailableWidth().SetMarginTop(10);
        svc.AddCell(new Cell(1, 2).Add(new Paragraph("SERVICE BREAKDOWN").SetFont(bold).SetFontSize(8.5f)
                .SetFontColor(ColorConstants.WHITE)).SetBackgroundColor(navy).SetPadding(5));
        foreach (var c in byCategory)
        {
            svc.AddCell(BodyCell(normal, c.Category, TextAlignment.LEFT));
            svc.AddCell(BodyCell(normal, c.Total.ToString("N2"), TextAlignment.RIGHT));
        }
        svc.AddCell(new Cell().Add(new Paragraph("Total (incl. GST)").SetFont(bold).SetFontSize(8.5f))
            .SetBackgroundColor(light).SetPadding(4).SetBorder(line));
        svc.AddCell(new Cell().Add(new Paragraph(byCategory.Sum(x => x.Total).ToString("N2"))
                .SetFont(bold).SetFontSize(8.5f).SetTextAlignment(TextAlignment.RIGHT))
            .SetBackgroundColor(light).SetPadding(4).SetBorder(line));
        doc.Add(svc);

        // ── (7) Invoice Summary ────────────────────────────────────────────────
        AddOutline(outlines, pdf, "Invoice Summary");
        var jobKeys  = bills.Where(b => b.JobOrderId is not null).Select(b => b.JobOrderId!.Value).Distinct().Count();
        var awbKeys  = bills.Where(b => b.SourceType == BillSourceType.AwbShipment && b.SourceId is not null)
                            .Select(b => b.SourceId!.Value).Distinct().Count();
        var expKeys  = bills.Where(b => b.SourceType == BillSourceType.ExportJob && b.SourceId is not null)
                            .Select(b => b.SourceId!.Value).Distinct().Count();
        var containers = bills.Where(b => !string.IsNullOrWhiteSpace(b.ContainerNumber))
                              .Select(b => b.ContainerNumber!).Distinct().Count();
        var shipments  = jobKeys + awbKeys + expKeys;   // every distinct operational record behind the invoice

        var sum = new Table(UnitValue.CreatePercentArray(new float[] { 1.6f, 1f })).UseAllAvailableWidth().SetMarginTop(8);
        var left = new Table(UnitValue.CreatePercentArray(new float[] { 1.4f, 1f })).SetBorder(line);
        left.AddCell(new Cell(1, 2).Add(new Paragraph("INVOICE SUMMARY").SetFont(bold).SetFontSize(8)
                .SetFontColor(teal)).SetBorder(Border.NO_BORDER).SetPadding(4));
        MetaRow(left, bold, normal, "Number of Bills",      bills.Count.ToString());
        MetaRow(left, bold, normal, "Number of Jobs",       jobKeys.ToString());
        MetaRow(left, bold, normal, "Number of Shipments",  shipments.ToString());
        MetaRow(left, bold, normal, "Number of Containers", containers.ToString());
        MetaRow(left, bold, normal, "Number of AWBs",       awbKeys.ToString());
        sum.AddCell(new Cell().SetBorder(Border.NO_BORDER).SetPadding(0).Add(left));

        // (6) Totals = the arithmetic SUM of the bills. GST is NOT recomputed here.
        var totals = new Table(UnitValue.CreatePercentArray(new float[] { 1.2f, 1f })).SetBorder(Border.NO_BORDER);
        AddTotal(totals, bold, normal, "Sub Total", $"{bills.Sum(b => b.SubTotal):N2}", light, false);
        AddTotal(totals, bold, normal, "GST",       $"{bills.Sum(b => b.GstAmount):N2}", light, false);
        AddTotal(totals, bold, normal, $"Grand Total ({cur})", $"{bills.Sum(b => b.TotalAmount):N2}", teal, true);
        sum.AddCell(new Cell().SetBorder(Border.NO_BORDER).SetPadding(0).Add(totals));
        doc.Add(sum);

        doc.Add(new Paragraph("Amount in Words: " + AmountInWords(bills.Sum(b => b.TotalAmount), cur))
            .SetFont(bold).SetFontSize(9).SetMarginTop(6));

        // ── (B) Linked References — for audit ──────────────────────────────────
        AddOutline(outlines, pdf, "Linked References");
        var jobNos = bills.Where(b => b.JobOrder is not null).Select(b => b.JobOrder!.JobOrderNo).Distinct().ToList();
        var refs = new Table(UnitValue.CreatePercentArray(new float[] { 1, 3.4f })).UseAllAvailableWidth().SetMarginTop(8).SetBorder(line);
        refs.AddCell(SpanHead("LINKED REFERENCES", bold, teal));
        MetaRow(refs, bold, normal, "Customer Invoice No", inv.CustomerInvoiceNumber);
        MetaRow(refs, bold, normal, "Included Bills", string.Join(", ", bills.Select(b => b.BillNo)));
        MetaRow(refs, bold, normal, "Originating Jobs", jobNos.Count > 0 ? string.Join(", ", jobNos) : "-");
        doc.Add(refs);

        // ── (E) Bank details + terms — from Company Master, never hardcoded ────
        var hasBank = !string.IsNullOrWhiteSpace(co.BankName) || !string.IsNullOrWhiteSpace(co.AccountNumber);
        var notes = new Table(UnitValue.CreatePercentArray(new float[] { 1, 1 })).UseAllAvailableWidth().SetMarginTop(8);

        var bankCell = new Cell().SetBorder(line).SetPadding(6)
            .Add(new Paragraph("Bank Details").SetFont(bold).SetFontSize(8).SetFontColor(teal));
        if (hasBank)
        {
            // One account today; the block is a list, so additional accounts drop in without a redesign.
            if (!string.IsNullOrWhiteSpace(co.BankName))      bankCell.Add(Small(normal, "Bank: " + co.BankName));
            if (!string.IsNullOrWhiteSpace(co.AccountName))   bankCell.Add(Small(normal, "Account Name: " + co.AccountName));
            if (!string.IsNullOrWhiteSpace(co.AccountNumber)) bankCell.Add(Small(normal, "Account No: " + co.AccountNumber));
            if (!string.IsNullOrWhiteSpace(co.IFSC))          bankCell.Add(Small(normal, "IFSC: " + co.IFSC));
            if (!string.IsNullOrWhiteSpace(co.Branch))        bankCell.Add(Small(normal, "Branch: " + co.Branch));
        }
        else bankCell.Add(Small(normal, "-"));
        notes.AddCell(bankCell);

        notes.AddCell(new Cell().SetBorder(line).SetPadding(6)
            .Add(new Paragraph("Terms & Conditions").SetFont(bold).SetFontSize(8).SetFontColor(teal))
            .Add(Small(normal, string.IsNullOrWhiteSpace(co.TermsAndConditions)
                ? (string.IsNullOrWhiteSpace(inv.PaymentTerms) ? "Due on receipt" : inv.PaymentTerms!)
                : co.TermsAndConditions!)));
        doc.Add(notes);

        // ── Signature + (9) footer ─────────────────────────────────────────────
        var sign = new Table(UnitValue.CreatePercentArray(new float[] { 1.4f, 1f })).UseAllAvailableWidth().SetMarginTop(18);
        sign.AddCell(new Cell().SetBorder(Border.NO_BORDER)
            .Add(new Paragraph(CoFooter).SetFont(normal).SetFontSize(7.5f).SetFontColor(ColorConstants.GRAY)));
        sign.AddCell(new Cell().SetBorder(Border.NO_BORDER).SetTextAlignment(TextAlignment.CENTER).SetPaddingTop(24)
            .Add(new Paragraph("_______________________").SetFont(normal).SetFontSize(9))
            .Add(new Paragraph(CoSignatory).SetFont(bold).SetFontSize(8.5f))
            .Add(new Paragraph("for " + CoName).SetFont(normal).SetFontSize(7.5f)));
        doc.Add(sign);

        doc.Add(new Paragraph(
                $"Generated from Bills: {string.Join(", ", bills.Select(b => b.BillNo))}\n" +
                $"Generated By: {generatedBy}    ·    Generated On: {DateTime.Now:dd-MMM-yyyy HH:mm}")
            .SetFont(normal).SetFontSize(7).SetFontColor(ColorConstants.GRAY)
            .SetTextAlignment(TextAlignment.CENTER).SetMarginTop(10));

        doc.Close();
        return ms.ToArray();
    }

    private static Paragraph Small(PdfFont font, string text) =>
        new Paragraph(text).SetFont(font).SetFontSize(8.5f).SetMarginBottom(0);

    /// <summary>(A) Internal bookmark to the page currently being written. Best-effort: a failure to add an
    /// outline must never cost us the invoice.</summary>
    private void AddOutline(PdfOutline? root, PdfDocument pdf, string title)
    {
        try
        {
            if (root is null) return;
            var page = Math.Max(1, pdf.GetNumberOfPages());
            root.AddOutline(title).AddDestination(PdfExplicitDestination.CreateFit(pdf.GetPage(page)));
        }
        catch (Exception ex)
        {
            _log.LogDebug(ex, "PDF bookmark '{Title}' skipped (non-fatal).", title);
        }
    }
}
