using System.Security.Cryptography;
using System.Text;
using VHSmart_Api.Shared.Domain.Admin;

namespace VHSmart_Api.Shared.Infrastructure.Persistence;

// The 6 packages of spec 21.5 (Database.md 5 "Seeded"): PLA, TRL, ADC, EASY-HOME, BSC-MICRO,
// LTE with their §21.5 limits (ADC <= 10 users / <= 5 premises, LTE <= 1, others unlimited =
// null). HasData runs once inside the migration - like RoleSeedData - so rows the owner later
// edits through an API are never overwritten at startup. The display names are defaults taken
// from the §21.5 descriptors; only "Advanced" (ADC) is confirmed by a screenshot (6.4) and
// the owner may rename the rest. HasData needs a stable primary key on every machine, so each
// id is a hash of a fixed string instead of Guid.NewGuid().
public static class SubscriptionPackageSeedData
{
    private const string SeedUser = "system";

    private static readonly DateTime SeedDate = new(2026, 9, 30, 0, 0, 0, DateTimeKind.Utc);

    public sealed record PackageSeed(
        string Code,
        string Name,
        int? MaxUsers,
        int? MaxPremises);

    public static readonly IReadOnlyList<PackageSeed> Seeds =
    [
        new("PLA", "Premium", null, null),
        new("TRL", "Trial", null, null),
        new("ADC", "Advanced", 10, 5),
        new("EASY-HOME", "Easy Home", null, null),
        new("BSC-MICRO", "Basic Micro", null, null),
        new("LTE", "Lite", 1, 1)
    ];

    public static IEnumerable<SubscriptionPackageEntity> PackageEntities() =>
        Seeds.Select(seed => new SubscriptionPackageEntity
        {
            Id = StableId($"subscription-package:{seed.Code}"),
            Code = seed.Code,
            Name = seed.Name,
            MaxUsers = seed.MaxUsers,
            MaxPremises = seed.MaxPremises,
            SysUserCreated = SeedUser,
            SysDateCreated = SeedDate
        });

    private static Guid StableId(string value) =>
        new(SHA256.HashData(Encoding.UTF8.GetBytes(value))[..16]);
}
