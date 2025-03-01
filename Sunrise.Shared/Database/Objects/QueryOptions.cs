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
}