using VHSmart_Api.Features.Support;
using VHSmart_Api.Shared.Domain.Support;
using VHSmart_Api.Shared.Exceptions;

namespace VHSmart_Api.Tests.Features.Support;

// Support > Feedback (spec 17.2): the card stores Category + Details under the JWT's user
// and company; nothing is read back or routed (delivery is HOLD - owner).
public class SubmitFeedbackTests
{
    [Fact]
    public async Task Handle_ValidSubmission_StoresRowWithJwtIdentity()
    {
        var user = SupportTestData.UserA();
        var db = await SupportTestData.CreateDbAsync(user);

        var response = await new SubmitFeedbackHandler(db, user)
            .Handle(
                new SubmitFeedbackCommand(FeedbackCategory.Complaint, "The portal was slow."),
                CancellationToken.None);

        Assert.NotEqual(Guid.Empty, response.Id);
        var stored = Assert.Single(db.Feedbacks);
        Assert.Equal(response.Id, stored.Id);
        Assert.Equal(SupportTestData.CompanyA, stored.CompanyId);
        Assert.Equal(Guid.Parse(user.UserId), stored.UserId);
        Assert.Equal(FeedbackCategory.Complaint, stored.Category);
        Assert.Equal("The portal was slow.", stored.Details);
        Assert.False(stored.IsDeleted);
    }

    [Fact]
    public async Task Handle_UnparsableUserId_ThrowsBusinessRule()
    {
        var user = new TestCurrentUser("not-a-guid", SupportTestData.CompanyA);
        var db = await SupportTestData.CreateDbAsync(user);

        var exception = await Assert.ThrowsAsync<BusinessRuleException>(() =>
            new SubmitFeedbackHandler(db, user)
                .Handle(
                    new SubmitFeedbackCommand(FeedbackCategory.Enquiry, "A question."),
                    CancellationToken.None));

        Assert.Equal("User id is invalid.", exception.Message);
        Assert.Empty(db.Feedbacks);
    }

    [Theory]
    [InlineData(FeedbackCategory.Enquiry)]
    [InlineData(FeedbackCategory.Complaint)]
    [InlineData(FeedbackCategory.Comment)]
    [InlineData(FeedbackCategory.Suggestion)]
    public void Validator_AllFourSpecCategories_AreValid(FeedbackCategory category)
    {
        var validator = new SubmitFeedbackValidator();

        var result = validator.Validate(
            new SubmitFeedbackCommand(category, "Some details."));

        Assert.True(result.IsValid);
    }

    [Fact]
    public void Validator_CategoryOutsideTheFour_Rejects()
    {
        var validator = new SubmitFeedbackValidator();

        var result = validator.Validate(
            new SubmitFeedbackCommand((FeedbackCategory)99, "Some details."));

        Assert.False(result.IsValid);
        Assert.Contains(
            result.Errors,
            error => error.ErrorMessage
                == "Category must be one of: Enquiry, Complaint, Comment, Suggestion.");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Validator_EmptyDetails_Rejects(string? details)
    {
        var validator = new SubmitFeedbackValidator();

        var result = validator.Validate(
            new SubmitFeedbackCommand(FeedbackCategory.Comment, details));

        Assert.False(result.IsValid);
        Assert.Contains(
            result.Errors,
            error => error.ErrorMessage == "Details are required.");
    }

    [Fact]
    public void Validator_DetailsAt4000_IsValid_AndLonger_Rejects()
    {
        var validator = new SubmitFeedbackValidator();
        var atLimit = new string('x', 4000);
        var overLimit = new string('x', 4001);

        Assert.True(validator
            .Validate(new SubmitFeedbackCommand(FeedbackCategory.Suggestion, atLimit))
            .IsValid);
        Assert.False(validator
            .Validate(new SubmitFeedbackCommand(FeedbackCategory.Suggestion, overLimit))
            .IsValid);
    }
}
