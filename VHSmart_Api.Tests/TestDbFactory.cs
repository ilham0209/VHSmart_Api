using System.Collections.Concurrent;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using VHSmart_Api.Shared.Domain;
using VHSmart_Api.Shared.Infrastructure.Persistence;
using VHSmart_Api.Shared.Infrastructure.Security;

namespace VHSmart_Api.Tests;

// Test entities live here because the production model only gains real tables task by task.
public class TestTenantRecord : BaseClass, ITenantEntity
{
    public Guid CompanyId { get; set; }

    public string Name { get; set; } = string.Empty;
}

public class TestGlobalRecord : BaseClass
{
    public string Name { get; set; } = string.Empty;
}

public sealed class TestCurrentUser(
    string userId,
    Guid companyId,
    bool isPlatformAdmin = false,
    bool viewAllCompanies = false) : ICurrentUser
{
    public string UserId { get; } = userId;

    public Guid CompanyId { get; } = companyId;

    public bool IsPlatformAdmin { get; } = isPlatformAdmin;

    public bool ViewAllCompanies { get; } = viewAllCompanies;
}

// The real VHSmartDbContext with test-only entities added through the DbSets below.
public class TestableVHSmartDbContext(
    DbContextOptions<VHSmartDbContext> options,
    ICurrentUser currentUser) : VHSmartDbContext(options, currentUser)
{
    public DbSet<TestTenantRecord> TestTenantRecords => Set<TestTenantRecord>();

    public DbSet<TestGlobalRecord> TestGlobalRecords => Set<TestGlobalRecord>();
}

public static class TestDbFactory
{
    // Named stores are shared per name so several contexts can open the same test database.
    private static readonly ConcurrentDictionary<string, InMemoryDatabaseRoot> DatabaseRoots = new();

    public static string NewDatabaseName() => $"VHSmartTests-{Guid.NewGuid():N}";

    public static TestableVHSmartDbContext Create(string databaseName, ICurrentUser? currentUser = null)
    {
        var options = new DbContextOptionsBuilder<VHSmartDbContext>()
            .UseInMemoryDatabase(databaseName, DatabaseRoots.GetOrAdd(databaseName, _ => new InMemoryDatabaseRoot()))
            .Options;

        return new TestableVHSmartDbContext(options, currentUser ?? new SystemCurrentUser());
    }
}
