using MediatR;
using Microsoft.EntityFrameworkCore;
using ShiftLedger.Application.Common.Exceptions;
using ShiftLedger.Application.Common.Interfaces;

namespace ShiftLedger.Application.Departments;

public record DeleteDepartmentCommand(Guid Id) : IRequest;

public class DeleteDepartmentCommandHandler(IAppDbContext db) : IRequestHandler<DeleteDepartmentCommand>
{
    public async Task Handle(DeleteDepartmentCommand request, CancellationToken cancellationToken)
    {
        var department = await db.Departments.FirstOrDefaultAsync(d => d.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException("Department not found.");

        // Department is not soft-deletable; users referencing it have DepartmentId set to null (FK ON DELETE SET NULL).
        db.Departments.Remove(department);
        await db.SaveChangesAsync(cancellationToken);
    }
}
