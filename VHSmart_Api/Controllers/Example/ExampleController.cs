using IMAS_API_Example.Features.Example;
using IMAS_API_Example.Shared.Models;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace IMAS_API_Example.Controllers.Example
{
    [ApiController]
    [Route("api/[Controller]")]
    public class ExampleController(ISender sender) : ControllerBase
    {
        [HttpGet]
        public async Task<ActionResult<DataGridResponse<GetAllExample.Response>>> GetAll([FromQuery] DataGridRequest request)
        {
            var result = await sender.Send(new GetAllExample.Query { Request = request });
            return Ok(result);
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<GetByIdExample.Response>> GetById(Guid id)
        {
            var result = await sender.Send(new GetByIdExample.Query(id));
            if (result == null)
            {
                return NotFound($"Entity with ID {id} not found.");
            }
            return Ok(result);
        }

        [HttpPost]
        public async Task<ActionResult<CreateExample.Response>> Create(CreateExample.Command command)
        {
            var result = await sender.Send(command);
            if (result == null)
            {
                return BadRequest("Invalid data.");
            }
            if (result.Id == Guid.Empty)
            {
                return BadRequest("ID cannot be empty.");
            }
            //return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);

            return Created($"/api/example{result.Id}", result);
        }

        [HttpPut("{id}")]
        public async Task<ActionResult<UpdateExample.Response>> Update(Guid id, UpdateExample.Command command)
        {
            if (id != command.Id)
            {
                return BadRequest("ID in the URL and ID in the body do not match.");
            }
            var result = await sender.Send(command);
            if (result == null)
            {
                return NotFound($"Entity with ID {id} not found.");
            }
            return Ok(result);
        }

        [HttpDelete("{id}")]
        public async Task<ActionResult<bool>> Delete(Guid id)
        {
            var result = await sender.Send(new DeleteExample.Command(id));
            if (!result)
            {
                return NotFound($"Entity with ID {id} not found.");
            }
            return NoContent();
            //return AcceptedResult($"Entity with ID {id} deleted successfully.");
        }
    }
}