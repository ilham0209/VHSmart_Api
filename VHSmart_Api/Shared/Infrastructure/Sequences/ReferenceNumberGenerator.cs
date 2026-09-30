using System.Globalization;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using VHSmart_Api.Shared.Domain.Admin;
using VHSmart_Api.Shared.Infrastructure.Persistence;

namespace VHSmart_Api.Shared.Infrastructure.Sequences;

public sealed class ReferenceNumberGenerator(
    VHSmartDbContext db,
    IConfiguration configuration) : IReferenceNumberGenerator
{
    private const int MaxAttempts = 10;

    // Q2: the application prefix is not decided yet, so config carries the placeholder value.
    private const string DefaultApplicationPrefix = "VHS";

    // Serializes number generation inside this process. The service is scoped, so an
    // instance-level lock would not cover the next request; the optimistic check on
    // LastNumber plus the retry covers the other instances of the API.
    private static readonly SemaphoreSlim Gate = new(1, 1);

    public async Task<string> NextAuditReferenceNumberAsync(
        string auditPrefix,
        string premiseKey,
        DateOnly scheduleDate,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(auditPrefix);
        ArgumentException.ThrowIfNullOrWhiteSpace(premiseKey);

        var day = scheduleDate.ToString("dd-MM-yyyy", CultureInfo.InvariantCulture);
        var number = await NextNumberAsync($"AUDIT/{auditPrefix}/{day}", cancellationToken);
        return string.Format(
            CultureInfo.InvariantCulture, "{0}/{1}/{2}({3})", auditPrefix, premiseKey, day, number);
    }

    public async Task<string> NextApplicationReferenceNumberAsync(
        string schemeCode,
        DateOnly date,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(schemeCode);

        var prefix = ApplicationPrefix();
        var day = date.ToString("ddMMyyyy", CultureInfo.InvariantCulture);
        var number = await NextNumberAsync($"APP/{prefix}/{schemeCode}/{day}", cancellationToken);
        return string.Format(
            CultureInfo.InvariantCulture, "{0}({1})/{2}/{3}", prefix, schemeCode, day, number);
    }

    private string ApplicationPrefix()
    {
        var prefix = configuration["Application:ReferencePrefix"];
        return string.IsNullOrWhiteSpace(prefix) ? DefaultApplicationPrefix : prefix;
    }

    private async Task<int> NextNumberAsync(string scope, CancellationToken cancellationToken)
    {
        await Gate.WaitAsync(cancellationToken);
        try
        {
            for (var attempt = 1; ; attempt++)
            {
                try
                {
                    return await IncrementAsync(scope, cancellationToken);
                }
                catch (Exception exception) when (attempt < MaxAttempts && IsLostRace(exception))
                {
                    // Another instance inserted or incremented the same scope first: drop the
                    // stale row and read what it committed.
                    DetachSequences();
                    await Task.Delay(TimeSpan.FromMilliseconds(attempt), cancellationToken);
                }
            }
        }
        finally
        {
            Gate.Release();
        }
    }

    private async Task<int> IncrementAsync(string scope, CancellationToken cancellationToken)
    {
        // In-memory test databases have no transactions; every relational provider gets one.
        await using var transaction = db.Database.IsRelational()
            ? await db.Database.BeginTransactionAsync(cancellationToken)
            : null;

        var sequence = await db.DocumentSequences
            .FirstOrDefaultAsync(x => x.Scope == scope, cancellationToken);

        if (sequence is null)
        {
            sequence = new DocumentSequenceEntity { Scope = scope, LastNumber = 1 };
            db.DocumentSequences.Add(sequence);
        }
        else
        {
            sequence.LastNumber++;
        }

        await db.SaveChangesAsync(cancellationToken);

        if (transaction is not null)
            await transaction.CommitAsync(cancellationToken);

        return sequence.LastNumber;
    }

    private void DetachSequences()
    {
        foreach (var entry in db.ChangeTracker.Entries<DocumentSequenceEntity>().ToList())
            entry.State = EntityState.Detached;
    }

    private static bool IsLostRace(Exception exception) => exception switch
    {
        DbUpdateConcurrencyException => true,
        DbUpdateException updateException =>
            updateException.InnerException is SqlException { Number: 2601 or 2627 },
        _ => false
    };
}
