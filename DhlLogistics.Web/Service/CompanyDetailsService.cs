namespace DhlLogistics.Web.Service;

using DhlLogistics.Shared.Models;
using DhlLogistics.Web.Repository;

/// <summary>
/// Application service for the single-record <see cref="CompanyDetails"/> master — the source of the
/// issuer company details used to brand the customer-invoice PDF. Lazily seeds one record (from the
/// values that were previously hard-coded in <c>InvoiceService</c>) so both the maintenance page and
/// the invoice generator always have a record to work with.
/// </summary>
public class CompanyDetailsService
{
    private readonly ICompanyDetailsRepository _repo;

    public CompanyDetailsService(ICompanyDetailsRepository repo) => _repo = repo;

    /// <summary>Returns the one company record, creating it from the legacy hard-coded values on first
    /// use so existing installs (and the invoice generator) never see a null record.</summary>
    public async Task<CompanyDetails> GetOrCreateAsync()
    {
        var existing = await _repo.GetSingleAsync();
        if (existing is not null) return existing;

        var seed = DefaultSeed();
        await _repo.AddAsync(seed);
        await _repo.SaveChangesAsync();
        return seed;
    }

    public async Task<CompanyDetails> UpdateAsync(CompanyDetails details)
    {
        var existing = await _repo.GetSingleAsync();
        if (existing is null)
        {
            details.CreatedOn = DateTime.UtcNow;
            await _repo.AddAsync(details);
            await _repo.SaveChangesAsync();
            return details;
        }

        existing.CompanyName         = details.CompanyName;
        existing.Tagline             = details.Tagline;
        existing.AddressLine1        = details.AddressLine1;
        existing.AddressLine2        = details.AddressLine2;
        existing.City                = details.City;
        existing.State               = details.State;
        existing.Country             = details.Country;
        existing.Pincode             = details.Pincode;
        existing.Phone               = details.Phone;
        existing.Mobile              = details.Mobile;
        existing.Email               = details.Email;
        existing.Website             = details.Website;
        existing.GSTIN               = details.GSTIN;
        existing.PAN                 = details.PAN;
        existing.CIN                 = details.CIN;
        existing.IEC                 = details.IEC;
        existing.LogoPath            = details.LogoPath;
        existing.BankName            = details.BankName;
        existing.AccountName         = details.AccountName;
        existing.AccountNumber       = details.AccountNumber;
        existing.IFSC                = details.IFSC;
        existing.SwiftCode           = details.SwiftCode;
        existing.Branch              = details.Branch;
        existing.AuthorisedSignatory = details.AuthorisedSignatory;
        existing.TermsAndConditions  = details.TermsAndConditions;
        existing.InvoiceFooter       = details.InvoiceFooter;
        existing.IsActive            = details.IsActive;
        existing.ModifiedOn          = DateTime.UtcNow;

        _repo.Update(existing);
        await _repo.SaveChangesAsync();
        return existing;
    }

    // ── Legacy defaults (previously hard-coded in InvoiceService.GeneratePdf) ────
    private static CompanyDetails DefaultSeed() => new()
    {
        CompanyName         = "PVGT Logistics Pvt. Ltd.",
        Tagline             = "DHL Authorised Freight Agent · Customs Clearance & Forwarding",
        AddressLine1        = "2nd Floor, Willingdon Island",
        City                = "Cochin",
        State               = "Kerala",
        Country             = "India",
        Pincode             = "682003",
        Phone               = "+91 484 000 0000",
        Email               = "accounts@pvgt.co.in",
        Website             = "www.pvgt.co.in",
        GSTIN               = "32ABCDE1234F1Z5",
        PAN                 = "ABCDE1234F",
        AuthorisedSignatory = "Authorised Signatory",
        InvoiceFooter       = "This is a computer-generated invoice.",
        IsActive            = true,
        CreatedOn           = DateTime.UtcNow,
    };
}
