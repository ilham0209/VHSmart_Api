using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using VHSmart_Api.Shared.Domain.HalalApplication;
using VHSmart_Api.Shared.Exceptions;
using VHSmart_Api.Shared.Infrastructure.Persistence;
using VHSmart_Api.Shared.Infrastructure.Security;

namespace VHSmart_Api.Features.HalalApplication.CertificateItem;

// "CLICK TO ADD" -> fill the number -> Add (spec 12.8 manual flow steps 3-4; the popup
// confirmation is client side). The handler links the item to a live AppHalalCertificates
// row of the SAME company, creating that row when the number is new (Database.md 10:
// "links it to (or creates) the HalalCertificates row"). The UQ (CompanyId, CertificateNo)
// is company wide, so a number another application of the company already owns is reused
// rather than duplicated - the lookup is case-insensitive to match SQL Server's default
// collation, which the UQ enforces there (flagged stance). An item that already carries a
// number may be re-pointed; the old certificate row stays because other items may cover it
// (flagged: the spec only shows the link on empty rows). Identity comes from the JWT; an
// unknown or foreign item answers 404.
public record AddCertificateNumberToItemCommand(
    Guid ItemId,
    string CertificateNo) : IRequest<AddCertificateNumberResponse>;

// The item id plus the linked certificate - the client needs the certificate id for the
// Upload step that follows the Add.
public record AddCertificateNumberResponse(
    Guid ItemId,
    Guid HalalCertificateId,
    string CertificateNumber);

public class AddCertificateNumberToItemValidator
    : AbstractValidator<AddCertificateNumberToItemCommand>
{
    public AddCertificateNumberToItemValidator()
    {
        RuleFor(x => x.ItemId).NotEmpty();

        RuleFor(x => x.CertificateNo)
            .Cascade(CascadeMode.Stop)
            .NotEmpty().WithMessage("Certificate number is required.")
            .MaximumLength(100)
            .WithMessage("Certificate number must be 100 characters or fewer.");
    }
}

public class AddCertificateNumberToItemHandler(VHSmartDbContext db, ICurrentUser user)
    : IRequestHandler<AddCertificateNumberToItemCommand, AddCertificateNumberResponse>
{
    public async Task<AddCertificateNumberResponse> Handle(
        AddCertificateNumberToItemCommand request,
        CancellationToken ct)
    {
        var item = await db.CertificateItems
            .FirstOrDefaultAsync(
                row => row.Id == request.ItemId && row.CompanyId == user.CompanyId, ct)
            ?? throw new NotFoundException("Certificate item not found.");

        var certificateNo = request.CertificateNo.Trim();

        // One live row per (CompanyId, CertificateNo): reuse it before creating, so two
        // items of one company can share a number instead of colliding on the UQ.
        var certificate = await db.HalalCertificates
            .FirstOrDefaultAsync(
                row => row.CompanyId == item.CompanyId
                    && row.CertificateNo.ToLower() == certificateNo.ToLower(),
                ct);

        if (certificate is null)
        {
            certificate = new HalalCertificateEntity
            {
                CompanyId = item.CompanyId,
                ApplicationId = item.ApplicationId,
                CertificateNo = certificateNo
            };
            db.HalalCertificates.Add(certificate);
        }

        item.HalalCertificateId = certificate.Id;
        await db.SaveChangesAsync(ct);

        return new AddCertificateNumberResponse(
            item.Id, certificate.Id, certificate.CertificateNo);
    }
}
