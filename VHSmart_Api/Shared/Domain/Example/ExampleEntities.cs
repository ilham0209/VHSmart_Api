using IMAS_API_Example.Shared.Domain;

namespace IMAS_API_Example.Shared.Domain.Example
{
    public class ExampleEntities : BaseClass
    {
        public Guid Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public int Year { get; set; }

    }
}
