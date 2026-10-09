using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using VHSmart_Api.Shared.Exceptions;
using VHSmart_Api.Shared.Infrastructure.Persistence;
using VHSmart_Api.Shared.Infrastructure.Security;

namespace VHSmart_Api.Features.HalalApplication.HalalCertificate;

// Edit of one Halal Certificate row (spec 12.8 Action "edit"; [VERIFY] the spec never
// captured which fields the legacy dialog showed, so this endpoint exposes exactly the
// Database.md 10 columns: Certificate No. plus the two dates - flagged for the owner).
// Certificate No. is required (Database.md marks it with an asterisk) and carries the
// company-wide UQ (CompanyId, CertificateNo): a number already owned by another live row
// of the same company answers 409. D-26 keeps certificate edits allowed after submit (the
// read-only rule names the certificate number as the exception), so there is no
// EnsureDraft here. Unknown or foreign certificates answer 404.
public record UpdateHalalCertificateCommand(
    Guid Id,
    string CertificateNumber,
    DateOnly? IssuedDate,
    DateOnly? ExpiryDate) : IRequest<HalalCertificateDetailResponse>;

public class UpdateHalalCertificateValidator : AbstractValidator<UpdateHalalCertificateCommand>
{
    public UpdateHalalCertificateValidator()
    {
        RuleFor(x => x.Id).NotEmpty();

        RuleFor(x => x.CertificateNumber)
            .Cascade(CascadeMode.Stop)
            .NotEmpty().WithMessage("Certificate number is required.")
            .MaximumLength(100)
            .WithMessage("Certificate number must be 100 characters or fewer.");
    }
}

public class UpdateHalalCertificateHandler(VHSmartDbContext db, ICurrentUser user)
    : IRequestHandler<UpdateHalalCertificateCommand, HalalCertificateDetailResponse>
{
    public async Task<HalalCertificateDetailResponse> Handle(
        UpdateHalalCertificateCommand request,
        CancellationToken ct)
    {
        var certificate = await db.HalalCertificates
            .FirstOrDefaultAsync(
                row => row.Id == request.Id && row.CompanyId == user.CompanyId, ct)
            ?? throw new NotFoundException("Halal certificate not found.");

        var certificateNo = request.CertificateNumber.Trim();
        var duplicate = await db.HalalCertificates.AnyAsync(
            row => row.Id != certificate.Id
                && row.CompanyId == certificate.CompanyId
                && row.CertificateNo.ToLower() == certificateNo.ToLower(),
            ct);
        if (duplicate)
            throw new ConflictException("Certificate number already exists.");

        certificate.CertificateNo = certificateNo;
        certificate.IssuedDate = request.IssuedDate;
        certificate.ExpiryDate = request.ExpiryDate;
        await db.SaveChangesAsync(ct);

        return await HalalCertificateDetailLoader.LoadAsync(db, certificate, ct);
    }
}
