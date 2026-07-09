namespace DhlLogistics.Web.Repository;

using DhlLogistics.Shared.Models;
using DhlLogistics.Web.Database;
using Microsoft.EntityFrameworkCore;

/// <summary>
/// EF Core implementation of <see cref="ICompanyDetailsRepository"/> over <see cref="AppDbContext"/>.
/// Writes are staged on the tracked context and committed by the caller via <see cref="SaveChangesAsync"/>.
/// </summary>
public class CompanyDetailsRepository : ICompanyDetailsRepository
{
    private readonly AppDbContext _db;

    public CompanyDetailsRepository(AppDbContext db) => _db = db;

    public Task<CompanyDetails?> GetSingleAsync() =>
        _db.CompanyDetails.OrderBy(c => c.Id).FirstOrDefaultAsync();

    public Task<bool> AnyAsync() => _db.CompanyDetails.AnyAsync();

    public async Task AddAsync(CompanyDetails details) => await _db.CompanyDetails.AddAsync(details);

    public void Update(CompanyDetails details) => _db.CompanyDetails.Update(details);

    public Task<int> SaveChangesAsync() => _db.SaveChangesAsync();
}
