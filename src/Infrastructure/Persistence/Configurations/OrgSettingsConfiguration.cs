using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ShiftLedger.Domain.Entities;

namespace ShiftLedger.Infrastructure.Persistence.Configurations;

public class OrgSettingsConfiguration : IEntityTypeConfiguration<OrgSettings>
{
    // Fixed identifiers so the single seed row is stable across migrations.
    private static readonly Guid SeedId = new("00000000-0000-0000-0000-000000000001");
    private static readonly Guid SeedRowVersion = new("00000000-0000-0000-0000-0000000000a1");

    public void Configure(EntityTypeBuilder<OrgSettings> builder)
    {
        builder.Property(o => o.CurrencyCode).HasMaxLength(3).IsFixedLength();

        // The single org-config row (Rule T4: no time zone). Admin-editable via the API later.
        builder.HasData(new OrgSettings
        {
            Id = SeedId,
            WeekStartDay = DayOfWeek.Monday,
            CurrencyCode = "USD",
            OvertimeMultiplier = 1.5m,
            UnpaidAlertDays = 7,
            RowVersion = SeedRowVersion,
        });
    }
}
