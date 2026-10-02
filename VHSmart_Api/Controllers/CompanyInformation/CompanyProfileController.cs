using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using VHSmart_Api.Features.CompanyInformation.Profiles;
using VHSmart_Api.Shared.Infrastructure.Security;

namespace VHSmart_Api.Controllers.CompanyInformation;

// Company > Profiles (spec 7.3). The company id is in the route because the spec 7.3 super
// user list opens any company's profile; the handlers re-check the scope (platform admin, or
// own company only) and answer 404 for anything else - the caller's identity still comes from
// the JWT alone (CodingRules 7.4). "staff-options" is a literal sub-path under {companyId}.
[ApiController]
[Route("api/company-information/profiles")]
[Authorize]
public class CompanyProfileController(ISender sender) : ControllerBase
{
    [HttpGet]
    [HasPermission(PermissionKeys.CompanyProfiles, PermissionAction.View)]
    public async Task<IActionResult> GetAll(
        [FromQuery] GetCompanyProfilesQuery query,
        CancellationToken ct) =>
        Ok(await sender.Send(query, ct));

    [HttpGet("{companyId:guid}/staff-options")]
    [HasPermission(PermissionKeys.CompanyProfiles, PermissionAction.View)]
    public async Task<IActionResult> GetStaffOptions(Guid companyId, CancellationToken ct) =>
        Ok(await sender.Send(new GetProfileStaffOptionsQuery(companyId), ct));

    [HttpGet("{companyId:guid}")]
    [HasPermission(PermissionKeys.CompanyProfiles, PermissionAction.View)]
    public async Task<IActionResult> GetById(Guid companyId, CancellationToken ct) =>
        Ok(await sender.Send(new GetCompanyProfileQuery(companyId), ct));

    [HttpPut("{companyId:guid}")]
    [HasPermission(PermissionKeys.CompanyProfiles, PermissionAction.Edit)]
    public async Task<IActionResult> Update(
        Guid companyId,
        UpdateCompanyProfileCommand command,
        CancellationToken ct) =>
        Ok(await sender.Send(command with { CompanyId = companyId }, ct));
}
