namespace IMAS_API_Example.Shared.Domain.Car
{
    public class CarEntities
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Brand { get; set; } = string.Empty;
        public string Colour { get; set; } = string.Empty;
        public int Year { get; set; }
    }
}
