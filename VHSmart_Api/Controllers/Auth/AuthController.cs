using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using VHSmart_Api.Features.Auth.Login;
using VHSmart_Api.Features.Auth.SwitchCompany;

namespace VHSmart_Api.Controllers.Auth;

// Identity endpoints (spec 6.2, D-29). Neither screen carries a permission key: login is
// anonymous by design (CodingRules 8.2) and Switch Company is a per-user identity action,
// not a screen from Appendix A - [Authorize] on the controller is its whole gate.
[ApiController]
[Route("api/auth")]
public class AuthController(ISender sender) : ControllerBase
{
    [AllowAnonymous]
    [HttpPost("login")]
    public async Task<IActionResult> Login(LoginCommand command, CancellationToken ct)
        => Ok(await sender.Send(command, ct));

    [Authorize]
    [HttpPost("switch-company")]
    public async Task<IActionResult> SwitchCompany(SwitchCompanyCommand command, CancellationToken ct)
        => Ok(await sender.Send(command, ct));
}
