using MediatR;

namespace VHSmart_Api.Features.HalalApplication.MyApplication;

// Application Survey Form (spec 12.4): the modal lists these four questions before the
// application is created, as Ya / Tidak radio buttons. The texts are the spec's English
// originals - the system shows them in Malay, but no Malay wording was captured anywhere in
// the spec set, so the frontend owns the translation until the owner supplies one (flagged).
// The answers are never trusted from here: the create handler enforces the D-08 key.
public record GetSurveyQuestionsQuery : IRequest<IReadOnlyList<SurveyQuestionResponse>>;

public record SurveyQuestionResponse(string Code, string Text);

public class GetSurveyQuestionsHandler : IRequestHandler<
    GetSurveyQuestionsQuery, IReadOnlyList<SurveyQuestionResponse>>
{
    // Order is the spec's list order (12.4 Q1..Q4); Code is the wire name the create
    // command's Survey* fields map to in that same order.
    private static readonly IReadOnlyList<SurveyQuestionResponse> Questions =
    [
        new("Q1",
            "Have you read and understood the Malaysian Halal Certification Procedure Manual?"),
        new("Q2",
            "Have you read and understood the Malaysian Halal standard MS 1500:2019 " +
            "(Halal food – production, preparation, handling and storage – " +
            "General guidelines, second revision)?"),
        new("Q3",
            "Does the company handle / process / distribute / store any of: pork / dog / " +
            "pork-and-dog derivatives / human elements; liquor / alcohol?"),
        new("Q4", "Has the company established an Internal Halal Committee?")
    ];

    public Task<IReadOnlyList<SurveyQuestionResponse>> Handle(
        GetSurveyQuestionsQuery request,
        CancellationToken cancellationToken) =>
        Task.FromResult(Questions);
}
