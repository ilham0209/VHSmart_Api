using IMAS_API_Example.Features.Example;
using IMAS_API_Example.Shared.Extensions;
using IMAS_API_Example.Shared.Infrastructure.Persistence;
using IMAS_API_Example.Shared.Models;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace IMAS_API_Example.Features.Example
{
    public static class GetByIdExample
    {
        public record Query(Guid Id) : IRequest<Response?>;

        public record Response(Guid Id, string Title, string Description, int Year);

        public class Handler(ExampleDbContext context) : IRequestHandler<Query, Response?>
        {
            public async Task<Response?> Handle(Query request, CancellationToken cancellationToken)
            {
                var entities = await context.ExampleEntities.ToListAsync(cancellationToken);
                var entity = entities.FirstOrDefault(e => e.Id == request.Id);
                if (entity == null)
                {
                    return null; // or throw an exception
                }
                return new Response(entity.Id, entity.Title, entity.Description, entity.Year);  //entity .Select(e => new Response(e.Id, e.Title, e.Description, e.Year));
            }
        }
    }
}
