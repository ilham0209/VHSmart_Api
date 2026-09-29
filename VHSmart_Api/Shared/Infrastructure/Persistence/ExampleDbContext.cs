using IMAS_API_Example.Shared.Domain.Example;
using Microsoft.EntityFrameworkCore;

namespace IMAS_API_Example.Shared.Infrastructure.Persistence
{
    public class ExampleDbContext : DbContext
    {
        public ExampleDbContext(DbContextOptions<ExampleDbContext> options) : base(options)
        {
        }
        public DbSet<ExampleEntities> ExampleEntities { get; set; } 
        
    }
}
