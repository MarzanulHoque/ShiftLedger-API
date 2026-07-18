using ShiftLedger.Domain.Entities;

namespace ShiftLedger.Application.PayRates;

// Resolves which effective-dated rate applies on a given calendar date (Rule P3): the rate
// whose window [EffectiveFrom, EffectiveTo] contains the date, where a null EffectiveTo means
// "still current". Payroll (P6) uses this so a payslip always snapshots the period-correct rate.
public static class PayRateResolver
{
    public static PayRate? Resolve(IEnumerable<PayRate> rates, DateOnly date) =>
        rates
            .Where(r => r.EffectiveFrom <= date && (r.EffectiveTo == null || date <= r.EffectiveTo))
            .OrderByDescending(r => r.EffectiveFrom)
            .FirstOrDefault();
}
