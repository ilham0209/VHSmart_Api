using IMAS_API_Example.Shared.Infrastructure.Persistence;
using MediatR;

namespace IMAS_API_Example.Features.Example
{
    public static class UpdateExample
    {
        public record Command(Guid Id, string Title, string Description, int Year) : IRequest<Response?>;
        public record Response(Guid Id, string Title, string Description, int Year);
        public class Handler(ExampleDbContext context) : IRequestHandler<Command, Response?>
        {
            public async Task<Response?> Handle(Command request, CancellationToken cancellationToken)
            {
                var entity = await context.ExampleEntities.FindAsync(request.Id);
                if (entity == null)
                {
                    return null; // or throw an exception
                }
                entity.Title = request.Title;
                entity.Description = request.Description;
                entity.Year = request.Year;
                await context.SaveChangesAsync(cancellationToken);
                return new Response(entity.Id, entity.Title, entity.Description, entity.Year);
            }
        }
    }

    //[ApiController]
    //[Route("api/Example")]
    //public class UpdateExampleController(ISender sender) : ControllerBase
    //{
    //    [HttpPut("{id}")]
    //    public async Task<ActionResult<UpdateExample.Response>> Update(Guid id, UpdateExample.Command command)
    //    {
    //        if (id != command.Id)
    //        {
    //            return BadRequest("ID in the URL and ID in the body do not match.");
    //        }
    //        var result = await sender.Send(command);
    //        if (result == null)
    //        {
    //            return NotFound($"Entity with ID {id} not found.");
    //        }
    //        return Ok(result);
    //    }

    //}
}
