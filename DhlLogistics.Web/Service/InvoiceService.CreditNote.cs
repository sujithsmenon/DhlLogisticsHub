namespace DhlLogistics.Web.Service;

using DhlLogistics.Shared.Models;
using Microsoft.EntityFrameworkCore;
using iText.Kernel.Pdf;
using iText.IO.Font.Constants;
using iText.Kernel.Font;
using iText.Layout;
using iText.Layout.Element;
using iText.Layout.Properties;
using iText.Layout.Borders;
using iText.Kernel.Colors;
using iText.IO.Image;

/// <summary>
/// Credit notes over an already-issued invoice.
///
/// <para>The system already had an <see cref="InvoiceDocumentType.CreditNote"/> document type, but nothing
/// that could GENERATE one — a credit note could only be uploaded from outside. This adds generation, so a
/// mis-issued invoice can be withdrawn on the record rather than by rewriting history.</para>
///
/// <para><b>Document-only by design.</b> Issuing a credit note here posts NO accounting. In this ERP A/R and
/// revenue are posted per Bill at <b>approval</b>, never at invoice issue — so a reversing entry would remove
/// revenue the customer genuinely owes and which is still invoiced under the replacement number. The credit
/// note withdraws the DOCUMENT, not the debt. If a formal GST credit note that reverses the ledger is ever
/// needed, that is a different operation and must post its own voucher explicitly.</para>
/// </summary>
public partial class InvoiceService
{
    /// <summary>
    /// Issues a credit note withdrawing a bill's current invoice, then (optionally) re-issues the invoice so
    /// the bill carries a correct number again.
    ///
    /// <para>The superseded invoice's PDF is retired (IsActive = false), never deleted — the customer may hold
    /// a copy, and the audit trail must show what was withdrawn and why.</para>
    /// </summary>
    public async Task<InvoiceDocument> IssueCreditNoteAsync(long billId, string reason, string? actor = null)
    {
        var bill = await _db.Bills
            .Include(b => b.BillingClient)
            .Include(b => b.Branch)
            .Include(b => b.Currency)
            .Include(b => b.Charges)
            .Include(b => b.InvoiceDocuments)
            .FirstOrDefaultAsync(b => b.Id == billId)
            ?? throw new InvalidOperationException("Bill not found.");

        if (string.IsNullOrWhiteSpace(bill.InvoiceNumber))
            throw new InvalidOperationException($"Bill {bill.BillNo} has no invoice to credit.");
        if (string.IsNullOrWhiteSpace(reason))
            throw new InvalidOperationException("A credit note must state its reason.");

        var user       = actor ?? await CurrentUserAsync();
        var creditedNo = bill.InvoiceNumber!;
        var company    = await _companyDetails.GetOrCreateAsync();

        // Number the credit note off the invoice it withdraws, so the pair is obvious on any statement.
        var creditNoteNo = "CN/" + creditedNo.Replace("INV/", "");

        var bytes = GenerateCreditNotePdf(bill, company, creditNoteNo, creditedNo, reason, user);

        // Retire the withdrawn invoice's PDF — kept for audit, no longer the active document.
        foreach (var d in bill.InvoiceDocuments
                     .Where(d => d.DocumentType == InvoiceDocumentType.CustomerInvoice && d.IsActive))
            d.IsActive = false;

        var nextVersion = bill.InvoiceDocuments
            .Where(d => d.DocumentType == InvoiceDocumentType.CreditNote)
            .Select(d => d.Version).DefaultIfEmpty(0).Max() + 1;

        var storedName = $"{Guid.NewGuid():N}.pdf";
        var relPath    = $"{bill.Id}/{storedName}";
        TryWriteToDisk(relPath, bytes);

        var doc = new InvoiceDocument
        {
            BillId           = bill.Id,
            DocumentType     = InvoiceDocumentType.CreditNote,
            FileName         = storedName,
            OriginalFileName = $"CreditNote-{creditNoteNo.Replace('/', '-')}-v{nextVersion}.pdf",
            FilePath         = relPath,
            Content          = bytes,
            ContentType      = "application/pdf",
            UploadedBy       = user,
            UploadedDate     = DateTime.UtcNow,
            Version          = nextVersion,
            IsActive         = true,
        };
        _db.InvoiceDocuments.Add(doc);

        // The bill's invoice is withdrawn. It is NOT re-issued here — the caller decides whether to reissue,
        // because a credit note without a replacement is a legitimate outcome.
        bill.InvoiceStatus = InvoiceStatus.Cancelled;
        bill.IsIssued      = false;      // free to be issued again under a correct number
        bill.InvoiceRemarks = $"Invoice {creditedNo} withdrawn by credit note {creditNoteNo}: {reason}";
        bill.ModifiedOn    = DateTime.UtcNow;
        bill.ModifiedBy    = user;

        // Audit — reuses the existing store, no new table.
        _db.WorkflowAuditLogs.Add(new WorkflowAuditLog
        {
            Kind       = WorkflowLogKind.Audit,
            Module     = "Billing",
            EntityType = "Bill",
            EntityId   = bill.Id,
            EntityRef  = bill.BillNo,
            Operation  = WorkflowOperationType.Update,
            Summary    = $"Credit note {creditNoteNo} issued, withdrawing invoice {creditedNo}.",
            Details    = $"Reason: {reason}. Amount {bill.TotalAmount:N2}. Document-only — no accounting posted "
                       + "(A/R and revenue post at bill approval, not at invoice issue).",
            Actor      = user,
            At         = DateTime.UtcNow,
        });

        await _db.SaveChangesAsync();
        _log.LogWarning("Credit note {CreditNoteNo} issued against invoice {InvoiceNo} for bill {BillNo} by {User}: {Reason}",
            creditNoteNo, creditedNo, bill.BillNo, user, reason);
        return doc;
    }

    // ── The document ─────────────────────────────────────────────────────────

    private byte[] GenerateCreditNotePdf(Bill bill, CompanyDetails co, string creditNoteNo,
                                         string creditedInvoiceNo, string reason, string user)
    {
        var bold   = PdfFontFactory.CreateFont(StandardFonts.HELVETICA_BOLD);
        var normal = PdfFontFactory.CreateFont(StandardFonts.HELVETICA);
        var red    = new DeviceRgb(190, 18, 60);      // a credit note is not an invoice — it must not look like one
        var navy   = new DeviceRgb(14, 42, 71);
        var light  = new DeviceRgb(240, 244, 247);
        var line   = new SolidBorder(new DeviceRgb(210, 216, 224), 0.8f);
        var cur    = bill.Currency?.CurrencyCode ?? "INR";

        using var ms     = new MemoryStream();
        using var writer = new PdfWriter(ms);
        using var pdf    = new PdfDocument(writer);
        var doc          = new Document(pdf);
        doc.SetMargins(34, 34, 34, 34);

        // Header — same company block as the invoice, all from the master.
        var header   = new Table(UnitValue.CreatePercentArray(new float[] { 1.1f, 2f })).UseAllAvailableWidth();
        var logoCell = new Cell().SetBorder(Border.NO_BORDER).SetVerticalAlignment(VerticalAlignment.MIDDLE);
        var webRoot  = _env.WebRootPath ?? "";
        var bundled  = Path.Combine(webRoot, "img", "pvgt-logo.png");
        if (CachedImage(co.LogoImage) is { } logoData)
            logoCell.Add(new Image(logoData).ScaleToFit(150, 70));
        else if (File.Exists(bundled))
            logoCell.Add(new Image(ImageDataFactory.Create(bundled)).ScaleToFit(150, 70));
        else
            logoCell.Add(new Paragraph(co.CompanyName).SetFont(bold).SetFontSize(16).SetFontColor(navy));
        header.AddCell(logoCell);
        header.AddCell(new Cell().SetBorder(Border.NO_BORDER).SetTextAlignment(TextAlignment.RIGHT)
            .Add(new Paragraph(co.CompanyName).SetFont(bold).SetFontSize(13).SetFontColor(navy).SetMarginBottom(1))
            .Add(new Paragraph(CoAddrLine(co)).SetFont(normal).SetFontSize(8).SetMarginBottom(1))
            .Add(new Paragraph(CoContactLine(co)).SetFont(normal).SetFontSize(8).SetMarginBottom(1))
            .Add(new Paragraph(CoGstinLine(co)).SetFont(bold).SetFontSize(8)));
        doc.Add(header);

        // Title band — RED, and it says CREDIT NOTE. It must never be mistaken for an invoice.
        doc.Add(new Paragraph("CREDIT NOTE").SetFont(bold).SetFontSize(14).SetFontColor(ColorConstants.WHITE)
            .SetBackgroundColor(red).SetTextAlignment(TextAlignment.CENTER).SetPadding(5).SetMarginTop(10));

        var client = bill.BillingClient;
        var info   = new Table(UnitValue.CreatePercentArray(new float[] { 1.3f, 1f })).UseAllAvailableWidth().SetMarginTop(8);
        info.AddCell(new Cell().SetBorder(line).SetPadding(7)
            .Add(new Paragraph("ISSUED TO").SetFont(bold).SetFontSize(8).SetFontColor(red).SetMarginBottom(2))
            .Add(new Paragraph(client?.CompanyName ?? "-").SetFont(bold).SetFontSize(10))
            .Add(new Paragraph(client?.Address ?? "").SetFont(normal).SetFontSize(8.5f)));

        var meta = new Table(UnitValue.CreatePercentArray(new float[] { 1, 1.2f })).SetBorder(line);
        MetaRow(meta, bold, normal, "Credit Note No", creditNoteNo);
        MetaRow(meta, bold, normal, "Date", DateTime.Now.ToString("dd-MMM-yyyy"));
        MetaRow(meta, bold, normal, "Withdraws Invoice", creditedInvoiceNo);
        MetaRow(meta, bold, normal, "Original Bill", bill.BillNo);
        MetaRow(meta, bold, normal, "Branch", bill.Branch?.BranchName ?? "-");
        info.AddCell(new Cell().SetBorder(Border.NO_BORDER).SetPaddingLeft(6).Add(meta));
        doc.Add(info);

        // Why — a credit note without a stated reason is worthless to an auditor.
        var why = new Table(UnitValue.CreatePercentArray(new float[] { 1 })).UseAllAvailableWidth()
            .SetMarginTop(8).SetBorder(line);
        why.AddCell(new Cell().SetBorder(Border.NO_BORDER).SetPadding(7)
            .Add(new Paragraph("REASON FOR WITHDRAWAL").SetFont(bold).SetFontSize(8).SetFontColor(red).SetMarginBottom(2))
            .Add(new Paragraph(reason).SetFont(normal).SetFontSize(9.5f)));
        doc.Add(why);

        // What was on the withdrawn invoice.
        var t = new Table(UnitValue.CreatePercentArray(new float[] { 0.5f, 4f, 1.1f, 1f, 1.3f, 1.5f }))
            .UseAllAvailableWidth().SetMarginTop(10);
        foreach (var h in new[] { "#", "Description", "SAC", "GST%", "GST Amt", "Net Amount" })
            t.AddHeaderCell(new Cell().Add(new Paragraph(h).SetFont(bold).SetFontSize(8.5f).SetFontColor(ColorConstants.WHITE))
                .SetBackgroundColor(navy).SetPadding(5));

        int n = 0;
        foreach (var c in bill.Charges.OrderBy(c => c.DisplayOrder))
        {
            t.AddCell(BodyCell(normal, (++n).ToString(), TextAlignment.CENTER));
            t.AddCell(BodyCell(normal, string.IsNullOrWhiteSpace(c.Description) ? "-" : c.Description, TextAlignment.LEFT));
            t.AddCell(BodyCell(normal, c.Sac?.SacCode ?? "", TextAlignment.LEFT));
            t.AddCell(BodyCell(normal, c.GstRate.ToString("N2"), TextAlignment.RIGHT));
            t.AddCell(BodyCell(normal, c.GstAmount.ToString("N2"), TextAlignment.RIGHT));
            t.AddCell(BodyCell(normal, c.NetAmount.ToString("N2"), TextAlignment.RIGHT));
        }
        doc.Add(t);

        var totals = new Table(UnitValue.CreatePercentArray(new float[] { 1.6f, 1f })).UseAllAvailableWidth().SetMarginTop(8);
        totals.AddCell(new Cell().SetBorder(line).SetPadding(7)
            .Add(new Paragraph("Amount in Words").SetFont(bold).SetFontSize(8).SetFontColor(red))
            .Add(new Paragraph(AmountInWords(bill.TotalAmount, cur)).SetFont(bold).SetFontSize(9)));

        var tt = new Table(UnitValue.CreatePercentArray(new float[] { 1.2f, 1f })).SetBorder(Border.NO_BORDER);
        AddTotal(tt, bold, normal, "Sub Total", $"{bill.SubTotal:N2}", light, false);
        AddTotal(tt, bold, normal, "GST", $"{bill.GstAmount:N2}", light, false);
        AddTotal(tt, bold, normal, $"Credited Total ({cur})", $"{bill.TotalAmount:N2}", red, true);
        totals.AddCell(new Cell().SetBorder(Border.NO_BORDER).SetPadding(0).Add(tt));
        doc.Add(totals);

        // Say plainly what this document does and does not do — an auditor should not have to infer it.
        doc.Add(new Paragraph(
                $"This credit note withdraws invoice {creditedInvoiceNo}, which was issued under a duplicated "
              + "invoice number. It cancels that DOCUMENT only. The underlying charges remain payable and are "
              + "re-invoiced under a corrected, unique invoice number. No amount is refunded by this note.")
            .SetFont(normal).SetFontSize(8).SetFontColor(navy)
            .SetBackgroundColor(light).SetPadding(7).SetMarginTop(10));

        var sign = new Table(UnitValue.CreatePercentArray(new float[] { 1.4f, 1f })).UseAllAvailableWidth().SetMarginTop(18);
        sign.AddCell(new Cell().SetBorder(Border.NO_BORDER)
            .Add(new Paragraph($"Issued by {user} on {DateTime.Now:dd-MMM-yyyy HH:mm}")
                .SetFont(normal).SetFontSize(7.5f).SetFontColor(ColorConstants.GRAY)));
        var signCell = new Cell().SetBorder(Border.NO_BORDER).SetTextAlignment(TextAlignment.CENTER);
        if (CachedImage(co.SignatureImage) is { } sigData)
            signCell.Add(new Image(sigData).ScaleToFit(120, 45).SetHorizontalAlignment(HorizontalAlignment.CENTER));
        else signCell.SetPaddingTop(24);
        signCell.Add(new Paragraph("_______________________").SetFont(normal).SetFontSize(9))
                .Add(new Paragraph(string.IsNullOrWhiteSpace(co.AuthorisedSignatory) ? "Authorised Signatory" : co.AuthorisedSignatory!)
                    .SetFont(bold).SetFontSize(8.5f))
                .Add(new Paragraph("for " + co.CompanyName).SetFont(normal).SetFontSize(7.5f));
        sign.AddCell(signCell);
        doc.Add(sign);

        doc.Close();
        return ms.ToArray();
    }
}
