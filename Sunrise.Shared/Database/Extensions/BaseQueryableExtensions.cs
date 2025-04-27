using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using Sunrise.Shared.Database.Objects;

namespace Sunrise.Shared.Database.Extensions;

public static class QueryableExtensions
{

    public static IQueryable<TEntity> PaginationTake<TEntity>(this IQueryable<TEntity> query, Pagination pagination) where TEntity : class
    {
        return query
            .Skip((pagination.Page - 1) * pagination.PageSize)
            .Take(pagination.PageSize);
    }

    public static IQueryable<TEntity> UseQueryOptions<TEntity>(this IQueryable<TEntity> query, QueryOptions? options) where TEntity : class
    {
        if (options == null) return query;

        if (options.Pagination != null)
        {
            if (!IsOrdered(query))
                query = query.OrderById();

            query = query.PaginationTake(options.Pagination);
        }

        if (options.QueryModifier != null)
            query = (IQueryable<TEntity>)options.QueryModifier(query);

        if (options.AsNoTracking) query = query.AsNoTracking();

        return query;
    }

    public static bool IsOrdered<T>(this IQueryable<T> queryable)
    {
        ArgumentNullException.ThrowIfNull(queryable);

        return queryable.Expression.Type == typeof(IOrderedQueryable<T>) || HasOrderingOperation(queryable.Expression);
    }

    private static bool HasOrderingOperation(Expression expression)
    {
        if (expression is MethodCallExpression methodCall)
        {
            if (methodCall.Method.Name.StartsWith("OrderBy") || methodCall.Method.Name.StartsWith("ThenBy"))
                return true;

            return methodCall.Arguments.Any(HasOrderingOperation);
        }

        return false;
    }

    // FIXME: We *should* have Id for all Models, but we don't have any base class we can rely on to use it in OrderBy.
    public static IQueryable<TEntity> OrderById<TEntity>(this IQueryable<TEntity> source) where TEntity : class
    {
        var idProperty = typeof(TEntity).GetProperty("Id");

        if (idProperty == null)
        {
            return source;
        }

        var parameter = Expression.Parameter(typeof(TEntity), "x");
        var property = Expression.Property(parameter, idProperty);
        var lambda = Expression.Lambda(property, parameter);

        var method = typeof(Queryable).GetMethods()
            .First(m => m.Name == "OrderBy"
                        && m.GetParameters().Length == 2)
            .MakeGenericMethod(typeof(TEntity), property.Type);

        return (IQueryable<TEntity>)method.Invoke(null,
            new object[]
            {
                source,
                lambda
            });
    }
}