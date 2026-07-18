using MediatR;
using Microsoft.EntityFrameworkCore;
using ShiftLedger.Application.Common.Interfaces;

namespace ShiftLedger.Application.Departments;

public record DepartmentDto(Guid Id, string Name);

public record GetDepartmentsQuery : IRequest<IReadOnlyList<DepartmentDto>>;

public class GetDepartmentsQueryHandler(IAppDbContext db) : IRequestHandler<GetDepartmentsQuery, IReadOnlyList<DepartmentDto>>
{
    public async Task<IReadOnlyList<DepartmentDto>> Handle(GetDepartmentsQuery request, CancellationToken cancellationToken)
    {
        return await db.Departments.AsNoTracking()
            .OrderBy(d => d.Name)
            .Select(d => new DepartmentDto(d.Id, d.Name))
            .ToListAsync(cancellationToken);
    }
}
