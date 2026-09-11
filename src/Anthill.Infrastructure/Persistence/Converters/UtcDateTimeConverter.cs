using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Anthill.Infrastructure.Persistence.Converters;

/// <summary>
/// Ép mọi <see cref="DateTime"/> về UTC trước khi ghi và gắn lại <c>Kind = Utc</c> khi đọc.
/// </summary>
/// <remarks>
/// Hai lý do bắt buộc phải có:
/// <list type="bullet">
/// <item>Npgsql ném lỗi khi ghi một <c>DateTime</c> có <c>Kind = Local</c> xuống cột
/// <c>timestamptz</c>; còn <c>Kind = Unspecified</c> thì bị hiểu theo giờ máy chủ.</item>
/// <item>EF đọc lên trả về <c>Kind = Unspecified</c>, và <c>System.Text.Json</c> chỉ thêm hậu tố
/// <c>Z</c> khi <c>Kind = Utc</c> — thiếu nó là frontend hiểu lệch đúng một múi giờ, mà hạn nộp
/// bài ở đây tính bằng phút (§3 quy tắc ③).</item>
/// </list>
/// </remarks>
public sealed class UtcDateTimeConverter : ValueConverter<DateTime, DateTime>
{
    public UtcDateTimeConverter()
        : base(
            value => value.Kind == DateTimeKind.Utc
                ? value
                : value.Kind == DateTimeKind.Local
                    ? value.ToUniversalTime()
                    : DateTime.SpecifyKind(value, DateTimeKind.Utc),
            value => DateTime.SpecifyKind(value, DateTimeKind.Utc))
    {
    }
}

/// <inheritdoc cref="UtcDateTimeConverter"/>
public sealed class NullableUtcDateTimeConverter : ValueConverter<DateTime?, DateTime?>
{
    public NullableUtcDateTimeConverter()
        : base(
            value => value == null
                ? null
                : value.Value.Kind == DateTimeKind.Utc
                    ? value
                    : value.Value.Kind == DateTimeKind.Local
                        ? value.Value.ToUniversalTime()
                        : DateTime.SpecifyKind(value.Value, DateTimeKind.Utc),
            value => value == null
                ? null
                : DateTime.SpecifyKind(value.Value, DateTimeKind.Utc))
    {
    }
}
