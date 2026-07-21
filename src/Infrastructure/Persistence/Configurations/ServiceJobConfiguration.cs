using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ShiftLedger.Domain.Entities;

namespace ShiftLedger.Infrastructure.Persistence.Configurations;

public class ServiceJobConfiguration : IEntityTypeConfiguration<ServiceJob>
{
    public void Configure(EntityTypeBuilder<ServiceJob> builder)
    {
        builder.Property(j => j.JobNumber).ValueGeneratedOnAdd();
        builder.HasIndex(j => j.JobNumber).IsUnique();

        builder.Property(j => j.Title).HasMaxLength(200);
        builder.Property(j => j.Description).HasMaxLength(2000);
        builder.Property(j => j.BikeModel).HasMaxLength(128);

        // Report/board query paths (docs/03 index strategy). Calendar dates map to native DATE via DateOnly.
        builder.HasIndex(j => j.Status);
        builder.HasIndex(j => j.AssignedMechanicId);
        builder.HasIndex(j => j.ReceivedDate);
        builder.HasIndex(j => j.DepartmentId); // department scope filter (Rule RB3)

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(j => j.AssignedMechanicId)
            .OnDelete(DeleteBehavior.SetNull);

        // Every job belongs to exactly one department (Rule RB3); Bill/BillLineItem/JobComment
        // inherit scope through this FK rather than duplicating DepartmentId (avoids C2 drift).
        builder.HasOne<Department>()
            .WithMany()
            .HasForeignKey(j => j.DepartmentId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
