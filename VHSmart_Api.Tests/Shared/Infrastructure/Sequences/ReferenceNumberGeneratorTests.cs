using System.Text.RegularExpressions;
using Microsoft.Extensions.Configuration;
using VHSmart_Api.Shared.Infrastructure.Sequences;

namespace VHSmart_Api.Tests.Shared.Infrastructure.Sequences;

public class ReferenceNumberGeneratorTests
{
    private static readonly DateOnly ScheduleDate = new(2026, 9, 30);

    [Fact]
    public async Task NextAuditReferenceNumberAsync_FirstCall_ReturnsFormattedNumber()
    {
        var (db, generator) = CreateGenerator();

        var referenceNumber = await generator.NextAuditReferenceNumberAsync("AUD01", "STORE01", ScheduleDate);

        Assert.Equal("AUD01/STORE01/30-09-2026(1)", referenceNumber);
    }

    [Fact]
    public async Task NextAuditReferenceNumberAsync_SamePrefixAndDate_ReturnsNextRunningNumber()
    {
        var (db, generator) = CreateGenerator();

        await generator.NextAuditReferenceNumberAsync("AUD01", "STORE01", ScheduleDate);
        var second = await generator.NextAuditReferenceNumberAsync("AUD01", "STORE02", ScheduleDate);

        // The premise is not part of the scope (D-09): the number runs per prefix + date.
        Assert.Equal("AUD01/STORE02/30-09-2026(2)", second);
    }

    [Fact]
    public async Task NextAuditReferenceNumberAsync_DifferentDate_StartsAtOne()
    {
        var (db, generator) = CreateGenerator();
        await generator.NextAuditReferenceNumberAsync("AUD01", "STORE01", ScheduleDate);

        var nextDay = await generator.NextAuditReferenceNumberAsync(
            "AUD01", "STORE01", ScheduleDate.AddDays(1));

        Assert.Equal("AUD01/STORE01/01-10-2026(1)", nextDay);
    }

    [Fact]
    public async Task NextAuditReferenceNumberAsync_DifferentPrefix_StartsAtOne()
    {
        var (db, generator) = CreateGenerator();
        await generator.NextAuditReferenceNumberAsync("AUD01", "STORE01", ScheduleDate);

        var otherPrefix = await generator.NextAuditReferenceNumberAsync("AUD02", "STORE01", ScheduleDate);

        Assert.Equal("AUD02/STORE01/30-09-2026(1)", otherPrefix);
    }

    [Fact]
    public async Task NextApplicationReferenceNumberAsync_UsesConfiguredPrefix()
    {
        var (db, generator) = CreateGenerator(prefix: "MYA22");

        var referenceNumber = await generator.NextApplicationReferenceNumberAsync("ML", ScheduleDate);

        Assert.Equal("MYA22(ML)/30092026/1", referenceNumber);
    }

    [Fact]
    public async Task NextApplicationReferenceNumberAsync_PrefixNotConfigured_FallsBackToPlaceholder()
    {
        var (db, generator) = CreateGenerator(prefix: null);

        var referenceNumber = await generator.NextApplicationReferenceNumberAsync("ML", ScheduleDate);

        Assert.Equal("VHS(ML)/30092026/1", referenceNumber);
    }

    [Fact]
    public async Task NextApplicationReferenceNumberAsync_SameSchemeAndDate_ReturnsNextRunningNumber()
    {
        var (db, generator) = CreateGenerator(prefix: "MYA22");

        await generator.NextApplicationReferenceNumberAsync("ML", ScheduleDate);
        var second = await generator.NextApplicationReferenceNumberAsync("ML", ScheduleDate);

        Assert.Equal("MYA22(ML)/30092026/2", second);
    }

    [Fact]
    public async Task NextApplicationReferenceNumberAsync_DifferentScheme_StartsAtOne()
    {
        var (db, generator) = CreateGenerator(prefix: "MYA22");
        await generator.NextApplicationReferenceNumberAsync("ML", ScheduleDate);

        var otherScheme = await generator.NextApplicationReferenceNumberAsync("SL", ScheduleDate);

        Assert.Equal("MYA22(SL)/30092026/1", otherScheme);
    }

    [Fact]
    public async Task NextAuditReferenceNumberAsync_ParallelCalls_ReturnEveryNumberExactlyOnce()
    {
        const int callers = 20;
        var databaseName = TestDbFactory.NewDatabaseName();

        var results = await Task.WhenAll(Enumerable.Range(0, callers).Select(async caller =>
        {
            var (_, generator) = CreateGenerator(databaseName: databaseName);
            return await generator.NextAuditReferenceNumberAsync("AUD01", "STORE01", ScheduleDate);
        }));

        var numbers = results
            .Select(result => int.Parse(Regex.Match(result, @"\((\d+)\)$").Groups[1].Value))
            .OrderBy(number => number);

        Assert.Equal(Enumerable.Range(1, callers), numbers);
    }

    [Fact]
    public async Task NextAuditReferenceNumberAsync_EmptyPrefix_Throws()
    {
        var (_, generator) = CreateGenerator();

        await Assert.ThrowsAsync<ArgumentException>(
            () => generator.NextAuditReferenceNumberAsync(" ", "STORE01", ScheduleDate));
    }

    [Fact]
    public async Task NextAuditReferenceNumberAsync_EmptyPremiseKey_Throws()
    {
        var (_, generator) = CreateGenerator();

        await Assert.ThrowsAsync<ArgumentException>(
            () => generator.NextAuditReferenceNumberAsync("AUD01", string.Empty, ScheduleDate));
    }

    [Fact]
    public async Task NextApplicationReferenceNumberAsync_EmptySchemeCode_Throws()
    {
        var (_, generator) = CreateGenerator();

        await Assert.ThrowsAsync<ArgumentException>(
            () => generator.NextApplicationReferenceNumberAsync(string.Empty, ScheduleDate));
    }

    private static (TestableVHSmartDbContext Db, ReferenceNumberGenerator Generator)
        CreateGenerator(string? prefix = "MYA22", string? databaseName = null)
    {
        var db = TestDbFactory.Create(databaseName ?? TestDbFactory.NewDatabaseName());

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Application:ReferencePrefix"] = prefix
            })
            .Build();

        return (db, new ReferenceNumberGenerator(db, configuration));
    }
}
