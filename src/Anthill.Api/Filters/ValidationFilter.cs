using Anthill.Application.Common;
using FluentValidation;
using FluentValidation.Results;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace Anthill.Api.Filters;

/// <summary>
/// Chạy validator FluentValidation cho mọi tham số của action, trước khi controller được gọi.
/// </summary>
/// <remarks>
/// <para>
/// Dự án chỉ có gói <c>FluentValidation.DependencyInjectionExtensions</c> — gói đó biết đăng ký
/// validator vào container nhưng <b>không</b> tự cắm vào pipeline của MVC (việc đó thuộc về
/// <c>FluentValidation.AspNetCore</c>, gói đã ngừng phát triển). Bộ lọc mười dòng này thay thế nó
/// mà không phải thêm dependency.
/// </para>
/// <para>
/// Thứ tự chạy: MVC bind model → bộ lọc mặc định của <c>[ApiController]</c> bắt lỗi bind (thứ tự
/// -2000) → bộ lọc này. Nên tới đây thì model đã bind xong, chỉ còn xét quy tắc nghiệp vụ.
/// </para>
/// </remarks>
public sealed class ValidationFilter : IAsyncActionFilter
{
    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var services = context.HttpContext.RequestServices;
        List<ValidationFailure>? failures = null;

        foreach (var argument in context.ActionArguments.Values)
        {
            if (argument is null)
            {
                continue;
            }

            // Validator tra theo kiểu thực tế của tham số; không có validator thì bỏ qua, đó là
            // trường hợp bình thường chứ không phải thiếu sót.
            var validatorType = typeof(IValidator<>).MakeGenericType(argument.GetType());
            if (services.GetService(validatorType) is not IValidator validator)
            {
                continue;
            }

            var validationContext = new ValidationContext<object>(argument);
            var result = await validator.ValidateAsync(validationContext, context.HttpContext.RequestAborted);

            if (!result.IsValid)
            {
                (failures ??= []).AddRange(result.Errors);
            }
        }

        if (failures is null || failures.Count == 0)
        {
            await next();
            return;
        }

        context.Result = new BadRequestObjectResult(BuildErrorResponse(failures));
    }

    /// <summary>
    /// <c>message</c> lấy lỗi đầu tiên chứ không phải câu chung chung: frontend hiện thông điệp đó
    /// thẳng lên toast, mà "Dữ liệu không hợp lệ" thì không nói được người dùng phải sửa gì (§1).
    /// </summary>
    private static ApiErrorResponse BuildErrorResponse(IReadOnlyCollection<ValidationFailure> failures)
    {
        var errors = failures
            .GroupBy(failure => ToCamelCase(failure.PropertyName), StringComparer.Ordinal)
            .ToDictionary(
                group => group.Key,
                group => group.Select(failure => failure.ErrorMessage).ToArray(),
                StringComparer.Ordinal);

        return new ApiErrorResponse
        {
            Message = failures.First().ErrorMessage,
            Errors = errors
        };
    }

    /// <summary>
    /// <c>FullName</c> → <c>fullName</c>. Khoá lỗi phải trùng tên trường trong JSON, nếu không form
    /// ở frontend không biết gắn thông báo vào ô nào.
    /// </summary>
    private static string ToCamelCase(string propertyName)
    {
        if (string.IsNullOrEmpty(propertyName) || !char.IsUpper(propertyName[0]))
        {
            return propertyName;
        }

        return char.ToLowerInvariant(propertyName[0]) + propertyName[1..];
    }
}
