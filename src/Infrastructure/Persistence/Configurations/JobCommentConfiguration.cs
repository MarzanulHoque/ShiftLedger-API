using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ShiftLedger.Domain.Entities;

namespace ShiftLedger.Infrastructure.Persistence.Configurations;

public class JobCommentConfiguration : IEntityTypeConfiguration<JobComment>
{
    public void Configure(EntityTypeBuilder<JobComment> builder)
    {
        builder.Property(c => c.Body).HasMaxLength(2000);
        builder.HasIndex(c => c.ServiceJobId);

        builder.HasOne<ServiceJob>()
            .WithMany()
            .HasForeignKey(c => c.ServiceJobId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(c => c.AuthorId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
