using FluentValidation;
using IMAS_API_Example.Shared.Domain.Example;
using IMAS_API_Example.Shared.Infrastructure.Persistence;
using MediatR;

namespace IMAS_API_Example.Features.Example
{
    public static class CreateExample
    {
        public record Command(string Title, string Description, int Year) : IRequest<Response?>;
        public record Response(Guid Id, string Title, string Description, int Year);

        public class Validator : AbstractValidator<Command>
        {
            public Validator()
            {
                RuleFor(x => x.Title).NotEmpty().Length(2, 100).WithMessage("Title is required");
                RuleFor(x => x.Description).NotEmpty().WithMessage("Description is required");
                RuleFor(x => x.Year).GreaterThan(2010).WithMessage("Year must be greater than 2010");
            }
        }
        public class Handler(ExampleDbContext context ) : IRequestHandler<Command, Response?>
        {
            private readonly IValidator<Command> validator = new Validator();
            public async Task<Response?> Handle(Command request, CancellationToken cancellationToken)
            {
                var validationResult = await validator.ValidateAsync(request, cancellationToken);
                if (!validationResult.IsValid)
                {
                    // Handle validation errors
                    // You can throw an exception or return null
                    // For example:
                    //throw new ValidationException(validationResult.Errors);
                    // or return null; // or throw an exception
                    //return null; // or throw an exception
                    return null;
                }

                var entity = new ExampleEntities 
                {
                    Id = Guid.NewGuid(),
                    Title = request.Title,
                    Description = request.Description,
                    Year = request.Year,
                };
                entity.SetCreated("Ilham");
                context.ExampleEntities.Add(entity);
                await context.SaveChangesAsync(cancellationToken);
                return new Response(entity.Id, entity.Title, entity.Description, entity.Year);
            }
        }
    }

    //[ApiController]
    //[Route("api/Example")]
    //public class CreateExampleController(ISender sender) : ControllerBase
    //{
    //    [HttpPost]
    //    public async Task<ActionResult<CreateExample.Response>> Create(CreateExample.Command command)
    //    {
    //        var result = await sender.Send(command);
    //        return Created($"/api/example{result.Id}",result);
    //    }

    //}
}
