using Microsoft.EntityFrameworkCore;
using VHSmart_Api.Features.Admin.CertificationBodies;
using VHSmart_Api.Shared.Domain.Admin;
using VHSmart_Api.Shared.Exceptions;

namespace VHSmart_Api.Tests.Features.Admin.CertificationBodies;

public class GetCertificationBodyByIdTests
{
    private static TestCurrentUser PlatformUser() =>
        new(Guid.NewGuid().ToString(), Guid.NewGuid(), Guid.NewGuid(), isPlatformAdmin: true);

    private static async Task<TestableVHSmartDbContext> CreateDbAsync(TestCurrentUser user)
    {
        var db = TestDbFactory.Create(TestDbFactory.NewDatabaseName(), user);
        await db.Database.EnsureCreatedAsync();
        return db;
    }

    [Fact]
    public async Task Handle_ExistingRow_ReturnsAllFormFields()
    {
        var user = PlatformUser();
        var db = await CreateDbAsync(user);
        var malaysiaId = await db.Countries
            .Where(country => country.IsoCode == "MYS")
            .Select(country => country.Id)
            .SingleAsync();
        var row = new CertificationBodyEntity
        {
            Name = "JAKIM",
            Acronym = "JAKIM",
            CountryId = malaysiaId,
            City = "Kuala Lumpur",
            Postcode = "50450",
            State = "Wilayah Persekutuan",
            Telephone = "0380000000",
            Email = "info@jakim.example",
            ContactPerson = "Director",
            BankName = "Bank Islam",
            BankAccountNo = "1234567890"
        };
        db.CertificationBodies.Add(row);
        await db.SaveChangesAsync();

        var response = await new GetCertificationBodyByIdHandler(db, user)
            .Handle(new GetCertificationBodyByIdQuery(row.Id), CancellationToken.None);

        Assert.Equal(row.Id, response.Id);
        Assert.Equal("JAKIM", response.Name);
        Assert.Equal("JAKIM", response.Acronym);
        Assert.Equal(malaysiaId, response.CountryId);
        Assert.Equal("Kuala Lumpur", response.City);
        Assert.Equal("info@jakim.example", response.Email);
        Assert.Equal("Director", response.ContactPerson);
        Assert.Equal("Bank Islam", response.BankName);
        Assert.Null(response.LogoFileName);
    }

    [Fact]
    public async Task Handle_UnknownId_ThrowsNotFound()
    {
        var user = PlatformUser();
        var db = await CreateDbAsync(user);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            new GetCertificationBodyByIdHandler(db, user)
                .Handle(new GetCertificationBodyByIdQuery(Guid.NewGuid()), CancellationToken.None));
    }

    [Fact]
    public async Task Handle_CompanyUser_ThrowsForbidden()
    {
        var db = await CreateDbAsync(new TestCurrentUser(Guid.NewGuid().ToString(), Guid.NewGuid()));

        await Assert.ThrowsAsync<ForbiddenException>(() =>
            new GetCertificationBodyByIdHandler(
                    db, new TestCurrentUser(Guid.NewGuid().ToString(), Guid.NewGuid()))
                .Handle(new GetCertificationBodyByIdQuery(Guid.NewGuid()), CancellationToken.None));
    }
}
