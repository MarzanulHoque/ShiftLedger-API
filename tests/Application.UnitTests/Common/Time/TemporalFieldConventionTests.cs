using System.Reflection;
using FluentAssertions;
using ShiftLedger.Domain.Common;
using ShiftLedger.Domain.Entities;
using Xunit;

namespace ShiftLedger.Application.UnitTests.Common.Time;

// Guards the two temporal conventions the whole time model rests on (docs/03 header).
public class TemporalFieldConventionTests
{
    private static readonly Type[] EntityTypes = typeof(BaseEntity).Assembly.GetTypes()
        .Where(t => t is { IsClass: true, Namespace: "ShiftLedger.Domain.Entities" })
        .ToArray();

    // Rule T4: no time zone is ever stored, and every stored instant is UTC (its name says so).
    [Fact]
    public void Entities_StoreNoTimeZone_AndAllInstantsAreUtcSuffixed_T4()
    {
        var properties = EntityTypes.SelectMany(t => t.GetProperties(BindingFlags.Public | BindingFlags.Instance)).ToList();

        properties.Should().NotContain(p => p.Name.Contains("TimeZone"),
            "no org-wide or per-user time-zone id may be persisted (T4)");

        var instantProps = properties
            .Where(p => p.PropertyType == typeof(DateTime) || p.PropertyType == typeof(DateTime?));
        instantProps.Should().OnlyContain(p => p.Name.EndsWith("Utc"),
            "every stored instant is UTC and named *Utc (T4)");
    }

    // Rule T9: calendar-day fields are DateOnly (native DATE) — they never shift across zones.
    [Fact]
    public void CalendarDateFields_AreDateOnly_T9()
    {
        typeof(ServiceJob).GetProperty(nameof(ServiceJob.ReceivedDate))!.PropertyType.Should().Be(typeof(DateOnly));
        typeof(ServiceJob).GetProperty(nameof(ServiceJob.DueDate))!.PropertyType.Should().Be(typeof(DateOnly?));
        typeof(PayRate).GetProperty(nameof(PayRate.EffectiveFrom))!.PropertyType.Should().Be(typeof(DateOnly));
        typeof(PayRate).GetProperty(nameof(PayRate.EffectiveTo))!.PropertyType.Should().Be(typeof(DateOnly?));
    }
}
