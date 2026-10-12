using Anthill.Application.Interfaces.Repositories;
using Anthill.Application.Interfaces.Security;
using Anthill.Application.Interfaces.Services;
using Anthill.Application.Interfaces.UnitOfWork;
using Anthill.Infrastructure.Authentication;
using Anthill.Infrastructure.FileStorage;
using Anthill.Infrastructure.Persistence;
using Anthill.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Npgsql;
using UnitOfWorkImplementation = Anthill.Infrastructure.UnitOfWork.UnitOfWork;

namespace Anthill.Infrastructure;

/// <summary>
/// Điểm ráp duy nhất của tầng Infrastructure vào container. Tầng Api chỉ cần gọi
/// <c>services.AddInfrastructure(builder.Configuration)</c>.
/// </summary>
public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services.AddPersistence(configuration);
        services.AddRepositories();
        services.AddSecurity(configuration);
        services.AddFileStorage(configuration);

        return services;
    }

    /// <summary>
    /// Nơi cất ảnh câu hỏi: đĩa của chính máy chạy API (mặc định) hoặc Cloudinary, chọn bằng
    /// <c>FileStorage:Provider</c>.
    /// </summary>
    /// <remarks>
    /// Production dùng Cloudinary vì Render xoá sạch đĩa mỗi lần khởi động lại. Cả hai nằm sau
    /// <c>IFileService</c> nên không controller nào phải biết.
    /// </remarks>
    private static IServiceCollection AddFileStorage(this IServiceCollection services, IConfiguration configuration)
    {
        var section = configuration.GetSection(FileStorageOptions.SectionName);

        // Gán tay từng trường, cùng lý do đã ghi ở AddSecurity: tầng này không kéo thêm gói
        // Microsoft.Extensions.Options.ConfigurationExtensions chỉ để có một lời gọi Bind.
        services.Configure<FileStorageOptions>(options =>
        {
            var defaults = new FileStorageOptions();

            options.RootPath = FirstNonEmpty(section["RootPath"], defaults.RootPath);
            options.UploadFolder = FirstNonEmpty(section["UploadFolder"], defaults.UploadFolder);
            options.RequestPath = FirstNonEmpty(section["RequestPath"], defaults.RequestPath);

            options.MaxBytes = long.TryParse(section["MaxBytes"], out var maxBytes) && maxBytes > 0
                ? maxBytes
                : defaults.MaxBytes;

            options.Provider = FirstNonEmpty(section["Provider"], defaults.Provider);
            options.Cloudinary = new CloudinaryOptions
            {
                CloudName = FirstNonEmpty(section["Cloudinary:CloudName"], string.Empty),
                ApiKey = FirstNonEmpty(section["Cloudinary:ApiKey"], string.Empty),
                ApiSecret = FirstNonEmpty(section["Cloudinary:ApiSecret"], string.Empty),
                Folder = FirstNonEmpty(section["Cloudinary:Folder"], defaults.Cloudinary.Folder)
            };
        });

        // Chọn nơi cất lúc đăng ký: cấu hình sai (nơi cất không có, thiếu khoá Cloudinary) thì chết ngay lúc khởi
        // động, xem ValidateSecurityConfiguration, chứ không đợi tới lần tải ảnh đầu tiên.
        if (string.Equals(section["Provider"], FileStorageOptions.CloudinaryProvider, StringComparison.OrdinalIgnoreCase))
        {
            // Một HttpClient dùng chung cho cả tiến trình, có thời hạn: một lời gọi treo không được giữ chân request
            // của giáo viên mãi. Kết nối được làm mới định kỳ để không giữ mãi một địa chỉ DNS đã đổi. Không dùng
            // IHttpClientFactory vì tầng này không kéo thêm gói chỉ để có một client.
            var cloudinaryClient = new HttpClient(new SocketsHttpHandler { PooledConnectionLifetime = TimeSpan.FromMinutes(5) })
            {
                Timeout = TimeSpan.FromSeconds(30)
            };

            services.AddSingleton<IFileService>(provider => new CloudinaryFileService(
                cloudinaryClient, provider.GetRequiredService<IOptions<FileStorageOptions>>()));
        }
        else
        {
            // Không giữ trạng thái theo request: một thể hiện cho cả tiến trình là đủ.
            services.AddSingleton<IFileService, LocalFileStorageService>();
        }

        return services;
    }

    private static IServiceCollection AddPersistence(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = ResolveConnectionString(configuration);

        services.AddDbContext<AppDbContext>(options =>
        {
            options.UseNpgsql(connectionString, npgsql =>
            {
                npgsql.MigrationsAssembly(typeof(AppDbContext).Assembly.GetName().Name!);

                // Cố ý KHÔNG bật EnableRetryOnFailure: execution strategy có thử lại sẽ từ chối
                // mọi transaction do người dùng tự mở, mà giao bài / nộp bài / xoá đề đều cần
                // IUnitOfWork.BeginTransactionAsync.
            });
        });

        return services;
    }

    private static IServiceCollection AddRepositories(this IServiceCollection services)
    {
        // Vòng đời Scoped: cùng một AppDbContext cho cả request, nên thay đổi trên nhiều bảng
        // nằm gọn trong một lần SaveChangesAsync.
        services.AddScoped<IUnitOfWork, UnitOfWorkImplementation>();

        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<ISubjectRepository, SubjectRepository>();
        services.AddScoped<IStudentGroupRepository, StudentGroupRepository>();
        services.AddScoped<IQuestionRepository, QuestionRepository>();
        services.AddScoped<IExamRepository, ExamRepository>();
        services.AddScoped<IExamAssignmentRepository, ExamAssignmentRepository>();
        services.AddScoped<IExamAttemptRepository, ExamAttemptRepository>();
        services.AddScoped<IStudentAnswerRepository, StudentAnswerRepository>();
        services.AddScoped<INotificationRepository, NotificationRepository>();

        return services;
    }

    private static IServiceCollection AddSecurity(this IServiceCollection services, IConfiguration configuration)
    {
        var jwtSection = configuration.GetSection(JwtOptions.SectionName);

        // Gán tay từng trường thay vì Configure(IConfiguration): tầng này cố ý không kéo thêm gói
        // Microsoft.Extensions.Options.ConfigurationExtensions chỉ để có một lời gọi Bind.
        services.Configure<JwtOptions>(options =>
        {
            var defaults = new JwtOptions();

            options.Issuer = FirstNonEmpty(jwtSection["Issuer"], defaults.Issuer);
            options.Audience = FirstNonEmpty(jwtSection["Audience"], defaults.Audience);
            options.SecretKey = FirstNonEmpty(
                jwtSection["SecretKey"],
                Environment.GetEnvironmentVariable("JWT_SECRET_KEY"),
                string.Empty);

            options.ExpiryMinutes = int.TryParse(jwtSection["ExpiryMinutes"], out var minutes) && minutes > 0
                ? minutes
                : defaults.ExpiryMinutes;
        });

        // Cả hai đều không giữ trạng thái theo request: một thể hiện cho cả tiến trình là đủ.
        services.AddSingleton<IPasswordHasher, PasswordHasher>();
        services.AddSingleton<IJwtTokenGenerator, JwtTokenGenerator>();

        // Scoped chứ không Singleton như hai dòng trên: nó đọc qua AppDbContext của chính request.
        services.AddScoped<IAccountSessionReader, AccountSessionReader>();

        return services;
    }

    /// <summary>
    /// Ưu tiên <c>ConnectionStrings:DefaultConnection</c> — biến môi trường
    /// <c>ConnectionStrings__DefaultConnection</c> ghi đè được nhờ chính nhà cung cấp cấu hình
    /// mặc định của ASP.NET Core. Chỉ khi khoá đó trống mới đọc tới <c>DATABASE_URL</c>, là biến
    /// mà Neon và phần lớn PaaS phát ra sẵn.
    /// </summary>
    private static string ResolveConnectionString(IConfiguration configuration)
    {
        var configured = configuration.GetConnectionString("DefaultConnection");
        if (!string.IsNullOrWhiteSpace(configured))
        {
            return configured.Trim();
        }

        var databaseUrl = Environment.GetEnvironmentVariable("DATABASE_URL");
        if (!string.IsNullOrWhiteSpace(databaseUrl))
        {
            return NormalizeDatabaseUrl(databaseUrl.Trim());
        }

        throw new InvalidOperationException(
            "Thiếu chuỗi kết nối. Đặt 'ConnectionStrings:DefaultConnection' trong appsettings, "
            + "hoặc biến môi trường ConnectionStrings__DefaultConnection / DATABASE_URL.");
    }

    /// <summary>
    /// Neon phát chuỗi kết nối dạng URI (<c>postgresql://user:pass@host/db?sslmode=require</c>),
    /// còn Npgsql chỉ hiểu dạng từ khoá. Chuyển đổi ở đây để deploy không phải gõ tay lại chuỗi
    /// kết nối mỗi lần đổi mật khẩu database.
    /// </summary>
    private static string NormalizeDatabaseUrl(string value)
    {
        if (!value.StartsWith("postgres://", StringComparison.OrdinalIgnoreCase)
            && !value.StartsWith("postgresql://", StringComparison.OrdinalIgnoreCase))
        {
            // Đã là dạng từ khoá, giữ nguyên.
            return value;
        }

        var uri = new Uri(value);
        var credentials = uri.UserInfo.Split(':', 2);

        var builder = new NpgsqlConnectionStringBuilder
        {
            Host = uri.Host,
            Port = uri.IsDefaultPort ? 5432 : uri.Port,
            Database = Uri.UnescapeDataString(uri.AbsolutePath.Trim('/')),
            Username = Uri.UnescapeDataString(credentials[0]),
            Password = credentials.Length > 1 ? Uri.UnescapeDataString(credentials[1]) : null,
            SslMode = SslMode.Require,

            // Neon free giới hạn số kết nối đồng thời; pool mặc định của Npgsql là 100 nên
            // vài lần khởi động lại là chạm trần.
            MaxPoolSize = 10
        };

        return builder.ConnectionString;
    }

    private static string FirstNonEmpty(params string?[] candidates) =>
        candidates.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value))?.Trim() ?? string.Empty;
}
