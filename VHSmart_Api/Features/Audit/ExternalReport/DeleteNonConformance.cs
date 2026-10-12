using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using VHSmart_Api.Shared.Exceptions;
using VHSmart_Api.Shared.Infrastructure.Persistence;
using VHSmart_Api.Shared.Infrastructure.Security;

namespace VHSmart_Api.Features.Audit.ExternalReport;

// Delete one NC of the report's tab (soft delete, CodingRules 7.1). The row must belong
// to a live own-company report; unknown/foreign -> 404.
public record DeleteNonConformanceCommand(Guid Id) : IRequest;

public class DeleteNonConformanceValidator : AbstractValidator<DeleteNonConformanceCommand>
{
    public DeleteNonConformanceValidator()
    {
        RuleFor(x => x.Id)
            .NotEmpty()
            .WithMessage("Id is required.");
    }
}

public class DeleteNonConformanceHandler(VHSmartDbContext db, ICurrentUser user)
    : IRequestHandler<DeleteNonConformanceCommand>
{
    public async Task Handle(DeleteNonConformanceCommand request, CancellationToken ct)
    {
        var nonConformance = await db.NonConformances
            .FirstOrDefaultAsync(row =>
                row.Id == request.Id
                && row.CompanyId == user.CompanyId
                && row.ExternalAuditReportId != null, ct)
            ?? throw new NotFoundException("Non conformance not found.");

        var reportOwned = await ExternalReportQueries.Own(db, user)
            .AsNoTracking()
            .AnyAsync(row => row.Id == nonConformance.ExternalAuditReportId, ct);
        if (!reportOwned)
        {
            throw new NotFoundException("Non conformance not found.");
        }

        db.NonConformances.Remove(nonConformance);
        await db.SaveChangesAsync(ct);
    }
}
