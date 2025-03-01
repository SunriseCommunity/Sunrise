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
    
    // TODO: Extend from entity, and add default sorting if none is provided in query
    public static IQueryable<TEntity> UseQueryOptions<TEntity>(this IQueryable<TEntity> query, QueryOptions? options) where TEntity : class
    {
        if (options == null) return query;
        
        if (options.Pagination != null) query = query.PaginationTake(options.Pagination);
        if (options.AsNoTracking) query = query.AsNoTracking();

        return query;
    }
}