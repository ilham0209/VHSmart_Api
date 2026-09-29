using IMAS_API_Example.Shared.Infrastructure.Persistence;
using MediatR;

namespace IMAS_API_Example.Features.Example
{
    public static class DeleteExample
    {
        public record Command(Guid Id) : IRequest<bool>;

        public class Handler(ExampleDbContext context) : IRequestHandler<Command, bool>
        {
            public async Task<bool> Handle(Command request, CancellationToken cancellationToken)
            {
                var entity = await context.ExampleEntities.FindAsync(request.Id);
                if (entity == null)
                {
                    return false; // or throw an exception
                }
                // Set the entity as deleted
                entity.SetModified("Ilham");
                entity.SetDeleted();
                //context.ExampleEntities.Remove(entity);
                await context.SaveChangesAsync(cancellationToken);
                return true;
            }
        }
    }

    //[ApiController]
    //[Route("api/Example")]
    //public class DeleteExampleController(ISender sender) : ControllerBase
    //{
    //    [HttpDelete("{id}")]
    //    public async Task<ActionResult<bool>> Delete(Guid id)
    //    {
    //        var result = await sender.Send(new DeleteExample.Command(id));
    //        if (!result)
    //        {
    //            return NotFound($"Entity with ID {id} not found.");
    //        }
    //        return NoContent();
    //    }

    //}
}
