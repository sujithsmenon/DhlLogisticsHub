namespace DhlLogistics.Web.Repository;

using DhlLogistics.Shared.Models;

/// <summary>
/// Data-access contract for the <see cref="JobOperation"/> tracking entity. Keeps EF query/persist
/// concerns behind an interface so the service layer (and future UI/API) stay storage-agnostic.
/// </summary>
public interface IJobOperationRepository
{
    Task<List<JobOperation>> GetByJobOrderAsync(long jobOrderId);
    Task<List<JobOperation>> GetAllAsync();
    Task<JobOperation?> GetByIdAsync(long id);
    Task<int> GetNextSequenceNoAsync(long jobOrderId);

    Task AddAsync(JobOperation operation);
    void Update(JobOperation operation);
    void Remove(JobOperation operation);

    Task<int> SaveChangesAsync();
}
