using Anthill.Api.Configuration;
using Anthill.Api.Middlewares;
using Anthill.Infrastructure.Authentication;
using Anthill.Infrastructure.FileStorage;
using Anthill.Infrastructure.Persistence.Seed;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Options;

namespace Anthill.Api.Extensions;

/// <summary>
/// Các bước dựng pipeline, tách khỏi <c>Program.cs</c>.
/// </summary>
public static class ApplicationBuilderExtensions
{
    /// <summary>
    /// Lưới đỡ exception. Phải là middleware <b>đầu tiên</b> — nó chỉ bắt được lỗi của những
    /// middleware nằm sau nó.
    /// </summary>
    public static IApplicationBuilder UseExceptionHandling(this IApplicationBuilder app) =>
        app.UseMiddleware<ExceptionMiddleware>();

    public static IApplicationBuilder UseRequestLogging(this IApplicationBuilder app) =>
        app.UseMiddleware<RequestLoggingMiddleware>();

    /// <summary>
    /// Phục vụ ảnh câu hỏi đã tải lên, ở đúng đường dẫn mà <c>IFileService</c> đã cấp trong URL.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Đây là chỗ duy nhất trong hệ thống phục vụ file do người dùng gửi lên</b>, nên ba lựa
    /// chọn dưới đây đều là biện pháp phòng thủ chứ không phải cấu hình cho vui:
    /// </para>
    /// <list type="bullet">
    /// <item><b>Bảng loại nội dung tự khai, không dùng bảng mặc định.</b> Bảng mặc định biết hàng
    /// trăm đuôi file, kể cả <c>.html</c> và <c>.svg</c>. Ở đây chỉ bốn loại ảnh; đuôi lạ sẽ không
    /// được phục vụ thay vì được phục vụ kèm một loại nội dung tuỳ ý.</item>
    /// <item><b><c>X-Content-Type-Options: nosniff</c>.</b> Không có nó, trình duyệt được phép tự
    /// đoán loại nội dung và có thể chạy một file được khai là ảnh như thể nó là HTML.</item>
    /// <item><b>Cache dài.</b> Tên file là GUID nên nội dung không bao giờ đổi dưới cùng một URL —
    /// điều kiện đủ để cache vĩnh viễn, và nó giúp học sinh làm bài không tải lại ảnh mỗi lần
    /// chuyển câu.</item>
    /// </list>
    /// <para>
    /// Thư mục được tạo ngay tại đây nếu chưa có: <c>PhysicalFileProvider</c> ném lỗi lúc khởi
    /// động khi trỏ vào một thư mục không tồn tại, và ở máy mới thì nó chưa tồn tại thật.
    /// </para>
    /// </remarks>
    public static WebApplication UseUploadedFiles(this WebApplication app)
    {
        ArgumentNullException.ThrowIfNull(app);

        var options = app.Services.GetRequiredService<IOptions<FileStorageOptions>>().Value;

        var uploadRoot = Path.GetFullPath(Path.Combine(
            app.Environment.ContentRootPath,
            options.RootPath,
            options.UploadFolder));

        Directory.CreateDirectory(uploadRoot);

        var contentTypes = new FileExtensionContentTypeProvider(new Dictionary<string, string>(
            StringComparer.OrdinalIgnoreCase)
        {
            [".png"] = "image/png",
            [".jpg"] = "image/jpeg",
            [".jpeg"] = "image/jpeg",
            [".gif"] = "image/gif",
            [".webp"] = "image/webp"
        });

        app.UseStaticFiles(new StaticFileOptions
        {
            FileProvider = new PhysicalFileProvider(uploadRoot),
            RequestPath = options.RequestPath.TrimEnd('/'),
            ContentTypeProvider = contentTypes,

            // Đuôi không có trong bảng trên thì trả 404, thay vì phục vụ kèm một loại nội dung đoán bừa.
            ServeUnknownFileTypes = false,

            OnPrepareResponse = context =>
            {
                context.Context.Response.Headers.XContentTypeOptions = "nosniff";
                context.Context.Response.Headers.CacheControl = "public, max-age=31536000, immutable";
            }
        });

        return app;
    }

    /// <summary>Swagger UI ở <c>/swagger</c>, kèm nút Authorize để dán JWT vào thử.</summary>
    public static WebApplication UseApiDocumentation(this WebApplication app)
    {
        ArgumentNullException.ThrowIfNull(app);

        app.UseSwagger();
        app.UseSwaggerUI(options =>
        {
            options.SwaggerEndpoint("/swagger/v1/swagger.json", "Anthill API v1");
            options.DocumentTitle = "Anthill API";

            // Danh sách endpoint mặc định mở toang hết; thu lại cho dễ nhìn khi có đủ 68 endpoint.
            options.DocExpansion(Swashbuckle.AspNetCore.SwaggerUI.DocExpansion.List);
        });

        return app;
    }

    /// <summary>
    /// Kiểm cấu hình bảo mật ngay lúc khởi động, trước khi có request đầu tiên.
    /// </summary>
    /// <remarks>
    /// Thiếu khoá ký mà để tới lúc chạy mới phát hiện thì lỗi hiện ra dưới dạng 500 ở một endpoint
    /// ngẫu nhiên, rất mất thời gian lần. Chết ngay lúc boot với thông điệp rõ ràng thì hơn.
    /// </remarks>
    public static WebApplication ValidateSecurityConfiguration(this WebApplication app)
    {
        ArgumentNullException.ThrowIfNull(app);

        var logger = app.Services.GetRequiredService<ILoggerFactory>().CreateLogger("Anthill.Startup");

        // Ném InvalidOperationException kèm hướng dẫn nếu SecretKey trống hoặc ngắn hơn 32 byte.
        app.Services.GetRequiredService<IOptions<JwtOptions>>().Value.Validate();

        var cors = app.Services.GetRequiredService<CorsPolicyOptions>();
        if (cors.IsEmpty)
        {
            // Không chặn khởi động: một API gọi từ máy chủ khác (hoặc chỉ dùng Swagger) không cần CORS.
            // Nhưng đây là nguyên nhân số một của "gọi API từ frontend thì trình duyệt báo lỗi CORS",
            // nên phải nói thật to.
            logger.LogWarning(
                "Cors:AllowedOrigins đang trống — mọi request từ trình duyệt ở origin khác sẽ bị chặn.");
        }
        else
        {
            logger.LogInformation("CORS cho phép origin: {Origins}", string.Join(", ", cors.All));
        }

        return app;
    }

    /// <summary>
    /// Nạp dữ liệu demo, chỉ ở môi trường Development.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Cố ý <b>không</b> gọi <c>Database.Migrate()</c>: migration là một bước riêng của quy trình
    /// deploy (<c>dotnet ef database update</c>), không phải việc làm lúc boot. Chạy migration tự
    /// động nghĩa là hai tiến trình khởi động cùng lúc sẽ cùng sửa lược đồ.
    /// </para>
    /// <para>
    /// Vì vậy database chưa tạo bảng thì bước này thất bại — và đó là chuyện bình thường ở lần chạy
    /// đầu. Ghi cảnh báo kèm đúng câu lệnh cần gõ rồi cho API khởi động tiếp, thay vì chết ngay và
    /// bắt người mới phải đọc stack trace để đoán ra.
    /// </para>
    /// </remarks>
    public static async Task<WebApplication> SeedDevelopmentDataAsync(
        this WebApplication app,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(app);

        if (!app.Environment.IsDevelopment())
        {
            return app;
        }

        var logger = app.Services.GetRequiredService<ILoggerFactory>().CreateLogger("Anthill.Seed");

        try
        {
            await DbSeeder.SeedAsync(app.Services, cancellationToken);
            logger.LogInformation(
                "Đã nạp dữ liệu demo. Ba tài khoản dùng chung mật khẩu '{Password}'.",
                DbSeeder.DemoPassword);
        }
        catch (Exception exception)
        {
            logger.LogWarning(
                exception,
                "Không nạp được dữ liệu demo. Kiểm tra ConnectionStrings:DefaultConnection và chạy "
                + "'dotnet ef database update --project src/Anthill.Infrastructure --startup-project src/Anthill.Api'.");
        }

        return app;
    }
}
