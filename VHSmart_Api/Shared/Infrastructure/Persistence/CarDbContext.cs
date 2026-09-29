using IMAS_API_Example.Shared.Domain.Car;
using Microsoft.EntityFrameworkCore;

namespace IMAS_API_Example.Shared.Infrastructure.Persistence
{
    public class CarDbContext : DbContext
    {
        public CarDbContext(DbContextOptions<CarDbContext> options) : base(options)
        {
        }
        public DbSet<CarEntities> CarEntities { get; set; }
       
    }
    
}
