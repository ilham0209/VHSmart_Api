using IMAS_API_Example.Shared.Models;
using Microsoft.EntityFrameworkCore;
using System.Linq.Expressions;
using System.Reflection;

namespace IMAS_API_Example.Shared.Extensions
{
    public static class QueryableExtensions
    {
        public static IQueryable<T> ApplySearch<T>(this IQueryable<T> query, string? searchTerm, params string[] searchProperties)
        {
            if (string.IsNullOrWhiteSpace(searchTerm) || !searchProperties.Any())
                return query;

            var parameter = Expression.Parameter(typeof(T), "x");
            Expression? combinedExpression = null;

            foreach (var property in searchProperties)
            {
                var propertyAccess = Expression.Property(parameter, property);
                var toLowerMethod = typeof(string).GetMethod("ToLower", Type.EmptyTypes);
                var containsMethod = typeof(string).GetMethod("Contains", new[] { typeof(string) });

                if (toLowerMethod != null && containsMethod != null)
                {
                    var propertyToLower = Expression.Call(propertyAccess, toLowerMethod);
                    var searchTermLower = Expression.Constant(searchTerm.ToLower());
                    var containsExpression = Expression.Call(propertyToLower, containsMethod, searchTermLower);

                    combinedExpression = combinedExpression == null
                        ? containsExpression
                        : Expression.OrElse(combinedExpression, containsExpression);
                }
            }

            if (combinedExpression != null)
            {
                var lambda = Expression.Lambda<Func<T, bool>>(combinedExpression, parameter);
                query = query.Where(lambda);
            }

            return query;
        }

        public static IQueryable<T> ApplySort<T>(this IQueryable<T> query, string? sortBy, bool sortDescending)
        {
            if (string.IsNullOrWhiteSpace(sortBy))
                return query;

            var parameter = Expression.Parameter(typeof(T), "x");
            var property = Expression.Property(parameter, sortBy);
            var lambda = Expression.Lambda(property, parameter);

            var methodName = sortDescending ? "OrderByDescending" : "OrderBy";
            var resultExpression = Expression.Call(
                typeof(Queryable),
                methodName,
                new Type[] { typeof(T), property.Type },
                query.Expression,
                Expression.Quote(lambda)
            );

            return query.Provider.CreateQuery<T>(resultExpression);
        }

        public static async Task<DataGridResponse<T>> ToDataGridResponseAsync<T>(
            this IQueryable<T> query,
            DataGridRequest request)
        {
            var totalRecords = await query.CountAsync();
            var totalPages = (int)Math.Ceiling((double)totalRecords / request.PageSize);

            var data = await query
                .Skip((request.Page - 1) * request.PageSize)
                .Take(request.PageSize)
                .ToListAsync();

            return new DataGridResponse<T>
            {
                Data = data,
                TotalRecords = totalRecords,
                TotalPages = totalPages,
                CurrentPage = request.Page,
                PageSize = request.PageSize,
                HasNextPage = request.Page < totalPages,
                HasPreviousPage = request.Page > 1
            };
        }
    }
}