# Anthill — Backend

Anthill là hệ thống thi trắc nghiệm trực tuyến dành cho một trường học hoặc một trung tâm dạy thêm.
Quản trị viên (hiệu trưởng, giám đốc trung tâm) lập lớp, cấp tài khoản học sinh và phân công giáo viên.
Giáo viên soạn ngân hàng câu hỏi theo môn, ra đề theo tỷ lệ độ khó và giao cho lớp mình dạy. Học sinh
làm bài có giờ đếm ngược, bài được chấm ở server. Tài khoản, lớp và quyền hạn thuộc về tổ chức chứ không
thuộc về từng người dùng — đó là chỗ Anthill khác một ứng dụng trắc nghiệm để tự ôn luyện.

Repo này là API. Giao diện nằm ở [Anthill_FE](https://github.com/HuynhHoaa/Anthill_FE)
(React 19 + TypeScript) và gọi thẳng các endpoint ở đây.

.NET 10, ASP.NET Core, EF Core 10 + Npgsql (PostgreSQL), JWT Bearer, BCrypt (work factor 12),
FluentValidation, SignalR, Swagger. Kiểm thử bằng xUnit + FluentAssertions và một bộ kiểm HTTP viết
bằng bash + curl.

Bốn project theo Clean Architecture: tầng Application không tham chiếu EF Core hay ASP.NET Core, nên
logic chấm điểm, hạn giờ và lịch lớp là hàm thuần có unit test riêng. Repository và UnitOfWork là cái
giá của ranh giới đó: service cần transaction và khoá dòng (`LockForUpdateAsync`) mà không chạm vào
`DbContext`. Lỗi nghiệp vụ đi qua `Result`/`ErrorCode`, dịch sang HTTP ở một chỗ
(`ApiControllerBase.ToStatusCode`).

```
src/Anthill.Domain/          entity, enum — không tham chiếu project nào
src/Anthill.Application/     DTO, interface, service nghiệp vụ, validator, hàm thuần trong Common/
src/Anthill.Infrastructure/  EF Core, repository, UnitOfWork, JWT, BCrypt, lưu file, seed
src/Anthill.Api/             controller, middleware, SignalR hub, tiến trình nền, cấu hình
tests/Anthill.Tests/         unit test
tests/api-checks/            bộ kiểm HTTP chạy trên API thật
```

## Trạng thái

Đủ luồng từ đầu tới cuối: quản trị viên lập lớp và cấp tài khoản, giáo viên soạn câu hỏi, ra đề theo tỷ
lệ độ khó và giao bài, học sinh làm bài có hạn giờ, bài được chấm ở server, kết quả và thông báo thời
gian thực. Có thêm xoá mềm học sinh và khung giờ riêng cho từng lớp. Chưa deploy.

Giới hạn đã biết:

- `POST /api/auth/refresh` luôn trả 401: chưa có bảng lưu refresh token nên `login` chưa trả refresh
  token. Access token sống 120 phút (480 ở Development), đủ cho một bài 45–60 phút.
- Ảnh câu hỏi ghi xuống đĩa của máy chạy API (`wwwroot/uploads`); nền tảng nào xoá đĩa khi khởi động
  lại thì cần một cài đặt `IFileService` khác.
- `security_stamp` và trạng thái tài khoản mới được đối chiếu ở `GET/PUT /api/auth/me`, `change-password`
  và lúc mở kết nối SignalR. Các endpoint còn lại (làm bài, kết quả, thông báo...) vẫn nhận token cũ của
  tài khoản vừa bị khoá hay vừa đưa vào diện xoá cho tới khi token hết hạn.
- Lưu nháp sau khi hết giờ trả 410, nhưng nếu `AutoSubmitExpiredAttemptsService` đã chốt bài trước gói
  nháp thì trả 409 (lượt không còn ở trạng thái đang làm). Client phải xử lý cả hai mã như "hết giờ".

## API

68 endpoint REST trong 13 controller cộng một hub SignalR; danh sách đầy đủ ở
[hợp đồng API](docs/Anthill_API_Contract.md) và Swagger. Mọi endpoint yêu cầu JWT trừ
`POST /api/auth/login` và `POST /api/auth/refresh`.

Hub `/hubs/notifications` nhận token qua query `access_token` và đẩy sự kiện khi có bài được giao,
có bài nộp, có bài tự nộp, và khi số thông báo chưa đọc thay đổi.

## Vài quyết định thiết kế đáng xem

**Chấm ở server, từ bản chụp.** Lúc sinh đề, câu hỏi và đáp án được sao vào `ExamPaperQuestion` /
`ExamPaperAnswer`; client chỉ gửi mã đáp án học sinh đã chọn, điểm do hàm thuần `AttemptGrading.Grade`
tính trên bản chụp, nên sửa câu hỏi sau này không đổi điểm bài đã chấm.

**Khung giờ theo lớp, đáp án giữ lại tới khi mọi lớp làm xong.** Mỗi lượt giao có thể mang cặp
`OpenAt`/`CloseAt` riêng; `AssignmentSchedule.Resolve` là quy tắc duy nhất quyết định khung giờ có hiệu
lực (repository có một bản dịch sang SQL để tính MAX giờ đóng). Đổi khung giờ thì lượt đã bắt đầu giữ
nguyên khung cũ. Điểm mở ngay khi chấm nếu giáo viên bật `showResultAfterSubmit`, còn đáp án từng câu
chỉ mở khi đã qua giờ đóng muộn nhất trong mọi lượt giao của đề, hoặc hạn nộp muộn nhất của lượt còn
đang làm nếu muộn hơn — lớp làm sáng thứ Ba không chuyền được đáp án cho lớp chiều thứ Năm. Response trả
kèm `answersVisibleAt` để giao diện báo giờ mở. (`AssignmentSchedule`,
`ExamAssignmentRepository.GetAnswersReleaseAtAsync`, `ResultMappingExtensions.ResolveAnswerVisibility`)

**Hạn giờ theo đồng hồ server, tự nộp khi hết giờ.** `AttemptDeadline` tính hạn nộp lúc bắt đầu và kẹp
vào giờ đóng có hiệu lực của lượt giao (giờ riêng của lớp nếu có, không thì giờ đóng đề); đồng hồ máy
học sinh chỉ để vẽ đếm ngược. `AutoSubmitExpiredAttemptsService` quét mỗi 30 giây, tối đa 50 bài một
lượt, chấm theo `ExpiresAt` trong database nên quét trễ không làm sai điểm, và mỗi vòng tự bắt lỗi để
một lần mất kết nối không làm chết cả tiến trình.

**Lưu nháp và giao bài chịu được gửi lặp, gửi sai thứ tự.** Mỗi gói nháp mang `clientSeq`; server bỏ
gói có số không lớn hơn `LastAppliedSeq` và trả lại số đang giữ để client chỉnh bộ đếm. Unique
`(AttemptId, ExamPaperQuestionId)` giữ mỗi câu một dòng đáp án; lưu nháp, nộp bài và chốt bài hết giờ
đều khoá dòng lượt làm bài bằng `SELECT ... FOR UPDATE` (`ExamAttemptRepository.LockForUpdateAsync`).
Giao bài dựa trên unique `(ExamId, StudentId)`: service bỏ qua học sinh đã có bài và báo lại số lượng
bị bỏ qua; hai request đua nhau thì index chặn request đến sau, và `ExceptionMiddleware` đổi SQLSTATE
23505 thành 409 thay vì 500.

**404 và 403 có chủ đích.** Tài nguyên ngoài phạm vi trả 404 để không dò được id nào có thật. Câu hỏi
của đồng nghiệp cùng môn — đang hiện trên màn hình nhưng không được sửa — trả 403 kèm lời giải thích,
vì báo "không tìm thấy" ở đó chỉ khiến giáo viên tưởng hệ thống hỏng. (`QuestionService.LoadWritableAsync`)

**Đổi trạng thái tài khoản thì phiên cũ bị cắt ở các cửa chính.** Mỗi JWT mang `security_stamp`; khoá,
đổi mật khẩu, cấp lại mật khẩu và đưa vào diện xoá đều xoay giá trị này, còn mở khoá và đăng xuất cố ý
không xoay. `GET/PUT /api/auth/me` và `change-password` đối chiếu stamp và trạng thái với database rồi
trả 401/403; hub từ chối kết nối mới. Các endpoint khác chưa đối chiếu (xem giới hạn ở trên).
(`AuthService.GuardAccountState`, `NotificationHub.OnConnectedAsync`)

Ngoài ra:

- Claim `role` sinh từ cột `User.Role`; trường `role` client gửi lúc đăng nhập chỉ để đối chiếu, sai
  là 403. (`AuthService.LoginAsync`)
- Upload qua bốn lớp kiểm: giới hạn 4 MB, lọc phần mở rộng, đối chiếu magic bytes với phần mở rộng, và
  tên file trên đĩa là GUID do server sinh. SVG cố ý bị loại vì chứa được `<script>`; ảnh được phục vụ
  với Content-Type lấy từ bảng khai sẵn theo phần mở rộng, kèm `X-Content-Type-Options: nosniff`.
  (`LocalFileStorageService`, `ApplicationBuilderExtensions.UseUploadedFiles`)
- Xoá mềm có lý do, ẩn danh sau 60 ngày. Chỉ quản trị viên yêu cầu xoá được, lý do từ 10 ký tự; tài
  khoản chờ xoá không đăng nhập được và khôi phục được trong 60 ngày. Hết hạn,
  `PurgeDeletedStudentsService` (quét sáu giờ một lần) gọi `StudentAnonymizer`: dữ liệu cá nhân bị thay
  hoặc xoá, còn lượt làm bài và điểm giữ nguyên để bảng điểm của lớp không thiếu dòng.

## Chạy trên máy

Cần .NET SDK 10, PostgreSQL (local hoặc một dịch vụ như Neon) và dotnet-ef 10.x. Chạy từ thư mục gốc repo
(nơi có `Anthill.Backend.sln`):

```bash
dotnet tool install --global dotnet-ef   # một lần
createdb -U postgres anthill
dotnet ef database update --project src/Anthill.Infrastructure --startup-project src/Anthill.Api
dotnet run --project src/Anthill.Api     # http://localhost:5002/swagger
```

Chuỗi kết nối ở `src/Anthill.Api/appsettings.Development.json` dùng `postgres/postgres`; sửa nếu máy bạn
khác. Đăng nhập bằng một tài khoản demo bên dưới.

| Địa chỉ | Nội dung |
|---|---|
| `http://localhost:5002/api/...` | API |
| `http://localhost:5002/swagger` | Swagger UI, chỉ có ở Development |
| `http://localhost:5002/health` | tiến trình còn sống + giờ server UTC |
| `http://localhost:5002/hubs/notifications` | SignalR |

`launchSettings.json` còn có profile `https` (thêm `https://localhost:7153`), `http-no-autosubmit` (tắt
tiến trình tự nộp bài) và `http-no-browser`, chọn bằng `--launch-profile`.

**Nối với frontend:** trong repo Anthill_FE, đặt `VITE_API_BASE_URL=http://localhost:5002` trong `.env`
(không kèm `/api`). Frontend chạy https thì API cũng phải chạy https. Môi trường Development đã cho phép
origin của Vite dev server (cổng 5173) và `vite preview` (cổng 4173), cả http lẫn https.

**Thử trong Swagger:** gọi `POST /api/auth/login` bằng một tài khoản demo, chép `accessToken`, bấm
**Authorize** và dán token vào (không cần gõ chữ `Bearer`).

## Tài khoản demo

`DbSeeder` nạp khi chạy ở Development, chạy lại bao nhiêu lần cũng không tạo trùng. Cả ba dùng chung
mật khẩu `anthill123`; ô đăng nhập nhận email, username hoặc mã học sinh.

| Vai trò | Đăng nhập bằng | Họ tên |
|---|---|---|
| Quản trị viên | `huy.pq@anthill.edu.vn` hoặc `huy.pq` | Phạm Quốc Huy |
| Giáo viên | `an.nv@anthill.edu.vn` hoặc `an.nv` | Nguyễn Văn An |
| Học sinh | `lan.tt` hoặc mã `HS1001` | Trần Thị Lan |

Seed còn tạo môn Khoa học tự nhiên (môn của giáo viên demo) và lớp "Lớp 8A" đã công bố, có giáo viên
và học sinh demo. Seed chỉ chạy ở Development nên database production không có ba tài khoản này. Nếu
từng chạy môi trường Development trên database thật thì phải xoá chúng.

## Cấu hình

| Khoá | Đặt khi deploy bằng | Mặc định / ghi chú |
|---|---|---|
| `ConnectionStrings:DefaultConnection` | `ConnectionStrings__DefaultConnection`, hoặc `DATABASE_URL` dạng URI | bắt buộc |
| `Jwt:SecretKey` | `Jwt__SecretKey` hoặc `JWT_SECRET_KEY` | bắt buộc, tối thiểu 32 byte |
| `Jwt:Issuer`, `Jwt:Audience`, `Jwt:ExpiryMinutes` | `Jwt__*` | `Anthill`, `Anthill.Client`, 120 phút |
| `Cors:AllowedOrigins` | `Cors__AllowedOrigins__0`, `__1`, ... | mặc định `https://anthill.vercel.app`, `https://*.vercel.app`; deploy thì đặt lại |
| `AutoSubmit:Enabled`, `IntervalSeconds`, `BatchSize` | `AutoSubmit__*` | `true`, 30, 50 |
| `FileStorage:MaxBytes` | `FileStorage__MaxBytes` | 4 MB |

`DATABASE_URL` (dạng `postgresql://user:pass@host/db`) chỉ được đọc khi `ConnectionStrings:DefaultConnection`
trống, và được tự chuyển sang chuỗi kết nối Npgsql. Hai điều dễ sai nhất:

- **`Jwt:SecretKey` trong `appsettings.Development.json` chỉ dùng cho dev.** Nó nằm trong git nên coi như
  công khai. Production phải đặt khoá riêng qua biến môi trường (`openssl rand -base64 48`); thiếu hoặc
  ngắn hơn 32 byte thì API dừng ngay lúc khởi động, kèm thông điệp chỉ rõ chỗ sửa.
- **CORS không được dùng `*`.** SignalR cần `AllowCredentials`, mà trình duyệt từ chối cặp
  `AllowAnyOrigin` + `AllowCredentials`. Phải liệt kê origin cụ thể; riêng phần subdomain thì chấp nhận
  một dấu sao (`https://*.vercel.app`) cho địa chỉ preview đổi theo từng lần deploy. Dấu sao chấp nhận
  được vì API không dùng cookie: token đi bằng header `Authorization` (hub dùng query `access_token`),
  nên CORS ở đây không phải lớp bảo mật. Mảng cấu hình bị đè theo từng chỉ số: chỉ đặt
  `Cors__AllowedOrigins__0` thì phần tử `__1` (dấu sao) vẫn còn. Không muốn nhận địa chỉ preview thì đặt
  cả `__1`, ví dụ trùng với `__0`.

## Kiểm thử

**Unit test** — `tests/Anthill.Tests/`, 221 test:

```bash
dotnet test
```

Chúng kiểm tra logic thuần, không cần database: chia blueprint, xáo đề có seed, vòng đời đề, hạn giờ và
điều kiện bắt đầu làm bài, chấm điểm, lịch theo lớp, cổng xem đáp án, ẩn danh hoá, luật validator.

**Bộ kiểm HTTP** — `tests/api-checks/`, 12 script, 676 phép kiểm; chi tiết từng bộ ở
[tests/api-checks/README.md](tests/api-checks/README.md). Chúng chạy trên API thật để kiểm thứ unit test
không với tới: 401/403/404, phân quyền theo vai trò, phạm vi dữ liệu, transaction. Có một bộ cho mỗi bước
từ `step2` tới `step8`, và năm bộ cho các phần thêm sau đó: lớp do quản trị viên lập, luồng từ lớp tới
học sinh, lịch theo lớp, xoá mềm, trạng thái tài khoản.

Cần API đang chạy, database đã migrate và đã có tài khoản demo (script đăng nhập bằng chúng), cùng
`bash`, `curl` và `python` (`lib.sh` dùng python để đọc JSON; trên Windows chạy trong Git Bash).
Địa chỉ mặc định là `http://localhost:5002`, đổi bằng biến `HOST`:

```bash
bash tests/api-checks/step6-attempts.sh
HOST=http://localhost:8080 bash tests/api-checks/step6-attempts.sh

# chạy cả 12 bộ
for s in tests/api-checks/*.sh; do [ "$(basename "$s")" = lib.sh ] || bash "$s"; done
```

Mỗi script tự dựng dữ liệu, dọn trong `trap EXIT` phần xoá được (đề đã có bài nộp được giữ lại, học
sinh chỉ bị khoá), và thoát với mã khác 0 khi có phép kiểm hỏng. Riêng `step5` tới `step8` còn để lại
lớp thử của chúng, vì bước dọn gỡ học sinh khỏi lớp và xoá lớp bằng token giáo viên, mà hai thao tác đó
chỉ quản trị viên được làm.

`step6-attempts.sh` có một quãng chờ 30 giây thật và `per-class-schedule.sh` chờ tới mốc giờ thật, để
chứng minh hạn giờ tính bằng đồng hồ server. Phép kiểm "lưu nháp sau khi hết giờ → 410" trong
`step6-attempts.sh` chập chờn: tiến trình tự nộp bài chốt lượt trước gói nháp thì API trả 409 (xem giới
hạn ở phần Trạng thái). Muốn chạy tất định thì bật API với `AutoSubmit__Enabled=false` hoặc profile
`http-no-autosubmit`.

## Phát triển

- Repo có sẵn 5 migration trong `src/Anthill.Infrastructure/Persistence/Migrations/`. Đổi entity thì
  thêm migration bằng `dotnet ef migrations add <Tên>` với cùng hai tham số `--project` /
  `--startup-project` ở trên và `--output-dir Persistence/Migrations`.
- API cố ý không tự chạy migration lúc khởi động: hai tiến trình khởi động cùng lúc sẽ cùng sửa lược đồ.
  Bật API khi chưa migrate thì log hiện cảnh báo kèm câu lệnh `dotnet ef database update`, và API vẫn
  khởi động.
- Mọi mốc thời gian là `DateTime` UTC, cột `timestamptz`, ra JSON dạng ISO 8601 có hậu tố `Z`.
- Enum lưu xuống DB dưới dạng chuỗi (`.HasConversion<string>()`).
- Mapping thủ công bằng extension method, không dùng AutoMapper.

## Tài liệu

| File | Nội dung |
|---|---|
| [docs/Anthill_API_Contract.md](docs/Anthill_API_Contract.md) | Hợp đồng API: 68 endpoint, quy tắc bảo mật, lược đồ database, quy tắc nghiệp vụ |
| [docs/Anthill_BE_Tech_Structure.md](docs/Anthill_BE_Tech_Structure.md) | Cấu trúc thư mục từng project |
| [docs/Anthill_Requirement.md](docs/Anthill_Requirement.md) | Đặc tả nghiệp vụ |
| [docs/Anthill_Workflow_Deploy.md](docs/Anthill_Workflow_Deploy.md) | Quy trình làm việc và phương án deploy |

Đặc tả nghiệp vụ viết từ trước khi có vai trò quản trị viên nên chỉ mô tả hai vai trò giáo viên và học
sinh. Hành vi hiện tại lấy code và hợp đồng API làm chuẩn.
