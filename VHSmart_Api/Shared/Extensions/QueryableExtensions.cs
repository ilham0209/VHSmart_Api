using System.Linq.Expressions;
using System.Reflection;
using Microsoft.EntityFrameworkCore;
using VHSmart_Api.Shared.Domain;
using VHSmart_Api.Shared.Models;

namespace VHSmart_Api.Shared.Extensions;

// List plumbing for every "Show N entries" table (spec 1.1): Search box, sortable column
// headers and paging. Handlers call these instead of writing Skip/Take by hand (CodingRules 4).
public static class QueryableExtensions
{
    private static readonly MethodInfo ToLowerMethod = typeof(string).GetMethod(nameof(string.ToLower), Type.EmptyTypes)
        ?? throw new InvalidOperationException("string.ToLower() was not found.");
    private static readonly MethodInfo ContainsMethod = typeof(string).GetMethod(nameof(string.Contains), [typeof(string)])
        ?? throw new InvalidOperationException("string.Contains(string) was not found.");

    public static IQueryable<T> ApplySearch<T>(this IQueryable<T> query, string? searchTerm, params string[] searchProperties)
    {
        if (string.IsNullOrWhiteSpace(searchTerm) || searchProperties.Length == 0)
            return query;

        var parameter = Expression.Parameter(typeof(T), "entity");
        var matches = new List<Expression>();

        foreach (var propertyName in searchProperties)
        {
            var property = Property(typeof(T), propertyName, nameof(searchProperties));
            if (property.PropertyType != typeof(string))
                throw new ArgumentException(
                    $"{typeof(T).Name}.{propertyName} is not a string column and cannot be searched.",
                    nameof(searchProperties));

            // COALESCE keeps a nullable column searchable: in memory null.ToLower() would throw.
            var column = Expression.Coalesce(
                Expression.Property(parameter, property),
                Expression.Constant(string.Empty, typeof(string)));
            var lowered = Expression.Call(column, ToLowerMethod);
            matches.Add(Expression.Call(lowered, ContainsMethod, Expression.Constant(searchTerm.ToLowerInvariant())));
        }

        var body = matches.Aggregate(Expression.OrElse);
        return query.Where(Expression.Lambda<Func<T, bool>>(body, parameter));
    }

    public static IQueryable<T> ApplySort<T>(this IQueryable<T> query, string? sortBy, bool sortDescending)
    {
        if (string.IsNullOrWhiteSpace(sortBy))
            return query;

        var property = Property(typeof(T), sortBy, nameof(sortBy));
        var parameter = Expression.Parameter(typeof(T), "entity");
        var key = Expression.Property(parameter, property);
        var keySelector = Expression.Lambda(key, parameter);
        var methodName = sortDescending ? nameof(Queryable.OrderByDescending) : nameof(Queryable.OrderBy);

        var ordered = query.Provider.CreateQuery<T>(
            Expression.Call(typeof(Queryable), methodName, [typeof(T), key.Type], query.Expression, Expression.Quote(keySelector)));

        // A sorted column can hold the same value on many rows; Id breaks the tie so a row can
        // never appear on two pages or fall between them.
        return ordered.OrderById(nameof(Queryable.ThenBy));
    }

    public static async Task<DataGridResponse<T>> ToDataGridResponseAsync<T>(
        this IQueryable<T> query,
        DataGridRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        // The page size comes from the client: clamp it so one request cannot pull the whole table.
        var pageSize = request.PageSize <= 0
            ? DataGridRequest.DefaultPageSize
            : Math.Min(request.PageSize, DataGridRequest.MaxPageSize);
        var page = request.Page <= 0 ? 1 : request.Page;

        query = query.EnsureOrder();

        var totalRecords = await query.CountAsync(cancellationToken);
        var totalPages = (int)Math.Ceiling(totalRecords / (double)pageSize);
        var offset = Math.Min((page - 1) * (long)pageSize, int.MaxValue);

        var data = await query
            .Skip((int)offset)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return new DataGridResponse<T>
        {
            Data = data,
            TotalRecords = totalRecords,
            TotalPages = totalPages,
            CurrentPage = page,
            PageSize = pageSize,
            HasNextPage = page < totalPages,
            HasPreviousPage = page > 1
        };
    }

    private static PropertyInfo Property(Type type, string propertyName, string argumentName) =>
        type.GetProperty(propertyName, BindingFlags.Public | BindingFlags.Instance)
        ?? throw new ArgumentException($"{type.Name} has no property '{propertyName}'.", argumentName);

    private static PropertyInfo? IdProperty(Type type) =>
        type.GetProperty(nameof(BaseClass.Id), BindingFlags.Public | BindingFlags.Instance);

    // Paging needs one stable order: without it the database may return rows in a different
    // order per query and pages overlap. Id is unique on every table (Database.md section 1).
    private static IQueryable<T> EnsureOrder<T>(this IQueryable<T> query) =>
        HasOrdering(query.Expression) ? query : query.OrderById(nameof(Queryable.OrderBy));

    private static IQueryable<T> OrderById<T>(this IQueryable<T> query, string methodName)
    {
        var id = IdProperty(typeof(T));
        if (id is null)
            return query;

        var parameter = Expression.Parameter(typeof(T), "entity");
        var keySelector = Expression.Lambda(Expression.Property(parameter, id), parameter);

        return query.Provider.CreateQuery<T>(
            Expression.Call(typeof(Queryable), methodName, [typeof(T), id.PropertyType], query.Expression, Expression.Quote(keySelector)));
    }

    // Walks the query pipeline (source argument only) looking for an OrderBy/ThenBy.
    private static bool HasOrdering(Expression expression) =>
        expression is MethodCallExpression call
        && ((call.Method.DeclaringType == typeof(Queryable)
                && (call.Method.Name.StartsWith("OrderBy", StringComparison.Ordinal)
                    || call.Method.Name.StartsWith("ThenBy", StringComparison.Ordinal)))
            || (call.Arguments.Count > 0 && HasOrdering(call.Arguments[0])));
}
