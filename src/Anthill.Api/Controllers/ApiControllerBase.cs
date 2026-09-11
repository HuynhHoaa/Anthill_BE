using Anthill.Application.Common;
using Microsoft.AspNetCore.Mvc;

namespace Anthill.Api.Controllers;

/// <summary>
/// Lớp nền của mọi controller: một chỗ duy nhất dịch <see cref="Result"/> sang HTTP.
/// </summary>
/// <remarks>
/// Tầng Application cố ý không biết gì về HTTP — nó chỉ phân loại lỗi bằng <see cref="ErrorCode"/>.
/// Phép dịch nằm gọn ở đây nên khi thêm controller mới, không ai phải nhớ lại "trùng email thì trả
/// 409 hay 400".
/// </remarks>
[ApiController]
[Produces("application/json")]
public abstract class ApiControllerBase : ControllerBase
{
    /// <summary>Thành công thì trả thẳng payload (frontend không bóc lớp vỏ nào), thất bại thì trả <c>{ message }</c>.</summary>
    protected IActionResult FromResult<TValue>(Result<TValue> result)
    {
        ArgumentNullException.ThrowIfNull(result);

        return result.IsSuccess
            ? Ok(result.Value)
            : FromFailure(result);
    }

    /// <summary>
    /// Cho thao tác không có dữ liệu trả về. Dùng 200 kèm một câu thay vì 204 để frontend có sẵn
    /// thông điệp hiện lên toast mà không phải tự chế.
    /// </summary>
    protected IActionResult FromResult(Result result, string successMessage)
    {
        ArgumentNullException.ThrowIfNull(result);

        return result.IsSuccess
            ? Ok(ApiMessageResponse.Of(successMessage))
            : FromFailure(result);
    }

    protected IActionResult FromFailure(Result result)
    {
        ArgumentNullException.ThrowIfNull(result);

        return StatusCode(ToStatusCode(result.ErrorCode), ApiErrorResponse.From(result));
    }

    /// <summary>
    /// Bảng dịch <see cref="ErrorCode"/> sang mã HTTP.
    /// </summary>
    /// <remarks>
    /// Vài lựa chọn có lý do rõ ràng, không phải quy ước tuỳ tiện:
    /// <list type="bullet">
    /// <item><b>404 thay vì 403</b> khi bản ghi tồn tại nhưng không thuộc người gọi — trả 403 là
    /// xác nhận id đó có thật, đủ để dò dần toàn bộ dữ liệu người khác (§4.2).</item>
    /// <item><b>410</b> cho cơ hội đã đóng vĩnh viễn, ví dụ lưu nháp sau khi hết giờ làm bài (§5).</item>
    /// <item><b>422</b> khi cú pháp đúng nhưng dữ liệu vô nghĩa, ví dụ answerId không thuộc
    /// questionId — khác hẳn 400 là "gửi sai hình dạng" (§5).</item>
    /// </list>
    /// </remarks>
    public static int ToStatusCode(ErrorCode errorCode) => errorCode switch
    {
        ErrorCode.None => StatusCodes.Status200OK,
        ErrorCode.Validation => StatusCodes.Status400BadRequest,
        ErrorCode.Unauthorized => StatusCodes.Status401Unauthorized,
        ErrorCode.Forbidden => StatusCodes.Status403Forbidden,
        ErrorCode.NotFound => StatusCodes.Status404NotFound,
        ErrorCode.Conflict => StatusCodes.Status409Conflict,
        ErrorCode.Gone => StatusCodes.Status410Gone,
        ErrorCode.Unprocessable => StatusCodes.Status422UnprocessableEntity,
        ErrorCode.Internal => StatusCodes.Status500InternalServerError,

        // Giá trị lạ nghĩa là có ErrorCode mới mà bảng này chưa cập nhật: 500 là câu trả lời trung
        // thực, còn hơn im lặng trả 200 cho một thất bại.
        _ => StatusCodes.Status500InternalServerError
    };
}
