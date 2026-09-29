using VHSmart_Api.Shared.Exceptions;
using VHSmart_Api.Shared.Infrastructure.Storage;

namespace VHSmart_Api.Tests.Shared.Infrastructure.Storage;

public class FileValidationTests
{
    private const long OneMegabyte = 1024 * 1024;

    [Fact]
    public void Validate_WithinDefaultRules_DoesNotThrow() =>
        FileValidation.Validate("certificate.pdf", OneMegabyte);

    [Fact]
    public void Validate_TooLarge_ThrowsBusinessRuleWithLimitInMessage()
    {
        var exception = Assert.Throws<BusinessRuleException>(() =>
            FileValidation.Validate("large.pdf", FileValidation.DefaultMaxSizeBytes + 1));

        Assert.Contains("10 MB", exception.Message);
    }

    [Theory]
    [InlineData("tool.exe")]
    [InlineData("notes.txt")]
    [InlineData("archive.zip")]
    public void Validate_DisallowedExtension_ThrowsBusinessRule(string fileName)
    {
        var exception = Assert.Throws<BusinessRuleException>(() => FileValidation.Validate(fileName, OneMegabyte));

        Assert.Contains("not allowed", exception.Message);
    }

    [Fact]
    public void Validate_WithoutExtension_ThrowsBusinessRule() =>
        Assert.Throws<BusinessRuleException>(() => FileValidation.Validate("README", OneMegabyte));

    [Fact]
    public void Validate_NullFileName_ThrowsBusinessRule() =>
        Assert.Throws<BusinessRuleException>(() => FileValidation.Validate(null, OneMegabyte));

    [Fact]
    public void Validate_UppercaseExtension_IsAllowed() =>
        FileValidation.Validate("PHOTO.PNG", OneMegabyte);

    [Fact]
    public void Validate_PdfOnlyList_RejectsOtherDocumentTypes()
    {
        Assert.Throws<BusinessRuleException>(() =>
            FileValidation.Validate("policy.docx", OneMegabyte, FileValidation.PdfOnlyExtensions));

        FileValidation.Validate("policy.pdf", OneMegabyte, FileValidation.PdfOnlyExtensions);
    }

    [Fact]
    public void Validate_CustomMaxSize_EnforcesTheOverride()
    {
        Assert.Throws<BusinessRuleException>(() =>
            FileValidation.Validate("picture.jpg", 6 * OneMegabyte, maxBytes: 5 * OneMegabyte));

        FileValidation.Validate("picture.jpg", 5 * OneMegabyte, maxBytes: 5 * OneMegabyte);
    }

    [Fact]
    public void Validate_FileNameLongerThanColumn_ThrowsBusinessRule() =>
        Assert.Throws<BusinessRuleException>(() =>
            FileValidation.Validate($"{new string('a', FileValidation.FileNameMaxLength)}.pdf", OneMegabyte));
}
