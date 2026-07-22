using System.Reflection;
using FluentAssertions;
using Microsoft.AspNetCore.Authorization;
using ShiftLedger.Api.Controllers;
using Xunit;

namespace ShiftLedger.Api.IntegrationTests.Hardening;

// Rule R1/R3: admin-only surfaces are gated server-side. This guards the [Authorize(Roles=Admin)]
// attributes so removing one is a failing test, not a silent privilege leak. (Handler-level
// own-data scoping for mechanics is covered by the R2/R3 tests in Jobs/.) No DB needed.
public class AdminAuthorizationTests
{
    private static bool RequiresAdmin(MemberInfo member) =>
        member.GetCustomAttributes<AuthorizeAttribute>(inherit: true)
            .Any(a => a.Roles?.Contains("Admin") == true);

    // P9: department CRUD is narrowed to SuperAdmin only (RB0 already grants org-wide CRUD).
    private static bool RequiresSuperAdminOnly(MemberInfo member) =>
        member.GetCustomAttributes<AuthorizeAttribute>(inherit: true)
            .Any(a => a.Roles == "SuperAdmin");

    // Whole controllers that are the owner's domain (R1).
    [Theory]
    [InlineData(typeof(UsersController))]
    [InlineData(typeof(DepartmentsController))]
    [InlineData(typeof(BillsController))]
    [InlineData(typeof(ReportsController))]
    public void AdminControllers_RequireAdminRole_R1(Type controller)
    {
        RequiresAdmin(controller).Should().BeTrue($"{controller.Name} is Admin-only (R1)");
    }

    // Mixed controllers: the admin-only actions must carry the role themselves (R1).
    [Theory]
    [InlineData(typeof(JobsController), nameof(JobsController.Create))]
    [InlineData(typeof(JobsController), nameof(JobsController.Update))]
    [InlineData(typeof(JobsController), nameof(JobsController.Delete))]
    [InlineData(typeof(JobsController), nameof(JobsController.Assign))]
    [InlineData(typeof(JobsController), nameof(JobsController.GetHistory))]
    [InlineData(typeof(DashboardController), nameof(DashboardController.Admin))]
    public void AdminActions_RequireAdminRole_R1(Type controller, string action)
    {
        RequiresAdmin(controller.GetMethod(action)!).Should().BeTrue($"{controller.Name}.{action} is Admin-only (R1)");
    }

    [Theory]
    [InlineData(typeof(DepartmentsController), nameof(DepartmentsController.Create))]
    [InlineData(typeof(DepartmentsController), nameof(DepartmentsController.Update))]
    [InlineData(typeof(DepartmentsController), nameof(DepartmentsController.Delete))]
    public void DepartmentMutations_RequireSuperAdminOnly_P9(Type controller, string action)
    {
        RequiresSuperAdminOnly(controller.GetMethod(action)!).Should().BeTrue($"{controller.Name}.{action} is SuperAdmin-only (P9)");
    }
}
