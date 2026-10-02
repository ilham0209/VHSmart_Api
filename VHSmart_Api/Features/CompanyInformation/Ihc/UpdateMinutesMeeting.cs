using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using VHSmart_Api.Shared.Exceptions;
using VHSmart_Api.Shared.Infrastructure.Persistence;
using VHSmart_Api.Shared.Infrastructure.Security;

namespace VHSmart_Api.Features.CompanyInformation.Ihc;

// Edit minutes meeting (spec 7.6 meeting form, Save). The id comes from the route, the
// company from the JWT; the handler loads the row with the explicit CompanyId match, so
// another tenant's meeting answers 404 (never 403). The attachment list is untouched by the
// meeting save (spec 7.6 keeps uploads on their own endpoint, same as P-01/P-02).
public record UpdateMinutesMeetingCommand(
    Guid Id,
    string Title,
    DateTime MeetingDate,
    TimeOnly? StartTime,
    TimeOnly? EndTime,
    string Location) : IRequest<MinutesMeetingDetailResponse>;

public class UpdateMinutesMeetingValidator : AbstractValidator<UpdateMinutesMeetingCommand>
{
    public UpdateMinutesMeetingValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
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

public class UpdateMinutesMeetingHandler(VHSmartDbContext db, ICurrentUser user)
    : IRequestHandler<UpdateMinutesMeetingCommand, MinutesMeetingDetailResponse>
{
    public async Task<MinutesMeetingDetailResponse> Handle(
        UpdateMinutesMeetingCommand request,
        CancellationToken ct)
    {
        var companyId = user.CompanyId;

        var entity = await db.MinutesMeetings
            .FirstOrDefaultAsync(
                row => row.Id == request.Id && row.CompanyId == companyId, ct)
            ?? throw new NotFoundException("Minutes meeting not found.");

        entity.Title = request.Title;
        entity.MeetingDate = request.MeetingDate;
        entity.StartTime = request.StartTime!.Value;
        entity.EndTime = request.EndTime!.Value;
        entity.Location = request.Location;

        await db.SaveChangesAsync(ct);

        return await MinutesMeetingResponseLoader.LoadAsync(db, companyId, entity.Id, ct);
    }
}
