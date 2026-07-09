namespace DhlLogistics.Web.Database.Configurations;

using DhlLogistics.Shared.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

/// <summary>
/// EF Core mapping for the <see cref="CompanyDetails"/> master. Self-contained (applied via
/// <c>ApplyConfiguration</c> in <see cref="AppDbContext"/>) so the new table touches no existing mapping.
/// </summary>
public class CompanyDetailsConfiguration : IEntityTypeConfiguration<CompanyDetails>
{
    public void Configure(EntityTypeBuilder<CompanyDetails> e)
    {
        e.ToTable("CompanyDetails");
        e.HasKey(c => c.Id);

        e.Property(c => c.CompanyName).HasMaxLength(200).IsRequired();
        e.Property(c => c.Tagline).HasMaxLength(300);
        e.Property(c => c.AddressLine1).HasMaxLength(200).IsRequired();
        e.Property(c => c.AddressLine2).HasMaxLength(200);
        e.Property(c => c.City).HasMaxLength(100);
        e.Property(c => c.State).HasMaxLength(100);
        e.Property(c => c.Country).HasMaxLength(100);
        e.Property(c => c.Pincode).HasMaxLength(20);
        e.Property(c => c.Phone).HasMaxLength(50).IsRequired();
        e.Property(c => c.Mobile).HasMaxLength(50);
        e.Property(c => c.Email).HasMaxLength(150).IsRequired();
        e.Property(c => c.Website).HasMaxLength(150);
        e.Property(c => c.GSTIN).HasMaxLength(20).IsRequired();
        e.Property(c => c.PAN).HasMaxLength(20);
        e.Property(c => c.CIN).HasMaxLength(30);
        e.Property(c => c.IEC).HasMaxLength(20);
        e.Property(c => c.LogoPath).HasMaxLength(300);
        e.Property(c => c.BankName).HasMaxLength(150);
        e.Property(c => c.AccountName).HasMaxLength(150);
        e.Property(c => c.AccountNumber).HasMaxLength(50);
        e.Property(c => c.IFSC).HasMaxLength(20);
        e.Property(c => c.SwiftCode).HasMaxLength(20);
        e.Property(c => c.Branch).HasMaxLength(150);
        e.Property(c => c.AuthorisedSignatory).HasMaxLength(150);
        e.Property(c => c.TermsAndConditions).HasMaxLength(2000);
        e.Property(c => c.InvoiceFooter).HasMaxLength(500);
    }
}
