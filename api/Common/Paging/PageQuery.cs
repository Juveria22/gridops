using System.ComponentModel.DataAnnotations;

namespace GridOps.Api.Common.Paging;

public enum SortDirection
{
    Asc,
    Desc
}

public class PageQuery
{
    [Range(1, int.MaxValue)]
    public int Page { get; init; } = 1;

    // capped so nobody pulls the whole table in one call
    [Range(1, 100)]
    public int PageSize { get; init; } = 25;

    public SortDirection SortDir { get; init; } = SortDirection.Desc;

    public int Skip => (Page - 1) * PageSize;
}
