using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using VHSmart_Api.Shared.Exceptions;
using VHSmart_Api.Shared.Infrastructure.Persistence;
using VHSmart_Api.Shared.Infrastructure.Security;

namespace VHSmart_Api.Features.Personnel.Staff;

// Edit staff (spec 7.4 modal, edit action): the same fields and conditional rules as the
// create; the e-mail stays editable and unique per company among live rows excluding self
// (UQ (CompanyId, Email)) -> handler 409. When a flag is No the cleared field is stored as
// null (same as create). The photo is a separate endpoint, so this command carries no file.
public record UpdateStaffCommand(
    Guid Id,
    string Email,
    Guid TitleId,
    string Name,
    string? IdType,
    string? IdNumber,
    string? EmployeeIdNumber,
    string? Gender,
    string? Religion,
    Guid DepartmentId,
    Guid DesignationId,
    string? OfficeNumber,
    string? MobileNumber,
    bool HasTyphoidInjection,
    DateTime? TyphoidExpiryDate,
    bool IsIhcMember,
    Guid? IhcRoleId) : IRequest<StaffResponse>;

public class UpdateStaffValidator : AbstractValidator<UpdateStaffCommand>
{
    public UpdateStaffValidator(VHSmartDbContext db, ICurrentUser user)
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.Email)
            .Cascade(CascadeMode.Stop)
            .NotEmpty().WithMessage("Email is required.")
            .EmailAddress().WithMessage("A valid email address is required.")
            .MaximumLength(254).WithMessage("Email must be 254 characters or fewer.");
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Name is required.")
            .MaximumLength(200).WithMessage("Name must be 200 characters or fewer.");
        RuleFor(x => x.TitleId)
            .Cascade(CascadeMode.Stop)
            .NotEmpty().WithMessage("Title is required.")
            .MustAsync((id, ct) => db.GeneralData.AnyAsync(
                row => row.Id == id && row.CompanyId == user.CompanyId, ct))
            .WithMessage("Title not found.");
        RuleFor(x => x.DepartmentId)
            .Cascade(CascadeMode.Stop)
            .NotEmpty().WithMessage("Department is required.")
            .MustAsync((id, ct) => db.GeneralData.AnyAsync(
                row => row.Id == id && row.CompanyId == user.CompanyId, ct))
            .WithMessage("Department not found.");
        RuleFor(x => x.DesignationId)
            .Cascade(CascadeMode.Stop)
            .NotEmpty().WithMessage("Designation is required.")
            .MustAsync((id, ct) => db.GeneralData.AnyAsync(
                row => row.Id == id && row.CompanyId == user.CompanyId, ct))
            .WithMessage("Designation not found.");
        RuleFor(x => x.TyphoidExpiryDate)
            .NotEmpty()
            .When(x => x.HasTyphoidInjection)
            .WithMessage("Typhoid injection expiry date is required when typhoid injection is Yes.");
        RuleFor(x => x.IhcRoleId)
            .Cascade(CascadeMode.Stop)
            .NotEmpty()
            .When(x => x.IsIhcMember)
            .WithMessage("IHC role is required when IHC member is Yes.")
            .MustAsync(async (id, ct) => id is null || await db.GeneralData.AnyAsync(
                row => row.Id == id && row.CompanyId == user.CompanyId, ct))
            .WithMessage("IHC role not found.");
        RuleFor(x => x.IdType).MaximumLength(50);
        RuleFor(x => x.IdNumber).MaximumLength(50);
        RuleFor(x => x.EmployeeIdNumber).MaximumLength(50);
        RuleFor(x => x.Gender).MaximumLength(20);
        RuleFor(x => x.Religion).MaximumLength(50);
        RuleFor(x => x.OfficeNumber).MaximumLength(30);
        RuleFor(x => x.MobileNumber).MaximumLength(30);
    }
}

public class UpdateStaffHandler(VHSmartDbContext db, ICurrentUser user)
    : IRequestHandler<UpdateStaffCommand, StaffResponse>
{
    public async Task<StaffResponse> Handle(UpdateStaffCommand request, CancellationToken ct)
    {
        var companyId = user.CompanyId;

        var entity = await db.Staffs.FirstOrDefaultAsync(
            row => row.Id == request.Id && row.CompanyId == companyId, ct)
            ?? throw new NotFoundException("Staff not found.");

        var duplicate = await db.Staffs.AnyAsync(
            row => row.CompanyId == companyId
                && row.Email == request.Email
                && row.Id != request.Id, ct);
        if (duplicate)
            throw new ConflictException("A staff member with this e-mail already exists.");

        entity.Email = request.Email;
        entity.TitleId = request.TitleId;
        entity.Name = request.Name;
        entity.IdType = request.IdType;
        entity.IdNumber = request.IdNumber;
        entity.EmployeeIdNumber = request.EmployeeIdNumber;
        entity.Gender = request.Gender;
        entity.Religion = request.Religion;
        entity.DepartmentId = request.DepartmentId;
        entity.DesignationId = request.DesignationId;
        entity.OfficeNumber = request.OfficeNumber;
        entity.MobileNumber = request.MobileNumber;
        entity.HasTyphoidInjection = request.HasTyphoidInjection;
        entity.TyphoidExpiryDate = request.HasTyphoidInjection ? request.TyphoidExpiryDate : null;
        entity.IsIhcMember = request.IsIhcMember;
        entity.IhcRoleId = request.IsIhcMember ? request.IhcRoleId : null;

        await db.SaveChangesAsync(ct);

        var companyName = await db.Companies
            .AsNoTracking()
            .Where(company => company.Id == companyId)
            .Select(company => company.Name)
            .FirstOrDefaultAsync(ct) ?? string.Empty;

        return CreateStaffHandler.ToResponse(entity, companyName);
    }
}
