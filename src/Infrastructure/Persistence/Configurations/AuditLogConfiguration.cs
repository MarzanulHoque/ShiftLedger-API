using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ShiftLedger.Domain.Entities;

namespace ShiftLedger.Infrastructure.Persistence.Configurations;

public class AuditLogConfiguration : IEntityTypeConfiguration<AuditLog>
{
    public void Configure(EntityTypeBuilder<AuditLog> builder)
    {
        builder.Property(a => a.EntityName).HasMaxLength(128);
        builder.Property(a => a.EntityId).HasMaxLength(64);
        builder.Property(a => a.Action).HasMaxLength(16);
        builder.HasIndex(a => new { a.EntityName, a.EntityId }); // entity/task history lookups
    }
}
