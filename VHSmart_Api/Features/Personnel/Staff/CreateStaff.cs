using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using VHSmart_Api.Shared.Domain.Companies;
using VHSmart_Api.Shared.Exceptions;
using VHSmart_Api.Shared.Infrastructure.Persistence;
using VHSmart_Api.Shared.Infrastructure.Security;

namespace VHSmart_Api.Features.Personnel.Staff;

// Add staff (spec 7.4 modal "Manage Staff", tab Staff Information): Email*, Company Name*,
// Title*, Name*, ID Type, ID Number, Employee ID Number, Gender, Religion, Department*,
// Designation*, Office Number, Mobile Number. Company Name is the caller's own company (JWT
// only, CodingRules 8.1) - never part of the command. Conditional rules [MANUAL]: the typhoid
// expiry date is required when Typhoid injection = Yes and the IHC role when "Are you IHC
// member?" = Yes; when a flag is No the cleared field is stored as null so the row can never
// hold an inconsistent pair. UQ (CompanyId, Email) among live rows (Database.md 3) -> handler
// 409; the legacy duplicate messages are client-side only, ours are server-side. The photo and
// the attachments are separate endpoints (A-03 pattern), so this command carries no file.
public record CreateStaffCommand(
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

public class CreateStaffValidator : AbstractValidator<CreateStaffCommand>
{
    public CreateStaffValidator(VHSmartDbContext db, ICurrentUser user)
    {
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

public class CreateStaffHandler(VHSmartDbContext db, ICurrentUser user)
    : IRequestHandler<CreateStaffCommand, StaffResponse>
{
    public async Task<StaffResponse> Handle(CreateStaffCommand request, CancellationToken ct)
    {
        var companyId = user.CompanyId;

        var duplicate = await db.Staffs.AnyAsync(
            row => row.CompanyId == companyId && row.Email == request.Email, ct);
        if (duplicate)
            throw new ConflictException("A staff member with this e-mail already exists.");

        var entity = new StaffEntity
        {
            CompanyId = companyId,
            Email = request.Email,
            TitleId = request.TitleId,
            Name = request.Name,
            IdType = request.IdType,
            IdNumber = request.IdNumber,
            EmployeeIdNumber = request.EmployeeIdNumber,
            Gender = request.Gender,
            Religion = request.Religion,
            DepartmentId = request.DepartmentId,
            DesignationId = request.DesignationId,
            OfficeNumber = request.OfficeNumber,
            MobileNumber = request.MobileNumber,
            HasTyphoidInjection = request.HasTyphoidInjection,
            TyphoidExpiryDate = request.HasTyphoidInjection ? request.TyphoidExpiryDate : null,
            IsIhcMember = request.IsIhcMember,
            IhcRoleId = request.IsIhcMember ? request.IhcRoleId : null
        };
        db.Staffs.Add(entity);
        await db.SaveChangesAsync(ct);

        var companyName = await db.Companies
            .AsNoTracking()
            .Where(company => company.Id == companyId)
            .Select(company => company.Name)
            .FirstOrDefaultAsync(ct) ?? string.Empty;

        return ToResponse(entity, companyName);
    }

    internal static StaffResponse ToResponse(StaffEntity staff, string companyName) =>
        new(
            staff.Id,
            staff.Email,
            staff.TitleId,
            staff.Name,
            staff.IdType,
            staff.IdNumber,
            staff.EmployeeIdNumber,
            staff.Gender,
            staff.Religion,
            staff.DepartmentId,
            staff.DesignationId,
            staff.OfficeNumber,
            staff.MobileNumber,
            staff.HasTyphoidInjection,
            staff.TyphoidExpiryDate,
            staff.IsIhcMember,
            staff.IhcRoleId,
            staff.Photo?.FileName,
            companyName,
            staff.SysDateModified);
}
