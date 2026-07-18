using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ShiftLedger.Domain.Entities;

namespace ShiftLedger.Infrastructure.Persistence.Configurations;

public class PayRateConfiguration : IEntityTypeConfiguration<PayRate>
{
    public void Configure(EntityTypeBuilder<PayRate> builder)
    {
        // Hot path for rate-for-date resolution (docs/03 index strategy).
        builder.HasIndex(r => new { r.EmployeeProfileId, r.EffectiveFrom });

        builder.HasOne<EmployeeProfile>()
            .WithMany()
            .HasForeignKey(r => r.EmployeeProfileId)
            .OnDelete(DeleteBehavior.Cascade);

        // Amount maps to DECIMAL(18,2) via the global decimal convention; the calendar
        // dates map to native DATE via DateOnly.
    }
}
