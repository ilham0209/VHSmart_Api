using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using VHSmart_Api.Shared.Domain.Companies;
using VHSmart_Api.Shared.Exceptions;
using VHSmart_Api.Shared.Infrastructure.Persistence;
using VHSmart_Api.Shared.Infrastructure.Security;

namespace VHSmart_Api.Features.Personnel.Trainings;

// Edit training (spec 7.5 "Manage Training" modal, Save). The id comes from the route, the
// company from the JWT; the handler loads the training with the explicit CompanyId match, so
// another tenant's row answers 404 (never 403). The attendance list is reconciled like every
// other child list in this system: rows for staff no longer in the list are soft-deleted (the
// filtered UQ (TrainingId, StaffId) frees the pair), new staff are added, the rest stay.
// Training name uniqueness excludes self (Database.md 6) -> 409.
public record UpdateTrainingCommand(
    Guid Id,
    TrainingType? TrainingType,
    string Name,
    DateTime TrainingDate,
    IReadOnlyList<Guid> AttendeeStaffIds) : IRequest<TrainingDetailResponse>;

public class UpdateTrainingValidator : AbstractValidator<UpdateTrainingCommand>
{
    public UpdateTrainingValidator(VHSmartDbContext db, ICurrentUser user)
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.TrainingType)
            .Cascade(CascadeMode.Stop)
            .NotNull().WithMessage("Training type is required.")
            .Must(value => value.HasValue && Enum.IsDefined(value.Value))
            .WithMessage("Training type is invalid.");
        RuleFor(x => x.Name)
            .Cascade(CascadeMode.Stop)
            .NotEmpty().WithMessage("Training name is required.")
            .MaximumLength(200).WithMessage("Training name must be 200 characters or fewer.");
        RuleFor(x => x.TrainingDate)
            .NotEmpty()
            .WithMessage("Training date is required.");
        RuleFor(x => x.AttendeeStaffIds)
            .Cascade(CascadeMode.Stop)
            .Must(ids => ids is { Count: > 0 })
            .WithMessage("Attendance is required.")
            .MustAsync(async (ids, ct) =>
            {
                var distinct = ids.Distinct().ToList();
                var found = await db.Staffs.CountAsync(
                    staff => staff.CompanyId == user.CompanyId && distinct.Contains(staff.Id), ct);
                return found == distinct.Count;
            })
            .WithMessage("Attendee not found.");
    }
}

public class UpdateTrainingHandler(VHSmartDbContext db, ICurrentUser user)
    : IRequestHandler<UpdateTrainingCommand, TrainingDetailResponse>
{
    public async Task<TrainingDetailResponse> Handle(
        UpdateTrainingCommand request,
        CancellationToken ct)
    {
        var companyId = user.CompanyId;

        var entity = await db.Trainings
            .FirstOrDefaultAsync(row => row.Id == request.Id && row.CompanyId == companyId, ct)
            ?? throw new NotFoundException("Training not found.");

        var duplicate = await db.Trainings.AnyAsync(
            row => row.CompanyId == companyId
                && row.Name == request.Name
                && row.Id != request.Id, ct);
        if (duplicate)
            throw new ConflictException("A training with this name already exists.");

        entity.TrainingType = request.TrainingType!.Value;
        entity.Name = request.Name;
        entity.TrainingDate = request.TrainingDate;

        var wanted = request.AttendeeStaffIds.Distinct().ToHashSet();
        var current = await db.TrainingAttendees
            .Where(row => row.TrainingId == entity.Id)
            .ToListAsync(ct);

        foreach (var row in current.Where(row => !wanted.Contains(row.StaffId)))
            db.TrainingAttendees.Remove(row);

        var present = current.Select(row => row.StaffId).ToHashSet();
        foreach (var staffId in wanted.Where(id => !present.Contains(id)))
        {
            db.TrainingAttendees.Add(new TrainingAttendeeEntity
            {
                CompanyId = companyId,
                TrainingId = entity.Id,
                StaffId = staffId
            });
        }

        await db.SaveChangesAsync(ct);

        return await TrainingResponseLoader.LoadAsync(db, companyId, entity.Id, ct);
    }
}
