using System.Linq.Expressions;
using Anthill.Application.Interfaces.Repositories;
using Anthill.Domain.Common;
using Anthill.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Anthill.Infrastructure.Repositories;

/// <summary>
/// Thao tác chung cho mọi entity.
/// </summary>
/// <remarks>
/// <see cref="Update"/>, <see cref="Remove"/> và <see cref="RemoveRange"/> chỉ đánh dấu thay đổi
/// trên change tracker; việc ghi xuống database do <c>IUnitOfWork.SaveChangesAsync</c> thực hiện
/// một lần cho cả nghiệp vụ. Vì vậy các repository con cũng <b>không</b> dùng
/// <c>ExecuteDelete</c>/<c>ExecuteUpdate</c> — chúng ghi ngay lập tức, phá vỡ ranh giới đó.
/// </remarks>
public class GenericRepository<T> : IGenericRepository<T> where T : BaseEntity
{
    public GenericRepository(AppDbContext context)
    {
        Context = context;
        DbSet = context.Set<T>();
    }

    protected AppDbContext Context { get; }

    protected DbSet<T> DbSet { get; }

    public virtual async Task<T?> GetByIdAsync(int id, CancellationToken cancellationToken = default) =>
        await DbSet.FindAsync(new object?[] { id }, cancellationToken);

    public virtual async Task<IReadOnlyList<T>> GetAllAsync(CancellationToken cancellationToken = default) =>
        await DbSet.AsNoTracking().ToListAsync(cancellationToken);

    public virtual async Task<IReadOnlyList<T>> FindAsync(
        Expression<Func<T, bool>> predicate,
        CancellationToken cancellationToken = default) =>
        await DbSet.Where(predicate).ToListAsync(cancellationToken);

    public virtual async Task<T?> FirstOrDefaultAsync(
        Expression<Func<T, bool>> predicate,
        CancellationToken cancellationToken = default) =>
        await DbSet.FirstOrDefaultAsync(predicate, cancellationToken);

    public virtual async Task<bool> ExistsAsync(
        Expression<Func<T, bool>> predicate,
        CancellationToken cancellationToken = default) =>
        await DbSet.AnyAsync(predicate, cancellationToken);

    public virtual async Task<int> CountAsync(
        Expression<Func<T, bool>>? predicate = null,
        CancellationToken cancellationToken = default) =>
        predicate is null
            ? await DbSet.CountAsync(cancellationToken)
            : await DbSet.CountAsync(predicate, cancellationToken);

    public virtual async Task AddAsync(T entity, CancellationToken cancellationToken = default) =>
        await DbSet.AddAsync(entity, cancellationToken);

    public virtual async Task AddRangeAsync(IEnumerable<T> entities, CancellationToken cancellationToken = default) =>
        await DbSet.AddRangeAsync(entities, cancellationToken);

    /// <summary>
    /// Báo cho change tracker rằng <paramref name="entity"/> sẽ được ghi ở lần <c>SaveChangesAsync</c> tới.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Chỉ gọi <c>DbSet.Update</c> khi entity đang <b>Detached</b> — tạo tay kèm khoá, hoặc đọc bằng
    /// <c>AsNoTracking</c>. Entity nạp bằng <c>FindAsync</c> hay truy vấn có theo dõi thì DbContext đã
    /// chụp sẵn giá trị gốc, và <c>SaveChanges</c> tự ghi đúng những cột thật sự đổi.
    /// </para>
    /// <para>
    /// Gọi <c>DbSet.Update</c> trên entity đang được theo dõi thì ngược lại: nó đánh dấu <b>mọi</b> cột
    /// (và cả những navigation đã Include) là Modified, tức là ghi lại nguyên bản đã đọc từ đầu request.
    /// Mọi thứ request khác commit trong lúc đó bị đè mất mà không ai biết. Ví dụ đã gặp: một lần
    /// <c>PUT /api/auth/me</c> nạp tài khoản ngay trước khi quản trị viên bấm khoá, lưu xong thì
    /// <c>Status</c>, <c>SecurityStamp</c> và <c>PasswordHash</c> quay về bản cũ — lệnh khoá biến mất và
    /// token cũ sống lại. Cấp lại mật khẩu, đưa vào diện xoá hay tự đổi mật khẩu cũng mất theo cùng
    /// một cách.
    /// </para>
    /// <para>
    /// Hai request cùng sửa <b>một</b> dòng thì hàm này không phân xử được ai thắng. Bảng <c>Users</c> có
    /// concurrency token (<c>xmin</c>, xem <c>UserConfiguration</c>): bên ghi trên bản đã cũ nhận
    /// <c>DbUpdateConcurrencyException</c>. Hệ quả cho nhánh Detached: entity <c>User</c> đọc bằng
    /// <c>AsNoTracking</c> không mang theo giá trị <c>xmin</c> (thuộc tính ẩn không nằm trên đối tượng),
    /// nên đem nó vào đây thì lần lưu luôn bị coi là xung đột — tài khoản phải được nạp có theo dõi.
    /// </para>
    /// </remarks>
    public virtual void Update(T entity)
    {
        if (Context.Entry(entity).State == EntityState.Detached)
        {
            DbSet.Update(entity);
        }
    }

    public virtual void Remove(T entity) => DbSet.Remove(entity);

    public virtual void RemoveRange(IEnumerable<T> entities) => DbSet.RemoveRange(entities);
}
