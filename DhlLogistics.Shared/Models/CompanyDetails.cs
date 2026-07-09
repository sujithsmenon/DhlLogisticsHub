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

    // ── Logo ────────────────────────────────────────────────────────────────────
    /// <summary>Web-root-relative path of the uploaded logo (e.g. <c>img/company/logo.png</c>).
    /// When empty the invoice falls back to the bundled <c>img/pvgt-logo.png</c>.</summary>
    public string? LogoPath { get; set; }

    // ── Banking Details ─────────────────────────────────────────────────────────
    public string? BankName { get; set; }
    public string? AccountName { get; set; }
    public string? AccountNumber { get; set; }
    public string? IFSC { get; set; }
    public string? SwiftCode { get; set; }
    public string? Branch { get; set; }

    // ── Invoice Settings ────────────────────────────────────────────────────────
    public string? AuthorisedSignatory { get; set; }
    public string? TermsAndConditions { get; set; }
    public string? InvoiceFooter { get; set; }

    // ── Flags / audit ───────────────────────────────────────────────────────────
    public bool IsActive { get; set; } = true;
    public DateTime CreatedOn { get; set; } = DateTime.UtcNow;
    public DateTime? ModifiedOn { get; set; }
}
