using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using VHSmart_Api.Shared.Domain.Admin;
using VHSmart_Api.Shared.Exceptions;
using VHSmart_Api.Shared.Infrastructure.Persistence;
using VHSmart_Api.Shared.Infrastructure.Security;

namespace VHSmart_Api.Features.Product.ManageProduct;

// Edit a product (spec 9.1). Same field rules as Create (kept in each feature file on
// purpose, CodingRules 4) and the same CompanyId-from-the-JWT mapping through ProductData,
// so Create and Update can never disagree. An unknown or foreign row answers 404 - the
// explicit CompanyId match keeps a Switch Company = ALL caller on their own rows.
public record UpdateProductCommand(
    Guid Id,
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

public class UpdateProductValidator : AbstractValidator<UpdateProductCommand>
{
    public UpdateProductValidator(VHSmartDbContext db, ICurrentUser user)
    {
        RuleFor(x => x.Id).NotEmpty();

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

public class UpdateProductHandler(VHSmartDbContext db, ICurrentUser user)
    : IRequestHandler<UpdateProductCommand, ProductResponse>
{
    public async Task<ProductResponse> Handle(
        UpdateProductCommand request,
        CancellationToken ct)
    {
        var entity = await db.Products
            .FirstOrDefaultAsync(
                row => row.Id == request.Id && row.CompanyId == user.CompanyId, ct);

        if (entity is null)
            throw new NotFoundException("Product not found.");

        ProductData.Apply(entity, request);
        await db.SaveChangesAsync(ct);

        return await ProductResponseData.From(db, entity, ct);
    }
}
