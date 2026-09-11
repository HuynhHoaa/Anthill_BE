namespace Anthill.Application.Common;

/// <summary>
/// Tham số phân trang, tự kẹp ngay khi model binder gán giá trị. §5 yêu cầu <b>kẹp</b> chứ
/// không báo lỗi: <c>page &lt; 1</c> về 1 · <c>pageSize &lt;= 0</c> về mặc định · chặn trên 200.
/// </summary>
/// <remarks>
/// Trần 200 tồn tại vì năm chỗ ở frontend đang truyền <c>pageSize = 500</c> để kéo trọn tập
/// dữ liệu về rồi tự tính thống kê (§1). Những chỗ đó phải chuyển sang endpoint thống kê riêng.
/// </remarks>
public class PaginationParams
{
    public const int DefaultPage = 1;
    public const int DefaultPageSize = 10;
    public const int MaxPageSize = 200;

    private int _page = DefaultPage;
    private int _pageSize = DefaultPageSize;

    public int Page
    {
        get => _page;
        set => _page = value < 1 ? DefaultPage : value;
    }

    public int PageSize
    {
        get => _pageSize;
        set => _pageSize = value switch
        {
            <= 0 => DefaultPageSize,
            > MaxPageSize => MaxPageSize,
            _ => value
        };
    }

    /// <summary>
    /// Số trang cuối cùng còn dữ liệu. §5: <c>page</c> vượt tổng số trang thì trả trang cuối
    /// thay vì trả một trang rỗng — người dùng xoá bớt bản ghi rồi F5 vẫn thấy dữ liệu.
    /// </summary>
    public int ResolvePage(int totalItems)
    {
        if (totalItems <= 0) return DefaultPage;
        var lastPage = (int)Math.Ceiling(totalItems / (double)PageSize);
        return Page > lastPage ? lastPage : Page;
    }

    /// <summary>Số bản ghi cần bỏ qua, tính theo trang <b>đã kẹp</b> với tổng số bản ghi.</summary>
    public int SkipFor(int totalItems) => (ResolvePage(totalItems) - 1) * PageSize;

    public static PaginationParams Of(int? page, int? pageSize) => new()
    {
        Page = page ?? DefaultPage,
        PageSize = pageSize ?? DefaultPageSize
    };
}
