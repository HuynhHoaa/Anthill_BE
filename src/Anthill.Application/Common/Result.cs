namespace Anthill.Application.Common;

/// <summary>
/// Phân loại lỗi nghiệp vụ. Tầng Api dịch giá trị này sang mã HTTP; tầng Application
/// cố ý không biết gì về HTTP.
/// </summary>
public enum ErrorCode
{
    None = 0,

    /// <summary>400 — dữ liệu gửi lên sai định dạng hoặc thiếu.</summary>
    Validation,

    /// <summary>401 — chưa đăng nhập, sai thông tin đăng nhập, token hết hạn.</summary>
    Unauthorized,

    /// <summary>403 — đã đăng nhập nhưng không đủ quyền, kể cả tài khoản bị khoá.</summary>
    Forbidden,

    /// <summary>404 — không tồn tại, hoặc tồn tại nhưng không thuộc người gọi (§4.2: không để dò id).</summary>
    NotFound,

    /// <summary>409 — trùng email/username/mã học sinh, hoặc trạng thái xung đột.</summary>
    Conflict,

    /// <summary>410 — cơ hội đã đóng vĩnh viễn, ví dụ lưu nháp sau khi hết giờ làm bài (§5).</summary>
    Gone,

    /// <summary>422 — cú pháp đúng nhưng dữ liệu vô nghĩa, ví dụ answerId không thuộc questionId.</summary>
    Unprocessable,

    /// <summary>500 — lỗi ngoài dự kiến.</summary>
    Internal
}

/// <summary>
/// Kết quả của một thao tác nghiệp vụ. Lỗi <b>mong đợi</b> (không tìm thấy, không đủ quyền,
/// trùng dữ liệu) đi qua đây thay vì ném exception — exception chỉ dành cho sự cố thật.
/// </summary>
public class Result
{
    protected Result(
        bool isSuccess,
        string? error,
        ErrorCode errorCode,
        IReadOnlyDictionary<string, string[]>? errors)
    {
        IsSuccess = isSuccess;
        Error = error;
        ErrorCode = errorCode;
        Errors = errors;
    }

    public bool IsSuccess { get; }

    public bool IsFailure => !IsSuccess;

    /// <summary>Thông điệp cho người dùng cuối. FE đọc đúng trường này (§1).</summary>
    public string? Error { get; }

    public ErrorCode ErrorCode { get; }

    /// <summary>Lỗi theo từng trường, dành cho form. NULL khi lỗi không gắn với trường nào.</summary>
    public IReadOnlyDictionary<string, string[]>? Errors { get; }

    public static Result Success() => new(true, null, ErrorCode.None, null);

    public static Result<T> Success<T>(T value) => new(value, true, null, ErrorCode.None, null);

    public static Result Failure(
        string error,
        ErrorCode code = ErrorCode.Validation,
        IReadOnlyDictionary<string, string[]>? errors = null) => new(false, error, code, errors);

    public static Result<T> Failure<T>(
        string error,
        ErrorCode code = ErrorCode.Validation,
        IReadOnlyDictionary<string, string[]>? errors = null) => new(default, false, error, code, errors);

    public static Result NotFound(string error) => Failure(error, ErrorCode.NotFound);

    public static Result<T> NotFound<T>(string error) => Failure<T>(error, ErrorCode.NotFound);

    public static Result Forbidden(string error) => Failure(error, ErrorCode.Forbidden);

    public static Result<T> Forbidden<T>(string error) => Failure<T>(error, ErrorCode.Forbidden);

    public static Result Unauthorized(string error) => Failure(error, ErrorCode.Unauthorized);

    public static Result<T> Unauthorized<T>(string error) => Failure<T>(error, ErrorCode.Unauthorized);

    public static Result Conflict(string error) => Failure(error, ErrorCode.Conflict);

    public static Result<T> Conflict<T>(string error) => Failure<T>(error, ErrorCode.Conflict);

    public static Result Gone(string error) => Failure(error, ErrorCode.Gone);

    public static Result<T> Gone<T>(string error) => Failure<T>(error, ErrorCode.Gone);

    public static Result Unprocessable(string error) => Failure(error, ErrorCode.Unprocessable);

    public static Result<T> Unprocessable<T>(string error) => Failure<T>(error, ErrorCode.Unprocessable);
}

/// <summary>Kết quả có mang giá trị trả về.</summary>
public sealed class Result<T> : Result
{
    internal Result(
        T? value,
        bool isSuccess,
        string? error,
        ErrorCode errorCode,
        IReadOnlyDictionary<string, string[]>? errors)
        : base(isSuccess, error, errorCode, errors)
    {
        Value = value;
    }

    /// <summary>Chỉ có nghĩa khi <see cref="Result.IsSuccess"/> là true.</summary>
    public T? Value { get; }

    /// <summary>Lấy giá trị của một kết quả đã biết chắc là thành công.</summary>
    public T ValueOrThrow => IsSuccess
        ? Value!
        : throw new InvalidOperationException($"Không thể đọc giá trị của một Result thất bại: {Error}");
}
