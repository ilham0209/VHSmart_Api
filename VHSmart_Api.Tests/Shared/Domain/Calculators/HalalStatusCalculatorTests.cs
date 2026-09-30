using VHSmart_Api.Shared.Domain.Calculators;

namespace VHSmart_Api.Tests.Shared.Domain.Calculators;

public class HalalStatusCalculatorTests
{
    private static readonly DateOnly Today = new(2026, 9, 30);
    private static readonly DateOnly NoExpiry = HalalStatusCalculator.NoExpiryDate;
    private const int ExpiringDays = 90;

    [Fact]
    public void IsExpired_DateBeforeToday_ReturnsTrue()
    {
        Assert.True(HalalStatusCalculator.IsExpired(Today.AddDays(-1), Today));
    }

    [Fact]
    public void IsExpired_OnExpiryDay_ReturnsFalse()
    {
        // Q5 default (D-04): a certificate is still valid on its own expiry day.
        Assert.False(HalalStatusCalculator.IsExpired(Today, Today));
    }

    [Fact]
    public void IsExpired_DateAfterToday_ReturnsFalse()
    {
        Assert.False(HalalStatusCalculator.IsExpired(Today.AddDays(1), Today));
    }

    [Fact]
    public void IsExpired_NullExpiry_ReturnsFalse()
    {
        Assert.False(HalalStatusCalculator.IsExpired(null, Today));
    }

    [Fact]
    public void IsExpired_SentinelDate_ReturnsFalse()
    {
        Assert.False(HalalStatusCalculator.IsExpired(NoExpiry, Today));
    }

    [Fact]
    public void HasNoExpiry_NullExpiry_ReturnsTrue()
    {
        Assert.True(HalalStatusCalculator.HasNoExpiry(null));
    }

    [Fact]
    public void HasNoExpiry_SentinelDate_ReturnsTrue()
    {
        Assert.True(HalalStatusCalculator.HasNoExpiry(NoExpiry));
    }

    [Fact]
    public void HasNoExpiry_RealDate_ReturnsFalse()
    {
        Assert.False(HalalStatusCalculator.HasNoExpiry(new DateOnly(2027, 1, 1)));
    }

    [Fact]
    public void IsExpiring_WithinThreshold_ReturnsTrue()
    {
        Assert.True(HalalStatusCalculator.IsExpiring(Today.AddDays(ExpiringDays - 1), Today, ExpiringDays));
    }

    [Fact]
    public void IsExpiring_OnThresholdDay_ReturnsTrue()
    {
        Assert.True(HalalStatusCalculator.IsExpiring(Today.AddDays(ExpiringDays), Today, ExpiringDays));
    }

    [Fact]
    public void IsExpiring_BeyondThreshold_ReturnsFalse()
    {
        Assert.False(HalalStatusCalculator.IsExpiring(Today.AddDays(ExpiringDays + 1), Today, ExpiringDays));
    }

    [Fact]
    public void IsExpiring_AlreadyExpiredDate_ReturnsFalse()
    {
        Assert.False(HalalStatusCalculator.IsExpiring(Today.AddDays(-1), Today, ExpiringDays));
    }

    [Fact]
    public void IsExpiring_NullExpiry_ReturnsFalse()
    {
        Assert.False(HalalStatusCalculator.IsExpiring(null, Today, ExpiringDays));
    }

    [Fact]
    public void IsExpiring_SentinelDate_ReturnsFalse()
    {
        Assert.False(HalalStatusCalculator.IsExpiring(NoExpiry, Today, ExpiringDays));
    }

    [Fact]
    public void IsExpiring_ZeroThreshold_ReturnsTrueOnlyForToday()
    {
        Assert.True(HalalStatusCalculator.IsExpiring(Today, Today, 0));
        Assert.False(HalalStatusCalculator.IsExpiring(Today.AddDays(1), Today, 0));
    }

    [Fact]
    public void Status_DateBeforeToday_ReturnsExpired()
    {
        Assert.Equal(HalalStatus.Expired, HalalStatusCalculator.Status(Today.AddDays(-1), Today));
    }

    [Fact]
    public void Status_DateAfterToday_ReturnsValid()
    {
        Assert.Equal(HalalStatus.Valid, HalalStatusCalculator.Status(Today.AddDays(1), Today));
    }

    [Fact]
    public void Status_NullExpiry_ReturnsValid()
    {
        Assert.Equal(HalalStatus.Valid, HalalStatusCalculator.Status(null, Today));
    }

    [Fact]
    public void Status_SentinelDate_ReturnsValid()
    {
        Assert.Equal(HalalStatus.Valid, HalalStatusCalculator.Status(NoExpiry, Today));
    }

    [Fact]
    public void ExpiryForDisplay_RealDate_ReturnsDate()
    {
        var expiryDate = new DateOnly(2027, 3, 1);

        Assert.Equal(expiryDate, HalalStatusCalculator.ExpiryForDisplay(expiryDate));
    }

    [Fact]
    public void ExpiryForDisplay_NullExpiry_ReturnsNull()
    {
        Assert.Null(HalalStatusCalculator.ExpiryForDisplay(null));
    }

    [Fact]
    public void ExpiryForDisplay_SentinelDate_ReturnsNull()
    {
        Assert.Null(HalalStatusCalculator.ExpiryForDisplay(NoExpiry));
    }
}
