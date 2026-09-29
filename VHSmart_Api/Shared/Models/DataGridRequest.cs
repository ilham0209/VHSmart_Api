namespace IMAS_API_Example.Shared.Models
{
    public class DataGridRequest
    {
        public int Page { get; set; } = 1;
        public int PageSize { get; set; } = 10;
        public string? SearchTerm { get; set; }
        public string? SortBy { get; set; }
        public bool SortDescending { get; set; } = false;
        public Dictionary<string, string>? Filters { get; set; }
    }
}