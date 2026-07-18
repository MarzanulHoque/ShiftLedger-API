using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using ShiftLedger.Application.Common.Exceptions;
using ShiftLedger.Application.Users;
using ShiftLedger.Domain.Enums;
using ShiftLedger.Infrastructure.Security;
using Xunit;

namespace ShiftLedger.Api.IntegrationTests.Users;

[Collection("Database")]
public class UserManagementTests(IntegrationTestFixture fixture)
{
    [Fact]
    public async Task CreateUser_PersistsHashedPassword()
    {
        await using var ctx = fixture.CreateContext();
        var hasher = new PasswordHasher();

        var id = await new CreateUserCommandHandler(ctx, hasher)
            .Handle(new CreateUserCommand("Jane Doe", "jane@test.local", "Secret#123", Role.Employee, null), default);

        await using var verify = fixture.CreateContext();
        var user = await verify.Users.FirstAsync(u => u.Id == id);
        user.Email.Should().Be("jane@test.local");
        user.PasswordHash.Should().NotBe("Secret#123");
        hasher.Verify("Secret#123", user.PasswordHash).Should().BeTrue();
    }

    [Fact]
    public async Task CreateUser_DuplicateEmail_Throws()
    {
        await using var ctx = fixture.CreateContext();
        var handler = new CreateUserCommandHandler(ctx, new PasswordHasher());
        await handler.Handle(new CreateUserCommand("A", "dup@test.local", "Secret#123", Role.Employee, null), default);

        var act = async () =>
            await handler.Handle(new CreateUserCommand("B", "dup@test.local", "Secret#456", Role.Employee, null), default);

        await act.Should().ThrowAsync<BusinessRuleException>();
    }

    [Fact]
    public async Task GetUsers_ExcludesSoftDeleted()
    {
        await using var ctx = fixture.CreateContext();
        var id = await new CreateUserCommandHandler(ctx, new PasswordHasher())
            .Handle(new CreateUserCommand("Del Ete", "delete@test.local", "Secret#123", Role.Employee, null), default);
        await new DeleteUserCommandHandler(ctx).Handle(new DeleteUserCommand(id), default);

        await using var verify = fixture.CreateContext();
        var users = await new GetUsersQueryHandler(verify).Handle(new GetUsersQuery(), default);
        users.Should().NotContain(u => u.Id == id);
    }
}
