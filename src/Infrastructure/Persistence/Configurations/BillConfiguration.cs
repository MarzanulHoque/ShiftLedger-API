using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ShiftLedger.Domain.Entities;

namespace ShiftLedger.Infrastructure.Persistence.Configurations;

public class BillConfiguration : IEntityTypeConfiguration<Bill>
{
    public void Configure(EntityTypeBuilder<Bill> builder)
    {
        // Rule B1: one bill per job — enforced at the DB as the backstop to the handler check.
        builder.HasIndex(b => b.ServiceJobId).IsUnique();
        builder.HasIndex(b => b.IsPaid); // unpaid-bills report path (docs/03 index strategy)

        builder.HasOne<ServiceJob>()
            .WithOne()
            .HasForeignKey<Bill>(b => b.ServiceJobId)
            .OnDelete(DeleteBehavior.Restrict); // jobs are soft-deleted; the bill is retained (J4)
    }
}
