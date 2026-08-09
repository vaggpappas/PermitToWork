using System.ComponentModel.DataAnnotations;

namespace PermitToWork.Application.Common;

/// <summary>
/// One page of results plus enough context for a client to draw a pager.
/// </summary>
public sealed record PagedResult<T>(IReadOnlyList<T> Items, int Page, int PageSize, int TotalCount)
{
    public int TotalPages => PageSize <= 0 ? 0 : (int)Math.Ceiling(TotalCount / (double)PageSize);

    public bool HasPreviousPage => Page > 1;

    public bool HasNextPage => Page < TotalPages;

    public static PagedResult<T> Empty(int pageSize) => new([], 1, pageSize, 0);
}

/// <summary>
/// Which page the caller wants.
/// <para>
/// Both values are clamped on the way in rather than validated and rejected. Page 0 and
/// page −5 both mean the first page; a request for ten thousand rows quietly becomes a
/// hundred. There is no "invalid page request" error to handle anywhere downstream,
/// because there is no way to express one.
/// </para>
/// </summary>
public record PageRequest
{
    public const int MaxPageSize = 100;

    private readonly int _page = 1;
    private readonly int _pageSize = 20;

    [Range(1, int.MaxValue)]
    public int Page
    {
        get => _page;
        init => _page = value < 1 ? 1 : value;
    }

    [Range(1, MaxPageSize)]
    public int PageSize
    {
        get => _pageSize;
        init => _pageSize = Math.Clamp(value, 1, MaxPageSize);
    }

    public int Skip => (Page - 1) * PageSize;
}
