using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using VHSmart_Api.Shared.Domain.Admin;
using VHSmart_Api.Shared.Domain.Companies;
using VHSmart_Api.Shared.Domain.Payment;
using VHSmart_Api.Shared.Infrastructure.Persistence;
using VHSmart_Api.Shared.Infrastructure.Security;
using VHSmart_Api.Shared.Infrastructure.Storage;
using VHSmart_Api.Tests.Features.Product.ManageProduct;
using VHSmart_Api.Tests.Shared.Infrastructure.Security;

namespace VHSmart_Api.Tests.Features.Payment.OtherPayment;

// Real pipeline with an in-memory store, a permission stub for Payment.Other and an
// in-memory StubFileStorage (no attachment route exists here yet, but the host registers the
// same seam the Certificate factory does).
internal sealed class OtherPaymentApiFactory : WebApplicationFactory<Program>
{
    private static readonly string TestSigningKey = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));

    private readonly Guid _grantedRoleId;
    private readonly bool _grantPermissions;
    private readonly string _databaseName = $"VHSmartOtherPaymentApiTests-{Guid.NewGuid():N}";

    public OtherPaymentApiFactory(Guid grantedRoleId, bool grantPermissions = true)
    {
        _grantedRoleId = grantedRoleId;
        _grantPermissions = grantPermissions;
    }

    public Guid CompanyId { get; } = Guid.NewGuid();

    public string CreateToken(Guid roleId, Guid? userId = null)
    {
        var parameters = Services
            .GetRequiredService<IOptionsMonitor<JwtBearerOptions>>()
            .Get(JwtBearerDefaults.AuthenticationScheme)
            .TokenValidationParameters;

        var key = parameters.IssuerSigningKey as SymmetricSecurityKey
            ?? throw new InvalidOperationException("Jwt:SigningKey did not reach the test host.");

        var token = new JwtSecurityToken(
            issuer: parameters.ValidIssuer,
            audience: parameters.ValidAudience,
            claims:
            [
                new Claim(JwtClaims.UserId, (userId ?? Guid.NewGuid()).ToString()),
                new Claim(JwtClaims.CompanyId, CompanyId.ToString()),
                new Claim(JwtClaims.RoleId, roleId.ToString()),
                new Claim(JwtClaims.IsPlatformAdmin, bool.FalseString),
                new Claim(JwtClaims.ViewAllCompanies, bool.FalseString)
            ],
            expires: DateTime.UtcNow.AddMinutes(5),
            signingCredentials: new SigningCredentials(key, SecurityAlgorithms.HmacSha256));

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    // Company + a Payment Category + a payee + one staff + one Other payment in the first
    // workflow step. Ids come back for the assertions.
    public async Task<(Guid CategoryId, Guid PayeeId, Guid StaffId, Guid PaymentId)>
        SeedStackAsync()
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<VHSmartDbContext>();
        await db.Database.EnsureCreatedAsync();

        db.Companies.Add(new CompanyEntity
        {
            Id = CompanyId,
            Name = "HALAL CO"
        });

        var category = new GeneralDataEntity
        {
            CompanyId = CompanyId,
            Group = GeneralDataGroup.PAYMENT,
            Category = "Payment Category",
            Name = "CATEGORY A"
        };
        var payee = new ServiceProviderEntity
        {
            CompanyId = CompanyId,
            Name = "SERUNAI COMMERCE",
            Address = "Jalan Gombak 1",
            Postcode = "50450",
            CountryId = Guid.NewGuid(),
            State = "Wilayah Persekutuan",
            Telephone = "0380000000",
            Email = $"{Guid.NewGuid():N}@example.com",
            ContactPerson = "Aminah",
            BankName = "MAYBANK",
            BankAccountNo = "1234-5678"
        };
        var staff = new StaffEntity
        {
            CompanyId = CompanyId,
            Name = "Staff One",
            Email = $"{Guid.NewGuid():N}@example.com"
        };
        db.GeneralData.Add(category);
        db.ServiceProviders.Add(payee);
        db.Staffs.Add(staff);
        await db.SaveChangesAsync();

        var payment = new OtherPaymentEntity
        {
            CompanyId = CompanyId,
            PaymentCategoryId = category.Id,
            ServiceProviderId = payee.Id,
            Amount = 150m,
            Status = PaymentStatus.RequestBankDraft,
            SubmittedByStaffId = staff.Id,
            SubmittedDate = new DateOnly(2026, 10, 6),
            ReceivedByStaffId = staff.Id,
            ReceivedDate = new DateOnly(2026, 10, 7),
            PaymentVoucherNo = "ABC"
        };
        db.Payments.Add(payment);
        await db.SaveChangesAsync();

        return (category.Id, payee.Id, staff.Id, payment.Id);
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseSetting("Jwt:SigningKey", TestSigningKey);

        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IDbContextOptionsConfiguration<VHSmartDbContext>>();
            services.RemoveAll<DbContextOptions<VHSmartDbContext>>();
            services.RemoveAll<DbContextOptions>();
            services.AddDbContext<VHSmartDbContext>(options => options.UseInMemoryDatabase(_databaseName));

            services.RemoveAll<IFileStorage>();
            services.AddSingleton<IFileStorage>(new StubFileStorage());

            var actions = new[] { PermissionAction.View, PermissionAction.Create, PermissionAction.Edit, PermissionAction.Delete };
            var granted = _grantPermissions
                ? actions.Select(action => (_grantedRoleId, PermissionKeys.PaymentOther, action)).ToArray()
                : Array.Empty<(Guid, string, PermissionAction)>();

            services.AddSingleton<IPermissionService>(new StubPermissionService(granted));
        });
    }
}
