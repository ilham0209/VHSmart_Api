using MediatR;
using VHSmart_Api.Shared.Infrastructure.Persistence;
using VHSmart_Api.Shared.Infrastructure.Security;

namespace VHSmart_Api.Features.Admin.Users;

// One user for the view/edit modal (spec 3.2). Data scope and 404 semantics live in
// UserLookup; the payload is the shared UserDetailsResponse.
public record GetUserByIdQuery(Guid Id) : IRequest<UserDetailsResponse>;

public class GetUserByIdHandler(VHSmartDbContext db, ICurrentUser caller)
    : IRequestHandler<GetUserByIdQuery, UserDetailsResponse>
{
    public async Task<UserDetailsResponse> Handle(GetUserByIdQuery request, CancellationToken ct)
    {
        var user = await UserLookup.GetVisibleUserAsync(db, caller, request.Id, ct);
        return await UserDetailsResponse.FromAsync(db, user, ct);
    }
}
