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
  token. Access token sống 480 phút, dài hơn bài kiểm tra dài nhất được phép (300 phút); API từ chối khởi động nếu
  `Jwt:ExpiryMinutes` thấp hơn 330. Khoá hoặc đổi mật khẩu vẫn cắt phiên ngay nhờ `SecurityStamp`, nên token
  sống lâu không làm mất khả năng thu hồi.
- Ảnh câu hỏi ghi xuống đĩa của máy chạy API (`wwwroot/uploads`); nền tảng nào xoá đĩa khi khởi động
  lại thì cần một cài đặt `IFileService` khác.
- Sổ kết nối SignalR (`UserConnectionRegistry`) nằm trong bộ nhớ của một tiến trình, nên lệnh cắt lúc
  xoay stamp chỉ với tới kết nối do chính tiến trình đó giữ. Chạy nhiều bản API sau bộ cân bằng tải thì
  phải kèm backplane và phát lệnh cắt qua đó; tới lúc ấy, kết nối ở bản khác vẫn bị chặn ở lời gọi hub
  kế tiếp và khi token hết hạn.
- Lưu nháp, nạp lại phiên và vào lại bài (`POST /api/exam-attempts`) sau khi hết giờ đều trả 410, dù
  `AutoSubmitExpiredAttemptsService` có chốt bài trước hay không; 409 chỉ dành cho bài học sinh tự nộp
  trước giờ. Chỗ còn để ngỏ là khung giờ đồng hồ: hai instance API lệch nhau vài mili giây thì một lời gọi
  đúng sát `ExpiresAt` có thể nhận 409 thay vì 410 (`AttemptClosure`).

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

**Đổi trạng thái tài khoản thì phiên cũ bị cắt trên mọi endpoint.** Mỗi JWT mang `security_stamp`;
khoá, đổi mật khẩu, cấp lại mật khẩu và đưa vào diện xoá đều xoay giá trị này, còn mở khoá và đăng xuất
cố ý không xoay. Sau khi kiểm chữ ký, `OnTokenValidated` tra tài khoản theo khoá chính (chỉ Id, Status,
SecurityStamp, không theo dõi thay đổi) và từ chối token khi tài khoản không còn, stamp đã đổi, hoặc
trạng thái không phải Active — áp cho mọi request có token, kể cả negotiate và kết nối SignalR. Trên
một WebSocket đã mở, mọi kết nối của tài khoản bị cắt ngay lúc stamp xoay, bộ lọc hub kiểm lại phiên ở
mỗi lời gọi phương thức và cắt kết nối kèm lý do, còn kết nối tự đóng khi token hết hạn. Token bị từ chối nhận **401** kèm lý do cụ thể, vì 401 là thứ khiến
frontend đưa người dùng về trang đăng nhập; mở khoá không làm token cũ sống lại. Vì vậy 401 chỉ dành cho
phiên đã chết: gõ sai mật khẩu hiện tại khi đổi mật khẩu là 400 gắn vào ô `currentPassword`. Riêng
`GET/PUT /api/auth/me` và `change-password` giữ hợp đồng cũ: tài khoản bị khoá hay chờ xoá nhận 403 kèm
câu nói rõ phải liên hệ ai. Đổi lại là thêm một truy vấn theo khoá chính trên mỗi request có token và
mỗi lời gọi hub; cố ý không cache, vì cache có hạn là mở lại đúng khoảng trễ này. (`SessionValidation`,
`AccountSessionReader`, `ServiceCollectionExtensions.OnTokenValidated`, `ReportsBlockedAccountAttribute`,
`SessionRevalidationHubFilter`, `UserConnectionRegistry`)

**Kết nối realtime chết ngay khi phiên chết.** Sự kiện server đẩy xuống không đi qua phương thức hub nào,
nên trước đây một WebSocket mở trước lúc khoá vẫn nhận thông báo cá nhân — và cả tiến độ nộp bài nếu là
giáo viên đang theo dõi một đề — cho tới lần gọi hub kế tiếp hoặc tới khi token hết hạn (tối đa 480 phút).
Hub ghi từng kết nối vào một sổ theo tài khoản, và mọi lối xoay `SecurityStamp` gọi cắt ngay sau khi
transaction commit (cắt trước commit thì client nối lại kịp đọc stamp cũ). Sổ nằm trong bộ nhớ một tiến
trình — giới hạn ghi ở đầu file lớp đó. (`UserConnectionRegistry`, `IUserConnectionCloser`)

**Lưu một entity chỉ ghi những cột thật sự đổi.** `GenericRepository.Update` không gọi `DbSet.Update`
trên entity DbContext đang theo dõi: hàm đó đánh dấu mọi cột là Modified, tức là ghi lại nguyên bản đã
đọc từ đầu request. Một lần `PUT /api/auth/me` nạp tài khoản ngay trước lúc quản trị viên bấm khoá từng
ghi lại `Status`, `SecurityStamp` và `PasswordHash` cũ, làm lệnh khoá biến mất. `session-revocation.sh`
bắn hàng loạt `PUT /api/auth/me` song song với lệnh khoá và lệnh cấp lại mật khẩu, lặp nhiều vòng, để
giữ điều này.

**Ghi lên bản tài khoản đã cũ thì 409, không ghi đè.** Bảng `Users` dùng cột hệ thống `xmin` của PostgreSQL
làm concurrency token: request đọc tài khoản rồi lưu sau khi người khác đã ghi dòng đó nhận 409. Riêng các
thao tác quyết định phiên (khoá, cấp lại hay đổi mật khẩu, đưa vào diện xoá, khôi phục) giữ khoá dòng
`FOR UPDATE` từ lúc đọc tới lúc commit, nên một tài khoản bắn `PUT /api/auth/me` liên tục cũng không làm
lệnh khoá trượt; tiến trình xoá dữ liệu khoá lô của nó bằng `SKIP LOCKED` để không đè lên tài khoản đang
được khôi phục. (`UserConfiguration`, `UserRepository.LockForUpdateAsync`, `ExceptionMiddleware`)

**Giáo viên chỉ thấy học sinh mình phụ trách.** `GET /api/students` của giáo viên chỉ trả các em ở lớp họ
được phân công và lớp đã công bố — cùng tập với danh sách lớp của họ. Lọc theo lớp người khác, lớp nháp
hay "chưa phân nhóm" ra trang rỗng chứ không báo lỗi. `GET /api/students/{id}` mở thêm cho em đã nhận
một đề do chính giáo viên đó tạo, để liên kết từ trang kết quả vẫn mở được hồ sơ em đã chuyển lớp hoặc
đã bị xoá dữ liệu cá nhân; ngoài hai trường hợp đó là 404. Quy tắc là một biểu thức duy nhất, ghép thẳng
vào truy vấn EF nên lọc trong SQL, và unit test chạy chính biểu thức ấy. Quản trị viên vẫn thấy mọi tài
khoản. (`TeacherStudentScope`, `UserRepository.GetStudentsPagedForTeacherAsync`)

Ngoài ra:

- Claim `role` sinh từ cột `User.Role`; trường `role` client gửi lúc đăng nhập chỉ để đối chiếu, sai
  là 403. (`AuthService.LoginAsync`)
- Upload qua bốn lớp kiểm: giới hạn 4 MB, lọc phần mở rộng, đối chiếu magic bytes với phần mở rộng, và
  tên file trên đĩa là GUID do server sinh. Giới hạn dung lượng chặn ngay ở đầu vào, trước khi form được
  đọc, và trả 400 kèm thông điệp. SVG cố ý bị loại vì chứa được `<script>`; ảnh được phục vụ
  với Content-Type lấy từ bảng khai sẵn theo phần mở rộng, kèm `X-Content-Type-Options: nosniff`.
  (`UploadSizeLimitAttribute`, `LocalFileStorageService`, `ApplicationBuilderExtensions.UseUploadedFiles`)
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
| `http://localhost:5002/health` | tiến trình còn sống, database với tới được (503 khi không) + giờ server UTC |
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
mật khẩu `123456`; ô đăng nhập nhận email, username hoặc mã học sinh.

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
| `Jwt:Issuer`, `Jwt:Audience`, `Jwt:ExpiryMinutes` | `Jwt__*` | `Anthill`, `Anthill.Client`, 480 phút (tối thiểu 330) |
| `Cors:AllowedOrigins` | `Cors__AllowedOrigins__0`, `__1`, ... | không có mặc định; **bắt buộc** ở Production, thiếu thì API dừng lúc khởi động |
| `AutoSubmit:Enabled`, `IntervalSeconds`, `BatchSize` | `AutoSubmit__*` | `true`, 30, 50 |
| `FileStorage:MaxBytes` | `FileStorage__MaxBytes` | 4 MB |

`DATABASE_URL` (dạng `postgresql://user:pass@host/db`) chỉ được đọc khi `ConnectionStrings:DefaultConnection`
trống, và được tự chuyển sang chuỗi kết nối Npgsql. Hai điều dễ sai nhất:

- **`Jwt:SecretKey` trong `appsettings.Development.json` chỉ dùng cho dev.** Nó nằm trong git nên coi như
  công khai. Production phải đặt khoá riêng qua biến môi trường (`openssl rand -base64 48`); thiếu hoặc
  ngắn hơn 32 byte thì API dừng ngay lúc khởi động, kèm thông điệp chỉ rõ chỗ sửa.
- **CORS không được dùng `*`, và không có địa chỉ mặc định nào.** SignalR cần `AllowCredentials`, mà
  trình duyệt từ chối cặp `AllowAnyOrigin` + `AllowCredentials`, nên origin phải liệt kê cụ thể. Phần
  subdomain chấp nhận một dấu sao (`https://*.vercel.app`) để chạy thử các bản preview đổi địa chỉ theo
  từng lần deploy — nhưng chỉ vậy thôi: `appsettings.json` để danh sách rỗng, còn `https://*.vercel.app`
  là "mọi trang bất kỳ ai deploy lên Vercel". Ở Production, danh sách rỗng làm API dừng ngay lúc khởi
  động kèm tên biến phải đặt, và một mẫu có dấu sao còn sót lại thì hiện ra trong log dưới dạng lỗi.
  Danh sách rỗng ở file gốc cũng gỡ luôn cái bẫy của mảng cấu hình: mảng bị đè theo từng chỉ số, nên
  trước đây đặt `Cors__AllowedOrigins__0` mà quên `__1` là dấu sao vẫn còn hiệu lực.

## Kiểm thử

**Unit test** — `tests/Anthill.Tests/`, 481 test:

```bash
dotnet test
```

Chúng kiểm tra logic thuần, không cần database: chia blueprint, xáo đề có seed, vòng đời đề, luật sửa đề,
hạn giờ và điều kiện bắt đầu làm bài, chấm điểm, lịch theo lớp, cổng xem đáp án, ẩn danh hoá, phạm vi học
sinh của giáo viên, kiểm phiên đăng nhập, luật validator.

**Bộ kiểm HTTP** — `tests/api-checks/`, hơn 1100 phép kiểm trong 19 script; chi tiết từng bộ ở
[tests/api-checks/README.md](tests/api-checks/README.md). Chúng chạy trên API thật để kiểm thứ unit test
không với tới: 401/403/404, phân quyền theo vai trò, phạm vi dữ liệu, transaction. Có một bộ cho mỗi bước
từ `step2` tới `step8`, và các bộ cho phần thêm sau đó: lớp do quản trị viên lập, luồng từ lớp tới học
sinh, lịch theo lớp, luật sửa đề, xoá mềm, trạng thái tài khoản, thu hồi phiên, phạm vi học sinh của giáo
viên, nhắc hạn, đổi lịch, và các lỗi từ đợt rà soát (`audit-fixes.sh`).

Cần API đang chạy, database đã migrate và đã có tài khoản demo (script đăng nhập bằng chúng), cùng
`bash`, `curl` và `python` (`lib.sh` dùng python để đọc JSON, `session-revocation.sh` dùng nó làm một
client WebSocket tối giản bằng thư viện chuẩn; trên Windows chạy trong Git Bash).

Riêng `exam-edit-rules.sh` cần thêm `psql`: hệ thống không có endpoint tạo giáo viên, nên script chép
tài khoản giáo viên demo thẳng trong database để có một đồng nghiệp cùng môn và một giáo viên chưa gán
môn. Script tìm `psql` trong `PATH`, không có thì lấy bản đứng cuối theo tên thư mục ở
`C:\Program Files\PostgreSQL\*\bin`.
Thông tin kết nối lấy từ `PGHOST`/`PGPORT`/`PGDATABASE`/`PGUSER`/`PGPASSWORD` nếu `PGPASSWORD` đã đặt;
không thì đọc `ConnectionStrings:DefaultConnection` trong user-secrets của `src/Anthill.Api`
(`dotnet user-secrets list`). Thiếu cả hai thì phép kiểm đó báo hỏng chứ không lặng lẽ bỏ qua.

Địa chỉ mặc định là `http://localhost:5002`, đổi bằng biến `HOST`:

```bash
bash tests/api-checks/step6-attempts.sh
HOST=http://localhost:8080 bash tests/api-checks/step6-attempts.sh

# chạy cả 15 bộ
for s in tests/api-checks/*.sh; do [ "$(basename "$s")" = lib.sh ] || bash "$s"; done
```

Mỗi script tự dựng dữ liệu, dọn trong `trap EXIT` phần xoá được (đề đã có bài nộp được giữ lại, học
sinh chỉ bị khoá), và thoát với mã khác 0 khi có phép kiểm hỏng.

`step6-attempts.sh` có một quãng chờ 30 giây thật và `per-class-schedule.sh` chờ tới mốc giờ thật, để
chứng minh hạn giờ tính bằng đồng hồ server. Phép kiểm "lưu nháp sau khi hết giờ → 410" trong
`step6-attempts.sh` cho cùng kết quả dù tiến trình tự nộp bài có chốt lượt trước gói nháp hay không, nên
không cần tắt tiến trình đó khi chạy script.

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

Chỗ nào tài liệu lệch với code thì code là chuẩn.
