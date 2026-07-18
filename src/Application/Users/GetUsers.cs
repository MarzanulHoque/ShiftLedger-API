using MediatR;
using Microsoft.EntityFrameworkCore;
using ShiftLedger.Application.Common.Interfaces;
using ShiftLedger.Domain.Enums;

namespace ShiftLedger.Application.Users;

public record UserDto(Guid Id, string FullName, string Email, Role Role, Guid? DepartmentId, bool IsActive);

public record GetUsersQuery : IRequest<IReadOnlyList<UserDto>>;

public class GetUsersQueryHandler(IAppDbContext db) : IRequestHandler<GetUsersQuery, IReadOnlyList<UserDto>>
{
    public async Task<IReadOnlyList<UserDto>> Handle(GetUsersQuery request, CancellationToken cancellationToken)
    {
        // Soft-deleted users are excluded by the global query filter.
        return await db.Users.AsNoTracking()
            .OrderBy(u => u.FullName)
            .Select(u => new UserDto(u.Id, u.FullName, u.Email, u.Role, u.DepartmentId, u.IsActive))
            .ToListAsync(cancellationToken);
    }
}
