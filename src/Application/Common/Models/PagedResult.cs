using Microsoft.EntityFrameworkCore;

namespace ShiftLedger.Application.Common.Models;

// The paging envelope every list endpoint returns (docs/04 §1): { items, page, pageSize, totalCount }.
public record PagedResult<T>(IReadOnlyList<T> Items, int Page, int PageSize, int TotalCount);

public static class Paging
{
    public const int DefaultPageSize = 20;
    public const int MaxPageSize = 100;

    // Clamps page/pageSize to sane bounds, counts, then fetches one page. Call on an already
    // filtered + ordered query.
    public static async Task<PagedResult<T>> ToPagedResultAsync<T>(
        this IQueryable<T> query, int? page, int? pageSize, CancellationToken cancellationToken)
    {
        var safePage = Math.Max(page ?? 1, 1);
        var safeSize = Math.Clamp(pageSize ?? DefaultPageSize, 1, MaxPageSize);

        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query.Skip((safePage - 1) * safeSize).Take(safeSize).ToListAsync(cancellationToken);

        return new PagedResult<T>(items, safePage, safeSize, totalCount);
    }
}
