using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ShiftLedger.Domain.Entities;

namespace ShiftLedger.Infrastructure.Persistence.Configurations;

public class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.Property(u => u.FullName).HasMaxLength(200);
        builder.Property(u => u.Email).HasMaxLength(256);
        builder.Property(u => u.PasswordHash).HasMaxLength(512);
        builder.HasIndex(u => u.Email).IsUnique(); // case-insensitive via MySQL default collation

        builder.HasOne<Department>()
            .WithMany()
            .HasForeignKey(u => u.DepartmentId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
