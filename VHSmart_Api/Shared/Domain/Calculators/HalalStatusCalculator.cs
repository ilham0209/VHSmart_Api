namespace VHSmart_Api.Shared.Domain.Calculators;

// Valid / Expired as the screens show it (§9.4, §12.8); the casing is decided by the caller.
public enum HalalStatus
{
    Valid,
    Expired
}

// Certificate, raw-material and product expiry rules in one place (D-04, D-03).
// Pure: every method gets "today" from the caller, so a test never depends on the clock.
public static class HalalStatusCalculator
{
    // D-04: null and the sentinel date both mean "no expiry" - displayed empty, never expired.
    public static readonly DateOnly NoExpiryDate = new(9999, 12, 31);

    public static bool HasNoExpiry(DateOnly? expiryDate) =>
        expiryDate is null || expiryDate.Value == NoExpiryDate;

    // D-04: expired only when the date is strictly before today - no grace day, and a
    // certificate is still valid on its own expiry day (Q5 default).
    public static bool IsExpired(DateOnly? expiryDate, DateOnly today)
    {
        if (HasNoExpiry(expiryDate))
            return false;

        return expiryDate.GetValueOrDefault() < today;
    }

    // D-03: "expiring" = runs out within the configured number of days (Halal:ExpiringDays).
    // Already-expired and no-expiry rows are not "expiring" - they are Expired / Valid.
    public static bool IsExpiring(DateOnly? expiryDate, DateOnly today, int expiringDays)
    {
        if (HasNoExpiry(expiryDate) || IsExpired(expiryDate, today))
            return false;

        return expiryDate.GetValueOrDefault() <= today.AddDays(expiringDays);
    }

    public static HalalStatus Status(DateOnly? expiryDate, DateOnly today) =>
        IsExpired(expiryDate, today) ? HalalStatus.Expired : HalalStatus.Valid;

    // What the screens put in the expiry column: empty for "no expiry".
    public static DateOnly? ExpiryForDisplay(DateOnly? expiryDate) =>
        HasNoExpiry(expiryDate) ? null : expiryDate;
}
