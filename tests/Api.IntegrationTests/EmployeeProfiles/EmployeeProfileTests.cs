using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using ShiftLedger.Application.Common.Exceptions;
using ShiftLedger.Application.EmployeeProfiles;
using ShiftLedger.Application.PayRates;
using ShiftLedger.Application.Users;
using ShiftLedger.Domain.Enums;
using ShiftLedger.Infrastructure.Persistence;
using ShiftLedger.Infrastructure.Security;
using Xunit;

namespace ShiftLedger.Api.IntegrationTests.EmployeeProfiles;

[Collection("Database")]
public class EmployeeProfileTests(IntegrationTestFixture fixture)
{
    private async Task<Guid> CreateUserAsync(AppDbContext ctx, string email)
        => await new CreateUserCommandHandler(ctx, new PasswordHasher())
            .Handle(new CreateUserCommand("Emp Loyee", email, "Secret#123", Role.Employee, null), default);

    [Fact]
    public async Task Upsert_CreatesThenUpdates_SameSingleProfile()
    {
        await using var ctx = fixture.CreateContext();
        var userId = await CreateUserAsync(ctx, "profile-upsert@test.local");
        var handler = new UpsertEmployeeProfileCommandHandler(ctx);

        var created = await handler.Handle(
            new UpsertEmployeeProfileCommand(userId, RateType.Hourly, PayCycle.Weekly), default);
        var updated = await handler.Handle(
            new UpsertEmployeeProfileCommand(userId, RateType.Monthly, PayCycle.Monthly), default);

        updated.Id.Should().Be(created.Id); // updated in place, not a second profile
        updated.RateType.Should().Be(RateType.Monthly);
        updated.PayCycle.Should().Be(PayCycle.Monthly);

        await using var verify = fixture.CreateContext();
        (await verify.EmployeeProfiles.CountAsync(p => p.UserId == userId)).Should().Be(1);
    }

    [Fact]
    public async Task Upsert_UnknownUser_Throws()
    {
        await using var ctx = fixture.CreateContext();
        var act = async () => await new UpsertEmployeeProfileCommandHandler(ctx)
            .Handle(new UpsertEmployeeProfileCommand(Guid.NewGuid(), RateType.Hourly, PayCycle.Weekly), default);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    // Rule P3: adding a dated rate closes the prior one (append-only) and a past date resolves to the historical rate.
    [Fact]
    public async Task AddPayRate_ClosesPriorRate_AndResolvesPeriodCorrectRate()
    {
        await using var ctx = fixture.CreateContext();
        var userId = await CreateUserAsync(ctx, "payrate-history@test.local");
        await new UpsertEmployeeProfileCommandHandler(ctx)
            .Handle(new UpsertEmployeeProfileCommand(userId, RateType.Hourly, PayCycle.Weekly), default);
        var addRate = new AddPayRateCommandHandler(ctx);

        await addRate.Handle(new AddPayRateCommand(userId, 20m, new DateOnly(2026, 1, 1)), default);
        await addRate.Handle(new AddPayRateCommand(userId, 25m, new DateOnly(2026, 7, 1)), default);

        await using var verify = fixture.CreateContext();
        var rates = await new GetPayRatesQueryHandler(verify).Handle(new GetPayRatesQuery(userId), default);

        rates.Should().HaveCount(2);
        rates.Single(r => r.Amount == 20m).EffectiveTo.Should().Be(new DateOnly(2026, 6, 30)); // closed
        rates.Single(r => r.Amount == 25m).EffectiveTo.Should().BeNull();                       // current

        var domainRates = await verify.PayRates.AsNoTracking()
            .Where(r => r.EmployeeProfileId == verify.EmployeeProfiles.First(p => p.UserId == userId).Id)
            .ToListAsync();
        PayRateResolver.Resolve(domainRates, new DateOnly(2026, 3, 15))!.Amount.Should().Be(20m);
        PayRateResolver.Resolve(domainRates, new DateOnly(2026, 8, 1))!.Amount.Should().Be(25m);
    }

    [Fact]
    public async Task AddPayRate_EffectiveNotAfterCurrent_Throws()
    {
        await using var ctx = fixture.CreateContext();
        var userId = await CreateUserAsync(ctx, "payrate-order@test.local");
        await new UpsertEmployeeProfileCommandHandler(ctx)
            .Handle(new UpsertEmployeeProfileCommand(userId, RateType.Hourly, PayCycle.Weekly), default);
        var addRate = new AddPayRateCommandHandler(ctx);
        await addRate.Handle(new AddPayRateCommand(userId, 20m, new DateOnly(2026, 7, 1)), default);

        var act = async () => await addRate.Handle(
            new AddPayRateCommand(userId, 25m, new DateOnly(2026, 6, 1)), default); // earlier than current

        await act.Should().ThrowAsync<BusinessRuleException>();
    }

    [Fact]
    public async Task AddPayRate_WithoutProfile_Throws()
    {
        await using var ctx = fixture.CreateContext();
        var userId = await CreateUserAsync(ctx, "payrate-noprofile@test.local");

        var act = async () => await new AddPayRateCommandHandler(ctx)
            .Handle(new AddPayRateCommand(userId, 20m, new DateOnly(2026, 1, 1)), default);

        await act.Should().ThrowAsync<NotFoundException>();
    }
}
