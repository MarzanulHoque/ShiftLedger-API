using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ShiftLedger.Domain.Entities;

namespace ShiftLedger.Infrastructure.Persistence.Configurations;

public class EmployeeProfileConfiguration : IEntityTypeConfiguration<EmployeeProfile>
{
    public void Configure(EntityTypeBuilder<EmployeeProfile> builder)
    {
        // 1:1 with User: one profile per user at most.
        builder.HasIndex(p => p.UserId).IsUnique();

        builder.HasOne<User>()
            .WithOne()
            .HasForeignKey<EmployeeProfile>(p => p.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        // RateType/PayCycle persist as strings via the global enum convention (AppDbContext.OnModelCreating).
    }
}
