namespace Anthill.Application.Common;

/// <summary>
/// Một lần ghi vừa vi phạm ràng buộc duy nhất của database — hai request cùng chen vào tạo một bản ghi
/// mà chỉ được phép tồn tại một lần (ví dụ unique index trên <c>ExamAttempts.AssignmentId</c>).
/// </summary>
/// <remarks>
/// <para>
/// Tầng Application cố ý không tham chiếu EF Core hay Npgsql (xem <c>Anthill.Application.csproj</c>),
/// nên không thể tự bắt <c>DbUpdateException</c>/<c>PostgresException</c>. Ngoại lệ này là bản bọc lại,
/// không mang gì của hai gói đó, để một service như <c>ExamAttemptService.CreateAttemptAsync</c> bắt
/// riêng được và tự quyết định lại (ví dụ: đọc lại bản ghi vừa thua rồi trả về đúng nó, thay vì để lỗi
/// rơi xuống <c>ExceptionMiddleware</c> thành một trang 409 chung chung).
/// </para>
/// <para>
/// Nơi duy nhất ném ngoại lệ này là phần cài đặt <c>IUnitOfWork.SaveChangesAsync</c> ở Infrastructure —
/// nó bắt <c>DbUpdateException</c> mang SQLSTATE <c>23505</c> rồi bọc lại. Nơi gọi không bắt riêng thì
/// <c>ExceptionMiddleware</c> vẫn dịch nó thành 409, đúng câu chữ như khi còn là <c>DbUpdateException</c>
/// thô.
/// </para>
/// </remarks>
public sealed class UniqueConstraintViolationException : Exception
{
    public UniqueConstraintViolationException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
