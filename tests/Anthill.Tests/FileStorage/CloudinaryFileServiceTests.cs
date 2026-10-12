using System.Net;
using System.Text;
using Anthill.Application.Common;
using Anthill.Application.DTOs.Files;
using Anthill.Infrastructure.FileStorage;
using FluentAssertions;

namespace Anthill.Tests.FileStorage;

public class CloudinaryFileServiceTests
{
    private static readonly byte[] Png =
        [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x00, 0x00, 0x00, 0x0D, 0x49, 0x48, 0x44, 0x52];

    private static FileStorageOptions Options() => new()
    {
        Provider = FileStorageOptions.CloudinaryProvider,
        Cloudinary = new CloudinaryOptions { CloudName = "demo", ApiKey = "key123", ApiSecret = "s3cret", Folder = "anthill" }
    };

    private sealed class FakeHandler(Func<HttpRequestMessage, Task<HttpResponseMessage>> respond) : HttpMessageHandler
    {
        public List<HttpRequestMessage> Requests { get; } = [];
        public List<string> Bodies { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Add(request);
            Bodies.Add(request.Content is null
                ? string.Empty
                : Encoding.Latin1.GetString(await request.Content.ReadAsByteArrayAsync(cancellationToken)));
            return await respond(request);
        }
    }

    private static HttpResponseMessage Json(HttpStatusCode status, string json) =>
        new(status) { Content = new StringContent(json, Encoding.UTF8, "application/json") };

    private static CloudinaryFileService Service(FileStorageOptions options, FakeHandler handler) =>
        new(new HttpClient(handler), Microsoft.Extensions.Options.Options.Create(options));

    private static FileUploadRequest Request(byte[] content, string fileName = "anh.png") => new()
    {
        Content = new MemoryStream(content),
        FileName = fileName,
        Length = content.Length
    };

    // Giá trị đối chiếu tính độc lập bằng hashlib: sha1("public_id=sample_image&timestamp=1315060510abcd").
    [Fact]
    public void Chu_ky_xep_tham_so_theo_ten_roi_bam_sha1_kem_bi_mat()
    {
        var signature = CloudinaryFileService.Sign(
            [new("timestamp", "1315060510"), new("public_id", "sample_image")], "abcd");

        signature.Should().Be("b4ad47fb4e25c7bf5f92a20089f9db59bc302313");
    }

    [Fact]
    public async Task Tai_len_gui_chu_ky_dung_va_tra_duong_dan_tuong_doi_giong_ban_ghi_xuong_dia()
    {
        var handler = new FakeHandler(_ => Task.FromResult(Json(HttpStatusCode.OK, """{"bytes":16,"public_id":"x"}""")));

        var result = await Service(Options(), handler).UploadImageAsync(Request(Png));

        result.IsSuccess.Should().BeTrue();
        var dto = result.ValueOrThrow;
        dto.Url.Should().MatchRegex(@"^/uploads/\d{6}/[0-9a-f]{32}\.png$");
        dto.ContentType.Should().Be("image/png");
        dto.SizeBytes.Should().Be(16);

        handler.Requests.Should().ContainSingle();
        handler.Requests[0].RequestUri!.ToString().Should().Be("https://api.cloudinary.com/v1_1/demo/image/upload");

        // Chữ ký phải khớp đúng public_id và timestamp đã gửi, ký bằng bí mật (bí mật không được gửi đi).
        var body = handler.Bodies[0];
        body.Should().NotContain("s3cret");
        var publicId = MultipartValue(body, "public_id");
        var timestamp = MultipartValue(body, "timestamp");
        publicId.Should().MatchRegex(@"^anthill/\d{6}/[0-9a-f]{32}$");
        MultipartValue(body, "signature").Should().Be(
            CloudinaryFileService.Sign([new("public_id", publicId), new("timestamp", timestamp)], "s3cret"));
        MultipartValue(body, "api_key").Should().Be("key123");
    }

    [Fact]
    public async Task Ten_nguoi_dung_khong_bao_gio_di_vao_public_id()
    {
        var handler = new FakeHandler(_ => Task.FromResult(Json(HttpStatusCode.OK, "{}")));

        await Service(Options(), handler).UploadImageAsync(Request(Png, "../../etc/passwd.png"));

        handler.Bodies[0].Should().NotContain("passwd");
    }

    [Fact]
    public async Task Anh_gia_mao_bi_chan_truoc_khi_goi_Cloudinary()
    {
        var handler = new FakeHandler(_ => Task.FromResult(Json(HttpStatusCode.OK, "{}")));
        var notAnImage = Encoding.ASCII.GetBytes("<script>alert(1)</script>");

        var result = await Service(Options(), handler).UploadImageAsync(Request(notAnImage));

        result.IsFailure.Should().BeTrue();
        handler.Requests.Should().BeEmpty();
    }

    [Fact]
    public async Task Cloudinary_loi_thi_tra_loi_ngan_khong_lo_chi_tiet()
    {
        var handler = new FakeHandler(_ => Task.FromResult(
            Json(HttpStatusCode.Unauthorized, """{"error":{"message":"Invalid Signature s3cret"}}""")));

        var result = await Service(Options(), handler).UploadImageAsync(Request(Png));

        result.IsFailure.Should().BeTrue();
        result.ErrorCode.Should().Be(ErrorCode.Internal);
        result.Error.Should().NotContain("s3cret").And.NotContain("Signature");
    }

    [Fact]
    public async Task Mat_ket_noi_khi_tai_len_tra_loi_ngan()
    {
        var handler = new FakeHandler(_ => throw new HttpRequestException("boom"));

        var result = await Service(Options(), handler).UploadImageAsync(Request(Png));

        result.IsFailure.Should().BeTrue();
        result.ErrorCode.Should().Be(ErrorCode.Internal);
    }

    [Fact]
    public async Task Xoa_goi_destroy_dung_public_id_va_coi_not_found_la_thanh_cong()
    {
        var handler = new FakeHandler(_ => Task.FromResult(Json(HttpStatusCode.OK, """{"result":"not found"}""")));
        var guid = new string('a', 32);

        var result = await Service(Options(), handler).DeleteAsync($"/uploads/202610/{guid}.png");

        result.IsSuccess.Should().BeTrue();
        handler.Requests[0].RequestUri!.ToString().Should().Be("https://api.cloudinary.com/v1_1/demo/image/destroy");
        UrlEncodedValue(handler.Bodies[0], "public_id").Should().Be($"anthill/202610/{guid}");
    }

    [Theory]
    [InlineData("/uploads/../appsettings.json")]
    [InlineData("/uploads/202610/not-a-guid.png")]
    [InlineData("/other/202610/aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa.png")]
    [InlineData("https://evil.example/uploads/202610/aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa.png")]
    [InlineData("")]
    [InlineData(null)]
    public async Task Xoa_tu_choi_duong_dan_khong_do_he_thong_cap_va_khong_goi_ra_ngoai(string? path)
    {
        var handler = new FakeHandler(_ => Task.FromResult(Json(HttpStatusCode.OK, """{"result":"ok"}""")));

        var result = await Service(Options(), handler).DeleteAsync(path);

        result.IsFailure.Should().BeTrue();
        handler.Requests.Should().BeEmpty();
    }

    [Fact]
    public void Chuyen_huong_dung_dia_chi_giao_hang_tu_duong_dan_tuong_doi()
    {
        var guid = new string('b', 32);

        CloudinaryFileService.TryBuildDeliveryUrl(Options(), $"/uploads/202610/{guid}.webp", out var url).Should().BeTrue();

        url.Should().Be($"https://res.cloudinary.com/demo/image/upload/anthill/202610/{guid}.webp");
    }

    [Theory]
    [InlineData("/uploads/202610/../../x.png")]
    [InlineData("/uploads/202610/aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa.svg")]
    [InlineData("/uploads/20261/aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa.png")]
    [InlineData("/uploads/202610/AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA.png")]
    [InlineData("//evil.example/uploads/202610/aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa.png")]
    public void Chuyen_huong_khong_bao_gio_di_ra_ngoai_khuon(string path)
    {
        CloudinaryFileService.TryBuildDeliveryUrl(Options(), path, out var url).Should().BeFalse();

        url.Should().BeEmpty();
    }

    [Fact]
    public void Chon_Cloudinary_ma_thieu_khoa_thi_cau_hinh_bi_tu_choi()
    {
        var options = new FileStorageOptions { Provider = FileStorageOptions.CloudinaryProvider };

        var act = () => options.Validate();

        act.Should().Throw<InvalidOperationException>().WithMessage("*CloudName*ApiKey*ApiSecret*");
    }

    [Fact]
    public void Noi_cat_khong_co_thi_cau_hinh_bi_tu_choi_con_Local_hay_Cloudinary_day_du_thi_qua()
    {
        var wrong = () => new FileStorageOptions { Provider = "S3" }.Validate();
        wrong.Should().Throw<InvalidOperationException>().WithMessage("*Provider*");

        var local = () => new FileStorageOptions().Validate();
        local.Should().NotThrow();

        var cloud = () => Options().Validate();
        cloud.Should().NotThrow();
    }

    private static string MultipartValue(string body, string name)
    {
        var marker = $"name={name}";
        var start = body.IndexOf(marker, StringComparison.Ordinal);
        if (start < 0)
        {
            start = body.IndexOf($"name=\"{name}\"", StringComparison.Ordinal);
        }

        var valueStart = body.IndexOf("\r\n\r\n", start, StringComparison.Ordinal) + 4;
        var valueEnd = body.IndexOf("\r\n", valueStart, StringComparison.Ordinal);
        return body[valueStart..valueEnd];
    }

    private static string UrlEncodedValue(string body, string name)
    {
        var pair = body.Split('&').Select(part => part.Split('=', 2)).First(part => part[0] == name);
        return Uri.UnescapeDataString(pair[1]);
    }
}
