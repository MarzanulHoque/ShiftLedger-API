using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using ShiftLedger.Application.Common.Exceptions;
using ShiftLedger.Application.Users;
using ShiftLedger.Domain.Entities;
using ShiftLedger.Domain.Enums;
using ShiftLedger.Infrastructure.Persistence.Configurations;
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
        var admin = TestCurrentUser.SuperAdmin(Guid.NewGuid());

        var id = await new CreateUserCommandHandler(ctx, hasher, admin, TestDepartmentScope.For(admin))
            .Handle(new CreateUserCommand("Jane Doe", "jane@test.local", "Secret#123", Role.Employee, DepartmentConfiguration.MechanicsId), default);

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
        var admin = TestCurrentUser.SuperAdmin(Guid.NewGuid());
        var handler = new CreateUserCommandHandler(ctx, new PasswordHasher(), admin, TestDepartmentScope.For(admin));
        await handler.Handle(new CreateUserCommand("A", "dup@test.local", "Secret#123", Role.Employee, DepartmentConfiguration.MechanicsId), default);

        var act = async () =>
            await handler.Handle(new CreateUserCommand("B", "dup@test.local", "Secret#456", Role.Employee, DepartmentConfiguration.MechanicsId), default);

        await act.Should().ThrowAsync<BusinessRuleException>();
    }

    // Rule RB1: a Super Admin can never be created via this endpoint, even by a caller who is one.
    [Fact]
    public async Task CreateUser_SuperAdminRole_Rejected_RB1()
    {
        await using var ctx = fixture.CreateContext();
        var admin = TestCurrentUser.SuperAdmin(Guid.NewGuid());
        var act = () => new CreateUserCommandHandler(ctx, new PasswordHasher(), admin, TestDepartmentScope.For(admin))
            .Handle(new CreateUserCommand("Nope", "nope@test.local", "Secret#123", Role.SuperAdmin, null), default);

        await act.Should().ThrowAsync<BusinessRuleException>();
    }

    // Rule RB5: a DepartmentAdmin can only provision Employees into their own department.
    [Fact]
    public async Task CreateUser_DepartmentAdmin_CrossDepartmentOrWrongRole_Rejected_RB5()
    {
        await using var ctx = fixture.CreateContext();
        var deptAdmin = TestCurrentUser.DepartmentAdmin(Guid.NewGuid(), DepartmentConfiguration.MechanicsId);
        var handler = new CreateUserCommandHandler(ctx, new PasswordHasher(), deptAdmin, TestDepartmentScope.For(deptAdmin));

        var crossDept = () => handler.Handle(
            new CreateUserCommand("Wash Hire", "wash-hire@test.local", "Secret#123", Role.Employee, DepartmentConfiguration.BikeWashId), default);
        await crossDept.Should().ThrowAsync<ForbiddenException>();

        var escalate = () => handler.Handle(
            new CreateUserCommand("Promote Me", "promote-me@test.local", "Secret#123", Role.DepartmentAdmin, DepartmentConfiguration.MechanicsId), default);
        await escalate.Should().ThrowAsync<ForbiddenException>();

        var id = await handler.Handle(
            new CreateUserCommand("Own Dept Hire", "own-dept-hire@test.local", "Secret#123", Role.Employee, DepartmentConfiguration.MechanicsId), default);
        id.Should().NotBeEmpty();
    }

    [Fact]
    public async Task GetUsers_ExcludesSoftDeleted()
    {
        await using var ctx = fixture.CreateContext();
        var admin = TestCurrentUser.SuperAdmin(Guid.NewGuid());
        var id = await new CreateUserCommandHandler(ctx, new PasswordHasher(), admin, TestDepartmentScope.For(admin))
            .Handle(new CreateUserCommand("Del Ete", "delete@test.local", "Secret#123", Role.Employee, DepartmentConfiguration.MechanicsId), default);
        await new DeleteUserCommandHandler(ctx, admin, TestDepartmentScope.For(admin)).Handle(new DeleteUserCommand(id), default);

        await using var verify = fixture.CreateContext();
        var users = await new GetUsersQueryHandler(verify, admin).Handle(new GetUsersQuery(), default);
        users.Should().NotContain(u => u.Id == id);
    }

    // Rule RB5: a DepartmentAdmin's user list is scoped to their own department; SuperAdmin sees everyone.
    [Fact]
    public async Task GetUsers_DepartmentAdmin_SeesOnlyOwnDepartment_RB5()
    {
        await using var ctx = fixture.CreateContext();
        var admin = TestCurrentUser.SuperAdmin(Guid.NewGuid());
        var creator = new CreateUserCommandHandler(ctx, new PasswordHasher(), admin, TestDepartmentScope.For(admin));
        var mechanicsId = await creator.Handle(
            new CreateUserCommand("Mech Person", "mech-person@test.local", "Secret#123", Role.Employee, DepartmentConfiguration.MechanicsId), default);
        var washId = await creator.Handle(
            new CreateUserCommand("Wash Person", "wash-person@test.local", "Secret#123", Role.Employee, DepartmentConfiguration.BikeWashId), default);

        await using var verify = fixture.CreateContext();
        var deptAdmin = TestCurrentUser.DepartmentAdmin(Guid.NewGuid(), DepartmentConfiguration.MechanicsId);
        var users = await new GetUsersQueryHandler(verify, deptAdmin).Handle(new GetUsersQuery(), default);

        users.Should().Contain(u => u.Id == mechanicsId);
        users.Should().NotContain(u => u.Id == washId);
    }

    // Rule RB1: the Super Admin's role can never be changed, and no one else may be promoted into it.
    [Fact]
    public async Task UpdateUser_SuperAdmin_CannotBeEdited_RB1()
    {
        Guid superAdminId, employeeId;
        await using (var setup = fixture.CreateContext())
        {
            var superAdminRow = new User { FullName = "Root Admin", Email = "root-admin@test.local", PasswordHash = "n/a", Role = Role.SuperAdmin };
            setup.Users.Add(superAdminRow);
            await setup.SaveChangesAsync();
            superAdminId = superAdminRow.Id;

            var admin = TestCurrentUser.SuperAdmin(Guid.NewGuid());
            employeeId = await new CreateUserCommandHandler(setup, new PasswordHasher(), admin, TestDepartmentScope.For(admin))
                .Handle(new CreateUserCommand("Plain Employee", "plain-employee@test.local", "Secret#123", Role.Employee, DepartmentConfiguration.MechanicsId), default);
        }

        var superAdmin = TestCurrentUser.SuperAdmin(Guid.NewGuid());
        await using var ctx = fixture.CreateContext(superAdmin);
        var handler = new UpdateUserCommandHandler(ctx, superAdmin, TestDepartmentScope.For(superAdmin));

        var editSuperAdmin = () => handler.Handle(
            new UpdateUserCommand(superAdminId, "Renamed", Role.SuperAdmin, null, true), default);
        await editSuperAdmin.Should().ThrowAsync<BusinessRuleException>();

        var promoteEmployee = () => handler.Handle(
            new UpdateUserCommand(employeeId, "Plain Employee", Role.SuperAdmin, DepartmentConfiguration.MechanicsId, true), default);
        await promoteEmployee.Should().ThrowAsync<BusinessRuleException>();
    }

    // Rule RB5: a DepartmentAdmin can only edit Employees in their own department — not promote,
    // not reach into another department, not touch another DepartmentAdmin.
    [Fact]
    public async Task UpdateUser_DepartmentAdmin_ScopedToOwnDepartmentEmployees_RB5()
    {
        Guid mechanicsEmployeeId, washEmployeeId, peerDeptAdminId;
        await using (var setup = fixture.CreateContext())
        {
            var admin = TestCurrentUser.SuperAdmin(Guid.NewGuid());
            var creator = new CreateUserCommandHandler(setup, new PasswordHasher(), admin, TestDepartmentScope.For(admin));
            mechanicsEmployeeId = await creator.Handle(
                new CreateUserCommand("Mech Employee", "mech-employee-upd@test.local", "Secret#123", Role.Employee, DepartmentConfiguration.MechanicsId), default);
            washEmployeeId = await creator.Handle(
                new CreateUserCommand("Wash Employee", "wash-employee-upd@test.local", "Secret#123", Role.Employee, DepartmentConfiguration.BikeWashId), default);
            peerDeptAdminId = await creator.Handle(
                new CreateUserCommand("Peer Admin", "peer-admin-upd@test.local", "Secret#123", Role.DepartmentAdmin, DepartmentConfiguration.MechanicsId), default);
        }

        var deptAdmin = TestCurrentUser.DepartmentAdmin(Guid.NewGuid(), DepartmentConfiguration.MechanicsId);
        await using var ctx = fixture.CreateContext(deptAdmin);
        var handler = new UpdateUserCommandHandler(ctx, deptAdmin, TestDepartmentScope.For(deptAdmin));

        var crossDept = () => handler.Handle(
            new UpdateUserCommand(washEmployeeId, "Wash Employee", Role.Employee, DepartmentConfiguration.BikeWashId, true), default);
        await crossDept.Should().ThrowAsync<ForbiddenException>();

        var touchPeerAdmin = () => handler.Handle(
            new UpdateUserCommand(peerDeptAdminId, "Peer Admin", Role.DepartmentAdmin, DepartmentConfiguration.MechanicsId, true), default);
        await touchPeerAdmin.Should().ThrowAsync<ForbiddenException>();

        var moveOwnDeptEmployeeOut = () => handler.Handle(
            new UpdateUserCommand(mechanicsEmployeeId, "Mech Employee", Role.Employee, DepartmentConfiguration.BikeWashId, true), default);
        await moveOwnDeptEmployeeOut.Should().ThrowAsync<ForbiddenException>();

        await handler.Handle(
            new UpdateUserCommand(mechanicsEmployeeId, "Mech Employee Renamed", Role.Employee, DepartmentConfiguration.MechanicsId, true), default);
    }

    // Rule RB1: the Super Admin can never be deleted.
    [Fact]
    public async Task DeleteUser_SuperAdmin_Rejected_RB1()
    {
        Guid superAdminId;
        await using (var setup = fixture.CreateContext())
        {
            var superAdminRow = new User { FullName = "Root Admin", Email = "root-admin-del@test.local", PasswordHash = "n/a", Role = Role.SuperAdmin };
            setup.Users.Add(superAdminRow);
            await setup.SaveChangesAsync();
            superAdminId = superAdminRow.Id;
        }

        var superAdmin = TestCurrentUser.SuperAdmin(Guid.NewGuid());
        await using var ctx = fixture.CreateContext(superAdmin);
        var act = () => new DeleteUserCommandHandler(ctx, superAdmin, TestDepartmentScope.For(superAdmin))
            .Handle(new DeleteUserCommand(superAdminId), default);

        await act.Should().ThrowAsync<BusinessRuleException>();
    }

    // Rule RB5: a DepartmentAdmin can only delete Employees within their own department.
    [Fact]
    public async Task DeleteUser_DepartmentAdmin_CrossDepartmentOrNonEmployee_Rejected_RB5()
    {
        Guid washEmployeeId, peerDeptAdminId;
        await using (var setup = fixture.CreateContext())
        {
            var admin = TestCurrentUser.SuperAdmin(Guid.NewGuid());
            var creator = new CreateUserCommandHandler(setup, new PasswordHasher(), admin, TestDepartmentScope.For(admin));
            washEmployeeId = await creator.Handle(
                new CreateUserCommand("Wash Employee", "wash-employee-del@test.local", "Secret#123", Role.Employee, DepartmentConfiguration.BikeWashId), default);
            peerDeptAdminId = await creator.Handle(
                new CreateUserCommand("Peer Admin", "peer-admin-del@test.local", "Secret#123", Role.DepartmentAdmin, DepartmentConfiguration.MechanicsId), default);
        }

        var deptAdmin = TestCurrentUser.DepartmentAdmin(Guid.NewGuid(), DepartmentConfiguration.MechanicsId);
        await using var ctx = fixture.CreateContext(deptAdmin);
        var handler = new DeleteUserCommandHandler(ctx, deptAdmin, TestDepartmentScope.For(deptAdmin));

        var crossDept = () => handler.Handle(new DeleteUserCommand(washEmployeeId), default);
        await crossDept.Should().ThrowAsync<ForbiddenException>();

        var touchPeerAdmin = () => handler.Handle(new DeleteUserCommand(peerDeptAdminId), default);
        await touchPeerAdmin.Should().ThrowAsync<ForbiddenException>();
    }
}
