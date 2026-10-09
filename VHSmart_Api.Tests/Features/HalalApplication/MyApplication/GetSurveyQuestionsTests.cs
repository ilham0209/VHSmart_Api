using VHSmart_Api.Features.HalalApplication.MyApplication;

namespace VHSmart_Api.Tests.Features.HalalApplication.MyApplication;

public class GetSurveyQuestionsTests
{
    [Fact]
    public async Task Handle_ReturnsTheFourQuestionsInSpecOrder()
    {
        var questions = await new GetSurveyQuestionsHandler()
            .Handle(new GetSurveyQuestionsQuery(), CancellationToken.None);

        Assert.Equal(new[] { "Q1", "Q2", "Q3", "Q4" },
            questions.Select(question => question.Code));
        Assert.Equal(4, questions.Count);
    }

    [Fact]
    public async Task Handle_QuestionTexts_CarryTheSpecSubjects()
    {
        var questions = await new GetSurveyQuestionsHandler()
            .Handle(new GetSurveyQuestionsQuery(), CancellationToken.None);

        Assert.Contains("Halal Certification Procedure Manual", questions[0].Text);
        Assert.Contains("MS 1500:2019", questions[1].Text);
        Assert.Contains("pork", questions[2].Text);
        Assert.Contains("Internal Halal Committee", questions[3].Text);
    }
}
