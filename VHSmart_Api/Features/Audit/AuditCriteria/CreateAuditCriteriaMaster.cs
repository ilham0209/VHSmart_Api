using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using VHSmart_Api.Shared.Domain.Audit;
using VHSmart_Api.Shared.Exceptions;
using VHSmart_Api.Shared.Infrastructure.Persistence;
using VHSmart_Api.Shared.Infrastructure.Security;

namespace VHSmart_Api.Features.Audit.AuditCriteria;

// The green "+" of the modal's Criteria / Sub Criteria dropdowns (spec 14.5: 'searchable
// dropdown plus green "+" that opens "Add New Audit Criteria" with a single required field
// Criteria and an Add button'; "Criteria and Sub Criteria are reusable master values created
// inline" [CONFIRMED / MANUAL]). One endpoint serves both kinds - the client sends which
// one the pressed "+". The unique rule (Database.md 11 UQ (CompanyId, Kind, Text) among
// live rows) means the value is taken while a live row exists: 409 with a friendly message
// instead of letting the index fire. The message and the field name "Text" are ours (the
// spec shows the label, not the error). CompanyId comes from the JWT only (CodingRules 8.1).
public record CreateAuditCriteriaMasterCommand(
    AuditCriteriaMasterKind Kind,
    string Text) : IRequest<AuditCriteriaMasterResponse>;

public record AuditCriteriaMasterResponse(
    Guid Id,
    AuditCriteriaMasterKind Kind,
    string Text);

public class CreateAuditCriteriaMasterValidator
    : AbstractValidator<CreateAuditCriteriaMasterCommand>
{
    public CreateAuditCriteriaMasterValidator()
    {
        RuleFor(x => x.Kind)
            .IsInEnum()
            .WithMessage("Kind must be Criteria or SubCriteria.");

        RuleFor(x => x.Text)
            .Cascade(CascadeMode.Stop)
            .NotEmpty().WithMessage("Text is required.")
            .MaximumLength(500).WithMessage("Text must be 500 characters or fewer.");
    }
}

public class CreateAuditCriteriaMasterHandler(VHSmartDbContext db, ICurrentUser user)
    : IRequestHandler<CreateAuditCriteriaMasterCommand, AuditCriteriaMasterResponse>
{
    public async Task<AuditCriteriaMasterResponse> Handle(
        CreateAuditCriteriaMasterCommand request,
        CancellationToken ct)
    {
        var duplicate = await db.AuditCriteriaMasters.AnyAsync(
            row => row.CompanyId == user.CompanyId
                && row.Kind == request.Kind
                && row.Text == request.Text,
            ct);
        if (duplicate)
            throw new ConflictException("This value already exists.");

        var entity = new AuditCriteriaMasterEntity
        {
            CompanyId = user.CompanyId,
            Kind = request.Kind,
            Text = request.Text
        };
        db.AuditCriteriaMasters.Add(entity);
        await db.SaveChangesAsync(ct);

        return new AuditCriteriaMasterResponse(entity.Id, entity.Kind, entity.Text);
    }
}
