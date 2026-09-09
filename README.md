# EduQuiz — Backend

API cho hệ thống kiểm tra trắc nghiệm trực tuyến EduQuiz: giáo viên soạn ngân hàng câu hỏi và ra đề
theo tỷ lệ độ khó, học sinh làm bài có đồng hồ đếm ngược, hệ thống chấm ở server.

**Frontend: [EduQuiz_FE](https://github.com/HuynhHoaa/EduQuiz_FE)** — React 19 + TypeScript, đã hoàn thiện
và đang chạy bằng mock. Backend này thay thế dần tầng mock đó theo đúng
[docs/EduQuiz_API_Contract.md](docs/EduQuiz_API_Contract.md).

Kiến trúc: Clean Architecture rút gọn + Repository + UnitOfWork + SignalR.

```
src/EduQuiz.Domain/          entity, enum — không tham chiếu ai
src/EduQuiz.Application/     DTO, interface, service nghiệp vụ, validator
src/EduQuiz.Infrastructure/  EF Core, repository, JWT, BCrypt, seed
src/EduQuiz.Api/             controller, middleware, hub, cấu hình
tests/EduQuiz.Tests/
```

`dotnet build` — 0 error, 0 warning. Ba trong tám bước đã chạy thật (xem mục 7).

### Vài chỗ đáng xem

**Khoá tài khoản và đổi mật khẩu làm token cũ chết ngay** thay vì chờ hết hạn: mỗi user giữ một
`SecurityStamp` nhúng trong token và kiểm ở mỗi request; xoay nó là mọi JWT đã phát mất hiệu lực.
Mở khoá thì không xoay — làm vậy chỉ bắt học sinh vừa được mở phải đăng nhập lại lần thứ hai vô cớ.

**Vai trò luôn đọc từ cột `Role` trong database.** Trường `role` client gửi lên chỉ để đối chiếu,
nên đăng nhập bằng tài khoản giáo viên mà khai `role: "admin"` sẽ nhận 403 chứ không leo được quyền.

**Upload ảnh qua bốn hàng rào** — chặn dung lượng trước khi đọc byte nào, lọc phần mở rộng, đối chiếu
*magic bytes* để file `<script>` đội đuôi `.png` vẫn bị chặn, và tên file lưu xuống đĩa do server sinh
nên tên người dùng gửi lên không chạm tới hệ thống file. SVG cố ý không nằm trong danh sách cho phép:
nó chứa được thẻ `script`, mà ảnh phục vụ từ cùng origin với API là một lỗ XSS hoàn chỉnh.

**Phân biệt 404 và 403 có chủ đích** — tài nguyên ngoài phạm vi trả 404 để người gọi không dò được nó
có tồn tại hay không; còn thứ đang hiển thị trên màn hình mà không có quyền sửa thì trả 403, vì báo
"không tìm thấy" ở đó chỉ khiến người dùng tưởng hệ thống hỏng.

**Hai unique index mang ngữ nghĩa nghiệp vụ** chứ không chỉ giữ toàn vẹn: `(ExamId, StudentId)` làm
việc giao bài idempotent, `(AttemptId, QuestionId)` cho lưu nháp ngữ nghĩa MERGE — autosave gọi lại
không sinh dòng đáp án trùng.

---

## 1. Yêu cầu môi trường

| Thành phần | Phiên bản | Ghi chú |
|---|---|---|
| .NET SDK | **10.0** trở lên | `dotnet --version` phải in ra `10.x` |
| PostgreSQL | **14** trở lên | Chạy local hoặc dùng Neon / Supabase |
| dotnet-ef | 10.x | Cài một lần: `dotnet tool install --global dotnet-ef` |

Không cần Docker. Không cần SQL Server.

---

## 2. Tạo database

Tạo một database rỗng tên `eduquiz`. EF Core sẽ dựng bảng ở bước sau.

```bash
# Cách 1 — dùng createdb
createdb -U postgres eduquiz

# Cách 2 — dùng psql
psql -U postgres -c "CREATE DATABASE eduquiz;"
```

Chuỗi kết nối mặc định cho máy lập trình nằm ở `src/EduQuiz.Api/appsettings.Development.json`:

```
Host=localhost;Port=5432;Database=eduquiz;Username=postgres;Password=postgres
```

Sửa lại `Username` / `Password` cho khớp máy bạn. Không commit mật khẩu thật lên git —
khi deploy thì đặt biến môi trường `ConnectionStrings__DefaultConnection` (hoặc `DATABASE_URL` dạng URI,
API tự chuyển đổi sang định dạng Npgsql).

---

## 3. Migration

Chạy từ thư mục gốc solution (`EduQuiz.Backend/`).

```bash
# Tạo migration đầu tiên (chỉ làm một lần, hoặc mỗi khi đổi entity)
dotnet ef migrations add InitialCreate \
  --project src/EduQuiz.Infrastructure \
  --startup-project src/EduQuiz.Api \
  --output-dir Persistence/Migrations

# Áp lược đồ xuống database
dotnet ef database update \
  --project src/EduQuiz.Infrastructure \
  --startup-project src/EduQuiz.Api
```

> Trên Windows PowerShell, thay `\` cuối dòng bằng backtick `` ` `` hoặc viết gộp một dòng.

API **cố ý không tự chạy migration lúc khởi động**: đó là một bước riêng của quy trình deploy.
Hai tiến trình khởi động cùng lúc mà cùng sửa lược đồ là chuyện không ai muốn gỡ.

Chưa chạy `database update` mà đã bật API thì log hiện một dòng cảnh báo về việc nạp dữ liệu demo
thất bại — API vẫn khởi động, chỉ là chưa có bảng nào.

---

## 4. Chạy API

```bash
dotnet run --project src/EduQuiz.Api
```

| Địa chỉ | Nội dung |
|---|---|
| `http://localhost:5002` | API, đường dẫn gốc `/api` |
| `http://localhost:5002/swagger` | Swagger UI (chỉ có ở Development) |
| `http://localhost:5002/health` | Kiểm tra tiến trình còn sống + giờ server UTC |
| `http://localhost:5002/hubs/notifications` | SignalR |

Cổng đặt trong `src/EduQuiz.Api/Properties/launchSettings.json`.

**Nối với frontend:** trong repo [EduQuiz_FE](https://github.com/HuynhHoaa/EduQuiz_FE), file `.env`, đặt `VITE_API_BASE_URL` sao cho URL đầy đủ ra đúng
`http://localhost:5002/api/...` — tức là `http://localhost:5002/api` nếu hàm service truyền đường dẫn
dạng `/auth/login`, hoặc `http://localhost:5002` nếu truyền `/api/auth/login`.
Origin `http://localhost:5173` (Vite dev server) đã nằm sẵn trong danh sách CORS của môi trường Development;
đổi cổng Vite thì phải thêm origin mới vào `Cors:AllowedOrigins` của `appsettings.Development.json`.

### Thử API trong Swagger

1. Mở `/swagger`, gọi `POST /api/auth/login` với một tài khoản demo ở mục 5.
2. Chép giá trị `accessToken` trong response.
3. Bấm nút **Authorize** ở góc trên bên phải, dán token vào (không cần gõ chữ `Bearer`), bấm Authorize.
4. Từ lúc đó mọi endpoint khác gọi được bình thường.

---

## 5. Ba tài khoản demo

Do `DbSeeder` tạo tự động khi chạy ở môi trường Development. Cả ba dùng **cùng một mật khẩu**:

```
eduquiz123
```

| Vai trò | Đăng nhập bằng | Họ tên |
|---|---|---|
| Quản trị viên | `huy.pq@eduquiz.vn` — hoặc username `huy.pq` | Phạm Quốc Huy |
| Giáo viên | `an.nv@eduquiz.vn` — hoặc username `an.nv` | Nguyễn Văn An |
| Học sinh | `lan.tt@student.edu.vn` — hoặc username `lan.tt`, mã HS `HS1001` | Trần Thị Lan |

Ô đăng nhập nhận cả ba dạng: email, username và mã học sinh.

> **Trước khi lên production phải xoá sạch ba tài khoản này và hằng số `DbSeeder.DemoPassword`**
> (§5 hợp đồng API). Chúng là dữ liệu demo, không phải dữ liệu hệ thống.

---

## 6. Cấu hình

| Khoá | Nguồn khi deploy | Bắt buộc |
|---|---|---|
| `ConnectionStrings:DefaultConnection` | `ConnectionStrings__DefaultConnection` hoặc `DATABASE_URL` | ✅ |
| `Jwt:SecretKey` | `Jwt__SecretKey` hoặc `JWT_SECRET_KEY` | ✅ |
| `Jwt:Issuer`, `Jwt:Audience`, `Jwt:ExpiryMinutes` | `Jwt__*` | có mặc định |
| `Cors:AllowedOrigins` | `Cors__AllowedOrigins__0`, `__1`, ... | ✅ nếu có frontend |

Hai điều dễ sai nhất:

- **`Jwt:SecretKey` trong `appsettings.Development.json` chỉ dùng cho dev.** Nó nằm trong git nên coi như
  công khai. Production phải đặt khoá riêng qua biến môi trường, dài tối thiểu 32 byte
  (`openssl rand -base64 48`). Thiếu hoặc quá ngắn thì API dừng ngay lúc khởi động, kèm thông điệp chỉ rõ chỗ sửa.
- **CORS không được dùng `*`.** SignalR cần `AllowCredentials`, mà trình duyệt từ chối thẳng cặp
  `AllowAnyOrigin` + `AllowCredentials`. Phải liệt kê origin cụ thể; riêng phần subdomain thì chấp nhận
  một dấu sao (`https://*.vercel.app`) để hợp với địa chỉ preview đổi theo từng lần deploy.

---

## 7. Tiến độ dựng backend

Tám bước theo §9 hợp đồng API, đúng thứ tự phụ thuộc. Sau mỗi bước, thay ruột nhóm hàm service
tương ứng ở frontend và kiểm tra bằng chính giao diện đã có.

| # | Bước | Nội dung | Trạng thái |
|:-:|---|---|:-:|
| 1 | **Auth + User** | JWT, BCrypt, 3 vai trò, `[Authorize(Roles=...)]`, 6 endpoint §4.1 | ✅ Xong |
| 2 | **Student + StudentGroup** | CRUD, unique index, phân quyền admin/teacher | ✅ Xong |
| 3 | **Question + AnswerOption** | CRUD, upload ảnh, phạm vi theo `SubjectId` | ✅ Xong |
| 4 | Exam + Blueprint | `blueprint-preview` trước, `POST /exams` sau | ⬜ Chưa |
| 5 | Assignment + ExamPaper | Giao bài, sinh đề, `randomizePerStudent` | ⬜ Chưa |
| 6 | Attempt | Bắt đầu, lưu nháp, nộp, **chấm ở server** | ⬜ Chưa |
| 7 | Result + Dashboard | Truy vấn tổng hợp | ⬜ Chưa |
| 8 | Notification + SignalR + BackgroundService | Sáu sự kiện §7, tự nộp bài khi hết giờ | 🔸 Hub đã dựng, sự kiện chưa |

**Đã chạy thật — 5 controller:**

| Controller | Endpoint |
|---|---|
| `AuthController` | `login` · `refresh` · `me` (GET/PUT) · `change-password` · `logout` |
| `StudentsController` | CRUD học sinh · `reset-password` · đổi trạng thái · kiểm tra trùng định danh |
| `StudentGroupsController` | CRUD nhóm lớp · thêm/xoá thành viên theo lô · `assignable-students` |
| `QuestionsController` | CRUD câu hỏi · đổi trạng thái ẩn/hiện · `summary` |
| `UploadsController` | Upload ảnh câu hỏi qua bốn hàng rào kiểm tra |

Bước 2 kiểm chứng bằng bộ 58 phép thử HTTP, bước 3 bằng bộ 53 phép — tất cả đều đạt.

`/api/auth/refresh` hiện trả **401**: hệ thống chưa có bảng lưu refresh token nên `login` không phát hành
chuỗi nào, mà cấp access token dựa trên một chuỗi server không kiểm chứng được là tự mở cửa sau.
Endpoint đứng sẵn để bước sau chỉ phải thay phần ruột.

---

## 8. Quy ước code

- File-scoped namespace, namespace theo đúng thư mục.
- `required` cho thuộc tính bắt buộc, `init` cho DTO bất biến.
- Enum lưu xuống DB dưới dạng **chuỗi** (`.HasConversion<string>()`) — đọc thẳng trong database dễ hơn nhiều.
- Mọi mốc thời gian là `DateTime` **UTC**, cột `timestamptz`, ra JSON dưới dạng ISO 8601 có hậu tố `Z`.
- Khoá chính `int` tự tăng.
- Mapping thủ công bằng extension method, **không dùng AutoMapper**.
- Comment tiếng Việt, thưa, chỉ ở chỗ quyết định nghiệp vụ không hiển nhiên.
- Lỗi nghiệp vụ đi qua `Result` + `ErrorCode`; exception chỉ dành cho sự cố thật.
  Phép dịch `ErrorCode` → mã HTTP nằm ở đúng một chỗ: `ApiControllerBase.ToStatusCode`.

## 9. Tài liệu liên quan

| File | Nội dung |
|---|---|
| [docs/EduQuiz_API_Contract.md](docs/EduQuiz_API_Contract.md) | 68 endpoint, 6 quy tắc bảo mật, lược đồ database, quy tắc nghiệp vụ |
| [docs/EduQuiz_BE_Tech_Structure.md](docs/EduQuiz_BE_Tech_Structure.md) | Cấu trúc thư mục từng project |
| [docs/EduQuiz_Requirement.md](docs/EduQuiz_Requirement.md) | Nghiệp vụ |
| [docs/EduQuiz_Workflow_Deploy.md](docs/EduQuiz_Workflow_Deploy.md) | Lộ trình và hạ tầng |
