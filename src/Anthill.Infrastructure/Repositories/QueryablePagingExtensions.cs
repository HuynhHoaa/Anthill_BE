using Anthill.Application.Common;
using Microsoft.EntityFrameworkCore;

namespace Anthill.Infrastructure.Repositories;

/// <summary>
/// Một chỗ duy nhất thực hiện quy tắc phân trang của §5: kẹp thay vì báo lỗi, và trả về
/// <b>số trang thực tế đã dùng</b> để frontend đồng bộ ngược lên URL.
/// </summary>
internal static class QueryablePagingExtensions
{
    public static async Task<PagedResult<T>> ToPagedResultAsync<T>(
        this IQueryable<T> query,
        PaginationParams pagination,
        CancellationToken cancellationToken = default)
    {
        var total = await query.CountAsync(cancellationToken);
        if (total == 0)
        {
            return PagedResult<T>.Empty(PaginationParams.DefaultPage, pagination.PageSize);
        }

        // Xoá bớt bản ghi rồi F5 ở trang cuối: kẹp về trang cuối còn dữ liệu thay vì trả trang rỗng.
        var page = pagination.ResolvePage(total);

        var items = await query
            .Skip((page - 1) * pagination.PageSize)
            .Take(pagination.PageSize)
            .ToListAsync(cancellationToken);

        return PagedResult<T>.Create(items, total, page, pagination.PageSize);
    }
}
