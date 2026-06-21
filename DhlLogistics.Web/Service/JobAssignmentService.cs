namespace DhlLogistics.Web.Service;

using DhlLogistics.Shared.Models;
using DhlLogistics.Web.Database;

public class JobAssignmentService
{
    private readonly AppDbContext       _db;
    private readonly NotificationService _notify;
    private readonly DashboardState      _dash;

    public JobAssignmentService(AppDbContext db, NotificationService notify, DashboardState dash)
    {
        _db     = db;
        _notify = notify;
        _dash   = dash;
    }

    public async Task CreateJobFromEmailAsync(PickupInfo info, string emailSubject)
    {
        var job = new PickupJob
        {
            JobCode       = $"JOB-{DateTime.UtcNow:yyyyMMdd-HHmmss}",
            ClientName    = info.ClientName,
            PickupAddress = info.PickupAddress,
            PickupCity    = info.PickupCity,
            PickupDate    = info.PickupDate,
            VolumeCbm     = info.VolumeCbm,
            WeightKg      = info.WeightKg,
            Destination   = info.Destination,
            DhlReference  = info.DhlReference,
            Status        = JobStatus.Pending,
        };

        _db.Jobs.Add(job);
        await _db.SaveChangesAsync();
        _dash.NotifyChanged();   // new Pending pickup → refresh dashboard counters

        // Push to all managers/admins
        await _notify.NotifyManagersAsync(
            title:   "New Pickup Job",
            body:    $"{job.JobCode} — {job.ClientName}, {job.PickupCity}",
            type:    "NewJob",
            jobId:   job.Id,
            jobCode: job.JobCode);
    }

    public async Task AssignJobAsync(int jobId, string executiveId, int? vehicleId = null)
    {
        var job = await _db.Jobs.FindAsync(jobId);
        if (job is null) return;

        job.AssignedExecutiveId = executiveId;
        job.AssignedVehicleId   = vehicleId;
        job.AssignedAt          = DateTime.UtcNow;
        job.Status              = JobStatus.Assigned;

        await _db.SaveChangesAsync();
        _dash.NotifyChanged();   // status change (Pending → Assigned) → refresh counters

        // Notify the executive their job is ready
        await _notify.NotifyUserAsync(
            userId:  executiveId,
            title:   "Job Assigned to You",
            body:    $"{job.JobCode} — Pick up from {job.PickupAddress}, {job.PickupCity}",
            type:    "JobAssigned",
            jobId:   job.Id,
            jobCode: job.JobCode);
    }
}
