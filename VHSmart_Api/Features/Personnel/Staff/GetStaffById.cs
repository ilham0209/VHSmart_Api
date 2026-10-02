using MediatR;
using Microsoft.EntityFrameworkCore;
using VHSmart_Api.Shared.Exceptions;
using VHSmart_Api.Shared.Infrastructure.Persistence;
using VHSmart_Api.Shared.Infrastructure.Security;

namespace VHSmart_Api.Features.Personnel.Staff;

// One staff row for the view / edit modal (spec 7.4 "Manage Staff"): every form field plus the
// Company Name cell and the photo file name. The FK ids pair with GET options - the frontend
// resolves the labels from its own lists. The explicit CompanyId match keeps a ViewAll caller
// on its own rows and answers 404 for everything else (CodingRules 9).
public record GetStaffByIdQuery(Guid Id) : IRequest<StaffResponse>;

public record StaffResponse(
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
    Guid? IhcRoleId,
    string? PhotoFileName,
    string CompanyName,
    DateTime? ModifiedDate);

public class GetStaffByIdHandler(VHSmartDbContext db, ICurrentUser user)
    : IRequestHandler<GetStaffByIdQuery, StaffResponse>
{
    public async Task<StaffResponse> Handle(GetStaffByIdQuery request, CancellationToken ct)
    {
        var staff = await db.Staffs
            .AsNoTracking()
            .FirstOrDefaultAsync(
                row => row.Id == request.Id && row.CompanyId == user.CompanyId, ct)
            ?? throw new NotFoundException("Staff not found.");

        var companyName = await db.Companies
            .AsNoTracking()
            .Where(company => company.Id == staff.CompanyId)
            .Select(company => company.Name)
            .FirstOrDefaultAsync(ct) ?? string.Empty;

        return new StaffResponse(
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
}
