namespace Sunrise.Shared.Database.Objects;

public class QueryOptions(bool asNoTracking = false, Pagination? pagination = null)
{
    public QueryOptions(Pagination? pagination = null) : this(false, pagination)
    {
    }

    public QueryOptions() : this(false)
    {
    }

    public bool AsNoTracking { get; set; } = asNoTracking;
    public Pagination? Pagination { get; set; } = pagination;

    /// <summary>
    ///     Allows adding includes or additional filters to the query.
    ///     Should not modify the core query conditions, to ensure operations like Count are not affected,
    ///     as Count is declared before query options are processed.
    /// </summary>
    public Func<IQueryable<object>, IQueryable<object>>? QueryModifier { get; set; }

    public bool IgnoreCountQueryIfExists { get; set; } = false;
}