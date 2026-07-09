namespace DhlLogistics.Web.Repository;

using DhlLogistics.Shared.Models;
using DhlLogistics.Web.Database;
using Microsoft.EntityFrameworkCore;

/// <summary>
/// EF Core implementation of <see cref="IJobOperationRepository"/> over <see cref="AppDbContext"/>.
/// Reads eager-load the related job / staff / vendor for display; writes are staged on the tracked
/// context and committed by the caller via <see cref="SaveChangesAsync"/>.
/// </summary>
public class JobOperationRepository : IJobOperationRepository
{
    private readonly AppDbContext _db;

    public JobOperationRepository(AppDbContext db) => _db = db;

    private IQueryable<JobOperation> WithRefs() => _db.Set<JobOperation>()
        .Include(o => o.JobOrder)
        .Include(o => o.AssignedStaff)
        .Include(o => o.Vendor);

    public Task<List<JobOperation>> GetByJobOrderAsync(long jobOrderId) =>
        WithRefs()
            .Where(o => o.JobOrderId == jobOrderId)
            .OrderBy(o => o.SequenceNo).ThenBy(o => o.Id)
            .ToListAsync();

    public Task<List<JobOperation>> GetAllAsync() =>
        WithRefs().OrderByDescending(o => o.Id).ToListAsync();

    public Task<JobOperation?> GetByIdAsync(long id) =>
        WithRefs().FirstOrDefaultAsync(o => o.Id == id);

    public async Task<int> GetNextSequenceNoAsync(long jobOrderId)
    {
        var max = await _db.Set<JobOperation>()
            .Where(o => o.JobOrderId == jobOrderId)
            .Select(o => (int?)o.SequenceNo)
            .MaxAsync();
        return (max ?? 0) + 1;
    }

    public async Task AddAsync(JobOperation operation) =>
        await _db.Set<JobOperation>().AddAsync(operation);

    public void Update(JobOperation operation) =>
        _db.Set<JobOperation>().Update(operation);

    public void Remove(JobOperation operation) =>
        _db.Set<JobOperation>().Remove(operation);

    public Task<int> SaveChangesAsync() => _db.SaveChangesAsync();
}
