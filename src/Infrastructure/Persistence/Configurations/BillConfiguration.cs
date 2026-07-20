using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ShiftLedger.Domain.Entities;

namespace ShiftLedger.Infrastructure.Persistence.Configurations;

public class BillConfiguration : IEntityTypeConfiguration<Bill>
{
    public void Configure(EntityTypeBuilder<Bill> builder)
    {
        builder.Property(b => b.BillNumber).ValueGeneratedOnAdd();
        builder.HasIndex(b => b.BillNumber).IsUnique();

        // Rule B1: one bill per job — enforced at the DB as the backstop to the handler check.
        builder.HasIndex(b => b.ServiceJobId).IsUnique();
        builder.HasIndex(b => b.IsPaid); // unpaid-bills report path (docs/03 index strategy)

        builder.HasOne<ServiceJob>()
            .WithOne()
            .HasForeignKey<Bill>(b => b.ServiceJobId)
            .OnDelete(DeleteBehavior.Restrict); // jobs are soft-deleted (J4); DeleteJobCommandHandler
            // decides the bill's fate itself (blocks if paid, cascades the soft-delete if unpaid) —
            // this FK only guards against a stray hard delete slipping past that handler.
    }
}
