using Anthill.Application.Common;
using Anthill.Infrastructure.FileStorage;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Options;

namespace Anthill.Api.Filters;

/// <summary>
/// Chặn file upload quá lớn <b>trước</b> khi ASP.NET Core đọc form, theo đúng trần
/// <c>FileStorage:MaxBytes</c>.
/// </summary>
/// <remarks>
/// <para>
/// Action nhận <c>IFormFile</c> nên bước bind model đọc trọn thân multipart (ghi ra đĩa tạm) rồi
/// action mới chạy tới hàng rào dung lượng trong <c>LocalFileStorageService</c>. Tới lúc đó chỉ còn
/// trần mặc định của framework đứng chắn — khoảng 28 MB của Kestrel, 128 MB của multipart: một file
/// 25 MB vẫn bị đọc hết chỉ để bị từ chối, còn file 30 MB thì nhận về một 413 không có body, và
/// frontend không có câu nào để hiện.
/// </para>
/// <para>
/// Đây là resource filter nên chạy trước bước bind model. Nó làm ba việc:
/// </para>
/// <list type="number">
/// <item>Request khai <c>Content-Length</c> lớn hơn trần thì trả lỗi ngay, không đọc byte nào. curl
/// và trình duyệt gửi kèm <c>Expect: 100-continue</c> với thân lớn, nên phần lớn file thậm chí không
/// rời máy người gửi.</item>
/// <item>Hạ trần thân request của server và trần multipart xuống đúng mức đó, cho request không khai
/// độ dài trước (chunked).</item>
/// <item>Tự đọc form trong <c>try</c> để lỗi vượt trần đi ra đúng hình dạng <c>{ message }</c>. Form
/// đã đọc được giữ lại trong request, nên bước bind model phía sau không đọc lại lần nữa.</item>
/// </list>
/// <para>
/// Trả 400 chứ không phải 413, và cùng một câu với service: người dùng thấy một lỗi duy nhất cho
/// "ảnh quá lớn" dù ảnh vượt trần 1 byte hay 1 GB. <see cref="ErrorCode"/> cũng không có mục nào
/// dịch sang 413, và thêm một mục chỉ cho chỗ này thì không đáng.
/// </para>
/// </remarks>
[AttributeUsage(AttributeTargets.Method)]
public sealed class UploadSizeLimitAttribute : Attribute, IFilterFactory
{
    /// <summary>Cấu hình đọc một lần lúc khởi động, nên một thể hiện filter dùng chung là đủ.</summary>
    public bool IsReusable => true;

    public IFilterMetadata CreateInstance(IServiceProvider serviceProvider) =>
        new UploadSizeLimitFilter(serviceProvider.GetRequiredService<IOptions<FileStorageOptions>>().Value);

    private sealed class UploadSizeLimitFilter : IAsyncResourceFilter
    {
        private readonly FileStorageOptions _options;

        public UploadSizeLimitFilter(FileStorageOptions options)
        {
            _options = options;
        }

        public async Task OnResourceExecutionAsync(ResourceExecutingContext context, ResourceExecutionDelegate next)
        {
            var httpContext = context.HttpContext;
            var request = httpContext.Request;
            var limit = _options.MaxRequestBytes;

            if (request.ContentLength > limit)
            {
                context.Result = TooLarge();
                return;
            }

            // IsReadOnly khi thân request đã bắt đầu được đọc; gán vào lúc đó thì ném lỗi. Hiện không
            // middleware nào đọc thân trước chỗ này, nhưng một middleware thêm sau thì có thể.
            if (httpContext.Features.Get<IHttpMaxRequestBodySizeFeature>() is { IsReadOnly: false } bodySize)
            {
                bodySize.MaxRequestBodySize = limit;
            }

            if (request.HasFormContentType)
            {
                // Cùng cách RequestFormLimitsAttribute của framework: thay IFormFeature trước khi form
                // được đọc. Trần multipart chỉ thật sự có tác dụng ở server không áp được trần thân
                // request; ở Kestrel, trần thân (đếm cả vỏ multipart) luôn chạm trước.
                httpContext.Features.Set<IFormFeature>(new FormFeature(request, new FormOptions
                {
                    MultipartBodyLengthLimit = limit
                }));

                try
                {
                    await request.ReadFormAsync(httpContext.RequestAborted);
                }
                catch (BadHttpRequestException exception)
                    when (exception.StatusCode == StatusCodes.Status413PayloadTooLarge)
                {
                    context.Result = TooLarge();
                    return;
                }
                catch (InvalidDataException)
                {
                    // Multipart hỏng cú pháp, hoặc vượt trần multipart ở server không có trần thân
                    // request. Không phân biệt được hai trường hợp mà không dựa vào chuỗi thông điệp
                    // tiếng Anh của framework, nên nói điều chắc chắn đúng.
                    context.Result = new BadRequestObjectResult(ApiErrorResponse.Of("Không đọc được file gửi lên."));
                    return;
                }
            }

            await next();
        }

        private BadRequestObjectResult TooLarge() => new(ApiErrorResponse.Of(_options.TooLargeMessage));
    }
}
