using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using VHSmart_Api.Shared.Domain.Admin;
using VHSmart_Api.Shared.Domain.Product;
using VHSmart_Api.Shared.Infrastructure.Persistence;
using VHSmart_Api.Shared.Infrastructure.Security;

namespace VHSmart_Api.Features.Product.ManageProduct;

// Add a product (spec 9.1 form "Manage Product", Common Product Information): Product Scheme,
// Name, Manufacturer, Brand and Category are starred, Marketing Method is optional. Every
// reference is checked against the row it must come from - the three general-data dropdowns
// are the caller's own rows (spec 5.1 reference data is per company; Brand is a COMPANY row,
// Category and Marketing Method are PRODUCT rows), the manufacturer must be a row of the
// caller that actually carries a manufacturer half (the options endpoint offers the same
// set), and the scheme is one of the seeded global AdmSchemes (12.1). CompanyId comes from
// the JWT only (CodingRules 8.1). No product-name uniqueness: Database.md 9 defines no
// unique index and the legacy check is [VERIFY] (flagged in the report). SchemeSpecificData
// and QrCodeKey are left null ("QR Code yet to be generated").
public record CreateProductCommand(
    Guid? SchemeId,
    string? Name,
    Guid? ManufacturerSupplierId,
    Guid? BrandId,
    Guid? CategoryId,
    string? Code,
    string? Gtin,
    string? NutritionContentClaims,
    string? PotentialAllergens,
    string? CalorieContent,
    string? AvailableAt,
    string? PackagingSize,
    Guid? MarketingMethodId) : IRequest<ProductResponse>;

public class CreateProductValidator : AbstractValidator<CreateProductCommand>
{
    public CreateProductValidator(VHSmartDbContext db, ICurrentUser user)
    {
        RuleFor(x => x.SchemeId)
            .Cascade(CascadeMode.Stop)
            .NotEmpty().WithMessage("Product scheme is required.")
            .MustAsync((id, ct) => db.Schemes.AnyAsync(row => row.Id == id, ct))
            .WithMessage("Product scheme not found.");

        RuleFor(x => x.Name)
            .Cascade(CascadeMode.Stop)
            .NotEmpty().WithMessage("Name is required.")
            .MaximumLength(200).WithMessage("Name must be 200 characters or fewer.");

        RuleFor(x => x.ManufacturerSupplierId)
            .Cascade(CascadeMode.Stop)
            .NotEmpty().WithMessage("Manufacturer is required.")
            .MustAsync(async (id, ct) => await db.ManufacturerSuppliers.AnyAsync(
                row => row.Id == id
                    && row.CompanyId == user.CompanyId
                    && row.ManufacturerName != null,
                ct))
            .WithMessage("Manufacturer not found.");

        RuleFor(x => x.BrandId)
            .Cascade(CascadeMode.Stop)
            .NotEmpty().WithMessage("Brand is required.")
            .MustAsync((id, ct) => db.GeneralData.AnyAsync(
                row => row.Id == id
                    && row.CompanyId == user.CompanyId
                    && row.Group == GeneralDataGroup.COMPANY
                    && row.Category == ProductGeneralDataCategory.Brand,
                ct))
            .WithMessage("Brand not found.");

        RuleFor(x => x.CategoryId)
            .Cascade(CascadeMode.Stop)
            .NotEmpty().WithMessage("Category is required.")
            .MustAsync((id, ct) => db.GeneralData.AnyAsync(
                row => row.Id == id
                    && row.CompanyId == user.CompanyId
                    && row.Group == GeneralDataGroup.PRODUCT
                    && row.Category == ProductGeneralDataCategory.ProductCategory,
                ct))
            .WithMessage("Category not found.");

        // Optional: a missing value (null) passes, an unknown id answers the message below
        // (Guid.Empty is not null and therefore not found - same stance as the premise's
        // optional dropdowns).
        RuleFor(x => x.MarketingMethodId)
            .MustAsync(async (id, ct) => id is null
                || await db.GeneralData.AnyAsync(
                    row => row.Id == id
                        && row.CompanyId == user.CompanyId
                        && row.Group == GeneralDataGroup.PRODUCT
                        && row.Category == ProductGeneralDataCategory.MarketingMethod,
                    ct))
            .WithMessage("Marketing method not found.");

        RuleFor(x => x.Code).MaximumLength(500)
            .WithMessage("Code must be 500 characters or fewer.");
        RuleFor(x => x.Gtin).MaximumLength(50)
            .WithMessage("GTIN must be 50 characters or fewer.");
        RuleFor(x => x.NutritionContentClaims).MaximumLength(500)
            .WithMessage("Nutrition content claims must be 500 characters or fewer.");
        RuleFor(x => x.PotentialAllergens).MaximumLength(500)
            .WithMessage("Potential allergens must be 500 characters or fewer.");
        RuleFor(x => x.CalorieContent).MaximumLength(100)
            .WithMessage("Calorie content must be 100 characters or fewer.");
        RuleFor(x => x.AvailableAt).MaximumLength(200)
            .WithMessage("Available at must be 200 characters or fewer.");
        RuleFor(x => x.PackagingSize).MaximumLength(100)
            .WithMessage("Packaging size must be 100 characters or fewer.");
    }
}

public class CreateProductHandler(VHSmartDbContext db, ICurrentUser user)
    : IRequestHandler<CreateProductCommand, ProductResponse>
{
    public async Task<ProductResponse> Handle(
        CreateProductCommand request,
        CancellationToken ct)
    {
        var entity = ProductData.Apply(new ProductEntity
        {
            CompanyId = user.CompanyId
        }, request);

        db.Products.Add(entity);
        await db.SaveChangesAsync(ct);

        return await ProductResponseData.From(db, entity, ct);
    }
}
