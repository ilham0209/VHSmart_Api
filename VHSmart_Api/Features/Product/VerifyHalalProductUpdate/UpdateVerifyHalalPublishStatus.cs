using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using VHSmart_Api.Shared.Exceptions;
using VHSmart_Api.Shared.Infrastructure.Persistence;
using VHSmart_Api.Shared.Infrastructure.Security;

namespace VHSmart_Api.Features.Product.VerifyHalalProductUpdate;

// Toggle the Verify Halal publish status of one product (spec 9.4 "Publish Status in Verify
// Halal", [CONFIRMED] values: "Published" / "Published Without Image"). The stored value is
// PublishedWithoutImage (no space, Database.md 9 nvarchar(30)). D-28: local only, no external
// Verify Halal call - this writes the column and nothing else. An unknown or foreign row
// answers 404 - the explicit CompanyId match keeps a Switch Company = ALL caller on their
// own rows.
public record UpdateVerifyHalalPublishStatusCommand(Guid Id, string PublishStatus) : IRequest;

public class UpdateVerifyHalalPublishStatusValidator
    : AbstractValidator<UpdateVerifyHalalPublishStatusCommand>
{
    public UpdateVerifyHalalPublishStatusValidator()
    {
        RuleFor(x => x.Id).NotEmpty();

        RuleFor(x => x.PublishStatus)
            .Cascade(CascadeMode.Stop)
            .NotEmpty().WithMessage("Publish status is required.")
            .Must(value => value is VerifyHalalPublishStatus.Published
                or VerifyHalalPublishStatus.PublishedWithoutImage)
            .WithMessage("Publish status is invalid.");
    }
}

public class UpdateVerifyHalalPublishStatusHandler(VHSmartDbContext db, ICurrentUser user)
    : IRequestHandler<UpdateVerifyHalalPublishStatusCommand>
{
    public async Task Handle(
        UpdateVerifyHalalPublishStatusCommand request,
        CancellationToken ct)
    {
        var entity = await db.Products
            .FirstOrDefaultAsync(
                row => row.Id == request.Id && row.CompanyId == user.CompanyId, ct);

        if (entity is null)
            throw new NotFoundException("Product not found.");

        entity.VerifyHalalPublishStatus = request.PublishStatus;
        await db.SaveChangesAsync(ct);
    }
}
