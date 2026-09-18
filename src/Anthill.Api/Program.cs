using Anthill.Api.Configuration;
using Anthill.Api.Extensions;
using Anthill.Api.Hubs;
using Anthill.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

// ---------------------------------------------------------------------------
// Đăng ký dịch vụ
// ---------------------------------------------------------------------------

// Database, repository, UnitOfWork, băm mật khẩu, cấp JWT — một lời gọi duy nhất.
builder.Services.AddInfrastructure(builder.Configuration);

// Service nghiệp vụ, validator, và ICurrentUser (bản cài đặt đọc claim từ HttpContext).
builder.Services.AddApplicationServices();

// Controller, JSON, CORS, Swagger, SignalR.
builder.Services.AddApiServices(builder.Configuration);

// Phải gọi SAU AddInfrastructure: nó lấy JwtOptions ra khỏi container thay vì đọc lại cấu hình.
builder.Services.AddJwtAuthentication();

var app = builder.Build();

// Thiếu khoá ký hay khoá quá ngắn thì chết ngay tại đây, kèm thông điệp nói rõ phải sửa ở đâu.
app.ValidateSecurityConfiguration();

// ---------------------------------------------------------------------------
// Pipeline
// ---------------------------------------------------------------------------

// Đầu tiên: chỉ bắt được lỗi của những middleware nằm sau nó.
app.UseExceptionHandling();
app.UseRequestLogging();

if (app.Environment.IsDevelopment())
{
    app.UseApiDocumentation();
}
else
{
    // Cố ý KHÔNG bật ở Development: frontend chạy http://localhost:5173 gọi sang http://localhost:5002
    // sẽ bị chuyển hướng 307 sang cổng https, mà preflight CORS không đi theo chuyển hướng — kết quả
    // là một lỗi CORS khó hiểu ở mọi request. Trên production, TLS do reverse proxy đảm nhiệm.
    app.UseHttpsRedirection();
}

// Ảnh câu hỏi. Đặt trước UseRouting vì đây là nội dung tĩnh, không cần đi qua bộ định tuyến —
// và cố ý đứng ngoài UseAuthentication: một ảnh minh hoạ nhúng bằng thẻ <img> thì trình duyệt
// không gửi kèm header Authorization, nên bắt xác thực ở đây sẽ làm mọi ảnh hỏng.
// Đổi lại, tên file là GUID nên không đoán được, và bảng loại nội dung chỉ nhận bốn đuôi ảnh.
app.UseUploadedFiles();

app.UseRouting();

// Sau UseRouting và trước UseAuthentication: phải chạy đủ sớm để trả lời được preflight OPTIONS,
// vốn không mang theo header Authorization.
app.UseCors(CorsPolicyOptions.PolicyName);

app.UseAuthentication();

// Sau UseAuthentication (cần biết ai đang gọi) và trước UseAuthorization: tài khoản còn phải đổi mật
// khẩu chỉ đi tiếp được tới ba endpoint gắn [AllowWhileMustChangePassword] (§5).
app.UseMustChangePasswordGate();

app.UseAuthorization();

app.MapControllers();

// Hub đọc token từ query string access_token, xem OnMessageReceived (§7).
// CloseOnAuthenticationExpiration: WebSocket chỉ xác thực một lần lúc mở rồi sống tiếp mà không hỏi
// lại token. Bật cờ này thì server tự đóng kết nối khi token của nó hết hạn, thay vì để kết nối sống
// lâu hơn chính token đã mở ra nó.
app.MapHub<NotificationHub>(
    NotificationHub.Route,
    options => options.CloseOnAuthenticationExpiration = true);

// Cho reverse proxy và dịch vụ hosting kiểm tra tiến trình còn sống; cũng là cách nhanh nhất để
// đối chiếu đồng hồ server khi gỡ lỗi phần hạn giờ làm bài.
app.MapGet("/health", () => Results.Ok(new
{
    status = "ok",
    serverTimeUtc = DateTime.UtcNow
}))
.ExcludeFromDescription();

// Dữ liệu demo, chỉ ở Development. Thất bại thì ghi cảnh báo chứ không chặn khởi động.
await app.SeedDevelopmentDataAsync();

await app.RunAsync();
