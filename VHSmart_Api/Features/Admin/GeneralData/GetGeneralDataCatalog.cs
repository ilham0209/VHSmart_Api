using MediatR;
using VHSmart_Api.Shared.Domain.Admin;

namespace VHSmart_Api.Features.Admin.GeneralData;

// The two dropdowns of the screen (spec 5.1): the Group list, and the Category list that
// depends on the selected Group - served from the code catalogue in one call.
public record GetGeneralDataCatalogQuery
    : IRequest<IReadOnlyList<GeneralDataCatalogResponse>>;

public record GeneralDataCatalogResponse(GeneralDataGroup Group, IReadOnlyList<string> Categories);

public class GetGeneralDataCatalogHandler
    : IRequestHandler<GetGeneralDataCatalogQuery, IReadOnlyList<GeneralDataCatalogResponse>>
{
    public Task<IReadOnlyList<GeneralDataCatalogResponse>> Handle(
        GetGeneralDataCatalogQuery request,
        CancellationToken ct)
    {
        IReadOnlyList<GeneralDataCatalogResponse> items =
        [
            .. GeneralDataCatalog.Groups.Select(group => new GeneralDataCatalogResponse(
                group, GeneralDataCatalog.Categories(group)))
        ];

        return Task.FromResult(items);
    }
}
