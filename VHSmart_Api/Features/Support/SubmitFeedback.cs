using FluentValidation;
using MediatR;
using VHSmart_Api.Shared.Domain.Support;
using VHSmart_Api.Shared.Exceptions;
using VHSmart_Api.Shared.Infrastructure.Persistence;
using VHSmart_Api.Shared.Infrastructure.Security;

namespace VHSmart_Api.Features.Support;

// Support screen (spec 17.2 [MANUAL]): the "More Questions? Submit your feedback" card -
// choose a Category (Enquiry, Complaint, Comment or Suggestion), enter Details, Submit.
// Store only: the table is written and nothing reads or routes it - delivery / e-mail /
// inbox is HOLD - owner (spec 17.2, TaskList DS-05), so no notification and no e-mail here.
// The submitter (UserId) and the company come from the JWT (CodingRules 7.4).
public record SubmitFeedbackCommand(
    FeedbackCategory Category,
    string? Details) : IRequest<SubmitFeedbackResponse>;

public record SubmitFeedbackResponse(Guid Id);

public class SubmitFeedbackValidator : AbstractValidator<SubmitFeedbackCommand>
{
    public SubmitFeedbackValidator()
    {
        RuleFor(command => command.Category)
            .IsInEnum()
            .WithMessage("Category must be one of: Enquiry, Complaint, Comment, Suggestion.");
        RuleFor(command => command.Details)
            .NotEmpty()
            .WithMessage("Details are required.")
            .MaximumLength(4000)
            .WithMessage("Details must not exceed 4000 characters.");
    }
}

public class SubmitFeedbackHandler(VHSmartDbContext db, ICurrentUser user)
    : IRequestHandler<SubmitFeedbackCommand, SubmitFeedbackResponse>
{
    public async Task<SubmitFeedbackResponse> Handle(
        SubmitFeedbackCommand request,
        CancellationToken ct)
    {
        if (!Guid.TryParse(user.UserId, out var userId))
        {
            throw new BusinessRuleException("User id is invalid.");
        }

        var feedback = new SupFeedbackEntity
        {
            CompanyId = user.CompanyId,
            UserId = userId,
            Category = request.Category,
            Details = request.Details!
        };

        db.Feedbacks.Add(feedback);
        await db.SaveChangesAsync(ct);

        return new SubmitFeedbackResponse(feedback.Id);
    }
}
