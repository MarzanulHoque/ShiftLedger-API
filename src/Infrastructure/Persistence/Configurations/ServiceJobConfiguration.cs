using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ShiftLedger.Domain.Entities;

namespace ShiftLedger.Infrastructure.Persistence.Configurations;

public class ServiceJobConfiguration : IEntityTypeConfiguration<ServiceJob>
{
    public void Configure(EntityTypeBuilder<ServiceJob> builder)
    {
        builder.Property(j => j.Title).HasMaxLength(200);
        builder.Property(j => j.Description).HasMaxLength(2000);
        builder.Property(j => j.BikeModel).HasMaxLength(128);

        // Report/board query paths (docs/03 index strategy). Calendar dates map to native DATE via DateOnly.
        builder.HasIndex(j => j.Status);
        builder.HasIndex(j => j.AssignedMechanicId);
        builder.HasIndex(j => j.ReceivedDate);

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(j => j.AssignedMechanicId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
