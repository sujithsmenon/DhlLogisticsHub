namespace DhlLogistics.Web.Service;

using DhlLogistics.Shared.Models;
using DhlLogistics.Web.Repository;
using Microsoft.AspNetCore.Components.Authorization;

/// <summary>
/// Application service for the <see cref="JobOperation"/> tracking entity — the foundation that lets
/// every <see cref="JobOrder"/> carry multiple operations (Customs Clearance, Freight Booking,
/// Transportation, Warehousing, …). Handles CRUD, audit-field stamping and sequence assignment on
/// top of <see cref="IJobOperationRepository"/>. Deliberately does NOT touch the existing JobOrder
/// workflow, billing or the inline <see cref="JobOrderOperation"/> sub-grid.
/// </summary>
public class JobOperationService
{
    private readonly IJobOperationRepository _repo;
    private readonly AuthenticationStateProvider _authProvider;

    public JobOperationService(IJobOperationRepository repo, AuthenticationStateProvider authProvider)
    {
        _repo = repo;
        _authProvider = authProvider;
    }

    private async Task<string> CurrentUserAsync()
    {
        var s = await _authProvider.GetAuthenticationStateAsync();
        return s.User?.Identity?.Name ?? "system";
    }

    // ── Queries ────────────────────────────────────────────────────────────────

    public async Task<List<JobOperationDto>> GetByJobOrderAsync(long jobOrderId)
    {
        var rows = await _repo.GetByJobOrderAsync(jobOrderId);
        return rows.Select(JobOperationDto.FromEntity).ToList();
    }

    /// <summary>Every operation across all jobs — feeds the Operations Dashboard.</summary>
    public async Task<List<JobOperationDto>> GetAllAsync()
    {
        var rows = await _repo.GetAllAsync();
        return rows.Select(JobOperationDto.FromEntity).ToList();
    }

    public async Task<JobOperationDto?> GetByIdAsync(long id)
    {
        var row = await _repo.GetByIdAsync(id);
        return row is null ? null : JobOperationDto.FromEntity(row);
    }

    // ── Commands ─────────────────────────────────────────────────────────────────

    public async Task<JobOperation> CreateAsync(JobOperation op)
    {
        var user = await CurrentUserAsync();
        if (op.SequenceNo <= 0)
            op.SequenceNo = await _repo.GetNextSequenceNoAsync(op.JobOrderId);
        op.CreatedBy  = user;
        op.CreatedOn  = DateTime.UtcNow;
        op.ModifiedBy = null;
        op.ModifiedOn = null;

        await _repo.AddAsync(op);
        await _repo.SaveChangesAsync();
        return op;
    }

    public async Task<JobOperation> UpdateAsync(JobOperation op)
    {
        var user = await CurrentUserAsync();
        var existing = await _repo.GetByIdAsync(op.Id)
            ?? throw new KeyNotFoundException($"JobOperation {op.Id} not found.");

        // Workflow rule: a Completed operation can never be moved back to Draft.
        if (existing.Status == JobOperationStatus.Completed && op.Status == JobOperationStatus.Draft)
            throw new InvalidOperationException("A completed operation cannot be returned to Draft.");

        existing.SequenceNo      = op.SequenceNo;
        existing.OperationType   = op.OperationType;
        existing.Department      = op.Department;
        existing.Status          = op.Status;
        existing.Priority        = op.Priority;
        existing.PlannedDate     = op.PlannedDate;
        existing.ActualDate      = op.ActualDate;
        existing.AssignedStaffId = op.AssignedStaffId;
        existing.VendorId        = op.VendorId;
        existing.Remarks         = op.Remarks;
        existing.ModifiedBy      = user;
        existing.ModifiedOn      = DateTime.UtcNow;

        _repo.Update(existing);
        await _repo.SaveChangesAsync();
        return existing;
    }

    public async Task<bool> DeleteAsync(long id)
    {
        var existing = await _repo.GetByIdAsync(id);
        if (existing is null) return false;

        _repo.Remove(existing);
        await _repo.SaveChangesAsync();
        return true;
    }
}
