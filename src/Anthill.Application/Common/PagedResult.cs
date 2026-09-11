using System.Text.Json.Serialization;

namespace Anthill.Application.Common;

/// <summary>
/// Một trang dữ liệu. Bốn trường được serialize đúng bằng <c>{ items, total, page, pageSize }</c>
/// vì frontend khai báo <c>Paged&lt;T&gt;</c> đúng như vậy, và đọc ngược trường <c>page</c>
/// để đồng bộ lên URL (§1, §5). Thêm hay đổi tên trường ở đây là làm hỏng FE.
/// </summary>
public sealed record PagedResult<T>
{
    public required IReadOnlyList<T> Items { get; init; }

    /// <summary>Tổng số bản ghi <b>sau khi lọc</b>, không phải số bản ghi trong trang.</summary>
    public required int Total { get; init; }

    /// <summary>Số trang thực tế đã dùng sau khi kẹp — không phải số client gửi lên.</summary>
    public required int Page { get; init; }

    public required int PageSize { get; init; }

    /// <summary>Suy ra, không serialize để giữ đúng hình dạng frontend đang đọc.</summary>
    [JsonIgnore]
    public int TotalPages => PageSize <= 0 ? 0 : (int)Math.Ceiling(Total / (double)PageSize);

    public static PagedResult<T> Create(IReadOnlyList<T> items, int total, int page, int pageSize) => new()
    {
        Items = items,
        Total = total,
        Page = page,
        PageSize = pageSize
    };

    public static PagedResult<T> Empty(int page, int pageSize) =>
        Create(Array.Empty<T>(), 0, page, pageSize);

    /// <summary>Đổi kiểu phần tử mà giữ nguyên thông tin phân trang — dùng khi map entity sang DTO.</summary>
    public PagedResult<TOut> Map<TOut>(Func<T, TOut> selector) =>
        PagedResult<TOut>.Create(Items.Select(selector).ToList(), Total, Page, PageSize);
}
