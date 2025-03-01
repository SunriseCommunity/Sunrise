namespace Sunrise.Shared.Database.Objects;

public class Pagination(int page, int pageSize)
{
    public int Page { get; set; } = page;
    public int PageSize { get; set; } = pageSize;
}