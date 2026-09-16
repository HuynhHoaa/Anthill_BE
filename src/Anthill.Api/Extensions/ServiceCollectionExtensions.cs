using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Unicode;
using Anthill.Api.BackgroundJobs;
using Anthill.Api.Configuration;
using Anthill.Api.Hubs;
using Anthill.Api.Realtime;
using Anthill.Api.Filters;
using Anthill.Api.Security;
using Anthill.Application.Common;
using Anthill.Application.Interfaces.Security;
using Anthill.Application.Interfaces.Realtime;
using Anthill.Application.Interfaces.Services;
using Anthill.Application.Services;
using Anthill.Application.Validators;
using Anthill.Infrastructure.Authentication;
using FluentValidation;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;

namespace Anthill.Api.Extensions;

/// <summary>
/// Toàn bộ phần đăng ký dịch vụ của tầng Api, tách khỏi <c>Program.cs</c> để file đó đọc được
/// như một mục lục.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Service nghiệp vụ và validator của tầng Application, cộng với bản cài đặt
    /// <see cref="ICurrentUser"/> — thứ duy nhất của tầng đó phải sống ở Api vì nó cần
    /// <c>HttpContext</c>.
    /// </summary>
    public static IServiceCollection AddApplicationServices(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddHttpContextAccessor();
        services.AddScoped<ICurrentUser, CurrentUser>();

        // Thêm theo đúng thứ tự của §9. Đã xong bước 1 (Auth + User), bước 2 (Student +
        // StudentGroup), bước 3 (Question), bước 4-5 (Exam + Assignment) và bước 6 (Attempt);
        // các service còn lại nối tiếp vào đây.
        // IFileService của bước 3 đăng ký ở AddInfrastructure, vì nó chạm tới đĩa.
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IStudentService, StudentService>();
        services.AddScoped<IStudentGroupService, StudentGroupService>();
        services.AddScoped<IQuestionService, QuestionService>();
        services.AddScoped<IExamService, ExamService>();
        services.AddScoped<IExamGeneratorService, ExamGeneratorService>();
        services.AddScoped<IExamAssignmentService, ExamAssignmentService>();
        services.AddScoped<IExamScheduleNotifier, ExamScheduleNotifier>();
        services.AddScoped<IDeadlineReminderService, DeadlineReminderService>();
        services.AddScoped<IResultService, ResultService>();
        services.AddScoped<IDashboardService, DashboardService>();
        services.AddScoped<IExamAttemptService, ExamAttemptService>();
        services.AddScoped<INotificationService, NotificationService>();

        // Cài đặt SignalR nằm ở tầng Api chứ không ở Infrastructure: hub là thành phần của
        // ASP.NET Core, và IHubContext chỉ có nghĩa ở nơi hub được đăng ký. Tầng Application vẫn
        // không biết gì về nó — nó chỉ nhìn thấy IRealtimeNotificationService.
        services.AddScoped<IRealtimeNotificationService, SignalRNotificationService>();

        // Quét cả assembly Application: thêm validator mới chỉ cần tạo file, không phải sửa chỗ này.
        services.AddValidatorsFromAssemblyContaining<LoginRequestValidator>();

        return services;
    }

    /// <summary>Controller, JSON, CORS, Swagger và SignalR.</summary>
    public static IServiceCollection AddApiServices(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services.AddApiControllers();
        services.AddCorsPolicy(configuration);
        services.AddSwaggerDocumentation();
        services.AddRealtime();

        return services;
    }

    private static IServiceCollection AddApiControllers(this IServiceCollection services)
    {
        services
            .AddControllers(options =>
            {
                // Bộ lọc chạy FluentValidation cho mọi action, xem ValidationFilter.
                options.Filters.Add<ValidationFilter>();
            })
            .AddJsonOptions(options =>
            {
                options.JsonSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;

                // Trường NULL biến mất khỏi JSON. Frontend khai báo code?, group?, phone?... là optional
                // nên bỏ hẳn đúng hơn là gửi null, và ApiErrorResponse.errors cũng cần điều này (§1).
                options.JsonSerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;

                // Cho ký tự tiếng Việt đi thẳng ra JSON thay vì thành ế, nhưng vẫn giữ phần thoát
                // của <, > và & — thông điệp lỗi có thể chứa dữ liệu người dùng gõ vào.
                options.JsonSerializerOptions.Encoder = JavaScriptEncoder.Create(UnicodeRanges.All);
            });

        services.Configure<ApiBehaviorOptions>(options =>
        {
            // ProblemDetails mặc định của ASP.NET Core trả { type, title, status, errors, traceId } —
            // không có trường "message", mà đó chính là trường frontend đọc (§1). Nắn lại về đúng
            // hình dạng ApiErrorResponse.
            options.InvalidModelStateResponseFactory = BuildModelStateErrorResponse;

            // Chặn bộ lọc mặc định biến kết quả 4xx không body thành ProblemDetails, cùng lý do trên.
            options.SuppressMapClientErrors = true;
        });

        return services;
    }

    /// <summary>
    /// Lỗi bind model (thiếu trường <c>required</c>, JSON sai kiểu, id không phải số...).
    /// </summary>
    /// <remarks>
    /// Thông điệp gốc của System.Text.Json là tiếng Anh và lộ tên kiểu .NET, nên phần <c>message</c>
    /// dùng một câu chung bằng tiếng Việt; chi tiết từng trường vẫn giữ trong <c>errors</c> cho ai
    /// đang gỡ lỗi.
    /// </remarks>
    private static IActionResult BuildModelStateErrorResponse(ActionContext context)
    {
        var errors = context.ModelState
            .Where(entry => entry.Value is { Errors.Count: > 0 })
            .ToDictionary(
                entry => NormalizeModelStateKey(entry.Key),
                entry => entry.Value!.Errors
                    .Select(error => string.IsNullOrWhiteSpace(error.ErrorMessage)
                        ? "Giá trị không hợp lệ."
                        : error.ErrorMessage)
                    .ToArray(),
                StringComparer.Ordinal);

        return new BadRequestObjectResult(new ApiErrorResponse
        {
            Message = "Dữ liệu gửi lên không hợp lệ.",
            Errors = errors.Count > 0 ? errors : null
        });
    }

    /// <summary>
    /// Khoá của ModelState có thể là <c>$.username</c> (lỗi từ System.Text.Json) hoặc
    /// <c>FullName</c> (lỗi từ model binder). Quy về một dạng camelCase để frontend gắn được
    /// thông báo vào đúng ô nhập.
    /// </summary>
    private static string NormalizeModelStateKey(string key)
    {
        var name = key.StartsWith("$.", StringComparison.Ordinal) ? key[2..] : key;

        if (name.Length == 0 || !char.IsUpper(name[0]))
        {
            return name;
        }

        return char.ToLowerInvariant(name[0]) + name[1..];
    }

    /// <summary>
    /// CORS. Đọc origin từ cấu hình và <b>không bao giờ</b> ghép <c>AllowAnyOrigin</c> với
    /// <c>AllowCredentials</c> — SignalR cần <c>AllowCredentials</c>, mà trình duyệt từ chối thẳng
    /// cặp đó ngay từ preflight.
    /// </summary>
    private static IServiceCollection AddCorsPolicy(this IServiceCollection services, IConfiguration configuration)
    {
        var corsOptions = CorsPolicyOptions.Load(configuration);

        services.AddSingleton(corsOptions);
        services.AddCors(options =>
        {
            options.AddPolicy(CorsPolicyOptions.PolicyName, policy => policy
                // SetIsOriginAllowed thay cho WithOrigins vì cấu hình chấp nhận mẫu có dấu sao
                // (https://*.vercel.app — địa chỉ preview đổi theo từng lần deploy). Middleware vẫn
                // phản hồi lại đúng origin cụ thể của request nên dùng được với AllowCredentials.
                .SetIsOriginAllowed(corsOptions.IsAllowed)
                .AllowAnyHeader()
                .AllowAnyMethod()
                .AllowCredentials()

                // Preflight của cùng một origin được nhớ trong 10 phút, bớt hẳn một request khứ hồi
                // cho mỗi lời gọi API có header Authorization.
                .SetPreflightMaxAge(TimeSpan.FromMinutes(10)));
        });

        return services;
    }

    private static IServiceCollection AddSwaggerDocumentation(this IServiceCollection services)
    {
        services.AddEndpointsApiExplorer();
        services.AddSwaggerGen(options =>
        {
            options.SwaggerDoc("v1", new OpenApiInfo
            {
                Title = "Anthill API",
                Version = "v1",
                Description =
                    "API của hệ thống kiểm tra trắc nghiệm trực tuyến Anthill. "
                    + "Mọi endpoint đều yêu cầu JWT trừ POST /api/auth/login."
            });

            options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
            {
                Name = "Authorization",
                Type = SecuritySchemeType.Http,
                Scheme = "bearer",
                BearerFormat = "JWT",
                In = ParameterLocation.Header,
                Description =
                    "Đăng nhập qua POST /api/auth/login rồi dán giá trị accessToken vào ô dưới. "
                    + "Không cần gõ chữ 'Bearer', Swagger tự thêm."
            });

            // Không có phần này thì nút Authorize vẫn hiện nhưng Swagger UI không đính header
            // Authorization vào request — bấm Try it out là nhận 401 mà không hiểu vì sao.
            // Danh sách scope rỗng: scheme này không phải oauth2 nên đặc tả bắt buộc mảng rỗng.
            options.AddSecurityRequirement(document => new OpenApiSecurityRequirement
            {
                [new OpenApiSecuritySchemeReference("Bearer", document, null)] = []
            });

            // Gộp phần XML comment của controller vào Swagger nếu đã bật GenerateDocumentationFile.
            var xmlPath = Path.Combine(AppContext.BaseDirectory, "Anthill.Api.xml");
            if (File.Exists(xmlPath))
            {
                options.IncludeXmlComments(xmlPath);
            }
        });

        return services;
    }

    private static IServiceCollection AddRealtime(this IServiceCollection services)
    {
        // Tiến trình nền tự nộp bài quá hạn. Bắt buộc phải có: học sinh tắt trình duyệt giữa giờ
        // thi thì không còn ai gọi submit, và bài đó nằm mãi ở trạng thái đang làm mà không có điểm.
        // BindConfiguration thay vì Configure(configuration.GetSection(...)): hàm này không nhận
        // IConfiguration, và nới chữ ký của nó chỉ vì một dòng cấu hình thì mọi lời gọi khác phải
        // sửa theo.
        services.AddOptions<AutoSubmitOptions>().BindConfiguration(AutoSubmitOptions.SectionName);
        services.AddHostedService<AutoSubmitExpiredAttemptsService>();

        // Nhắc học sinh chưa vào thi khi giờ của em sắp hết. Cùng lý do với tiến trình trên: em không mở
        // ứng dụng thì không có đồng hồ nào chạy trên máy em, mà đúng em đó mới là người cần được nhắc.
        services.AddOptions<DeadlineReminderOptions>().BindConfiguration(DeadlineReminderOptions.SectionName);
        services.AddHostedService<NotifyDeadlineSoonService>();

        // Xoá dữ liệu cá nhân của tài khoản đã hết thời gian chờ. Quét thưa hơn nhiều so với tiến
        // trình trên vì trễ vài tiếng ở đây không ai nhận ra, mà việc nó làm thì không hoàn tác được.
        services.AddHostedService<PurgeDeletedStudentsService>();

        services.AddSignalR(options =>
        {
            // Tắt ở mọi môi trường: bật lên thì thông điệp exception trong hub đi thẳng xuống client,
            // lộ tên kiểu và cấu trúc bên trong. Riêng HubException vẫn tới được client ở cả hai chế
            // độ, nên phần từ chối quyền trong NotificationHub vẫn nói rõ được lý do.
            options.EnableDetailedErrors = false;

            // OnTokenValidated chỉ thấy request HTTP; lời gọi hub trên một WebSocket đã mở thì không.
            // Bộ lọc này kiểm lại phiên ở từng lời gọi, xem SessionRevalidationHubFilter.
            options.AddFilter<SessionRevalidationHubFilter>();
        });

        // Không giữ trạng thái gì: một thể hiện cho cả tiến trình, IAccountSessionReader lấy từ scope
        // của từng lời gọi hub.
        services.AddSingleton<SessionRevalidationHubFilter>();

        // Scoped có chủ đích: nó cắt kết nối lúc scope của lời gọi hub bị dispose, xem lớp đó.
        services.AddScoped<RevokedConnectionCloser>();

        // Một sổ cho cả tiến trình: hub ghi vào, các lối xoay SecurityStamp đọc ra qua IUserConnectionCloser.
        // Đăng ký hai lần trỏ về cùng một thể hiện — hai thể hiện riêng là một sổ ghi mà không ai đọc.
        services.AddSingleton<UserConnectionRegistry>();
        services.AddSingleton<IUserConnectionCloser>(provider => provider.GetRequiredService<UserConnectionRegistry>());

        // Dạy SignalR biết Clients.User(id) tra theo claim "sub".
        services.AddSingleton<IUserIdProvider, SubClaimUserIdProvider>();

        return services;
    }

    /// <summary>
    /// Xác thực JWT Bearer.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Cấu hình đọc từ <see cref="JwtOptions"/> đã được <c>AddInfrastructure</c> đăng ký, nên khoá ký
    /// chỉ được khai báo ở đúng một chỗ trong cả hệ thống. Vì vậy phải gọi <b>sau</b>
    /// <c>AddInfrastructure</c>.
    /// </para>
    /// <para>
    /// <c>MapInboundClaims = false</c> là điều kiện cần của <see cref="CurrentUser"/>: bật ánh xạ mặc
    /// định thì <c>sub</c> bị đổi tên thành <c>ClaimTypes.NameIdentifier</c>, <c>role</c> thành
    /// <c>ClaimTypes.Role</c>, và <c>Context.UserIdentifier</c> của SignalR cũng lệch theo.
    /// </para>
    /// </remarks>
    public static IServiceCollection AddJwtAuthentication(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services
            .AddAuthentication(options =>
            {
                options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
                options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
            })
            .AddJwtBearer();

        // Configure<TDep> để lấy JwtOptions ra khỏi container thay vì tự đọc lại IConfiguration —
        // đọc lại là mở đường cho hai chỗ hiểu khác nhau về cùng một khoá ký.
        services
            .AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
            .Configure<IOptions<JwtOptions>>((bearer, accessor) =>
            {
                var jwt = accessor.Value;
                jwt.Validate();

                bearer.MapInboundClaims = false;
                bearer.SaveToken = false;

                bearer.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidIssuer = jwt.Issuer,

                    ValidateAudience = true,
                    ValidAudience = jwt.Audience,

                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.SecretKey)),

                    ValidateLifetime = true,
                    RequireExpirationTime = true,

                    // Mặc định là 5 phút. Với một hệ thống mà hạn nộp bài tính bằng phút, cho token
                    // sống thêm 5 phút sau khi hết hạn là quá rộng rãi.
                    ClockSkew = TimeSpan.FromSeconds(30),

                    // Hai dòng này ghép với MapInboundClaims = false: User.Identity.Name đọc claim
                    // "name", và [Authorize(Roles = "Teacher")] đọc claim "role" — đúng tên mà
                    // JwtTokenGenerator đã đặt vào token.
                    NameClaimType = "name",
                    RoleClaimType = CurrentUser.RoleClaimType
                };

                bearer.Events = new JwtBearerEvents
                {
                    OnMessageReceived = OnMessageReceived,
                    OnTokenValidated = OnTokenValidated,
                    OnChallenge = OnChallenge,
                    OnForbidden = OnForbidden
                };
            });

        services.AddAuthorization();

        return services;
    }

    /// <summary>
    /// WebSocket không cho đặt header <c>Authorization</c>, nên client SignalR gửi token qua query
    /// string <c>access_token</c> (§7). Chỉ chấp nhận đường này cho các đường dẫn <c>/hubs</c> —
    /// mở rộng cho cả REST là để token nằm lại trong log truy cập và lịch sử trình duyệt.
    /// </summary>
    private static Task OnMessageReceived(MessageReceivedContext context)
    {
        var path = context.HttpContext.Request.Path;
        if (!path.StartsWithSegments("/hubs", StringComparison.OrdinalIgnoreCase))
        {
            return Task.CompletedTask;
        }

        var accessToken = context.Request.Query["access_token"].ToString();
        if (!string.IsNullOrEmpty(accessToken))
        {
            context.Token = accessToken;
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// Chữ ký và hạn dùng đã đạt; giờ đối chiếu token với tài khoản <b>hiện tại</b> trong database.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Khoá, đưa vào diện xoá, đổi hay cấp lại mật khẩu đều xoay <c>SecurityStamp</c>. Không có bước
    /// này thì token cũ vẫn làm bài, xem kết quả, đọc thông báo tới khi hết hạn (120 phút) — trong khi
    /// hộp thoại khoá tài khoản hứa với quản trị viên rằng em đó bị đăng xuất ngay.
    /// </para>
    /// <para>
    /// Chạy cho mọi request có token, kể cả negotiate và request mở kết nối của SignalR (token đi qua
    /// query, xem <see cref="OnMessageReceived"/>). Kết quả xác thực được nhớ trong request, nên dù
    /// UseAuthentication và UseAuthorization cùng hỏi thì database cũng chỉ bị tra một lần.
    /// </para>
    /// <para>
    /// Đánh đổi: thêm một truy vấn theo khoá chính trên mỗi request có token. Chấp nhận được ở quy mô
    /// này; cache có hạn thì rẻ hơn nhưng mở lại đúng khoảng trễ mà bước này sinh ra để đóng (xem
    /// <c>AccountSessionReader</c>). Database lỗi thì request lỗi theo — đóng cửa chứ không cho qua.
    /// </para>
    /// </remarks>
    private static async Task OnTokenValidated(TokenValidatedContext context)
    {
        var principal = context.Principal;
        var reader = context.HttpContext.RequestServices.GetRequiredService<IAccountSessionReader>();

        // Cùng một hàm với bộ lọc hub (SessionRevalidationHubFilter), để REST và hub không thể kết
        // luận khác nhau về cùng một token.
        var verdict = await SessionValidation.CheckAsync(
            principal?.FindFirst(CurrentUser.UserIdClaimType)?.Value,
            principal?.FindFirst(CurrentUser.SecurityStampClaimType)?.Value,
            reader,
            context.HttpContext.RequestAborted);

        if (verdict == SessionVerdict.Valid)
        {
            return;
        }

        // UseRouting đứng trước UseAuthentication nên endpoint đã được chọn ở thời điểm này.
        if (SessionValidation.IsBlockedAccount(verdict)
            && context.HttpContext.GetEndpoint()?.Metadata.GetMetadata<ReportsBlockedAccountAttribute>() is not null)
        {
            return;
        }

        context.Fail(new RevokedSessionException(SessionValidation.MessageFor(verdict)));
    }

    /// <summary>
    /// Mang câu chữ của phép kiểm phiên từ <see cref="OnTokenValidated"/> sang <see cref="OnChallenge"/>,
    /// để 401 nói được "tài khoản đã bị khoá" thay vì câu "hết hạn" chung chung.
    /// </summary>
    private sealed class RevokedSessionException(string message) : Exception(message);

    /// <summary>401 mặc định của JwtBearer có body rỗng; frontend đọc trường <c>message</c> (§1).</summary>
    private static async Task OnChallenge(JwtBearerChallengeContext context)
    {
        context.HandleResponse();

        if (context.Response.HasStarted)
        {
            return;
        }

        var message = context.AuthenticateFailure is RevokedSessionException revoked
            ? revoked.Message
            : "Phiên đăng nhập đã hết hạn. Vui lòng đăng nhập lại.";

        context.Response.StatusCode = StatusCodes.Status401Unauthorized;
        await WriteMessageAsync(context.Response, message);
    }

    private static Task OnForbidden(ForbiddenContext context)
    {
        if (context.Response.HasStarted)
        {
            return Task.CompletedTask;
        }

        context.Response.StatusCode = StatusCodes.Status403Forbidden;
        return WriteMessageAsync(context.Response, "Bạn không có quyền thực hiện thao tác này.");
    }

    private static Task WriteMessageAsync(HttpResponse response, string message)
    {
        response.ContentType = "application/json; charset=utf-8";

        return response.WriteAsJsonAsync(
            ApiErrorResponse.Of(message),
            ErrorJsonOptions,
            cancellationToken: CancellationToken.None);
    }

    private static readonly JsonSerializerOptions ErrorJsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Encoder = JavaScriptEncoder.Create(UnicodeRanges.All)
    };
}
