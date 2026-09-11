using Anthill.Application.Common;
using Anthill.Application.Interfaces.Security;
using Anthill.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Anthill.Infrastructure.Authentication;

/// <summary>
/// Cài đặt <see cref="IAccountSessionReader"/> bằng một phép chiếu theo khoá chính.
/// </summary>
/// <remarks>
/// <para>
/// Mỗi request có token trả thêm đúng một truy vấn này. Ở quy mô một trường hay một trung tâm, đó là
/// cái giá rẻ hơn nhiều so với lựa chọn còn lại — cache trạng thái tài khoản vài chục giây — vì cache
/// nghĩa là một em vừa bị khoá vẫn nộp được bài trong khoảng trễ ấy, đúng điều tính năng này sinh ra
/// để chặn. Nếu một ngày tải lên cao, chỗ cần làm là cache có thu hồi chủ động (xoay stamp thì xoá
/// khoá cache), không phải cache có hạn.
/// </para>
/// <para>
/// <c>AsNoTracking</c>: DbContext này dùng chung cả request, và một bản <c>User</c> đã được theo dõi
/// sẽ đè lên entity mà service nạp sau đó để sửa.
/// </para>
/// </remarks>
public sealed class AccountSessionReader : IAccountSessionReader
{
    private readonly AppDbContext _dbContext;

    public AccountSessionReader(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    /// <inheritdoc />
    public Task<AccountSessionState?> GetAsync(int userId, CancellationToken cancellationToken = default) =>
        _dbContext.Users
            .AsNoTracking()
            .Where(user => user.Id == userId)
            .Select(user => new AccountSessionState(user.Id, user.Status, user.SecurityStamp))
            .FirstOrDefaultAsync(cancellationToken);
}
