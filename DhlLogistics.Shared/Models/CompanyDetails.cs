namespace DhlLogistics.Shared.Models;

using System.ComponentModel.DataAnnotations;

/// <summary>
/// The issuer company's details, maintained from the Masters → Company Details page and used to brand
/// generated documents (primarily the customer-invoice PDF). Replaces the constants that were
/// previously hard-coded in <c>InvoiceService.GeneratePdf</c>. Modelled as a single company record.
/// </summary>
public class CompanyDetails
{
    public int Id { get; set; }

    // ── Company Information ─────────────────────────────────────────────────────
    [Required]
    public string CompanyName { get; set; } = string.Empty;
    public string? Tagline { get; set; }

    // ── Address ─────────────────────────────────────────────────────────────────
    [Required]
    public string AddressLine1 { get; set; } = string.Empty;
    public string? AddressLine2 { get; set; }
    public string? City { get; set; }
    public string? State { get; set; }
    public string? Country { get; set; }
    public string? Pincode { get; set; }

    // ── Contact Details ─────────────────────────────────────────────────────────
    [Required]
    public string Phone { get; set; } = string.Empty;
    public string? Mobile { get; set; }
    [Required]
    public string Email { get; set; } = string.Empty;
    public string? Website { get; set; }

    // ── Tax Information ─────────────────────────────────────────────────────────
    [Required]
    public string GSTIN { get; set; } = string.Empty;
    public string? PAN { get; set; }
    public string? CIN { get; set; }
    public string? IEC { get; set; }

    // ── Branding images ─────────────────────────────────────────────────────────
    // Stored as BYTES in the DB, not as files on disk. The app runs on Elastic Beanstalk, whose instance
    // filesystem is per-instance and NOT persistent — an uploaded image written to wwwroot survives only
    // until the next deploy or scale event, and then the invoice silently loses its branding. This is the
    // same reason InvoiceDocument keeps its PDF bytes in the DB.

    /// <summary>Legacy: web-root-relative path of a logo uploaded before images moved into the DB.
    /// Still honoured as a fallback so existing installs keep their logo.</summary>
    public string? LogoPath { get; set; }

    /// <summary>Company logo. Takes precedence over <see cref="LogoPath"/>.</summary>
    public byte[]? LogoImage { get; set; }

    /// <summary>Authorised signatory's signature, printed above the signature line.</summary>
    public byte[]? SignatureImage { get; set; }

    /// <summary>Company seal / stamp, printed in the signature area.</summary>
    public byte[]? SealImage { get; set; }

    /// <summary>Payment QR code, printed beside the bank details.</summary>
    public byte[]? QrCodeImage { get; set; }

    // ── Banking Details ─────────────────────────────────────────────────────────
    public string? BankName { get; set; }
    public string? AccountName { get; set; }
    public string? AccountNumber { get; set; }
    public string? IFSC { get; set; }
    public string? SwiftCode { get; set; }
    public string? Branch { get; set; }

    /// <summary>UPI VPA (e.g. <c>pvgt@hdfcbank</c>), printed with the bank block.</summary>
    public string? UpiId { get; set; }

    // ── Invoice Settings ────────────────────────────────────────────────────────
    public string? AuthorisedSignatory { get; set; }
    public string? TermsAndConditions { get; set; }
    public string? InvoiceFooter { get; set; }

    // ── Flags / audit ───────────────────────────────────────────────────────────
    public bool IsActive { get; set; } = true;
    public DateTime CreatedOn { get; set; } = DateTime.UtcNow;
    public DateTime? ModifiedOn { get; set; }
}
