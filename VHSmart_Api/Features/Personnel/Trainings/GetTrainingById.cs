using MediatR;
using Microsoft.EntityFrameworkCore;
using VHSmart_Api.Shared.Domain.Companies;
using VHSmart_Api.Shared.Exceptions;
using VHSmart_Api.Shared.Infrastructure.Persistence;
using VHSmart_Api.Shared.Infrastructure.Security;

namespace VHSmart_Api.Features.Personnel.Trainings;

// Detail of one training - the state the "Manage Training" modal opens with (spec 7.5): the
// training fields, the Attendance List (#, Name, Designation, IHC Member, Role in IHC) and the
// Training Module List (#, Module Name, Module Type, plus the file name so the client knows
// whether the download action has anything to stream). Everything is scoped to the caller's
// company; an unknown or foreign training answers 404 (never 403). The same loader builds the
// response after Create/Update/AddModule, so one code path defines the shape.
public record GetTrainingByIdQuery(Guid Id) : IRequest<TrainingDetailResponse>;

public record TrainingDetailResponse(
    Guid Id,
    string Name,
    TrainingType TrainingType,
    string Company,
    DateTime TrainingDate,
    IReadOnlyList<TrainingAttendeeResponse> Attendees,
    IReadOnlyList<TrainingModuleResponse> Modules);

public record TrainingAttendeeResponse(
    Guid StaffId,
    int No,
    string Name,
    string Designation,
    bool IsIhcMember,
    string? IhcRole);

public record TrainingModuleResponse(
    Guid Id,
    int No,
    string ModuleName,
    string ModuleType,
    string? FileName);

public class GetTrainingByIdHandler(VHSmartDbContext db, ICurrentUser user)
    : IRequestHandler<GetTrainingByIdQuery, TrainingDetailResponse>
{
    public Task<TrainingDetailResponse> Handle(
        GetTrainingByIdQuery request,
        CancellationToken ct) =>
        TrainingResponseLoader.LoadAsync(db, user.CompanyId, request.Id, ct);
}

internal static class TrainingResponseLoader
{
    public static async Task<TrainingDetailResponse> LoadAsync(
        VHSmartDbContext db,
        Guid companyId,
        Guid trainingId,
        CancellationToken ct)
    {
        var training = await db.Trainings
            .AsNoTracking()
            .Where(row => row.Id == trainingId && row.CompanyId == companyId)
            .Select(row => new { row.Id, row.Name, row.TrainingType, row.TrainingDate })
            .FirstOrDefaultAsync(ct)
            ?? throw new NotFoundException("Training not found.");

        var companyName = await db.Companies
            .AsNoTracking()
            .Where(row => row.Id == companyId)
            .Select(row => row.Name)
            .FirstOrDefaultAsync(ct) ?? string.Empty;

        // Soft-deleted trainings and staff drop out of the tenant/global filters, so the lists
        // below only ever show live rows.
        var attendees = await (
                from attendee in db.TrainingAttendees.AsNoTracking()
                    .Where(row => row.TrainingId == trainingId)
                join staff in db.Staffs.AsNoTracking()
                    on attendee.StaffId equals staff.Id
                orderby staff.Name
                select new
                {
                    staff.Id,
                    staff.Name,
                    Designation = staff.Designation == null ? string.Empty : staff.Designation.Name,
                    staff.IsIhcMember,
                    IhcRole = staff.IhcRole == null ? null : staff.IhcRole.Name
                })
            .ToListAsync(ct);

        var modules = await db.TrainingModules
            .AsNoTracking()
            .Where(row => row.TrainingId == trainingId)
            .OrderBy(row => row.SysDateCreated)
            .ThenBy(row => row.Id)
            .Select(row => new
            {
                row.Id,
                row.ModuleName,
                ModuleType = row.ModuleType == null ? string.Empty : row.ModuleType.Name,
                FileName = row.Document == null ? null : row.Document.FileName
            })
            .ToListAsync(ct);

        return new TrainingDetailResponse(
            training.Id,
            training.Name,
            training.TrainingType,
            companyName,
            training.TrainingDate,
            [.. attendees.Select((row, index) => new TrainingAttendeeResponse(
                row.Id,
                index + 1,
                row.Name,
                row.Designation,
                row.IsIhcMember,
                row.IhcRole))],
            [.. modules.Select((row, index) => new TrainingModuleResponse(
                row.Id,
                index + 1,
                row.ModuleName,
                row.ModuleType,
                row.FileName))]);
    }
}
