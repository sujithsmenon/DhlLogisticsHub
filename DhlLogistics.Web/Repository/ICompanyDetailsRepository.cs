namespace DhlLogistics.Web.Repository;

using DhlLogistics.Shared.Models;

/// <summary>
/// Data-access contract for the single-record <see cref="CompanyDetails"/> master. Keeps EF
/// query/persist concerns behind an interface so the service layer (and the invoice generator) stay
/// storage-agnostic.
/// </summary>
public interface ICompanyDetailsRepository
{
    /// <summary>The one company record, or null if none has been created yet.</summary>
    Task<CompanyDetails?> GetSingleAsync();
    Task<bool> AnyAsync();

    Task AddAsync(CompanyDetails details);
    void Update(CompanyDetails details);

    Task<int> SaveChangesAsync();
}
