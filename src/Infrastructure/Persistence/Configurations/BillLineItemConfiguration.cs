using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ShiftLedger.Domain.Entities;

namespace ShiftLedger.Infrastructure.Persistence.Configurations;

public class BillLineItemConfiguration : IEntityTypeConfiguration<BillLineItem>
{
    public void Configure(EntityTypeBuilder<BillLineItem> builder)
    {
        builder.Property(l => l.Description).HasMaxLength(300);
        builder.HasIndex(l => l.BillId);

        // Quantity and UnitPrice map to DECIMAL(18,2) via the global decimal convention.
        builder.HasOne<Bill>()
            .WithMany()
            .HasForeignKey(l => l.BillId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
