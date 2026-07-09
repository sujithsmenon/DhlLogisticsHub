namespace DhlLogistics.Web.Database.Configurations;

using DhlLogistics.Shared.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

/// <summary>
/// EF Core mapping for the new <see cref="JobOperation"/> tracking entity. Kept in its own
/// configuration class (applied via <c>ApplyConfiguration</c> in <see cref="AppDbContext"/>) so
/// the foundation table is fully self-contained and does not touch any existing JobOrder mapping.
/// </summary>
public class JobOperationConfiguration : IEntityTypeConfiguration<JobOperation>
{
    public void Configure(EntityTypeBuilder<JobOperation> e)
    {
        e.ToTable("JobOperations");
        e.HasKey(o => o.Id);

        e.Property(o => o.OperationType).HasMaxLength(100).IsRequired();
        e.Property(o => o.Department).HasMaxLength(100);
        e.Property(o => o.Status).HasConversion<int>();
        e.Property(o => o.Priority).HasConversion<int>();

        // Parent job: 1 JobOrder ──< many JobOperation. Cascade-delete children with the job.
        // Uses a shadow collection (WithMany()) so the existing JobOrder entity is NOT modified.
        e.HasOne(o => o.JobOrder)
            .WithMany()
            .HasForeignKey(o => o.JobOrderId)
            .OnDelete(DeleteBehavior.Cascade);

        e.HasOne(o => o.AssignedStaff)
            .WithMany()
            .HasForeignKey(o => o.AssignedStaffId)
            .OnDelete(DeleteBehavior.SetNull);

        e.HasOne(o => o.Vendor)
            .WithMany()
            .HasForeignKey(o => o.VendorId)
            .OnDelete(DeleteBehavior.SetNull);

        e.HasIndex(o => o.JobOrderId);
        e.HasIndex(o => new { o.JobOrderId, o.SequenceNo });
        e.HasIndex(o => o.Status);
    }
}
