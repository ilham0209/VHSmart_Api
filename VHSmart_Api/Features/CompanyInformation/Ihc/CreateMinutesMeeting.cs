using FluentValidation;
using MediatR;
using VHSmart_Api.Shared.Domain.Companies;
using VHSmart_Api.Shared.Infrastructure.Persistence;
using VHSmart_Api.Shared.Infrastructure.Security;

namespace VHSmart_Api.Features.CompanyInformation.Ihc;

// Add minutes meeting (spec 7.6 form "Meeting Details": Title*, Company*, Date*, Location*,
// Start Time*, End Time*). Company* is the caller's own company and therefore never part of
// the command (CodingRules 8.1). Times travel as nullable TimeOnly so a missing value fails
// with "required" instead of silently binding to midnight. D-14: the legacy data quirk stores
// Start > End and the new system rejects it server-side. Database.md 4 defines no unique
// index for this table, so the title may repeat (no 409 rule exists anywhere for meetings).
public record CreateMinutesMeetingCommand(
    string Title,
    DateTime MeetingDate,
    TimeOnly? StartTime,
    TimeOnly? EndTime,
    string Location) : IRequest<MinutesMeetingDetailResponse>;

public class CreateMinutesMeetingValidator : AbstractValidator<CreateMinutesMeetingCommand>
{
    public CreateMinutesMeetingValidator()
    {
        RuleFor(x => x.Title)
            .Cascade(CascadeMode.Stop)
            .NotEmpty().WithMessage("Title is required.")
            .MaximumLength(200).WithMessage("Title must be 200 characters or fewer.");
        RuleFor(x => x.MeetingDate)
            .NotEmpty()
            .WithMessage("Meeting date is required.");
        RuleFor(x => x.StartTime)
            .NotNull()
            .WithMessage("Start time is required.");
        RuleFor(x => x.EndTime)
            .Cascade(CascadeMode.Stop)
            .NotNull().WithMessage("End time is required.")
            // D-14 verbatim: only a strictly later End than Start passes; a missing start
            // was already reported by the rule above.
            .Must((command, end) => command.StartTime is null || command.StartTime <= end)
            .WithMessage("End time must be later than start time.");
        RuleFor(x => x.Location)
            .Cascade(CascadeMode.Stop)
            .NotEmpty().WithMessage("Location is required.")
            .MaximumLength(200).WithMessage("Location must be 200 characters or fewer.");
    }
}

public class CreateMinutesMeetingHandler(VHSmartDbContext db, ICurrentUser user)
    : IRequestHandler<CreateMinutesMeetingCommand, MinutesMeetingDetailResponse>
{
    public async Task<MinutesMeetingDetailResponse> Handle(
        CreateMinutesMeetingCommand request,
        CancellationToken ct)
    {
        var companyId = user.CompanyId;

        var entity = new MinutesMeetingEntity
        {
            CompanyId = companyId,
            Title = request.Title,
            MeetingDate = request.MeetingDate,
            StartTime = request.StartTime!.Value,
            EndTime = request.EndTime!.Value,
            Location = request.Location
        };
        db.MinutesMeetings.Add(entity);
        await db.SaveChangesAsync(ct);

        return await MinutesMeetingResponseLoader.LoadAsync(db, companyId, entity.Id, ct);
    }
}
