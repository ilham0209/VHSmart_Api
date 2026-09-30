using VHSmart_Api.Shared.Domain.Calculators;

namespace VHSmart_Api.Tests.Shared.Domain.Calculators;

public class PremiseDocumentStatusCalculatorTests
{
    private static readonly DateOnly Today = new(2026, 9, 30);
    private static readonly DateOnly FutureExpiry = Today.AddDays(60);
    private static readonly DateOnly PastExpiry = Today.AddDays(-1);
    private static readonly DateOnly NoExpiry = HalalStatusCalculator.NoExpiryDate;

    [Fact]
    public void RequiredDocumentTypes_MatchesD15List()
    {
        string[] expected = ["APPOINTMENT LETTER", "BUSINESS LICENSE", "COMPANY INFORMATION", "FOSIM", "HALAL CERTIFICATE"];

        Assert.Equal(expected, PremiseDocumentStatusCalculator.RequiredDocumentTypes);
    }

    [Fact]
    public void Calculate_AllRequiredDocumentsUploaded_ReturnsCompleteDocumentation()
    {
        var status = PremiseDocumentStatusCalculator.Calculate(Uploaded(FutureExpiry), Today);

        Assert.Equal("COMPLETE DOCUMENTATION", status.Text);
        Assert.True(status.IsComplete);
        Assert.Equal(0, status.ExpiredCount);
    }

    [Fact]
    public void Calculate_RequiredTypeMissing_ReturnsNotComplete()
    {
        var documents = Uploaded(FutureExpiry);
        documents.RemoveAll(document => document.DocumentType == "FOSIM");

        var status = PremiseDocumentStatusCalculator.Calculate(documents, Today);

        Assert.Equal("NOT COMPLETE DOCUMENTATION", status.Text);
        Assert.False(status.IsComplete);
    }

    [Fact]
    public void Calculate_NoDocumentsAtAll_ReturnsNotComplete()
    {
        var status = PremiseDocumentStatusCalculator.Calculate([], Today);

        Assert.Equal("NOT COMPLETE DOCUMENTATION", status.Text);
        Assert.False(status.IsComplete);
    }

    [Fact]
    public void Calculate_DocumentWithoutUpload_ReturnsNotComplete()
    {
        var documents = Uploaded(FutureExpiry);
        var index = documents.FindIndex(document => document.DocumentType == "FOSIM");
        documents[index] = documents[index] with { HasUpload = false };

        var status = PremiseDocumentStatusCalculator.Calculate(documents, Today);

        // The untouched row shows "N/A" in the UI, which is still "no upload" (D-15).
        Assert.Equal("NOT COMPLETE DOCUMENTATION", status.Text);
        Assert.False(status.IsComplete);
    }

    [Fact]
    public void Calculate_OneExpiredDocument_ReturnsOneOfTheDocumentHasExpired()
    {
        var documents = Uploaded(FutureExpiry);
        var index = documents.FindIndex(document => document.DocumentType == "HALAL CERTIFICATE");
        documents[index] = documents[index] with { ExpiryDate = PastExpiry };

        var status = PremiseDocumentStatusCalculator.Calculate(documents, Today);

        Assert.Equal("1 OF THE DOCUMENT HAS EXPIRED", status.Text);
        Assert.False(status.IsComplete);
        Assert.Equal(1, status.ExpiredCount);
    }

    [Fact]
    public void Calculate_TwoExpiredDocuments_ReturnsTwoOfTheDocumentHasExpired()
    {
        var documents = Uploaded(FutureExpiry);
        foreach (var documentType in new[] { "HALAL CERTIFICATE", "BUSINESS LICENSE" })
        {
            var index = documents.FindIndex(document => document.DocumentType == documentType);
            documents[index] = documents[index] with { ExpiryDate = PastExpiry };
        }

        var status = PremiseDocumentStatusCalculator.Calculate(documents, Today);

        Assert.Equal("2 OF THE DOCUMENT HAS EXPIRED", status.Text);
        Assert.False(status.IsComplete);
        Assert.Equal(2, status.ExpiredCount);
    }

    [Fact]
    public void Calculate_MissingAndExpiredDocument_ReturnsNotCompleteFirst()
    {
        // D-15 order: "no upload" wins over "expired" - only complete premises are counted.
        var documents = Uploaded(PastExpiry);
        documents.RemoveAll(document => document.DocumentType == "FOSIM");

        var status = PremiseDocumentStatusCalculator.Calculate(documents, Today);

        Assert.Equal("NOT COMPLETE DOCUMENTATION", status.Text);
        Assert.False(status.IsComplete);
        Assert.Equal(0, status.ExpiredCount);
    }

    [Fact]
    public void Calculate_DocumentExpiringOnSentinelDate_ReturnsComplete()
    {
        var status = PremiseDocumentStatusCalculator.Calculate(Uploaded(NoExpiry), Today);

        Assert.Equal("COMPLETE DOCUMENTATION", status.Text);
        Assert.True(status.IsComplete);
    }

    [Fact]
    public void Calculate_DocumentWithoutExpiryDate_ReturnsComplete()
    {
        var status = PremiseDocumentStatusCalculator.Calculate(Uploaded(null), Today);

        Assert.Equal("COMPLETE DOCUMENTATION", status.Text);
        Assert.True(status.IsComplete);
    }

    [Fact]
    public void Calculate_DocumentExpiringOnExpiryDay_ReturnsComplete()
    {
        // Q5 default (D-04): not expired on the day itself.
        var status = PremiseDocumentStatusCalculator.Calculate(Uploaded(Today), Today);

        Assert.Equal("COMPLETE DOCUMENTATION", status.Text);
        Assert.True(status.IsComplete);
    }

    [Fact]
    public void Calculate_DocumentTypeCasingDiffers_ReturnsComplete()
    {
        var documents = Uploaded(FutureExpiry);
        var index = documents.FindIndex(document => document.DocumentType == "FOSIM");
        documents[index] = documents[index] with { DocumentType = "Fosim" };

        var status = PremiseDocumentStatusCalculator.Calculate(documents, Today);

        Assert.Equal("COMPLETE DOCUMENTATION", status.Text);
        Assert.True(status.IsComplete);
    }

    [Fact]
    public void Calculate_UnknownExtraDocumentType_IsIgnored()
    {
        var documents = Uploaded(FutureExpiry);
        documents.Add(new PremiseDocument("PLAN LAYOUT", HasUpload: true, ExpiryDate: PastExpiry));

        var status = PremiseDocumentStatusCalculator.Calculate(documents, Today);

        // Types outside the D-15 list are not part of the rule, expired or not.
        Assert.Equal("COMPLETE DOCUMENTATION", status.Text);
        Assert.True(status.IsComplete);
        Assert.Equal(0, status.ExpiredCount);
    }

    private static List<PremiseDocument> Uploaded(DateOnly? expiryDate) =>
        PremiseDocumentStatusCalculator.RequiredDocumentTypes
            .Select(documentType => new PremiseDocument(documentType, HasUpload: true, expiryDate))
            .ToList();
}
