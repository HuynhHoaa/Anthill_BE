using Anthill.Application.Common;
using Anthill.Application.DTOs.Files;
using Anthill.Infrastructure.FileStorage;
using FluentAssertions;
using Microsoft.Extensions.Options;

namespace Anthill.Tests.FileStorage;

/// <summary>
/// Trần dung lượng ảnh được chặn ở hai chỗ: đầu vào HTTP (<c>UploadSizeLimitAttribute</c>, trước khi
/// form được đọc) và service (theo độ dài file). Hai chỗ phải cùng một trần và cùng một câu.
/// </summary>
/// <remarks>
/// Lệch nhau theo một chiều thì ảnh hợp lệ bị đầu vào chặn oan; lệch theo chiều kia thì người dùng
/// thấy hai thông điệp khác nhau cho cùng một lỗi, tuỳ ảnh to hơn trần bao nhiêu.
/// </remarks>
public class UploadSizeLimitTests
{
    private static FileStorageOptions Options(long maxBytes) => new() { MaxBytes = maxBytes };

    [Fact]
    public void Tran_than_request_rong_hon_tran_anh_dung_bang_phan_vo_multipart()
    {
        var options = Options(4 * 1024 * 1024);

        options.MaxRequestBytes.Should().Be(options.MaxBytes + FileStorageOptions.MultipartOverheadBytes);

        // Một ảnh đúng bằng trần, cộng vỏ multipart vài trăm byte, phải lọt qua được đầu vào.
        options.MaxRequestBytes.Should().BeGreaterThan(options.MaxBytes + 1024);
    }

    [Fact]
    public void Tran_than_request_di_theo_cau_hinh_khong_co_dinh_4_MB()
    {
        Options(10 * 1024 * 1024).MaxRequestBytes
            .Should().Be(10 * 1024 * 1024 + FileStorageOptions.MultipartOverheadBytes);
    }

    [Theory]
    [InlineData(4 * 1024 * 1024, "4 MB")]
    [InlineData(2 * 1024 * 1024, "2 MB")]
    public void Thong_diep_qua_lon_neu_dung_tran_da_cau_hinh(long maxBytes, string expected)
    {
        Options(maxBytes).TooLargeMessage.Should().Contain(expected);
    }

    [Fact]
    public async Task Service_tu_choi_file_vuot_tran_bang_cung_cau_voi_dau_vao_ma_khong_doc_byte_nao()
    {
        var options = Options(1024);
        var service = new LocalFileStorageService(Microsoft.Extensions.Options.Options.Create(options));
        await using var content = new MemoryStream(new byte[1025]);

        var result = await service.UploadImageAsync(new FileUploadRequest
        {
            Content = content,
            FileName = "anh.png",
            Length = 1025
        });

        result.IsFailure.Should().BeTrue();
        result.ErrorCode.Should().Be(ErrorCode.Validation);
        result.Error.Should().Be(options.TooLargeMessage);
        content.Position.Should().Be(0, "hàng rào dung lượng phải chặn trước khi đọc nội dung");
    }

    [Fact]
    public async Task File_dung_bang_tran_qua_duoc_hang_rao_dung_luong()
    {
        var options = Options(1024);
        var service = new LocalFileStorageService(Microsoft.Extensions.Options.Options.Create(options));

        // Nội dung toàn số 0 nên sẽ hỏng ở hàng rào magic bytes phía sau — đủ để biết nó đã vượt qua
        // hàng rào dung lượng mà không cần ghi file nào xuống đĩa.
        await using var content = new MemoryStream(new byte[1024]);

        var result = await service.UploadImageAsync(new FileUploadRequest
        {
            Content = content,
            FileName = "anh.png",
            Length = 1024
        });

        result.IsFailure.Should().BeTrue();
        result.Error.Should().NotBe(options.TooLargeMessage);
    }
}
