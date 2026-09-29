namespace VHSmart_Api.Shared.Models;

// One "Show N entries" table request (spec 1.1): paging, the Search box and the column sort.
public class DataGridRequest
{
    public const int DefaultPageSize = 10;
    public const int MaxPageSize = 100;

    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = DefaultPageSize;
    public string? SearchTerm { get; set; }
    public string? SortBy { get; set; }
    public bool SortDescending { get; set; }
    public Dictionary<string, string>? Filters { get; set; }
}
