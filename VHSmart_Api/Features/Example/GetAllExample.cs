using IMAS_API_Example.Features.Example;
using IMAS_API_Example.Shared.Extensions;
using IMAS_API_Example.Shared.Infrastructure.Persistence;
using IMAS_API_Example.Shared.Models;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace IMAS_API_Example.Features.Example
{
    public static class GetAllExample
    {
        public class Query : IRequest<DataGridResponse<Response>>
        {
            public DataGridRequest Request { get; set; } = new();
        }

        public class Response
        {
            public Guid Id { get; set; }
            public string Title { get; set; } = string.Empty;
            public string Description { get; set; } = string.Empty;
            public int Year { get; set; }
        }

        public class Handler : IRequestHandler<Query, DataGridResponse<Response>>
        {
            private readonly ExampleDbContext _context;

            public Handler(ExampleDbContext context)
            {
                _context = context;
            }

            public async Task<DataGridResponse<Response>> Handle(Query request, CancellationToken cancellationToken)
            {
                var query = _context.ExampleEntities
                    .AsQueryable()
                    .ApplySearch(request.Request.SearchTerm, "Title", "Description")
                    .ApplySort(request.Request.SortBy, request.Request.SortDescending);

                // Apply additional filters if provided
                if (request.Request.Filters?.Any() == true)
                {
                    foreach (var filter in request.Request.Filters)
                    {
                        switch (filter.Key.ToLower())
                        {
                            case "year":
                                if (int.TryParse(filter.Value, out int year))
                                {
                                    query = query.Where(x => x.Year == year);
                                }
                                break;
                            case "yearfrom":
                                if (int.TryParse(filter.Value, out int yearFrom))
                                {
                                    query = query.Where(x => x.Year >= yearFrom);
                                }
                                break;
                            case "yearto":
                                if (int.TryParse(filter.Value, out int yearTo))
                                {
                                    query = query.Where(x => x.Year <= yearTo);
                                }
                                break;
                        }
                    }
                }

                var mappedQuery = query.Select(x => new Response
                {
                    Id = x.Id,
                    Title = x.Title,
                    Description = x.Description,
                    Year = x.Year,
                });

                return await mappedQuery.ToDataGridResponseAsync(request.Request);
            }
        }
    }
}