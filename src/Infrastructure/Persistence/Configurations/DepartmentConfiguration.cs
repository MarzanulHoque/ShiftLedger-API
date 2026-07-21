using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ShiftLedger.Domain.Entities;

namespace ShiftLedger.Infrastructure.Persistence.Configurations;

public class DepartmentConfiguration : IEntityTypeConfiguration<Department>
{
    // Phase-1 departments (v2 restructure): fixed IDs so every layer - migrations, seed data,
    // tests - can reference a real, always-present Department without a lookup query.
    public static readonly Guid MechanicsId = new("00000000-0000-0000-0000-000000000101");
    public static readonly Guid BikeWashId = new("00000000-0000-0000-0000-000000000102");
    private static readonly Guid MechanicsRowVersion = new("00000000-0000-0000-0000-0000000000b1");
    private static readonly Guid BikeWashRowVersion = new("00000000-0000-0000-0000-0000000000b2");

    public void Configure(EntityTypeBuilder<Department> builder)
    {
        builder.Property(d => d.Name).HasMaxLength(128);
        builder.HasIndex(d => d.Name).IsUnique();

        builder.HasData(
            new Department { Id = MechanicsId, Name = "Mechanics", RowVersion = MechanicsRowVersion },
            new Department { Id = BikeWashId, Name = "Bike Wash", RowVersion = BikeWashRowVersion });
    }
}
