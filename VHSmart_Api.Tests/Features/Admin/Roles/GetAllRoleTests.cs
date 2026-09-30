using Microsoft.EntityFrameworkCore;
using VHSmart_Api.Features.Admin.Roles;
using VHSmart_Api.Shared.Domain.Admin;
using VHSmart_Api.Shared.Exceptions;
using VHSmart_Api.Shared.Models;

namespace VHSmart_Api.Tests.Features.Admin.Roles;

public class GetAllRoleTests
{
    private static TestCurrentUser PlatformAdmin() =>
        new(Guid.NewGuid().ToString(), Guid.NewGuid(), isPlatformAdmin: true);

    private static TestCurrentUser CompanyUser() =>
        new(Guid.NewGuid().ToString(), Guid.NewGuid());

    private static async Task<TestableVHSmartDbContext> CreateSeededDatabaseAsync()
    {
        var db = TestDbFactory.Create(TestDbFactory.NewDatabaseName(), PlatformAdmin());
        await db.Database.EnsureCreatedAsync();
        return db;
    }

    [Fact]
    public async Task Handle_PlatformAdmin_ReturnsSeededRolesAsGrid()
    {
        var db = await CreateSeededDatabaseAsync();

        var result = await new GetAllRoleHandler(db, PlatformAdmin())
            .Handle(new GetAllRoleQuery(), CancellationToken.None);

        Assert.Equal(3, result.TotalRecords);
        Assert.Equal(
            ["Auditor / Chief Auditor", "Restaurant / Premise Manager", "VH Smart Admin"],
            result.Data.Select(role => role.Name).OrderBy(name => name));
        Assert.All(result.Data, role => Assert.True(role.IsSystemRole));
    }

    [Fact]
    public async Task Handle_SearchTerm_FiltersByName()
    {
        var db = await CreateSeededDatabaseAsync();
        var query = new GetAllRoleQuery
        {
            Request = new DataGridRequest { SearchTerm = "auditor" }
        };

        var result = await new GetAllRoleHandler(db, PlatformAdmin())
            .Handle(query, CancellationToken.None);

        var role = Assert.Single(result.Data);
        Assert.Equal("Auditor / Chief Auditor", role.Name);
        Assert.Equal(1, result.TotalRecords);
    }

    [Fact]
    public async Task Handle_PageSizeTwo_ReturnsTwoRowsAndTwoPages()
    {
        var db = await CreateSeededDatabaseAsync();
        var query = new GetAllRoleQuery
        {
            Request = new DataGridRequest { Page = 2, PageSize = 2 }
        };

        var result = await new GetAllRoleHandler(db, PlatformAdmin())
            .Handle(query, CancellationToken.None);

        // 3 rows, 2 per page: the second page holds the third row and nothing else.
        Assert.Single(result.Data);
        Assert.Equal(3, result.TotalRecords);
        Assert.Equal(2, result.TotalPages);
        Assert.Equal(2, result.CurrentPage);
        Assert.False(result.HasNextPage);
        Assert.True(result.HasPreviousPage);
    }

    [Fact]
    public async Task Handle_CompanySpecificRole_AppearsInList()
    {
        var db = await CreateSeededDatabaseAsync();
        db.Roles.Add(new RoleEntity
        {
            CompanyId = Guid.NewGuid(),
            Name = "Company Auditor",
            IsSystemRole = false
        });
        await db.SaveChangesAsync();

        var result = await new GetAllRoleHandler(db, PlatformAdmin())
            .Handle(new GetAllRoleQuery(), CancellationToken.None);

        Assert.Equal(4, result.TotalRecords);
        Assert.Contains(result.Data, role => role.Name == "Company Auditor" && !role.IsSystemRole);
    }

    [Fact]
    public async Task Handle_NotPlatformAdmin_ThrowsForbidden()
    {
        var db = await CreateSeededDatabaseAsync();

        var exception = await Assert.ThrowsAsync<ForbiddenException>(
            () => new GetAllRoleHandler(db, CompanyUser())
                .Handle(new GetAllRoleQuery(), CancellationToken.None));

        Assert.Equal("Only a platform administrator can manage roles.", exception.Message);
    }

    [Fact]
    public async Task Handle_SoftDeletedRole_IsNotListed()
    {
        var db = await CreateSeededDatabaseAsync();
        var role = await db.Roles.FirstAsync(candidate => candidate.Name == "Restaurant / Premise Manager");
        db.Roles.Remove(role);
        await db.SaveChangesAsync();

        var result = await new GetAllRoleHandler(db, PlatformAdmin())
            .Handle(new GetAllRoleQuery(), CancellationToken.None);

        Assert.Equal(2, result.TotalRecords);
        Assert.DoesNotContain(result.Data, candidate => candidate.Name == "Restaurant / Premise Manager");
    }
}
