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
using VHSmart_Api.Shared.Domain;
using VHSmart_Api.Shared.Domain.Admin;
using VHSmart_Api.Shared.Domain.Companies;
using VHSmart_Api.Shared.Domain.HalalApplication;
using VHSmart_Api.Shared.Domain.Payment;
using VHSmart_Api.Shared.Domain.Product;
using VHSmart_Api.Shared.Infrastructure.Persistence;
using VHSmart_Api.Shared.Infrastructure.Security;
using VHSmart_Api.Shared.Infrastructure.Storage;
using VHSmart_Api.Tests.Features.Product.ManageProduct;
using VHSmart_Api.Tests.Shared.Infrastructure.Security;

namespace VHSmart_Api.Tests.Features.Payment.CertificatePayment;

// Real pipeline with an in-memory store, a permission stub for Payment.Certificate and an
// in-memory StubFileStorage so the attachment routes never touch a storage root.
internal sealed class CertificatePaymentApiFactory : WebApplicationFactory<Program>
{
    private static readonly string TestSigningKey = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));

    private readonly Guid _grantedRoleId;
    private readonly bool _grantPermissions;
    private readonly string _databaseName = $"VHSmartCertificatePaymentApiTests-{Guid.NewGuid():N}";

    public CertificatePaymentApiFactory(Guid grantedRoleId, bool grantPermissions = true)
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

    // Company + CB + scheme + two staff + a batched application with a brand product chain +
    // one payment in the first workflow step with one PDF attachment. Ids come back for the
    // assertions.
    public async Task<(
        Guid SchemeId,
        Guid ApplicationId,
        Guid StaffId,
        Guid PaymentId,
        Guid AttachmentId)> SeedFullStackAsync()
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<VHSmartDbContext>();
        await db.Database.EnsureCreatedAsync();

        var certificationBody = new CertificationBodyEntity
        {
            Name = "JABATAN KEMAJUAN ISLAM MALAYSIA (JAKIM)"
        };
        var scheme = new SchemeEntity { Code = "FOOD", Name = "Halal Food", SortOrder = 1 };
        db.CertificationBodies.Add(certificationBody);
        db.Schemes.Add(scheme);
        await db.SaveChangesAsync();

        db.Companies.Add(new CompanyEntity
        {
            Id = CompanyId,
            Name = "HALAL CO",
            CertificationBodyId = certificationBody.Id
        });

        var brand = new GeneralDataEntity
        {
            CompanyId = CompanyId,
            Group = GeneralDataGroup.PRODUCT,
            Category = "Brand",
            Name = "Seri Rasa"
        };
        var category = new GeneralDataEntity
        {
            CompanyId = CompanyId,
            Group = GeneralDataGroup.PRODUCT,
            Category = "Product Category",
            Name = "Sauce"
        };
        db.GeneralData.AddRange(brand, category);
        await db.SaveChangesAsync();

        var batch = new BatchEntity
        {
            CompanyId = CompanyId,
            SchemeId = scheme.Id,
            Name = "BATCH-2026"
        };
        db.Batches.Add(batch);
        await db.SaveChangesAsync();

        var product = new ProductEntity
        {
            CompanyId = CompanyId,
            SchemeId = scheme.Id,
            Name = "Rendang Paste",
            BrandId = brand.Id,
            CategoryId = category.Id
        };
        db.Products.Add(product);
        await db.SaveChangesAsync();

        db.BatchProducts.Add(new BatchProductEntity
        {
            CompanyId = CompanyId,
            BatchId = batch.Id,
            ProductId = product.Id,
            MappingStatus = BatchProductMappingStatus.Active
        });

        var application = new ApplicationEntity
        {
            CompanyId = CompanyId,
            ReferenceNo = $"HAL({scheme.Id:N})/12102026/1",
            ApplicationType = "New",
            Status = ApplicationStatus.ApplicationApproved,
            StatusDate = DateTime.UtcNow,
            SchemeId = scheme.Id,
            BatchId = batch.Id,
            CbApplicationNo = "CB-001"
        };
        db.Applications.Add(application);

        var staff = new StaffEntity
        {
            CompanyId = CompanyId,
            Name = "Staff One",
            Email = $"{Guid.NewGuid():N}@example.com"
        };
        db.Staffs.Add(staff);
        await db.SaveChangesAsync();

        var payment = new CertificatePaymentEntity
        {
            CompanyId = CompanyId,
            HalalApplicationId = application.Id,
            Amount = 500m,
            Status = PaymentStatus.RequestBankDraft,
            SubmittedByStaffId = staff.Id,
            SubmittedDate = new DateOnly(2026, 10, 6),
            ReceivedByStaffId = staff.Id,
            ReceivedDate = new DateOnly(2026, 10, 7)
        };
        db.Payments.Add(payment);

        var attachment = new PaymentAttachmentEntity
        {
            CompanyId = CompanyId,
            PaymentId = payment.Id,
            DocumentType = "Charge Letter",
            Document = new StoredFile
            {
                FileName = "charge-letter.pdf",
                StorageKey = $"{Guid.NewGuid():N}",
                ContentType = "application/pdf",
                SizeBytes = 128
            }
        };
        db.PaymentAttachments.Add(attachment);
        await db.SaveChangesAsync();

        return (scheme.Id, application.Id, staff.Id, payment.Id, attachment.Id);
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
                ? actions.Select(action => (_grantedRoleId, PermissionKeys.PaymentCertificate, action)).ToArray()
                : Array.Empty<(Guid, string, PermissionAction)>();

            services.AddSingleton<IPermissionService>(new StubPermissionService(granted));
        });
    }
}
