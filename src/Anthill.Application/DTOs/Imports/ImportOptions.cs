namespace Anthill.Application.DTOs.Imports;

/// <summary>Lựa chọn của quản trị viên khi nhập học sinh từ Excel.</summary>
/// <param name="DefaultGroupId">Lớp áp cho những dòng để trống ô Lớp (nhập cả một lớp từ một danh sách chỉ có tên và mã).</param>
/// <param name="Password">Mật khẩu ban đầu dùng chung cho các tài khoản mới của đợt này; mỗi em bị ép đổi ở lần đăng nhập đầu.</param>
/// <param name="DryRun"><c>true</c>: chỉ kiểm tra và đếm, chưa ghi gì.</param>
public sealed record ImportStudentsOptions(int? DefaultGroupId, string? Password, bool DryRun);

/// <summary>Lựa chọn của quản trị viên khi nhập giáo viên từ Excel.</summary>
/// <param name="DefaultSubjectId">Môn áp cho những dòng để trống ô Môn (nhập cả một tổ bộ môn).</param>
/// <param name="Password">Mật khẩu ban đầu dùng chung cho các tài khoản mới của đợt này.</param>
/// <param name="DryRun"><c>true</c>: chỉ kiểm tra và đếm, chưa ghi gì.</param>
public sealed record ImportTeachersOptions(int? DefaultSubjectId, string? Password, bool DryRun);

/// <summary>Lựa chọn của quản trị viên khi nhập lớp từ Excel.</summary>
/// <param name="DryRun"><c>true</c>: chỉ kiểm tra và đếm, chưa ghi gì.</param>
public sealed record ImportGroupsOptions(bool DryRun);
