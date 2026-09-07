namespace QuizApp.Api.Contracts.V1.Common;

/// <summary>Standard paged envelope per 05-API-Design.md §5.2.</summary>
public sealed record PagedResult<T>(IReadOnlyList<T> Items, int Page, int PageSize, int TotalCount)
{
    public int TotalPages => PageSize <= 0 ? 0 : (int)Math.Ceiling(TotalCount / (double)PageSize);
}

/// <summary>Common paging query parameters, bound from the query string.</summary>
public sealed record PagingQuery(int Page = 1, int PageSize = 50, string? Sort = null);
