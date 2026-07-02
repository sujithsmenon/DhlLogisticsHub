namespace DhlLogistics.Web.Service;

using DhlLogistics.Shared.Models;
using DhlLogistics.Web.Database;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging;

/// <summary>One-time (repeatable) backfill result.</summary>
public sealed class BillingSyncResult
{
    public int Created { get; set; }
    public int Updated { get; set; }
    public int Skipped { get; set; }
    public int Failed  { get; set; }
    public int Total => Created + Updated + Skipped + Failed;
    public List<string> Errors { get; } = new();

    public override string ToString() =>
        $"{Total} job(s): {Created} created, {Updated} updated, {Skipped} skipped, {Failed} failed.";
}

/// <summary>
/// Idempotent data-migration service that reconciles existing Clearance/Forwarding jobs with their
/// 1:1 linked bill. Reuses <see cref="BillService.UpsertForJobAsync"/> (keyed on JobOrderId + Mode)
/// so it can be run any number of times without ever creating a duplicate bill:
/// <list type="bullet">
///   <item>Bill missing → create it (Draft, header FKs copied from the job).</item>
///   <item>Bill exists → verify FK link and refresh the copied header fields.</item>
/// </list>
/// Each job is committed independently so one bad row can't abort the whole run.
/// </summary>
public class BillingSyncService
{
    private readonly AppDbContext _db;
    private readonly BillService _bills;
    private readonly AccountingService _accounting;
    private readonly DashboardState _dash;
    private readonly ILogger<BillingSyncService> _log;

    private const string SyncActor = "billing-sync";

    public BillingSyncService(AppDbContext db, BillService bills, AccountingService accounting,
                              DashboardState dash, ILogger<BillingSyncService> log)
    {
        _db    = db;
        _bills = bills;
        _accounting = accounting;
        _dash  = dash;
        _log   = log;
    }

    /// <summary>
    /// Scans every Clearance/Forwarding job and ensures its 1:1 bill exists and is in sync.
    /// When <paramref name="updateExisting"/> is false (startup backfill), jobs that already have a
    /// bill are skipped so repeated boots don't churn the bill event log — only missing bills are created.
    /// </summary>
    public async Task<BillingSyncResult> SyncAllAsync(bool updateExisting = true)
    {
        var result = new BillingSyncResult();

        // Bills exist only after Approval (workflow rule #3), so only Approved (and Closed = approved+
        // completed) Clearance/Forwarding jobs are the source of truth for billing. Draft/Submitted/
        // Verified jobs have no bill yet and are intentionally skipped.
        var jobs = await _db.JobOrders
            .Where(j => (j.Mode == JobMode.Clearance || j.Mode == JobMode.Forwarding)
                     && (j.Status == JobOrderStatus.Approved || j.Status == JobOrderStatus.Closed))
            .OrderBy(j => j.Id)
            .ToListAsync();

        foreach (var job in jobs)
        {
            IDbContextTransaction? tx = null;
            try
            {
                var mode    = BillService.BillModeFor(job.Mode);
                var existed = await _db.Bills.AnyAsync(b => b.JobOrderId == job.Id && b.Mode == mode);

                // Each job is reconciled atomically so one bad row can't half-write bill + vouchers.
                tx = await _db.Database.BeginTransactionAsync();

                if (existed && !updateExisting)
                {
                    // Bill already present and this is the create-only startup pass → don't churn the bill,
                    // but still reconcile Accounts (idempotent — only posts vendor costs not yet posted).
                    await _accounting.PostJobExpensesAsync(job, SyncActor);
                    await tx.CommitAsync();
                    result.Skipped++;
                    continue;
                }

                await _bills.UpsertForJobAsync(job, SyncActor);          // create-if-missing / refresh headers
                await _accounting.PostJobExpensesAsync(job, SyncActor);  // vendor costs → expense/payable
                await tx.CommitAsync();
                if (existed) result.Updated++; else result.Created++;
            }
            catch (Exception ex)
            {
                if (tx is not null) await tx.RollbackAsync();
                result.Failed++;
                result.Errors.Add($"{job.JobOrderNo}: {ex.Message}");
                _log.LogWarning(ex, "Billing sync failed for job {JobNo}.", job.JobOrderNo);
            }
            finally
            {
                if (tx is not null) await tx.DisposeAsync();
            }
        }

        _dash.NotifyChanged();   // refresh dashboard counters after synchronization
        _log.LogInformation("Billing sync complete — {Result}", result);
        return result;
    }
}
