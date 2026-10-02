using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using VHSmart_Api.Shared.Domain.Companies;
using VHSmart_Api.Shared.Exceptions;
using VHSmart_Api.Shared.Infrastructure.Persistence;
using VHSmart_Api.Shared.Infrastructure.Security;

namespace VHSmart_Api.Features.Personnel.Trainings;

// Add training (spec 7.5 modal "Manage Training": Company*, Training Type*, Training Name*,
// Date*, Attendance*). Company* is the caller's own company and therefore never part of the
// command (CodingRules 8.1). TrainingType travels as a nullable enum so a missing value fails
// with "required" instead of silently defaulting to AllStaff (R-02 pattern). Attendance is
// validated staff of the caller's company; the duplicate-attendee check itself is client-side
// only (spec 7.5 [CODE]) - the handler just de-duplicates. The unique training name is
// server-enforced (Database.md 6: create AND update excluding self) -> 409. Modules are a
// separate endpoint: the spec [VERIFY] does not know whether they can exist before the
// training is saved, so the training row always comes first.
public record CreateTrainingCommand(
    TrainingType? TrainingType,
    string Name,
    DateTime TrainingDate,
    IReadOnlyList<Guid> AttendeeStaffIds) : IRequest<TrainingDetailResponse>;

public class CreateTrainingValidator : AbstractValidator<CreateTrainingCommand>
{
    public CreateTrainingValidator(VHSmartDbContext db, ICurrentUser user)
    {
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

public class CreateTrainingHandler(VHSmartDbContext db, ICurrentUser user)
    : IRequestHandler<CreateTrainingCommand, TrainingDetailResponse>
{
    public async Task<TrainingDetailResponse> Handle(
        CreateTrainingCommand request,
        CancellationToken ct)
    {
        var companyId = user.CompanyId;

        var duplicate = await db.Trainings.AnyAsync(
            row => row.CompanyId == companyId && row.Name == request.Name, ct);
        if (duplicate)
            throw new ConflictException("A training with this name already exists.");

        var entity = new TrainingEntity
        {
            CompanyId = companyId,
            TrainingType = request.TrainingType!.Value,
            Name = request.Name,
            TrainingDate = request.TrainingDate
        };
        db.Trainings.Add(entity);

        // Id is assigned by BaseClass at construction, so the children can carry it now.
        foreach (var staffId in request.AttendeeStaffIds.Distinct())
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
