namespace VHSmart_Api.Shared.Models;

// Answer of a "Show N entries" table: the page plus everything the footer shows
// ("Showing x to y of z entries", Previous / Next — spec 1.1).
public class DataGridResponse<T>
{
    public IEnumerable<T> Data { get; set; } = [];
    public int TotalRecords { get; set; }
    public int TotalPages { get; set; }
    public int CurrentPage { get; set; }
    public int PageSize { get; set; }
    public bool HasNextPage { get; set; }
    public bool HasPreviousPage { get; set; }
}
