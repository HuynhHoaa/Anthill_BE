# Anthill — Hợp Đồng API

Tài liệu này được **rút ra từ tầng service của frontend** trước khi viết backend, không phải thiết kế trên giấy: mỗi hàm trong 9 file `src/services/` của repo frontend tương ứng một endpoint, và các quy tắc nghiệp vụ được chép ra từ chính thân hàm.

Backend đã được dựng theo tài liệu này, và frontend đã chuyển mọi hàm service sang gọi `apiClient` — **chữ ký hàm giữ nguyên nên không trang nào phải sửa**. Tài liệu được cập nhật theo code; chỗ nào lệch thì code là chuẩn.

Ngày lập: 06/09/2026 · Nguồn: 9 service, ~50 hàm · Hiện tại: **69 endpoint** (kể cả `POST /api/exams/{id}/duplicate`)

> Đọc kèm: `Anthill_Requirement.md` (nghiệp vụ), `Anthill_BE_Tech_Structure.md` (kiến trúc BE), `Anthill_Workflow_Deploy.md` (lộ trình và hạ tầng).

---

## 1. Quy ước chung

| Hạng mục | Quy ước |
|---|---|
| Base URL | `/api` · biến môi trường FE: `VITE_API_BASE_URL` |
| Xác thực | JWT Bearer. **Mọi endpoint đều yêu cầu token trừ `POST /api/auth/login`** |
| Đường dẫn | Tiếng Anh, số nhiều, kebab-case: `/api/exam-attempts`, `/api/student-groups` |
| Kiểm tra sống | `GET /health` (ngoài `/api`, không cần token): **200** `{ status, database, serverTimeUtc }` khi mở được kết nối database, **503** (`status = degraded`) khi không, giới hạn 5 giây. Dùng cho reverse proxy và cho lời gọi đánh thức API ngủ |
| Phân trang | Query `page` (1-based), `pageSize`. Trả `{ items, total, page, pageSize }` |
| Ngày giờ | **ISO 8601 UTC** ở mọi request và response |
| Lỗi | `{ message: string, errors?: Record<string,string[]> }` — FE đọc trường `message` |
| Tìm kiếm | Không dấu, không phân biệt hoa thường (xem §6) |

### Điểm chuyển đổi cần biết trước khi tích hợp

**Ngày giờ.** FE hiện dùng chuỗi `'DD/MM/YYYY HH:mm'` theo giờ máy người dùng. API phải dùng ISO 8601 UTC. Chỗ chuyển đổi đặt ở `src/lib/apiClient.ts` — parse khi nhận, format khi gửi. Không sửa rải rác trong page.

**Ảnh.** API **không nhận data URI** — ảnh phải upload trước qua `POST /api/uploads/images` (multipart, trường `file`, PNG/JPEG/GIF/WebP, tối đa `FileStorage:MaxBytes` = 4 MB, loại ảnh xét theo đuôi file lẫn magic bytes) rồi gửi URL kèm câu hỏi. Response `{ url, fileName, sizeBytes, contentType }`, trong đó `url` là **đường dẫn tương đối** (`/uploads/202609/<guid>.png`) do origin của API phục vụ. FE upload ngay khi giáo viên chọn ảnh (`ImageUploader` → `uploadService`), khoá nút lưu câu hỏi trong lúc ảnh đang tải, lưu nguyên đường dẫn tương đối, và chỉ ghép với `VITE_API_BASE_URL` lúc hiển thị (`resolveMediaUrl` trong `src/lib/apiClient.ts`); URL tuyệt đối được giữ nguyên.

**Phân trang lách.** Năm chỗ ở FE đang truyền `pageSize` rất lớn (500) để lấy trọn tập dữ liệu rồi tự tính thống kê. BE nên chặn trên `pageSize` tối đa 200 và cung cấp endpoint thống kê riêng.

---

## 2. Ba vai trò

> **Admin sở hữu TỔ CHỨC** (tài khoản học sinh, lớp, xếp học sinh vào lớp, phân công giáo viên).
> **Teacher sở hữu VIỆC DẠY** (ngân hàng câu hỏi, đề, giao bài, kết quả) trong những lớp đã công bố mà mình được phân công.
> **Student làm bài và xem kết quả của chính mình.**

| Khu vực | admin | teacher | student |
|---|:---:|:---:|:---:|
| Tài khoản học sinh (tạo/sửa/khoá/reset mật khẩu, yêu cầu xoá/khôi phục) | ✅ | đọc, chỉ học sinh mình phụ trách (§4.3) | — |
| Lớp, thành viên, công bố, phân công giáo viên | ✅ | đọc, chỉ lớp đã công bố mình được phân công (§4.4) | — |
| Ngân hàng câu hỏi | đọc | ✅ | — |
| Bài kiểm tra, giao bài | — | ✅ | — |
| Kết quả toàn lớp | — | ✅ | — |
| Làm bài, kết quả của mình | — | — | ✅ |
| Hồ sơ, thông báo của mình | ✅ | ✅ | ✅ |

---

## 3. Sáu quy tắc bảo mật không được vi phạm

Đây là phần quan trọng nhất của tài liệu. Sáu điều dưới đây hiện **đang đúng ở FE chỉ vì mọi thứ chạy trong một trình duyệt**; lên API thật mà không làm lại ở server thì mỗi điều là một lỗ hổng.

**① Đáp án đúng không được rời server trong lúc làm bài.**
`attemptService` cố ý ghi đè `isCorrect = false` trước khi trả câu hỏi xuống. Ở BE phải dùng **hai DTO tách biệt**: `AttemptAnswerOptionDto { id, content, imageUrl }` cho lúc làm bài (không khai báo trường `isCorrect`, kể cả gán false) và `ResultAnswerOptionDto { id, content, imageUrl, isCorrect }` cho lúc xem kết quả. Học sinh mở DevTools là thấy toàn bộ response — gán false không đủ, phải **không có trường đó**.

**② Chấm điểm ở server, luôn luôn.**
`submitAttempt` hiện tính điểm ngay trong FE rồi ghi vào mock. Client chỉ được gửi `{ questionId: answerId }`; server đối chiếu `AnswerOption.IsCorrect` trong DB, đếm, tính điểm. **Không bao giờ nhận `score`/`correctCount` từ client.**

**③ Hạn giờ tính bằng đồng hồ server.**
`expiresAt = startedAt + durationMinutes`, lưu vào bảng Attempt lúc bắt đầu, mọi kiểm tra so với `DateTime.UtcNow`. Client đổi giờ máy chỉ làm lệch cái đồng hồ trên màn hình, không mở rộng được thời gian thật.
Và: **phải có background job** quét các attempt `inprogress` quá hạn để tự chấm — vì học sinh tắt trình duyệt thì không ai gọi `submit` cả.

**④ Danh tính suy từ JWT, không nhận từ client.**
`listMyExams`, `listMyResults`, `getStudentDashboard`, `startAttempt`, `saveAnswers`, `submitAttempt` — không endpoint nào nhận `studentId`. Kiểm tra quyền sở hữu trên từng `attemptId`/`assignmentId` trước khi làm bất cứ việc gì.

**⑤ Role đọc từ DB, không tin trường `role` client gửi khi login.**
Mock hiện lấy hồ sơ theo đúng role client chọn (`getDefaultProfile(payload.role)`). Bê nguyên logic đó lên BE là **leo thang quyền tức thì** — gửi `role: 'admin'` là thành admin. Trường `role` trong request chỉ dùng để đối chiếu, hoặc bỏ hẳn.

**⑥ Quyền xem đáp án thực thi ở server.**
Quy tắc: giáo viên sở hữu đề luôn xem được. Học sinh xem **điểm** (`score`, `maxScore`, `correctCount`) khi *(attempt là của mình)* **và** *(status = graded)* **và** *(exam.showResultAfterSubmit = true)*. **Đáp án từng câu** (`correctAnswerId`, `isCorrect`) cần thêm điều kiện thứ tư: *(mọi lượt giao của đề đã qua giờ đóng có hiệu lực — lịch riêng của lớp nếu có, ngược lại lịch chung của đề)* — một đề giao cho nhiều lớp với nhiều khung giờ, và em lớp làm trước không được cầm đáp án chuyền cho lớp chưa thi. Khi không đủ điều kiện, response phải **không chứa** các trường đó — không phải ẩn ở giao diện. `GET /api/results/{attemptId}` trả kèm `answersVisibleAt` (mốc đáp án sẽ mở; vắng mặt khi đã mở, hoặc khi không có mốc nào để hẹn).

---

## 4. Danh sách endpoint

### 4.1 Xác thực và hồ sơ

| Method | Path | Vai trò | Mô tả |
|---|---|---|---|
| POST | `/api/auth/login` | — | Đăng nhập bằng email (teacher/admin) hoặc username/mã HS (student). Trả JWT + hồ sơ |
| POST | `/api/auth/refresh` | — | Cấp lại access token. **Bổ sung** — cần thiết vì học sinh làm bài 45–60 phút, hết token giữa chừng là mất bài |
| GET | `/api/auth/me` | tất cả | Hồ sơ người đang đăng nhập, suy từ JWT |
| PUT | `/api/auth/me` | tất cả | Cập nhật thông tin liên hệ của chính mình. **Học sinh không tự đổi `fullName` và `email`** (quản trị viên giữ danh tính trên bảng điểm): gửi giá trị khác trả **400** kèm `errors.fullName` / `errors.email`, gửi lại đúng giá trị đang có thì hợp lệ. `email` tối đa 256 ký tự |
| POST | `/api/auth/change-password` | tất cả | Đổi mật khẩu, xác minh mật khẩu hiện tại. Sai mật khẩu hiện tại → **400** kèm `errors.currentPassword`; 401 chỉ khi phiên đã chết |
| POST | `/api/auth/logout` | tất cả | Thu hồi phiên (chỉ cần khi dùng refresh token) |

`LoginResponse`: `{ accessToken, tokenType: 'Bearer', expiresAt, refreshToken?, user: UserProfileDto }`
JWT claims tối thiểu: `sub` = userId, `role`, `name`, `subject_id` (giáo viên), `security_stamp`.

### 4.2 Thông báo

| Method | Path | Vai trò | Mô tả |
|---|---|---|---|
| GET | `/api/notifications` | tất cả | Thông báo của chính mình, mới nhất trước. Query `unreadOnly`, `page`, `pageSize` |
| GET | `/api/notifications/unread-count` | tất cả | Số chưa đọc cho badge chuông |
| PATCH | `/api/notifications/{id}/read` | tất cả | Đánh dấu một thông báo đã đọc |
| POST | `/api/notifications/mark-all-read` | tất cả | Đánh dấu tất cả đã đọc |

> **`link` của thông báo nộp bài** (`examSubmitted`, `autoSubmitted`) trỏ tới trang kết quả của chính lượt đó: `/giao-vien/ket-qua/{attemptId}` cho giáo viên, `/hoc-sinh/ket-qua/{attemptId}` cho học sinh.

> **Khác biệt lớn so với FE:** mock lọc thông báo theo `audience === role` (dùng chung cho cả vai trò). Ở BE phải gắn với **`RecipientUserId`** cụ thể. `markAsRead` hiện tìm theo id trên toàn mảng — bất kỳ ai cũng đọc được thông báo của người khác; BE trả **404** (không phải 403) khi id không thuộc người gọi, để không dò được id hợp lệ.

### 4.3 Học sinh

| Method | Path | Vai trò | Mô tả |
|---|---|---|---|
| GET | `/api/students` | admin, teacher | Danh sách có phân trang, lọc theo từ khoá / nhóm / trạng thái. Giáo viên chỉ nhận học sinh ở lớp đã công bố mà mình được phân công |
| GET | `/api/students/{id}` | admin, teacher | Hồ sơ một học sinh. Giáo viên: học sinh lớp mình dạy, hoặc đã nhận một đề do chính mình tạo; ngoài ra 404 |
| POST | `/api/students` | admin | Tạo tài khoản kèm mật khẩu ban đầu |
| PUT | `/api/students/{id}` | admin | Cập nhật hồ sơ, nhóm, trạng thái. **Không nhận mật khẩu** |
| PATCH | `/api/students/{id}/status` | admin | Khoá / mở khoá — chỉ nhận `active` hoặc `locked`; hai trạng thái xoá đi qua hai endpoint riêng bên dưới |
| POST | `/api/students/{id}/reset-password` | admin | **Bổ sung** — cấp lại mật khẩu khi học sinh quên |
| POST | `/api/students/{id}/request-deletion` | admin | **Bổ sung** — đưa vào diện chờ xoá (`pendingDeletion`), lý do bắt buộc (≥ 10 ký tự). Không đăng nhập được, token cũ chết ngay. Sau 60 ngày tiến trình nền ẩn danh hoá, gỡ khỏi lớp và chuyển sang `deleted`; lượt làm bài và điểm giữ nguyên |
| POST | `/api/students/{id}/restore` | admin | **Bổ sung** — khôi phục tài khoản đang chờ xoá, trả về **đúng trạng thái trước khi vào diện xoá**: em đang bị khoá vẫn `locked` (cột `StatusBeforeDeletion`); bản ghi chờ xoá từ trước khi có cột này về `active`. Đã ẩn danh hoá rồi thì **409** |
| GET | `/api/students/availability` | admin | **Bổ sung** — kiểm tra email/username/code đã dùng chưa, thay cho việc FE tải 500 bản ghi về so sánh tại chỗ |

`Student.status`: `active` · `locked` · `pendingDeletion` · `deleted`.

> **Phạm vi đọc của giáo viên.** Danh sách là "lớp tôi đang dạy": học sinh có `GroupId` thuộc một lớp **đã công bố** mà giáo viên **được phân công** — đúng tập lớp `GET /api/student-groups` trả cho họ. Bộ lọc `groupId` / `status` / `search` chạy bên trong phạm vi đó: `groupId` của lớp khác hay lớp nháp, và `groupId=0` ("chưa phân nhóm"), đều ra trang rỗng, không lỗi. Hồ sơ rộng hơn danh sách một vế: mở được cả học sinh đã nhận một đề do chính giáo viên đó tạo, vì kết quả thuộc về đề chứ không thuộc về lớp hiện tại — em chuyển lớp hay đã bị xoá dữ liệu cá nhân thì liên kết từ bảng kết quả vẫn phải mở được. Ngoài phạm vi trả **404** cùng thông điệp với id không tồn tại. Admin không bị giới hạn.

### 4.4 Nhóm lớp

| Method | Path | Vai trò | Mô tả |
|---|---|---|---|
| GET | `/api/student-groups` | admin, teacher | Danh sách lớp kèm sĩ số. Query `search`, `status` (`draft` · `published` · `all`, mặc định `all`; giá trị khác trả 400). Giáo viên chỉ nhận lớp **đã công bố** mà mình **được phân công** |
| GET | `/api/student-groups/{id}` | admin, teacher | Chi tiết một lớp. Lớp ngoài phạm vi của giáo viên trả **404** |
| POST | `/api/student-groups` | admin | Lập lớp. Lớp mới ra đời ở dạng **nháp** — giáo viên chưa thấy |
| PUT | `/api/student-groups/{id}` | admin | Đổi tên và mô tả |
| DELETE | `/api/student-groups/{id}` | admin | Xoá lớp **rỗng** (còn học sinh hoặc tài khoản chờ xoá thì **409**). Không kéo theo tài khoản học sinh |
| GET | `/api/student-groups/{id}/students` | admin, teacher | Thành viên của lớp |
| GET | `/api/student-groups/{id}/assignable-students` | admin | Học sinh có thể xếp vào lớp (gồm cả em đang ở lớp khác — thêm vào là chuyển lớp) |
| POST | `/api/student-groups/{id}/students` | admin | Xếp **nhiều** học sinh vào lớp trong một lần gọi. Trả `added`, `skippedAlreadyMember`, `skippedNotFound`, `skippedRetired` |
| DELETE | `/api/student-groups/{id}/students/{studentId}` | admin | Gỡ khỏi lớp (đặt `GroupId = NULL`) |
| POST | `/api/student-groups/{id}/publish` | admin | **Bổ sung** — công bố lớp nháp; từ đây giáo viên được phân công mới thấy lớp. Đã công bố rồi thì **409** |
| POST | `/api/student-groups/{id}/unpublish` | admin | **Bổ sung** — đưa lớp về nháp (lùi lại khi công bố nhầm). Không đụng tới học sinh hay bài đã giao. Vốn là nháp thì **409** |
| GET | `/api/student-groups/{id}/assignable-teachers` | admin | **Bổ sung** — giáo viên có thể phân công, kèm trạng thái tài khoản |
| POST | `/api/student-groups/{id}/teachers` | admin | **Bổ sung** — phân công **nhiều** giáo viên phụ trách lớp, báo lại từng loại bị bỏ qua |
| DELETE | `/api/student-groups/{id}/teachers/{teacherId}` | admin | **Bổ sung** — gỡ giáo viên khỏi lớp. Bài đã giao gắn với học sinh nên không bị ảnh hưởng |

> Lớp thuộc về **tổ chức**, không thuộc về giáo viên nào: quản trị viên lập lớp, xếp học sinh, phân công một hoặc nhiều giáo viên (bảng `StudentGroupTeacher`) rồi công bố. Giáo viên chỉ đọc và giao bài cho lớp đã công bố mình được phân công.
> Đổi tên lớp không phải ghi lại bản ghi học sinh nào — học sinh chỉ giữ khoá ngoại `GroupId`, tên luôn đọc qua JOIN.
> `POST .../students` nhận mảng: FE từng gọi lặp từng em rồi tự đếm thành công/thất bại. Gộp thành batch để tránh N request.

### 4.5 Ngân hàng câu hỏi

| Method | Path | Vai trò | Mô tả |
|---|---|---|---|
| GET | `/api/questions` | teacher, admin | Danh sách, lọc theo từ khoá / chủ đề / độ khó / trạng thái |
| GET | `/api/questions/topics` | teacher, admin | Tập chủ đề đang có (cho dropdown và gợi ý nhập) |
| GET | `/api/questions/summary` | teacher, admin | Thống kê ngân hàng. Gộp 3 lời gọi FE đang bắn song song |
| GET | `/api/questions/{id}` | teacher, admin | Một câu hỏi kèm đáp án |
| POST | `/api/questions` | teacher, admin | Tạo. Đáp án lồng trong body, BE cấp id |
| PUT | `/api/questions/{id}` | teacher, admin | Sửa toàn bộ |
| DELETE | `/api/questions/{id}` | teacher, admin | Xoá |
| PATCH | `/api/questions/{id}/status` | teacher, admin | Bật / ẩn nhanh |
| POST | `/api/uploads/images` | teacher, admin | Upload ảnh, trả URL |

> **Sửa câu hỏi:** FE **thay cả mảng đáp án** chứ không sửa tại chỗ, có chú thích rõ *"để các bài làm đã chấm giữ nguyên đáp án cũ"*. BE phải giữ đúng ý này — bằng versioning hoặc bằng snapshot vào attempt (xem §6).
> **Xoá câu hỏi:** FE xoá thẳng, không kiểm tra câu đó đã nằm trong đề hay bài làm nào. BE **bắt buộc** phải kiểm.

### 4.6 Bài kiểm tra

| Method | Path | Vai trò | Mô tả |
|---|---|---|---|
| GET | `/api/exams` | teacher, admin | Danh sách, tìm kiếm + lọc theo vòng đời |
| GET | `/api/exams/{id}` | teacher, admin | Chi tiết một đề |
| POST | `/api/exams` | teacher | Tạo đề theo blueprint. **Đề không chứa danh sách câu hỏi cụ thể** |
| PUT | `/api/exams/{id}` | teacher | Sửa toàn bộ cấu hình. Chưa em nào bắt đầu làm: sửa được mọi trường, bản đề đã sinh cho các lượt giao được **sinh lại** theo cấu hình mới (ngân hàng không đủ câu thì từ chối, không đổi gì); đề không còn lượt giao nào (đã thu hồi hết) thì bản đề cũ bị bỏ, lần giao sau sinh theo cấu hình mới. Đã có em bắt đầu (`blueprintFrozen`): đổi số câu, ba tỷ lệ, chủ đề, điểm mỗi câu, thời lượng, tuỳ chọn trộn hoặc đưa về nháp trả **409**; tên, mô tả, lịch và `showResultAfterSubmit` vẫn sửa được. Điểm đã chấm không bao giờ đổi |
| PATCH | `/api/exams/{id}/schedule` | teacher | **Chỉ** đổi lịch mở/đóng — hẹp và an toàn hơn PUT |
| DELETE | `/api/exams/{id}` | teacher | Xoá đề kèm toàn bộ bản ghi giao bài và bản đề đã sinh. Đã có học sinh nộp bài thì **409** — để đề tự đóng theo lịch, kết quả của các em vẫn tra cứu được |
| POST | `/api/exams/blueprint-preview` | teacher | **Trái tim của tính năng.** Nhận cấu hình đang gõ dở, trả số câu cần / số câu có / hợp lệ hay không |
| GET | `/api/exams/{id}/blueprint` | teacher | Blueprint của đề đã lưu |
| POST | `/api/exams/{id}/publish` | teacher | **Bổ sung** — chuyển nháp sang xuất bản. FE hiện làm việc này bằng cách gửi PUT với `lifecycle` bỏ trống |
| POST | `/api/exams/{id}/duplicate` | teacher | **Bổ sung** — nhân bản đề của chính mình thành một bản **nháp** mới, cùng cấu hình, tên thêm " (bản sao)", chưa giao cho ai. Lối ra khi cấu hình đã khoá. Đề của giáo viên khác trả 404; giáo viên chưa được gán môn trả 403 như khi tạo đề |
| GET | `/api/exams/{id}/assignments` | teacher | Danh sách học sinh đã giao kèm trạng thái làm bài. Mỗi dòng mang khung giờ thật của em (`openAt`, `closeAt`, `hasScheduleOverride`, `assignedViaGroupId`); `expired` tính theo khung giờ đó |
| POST | `/api/exams/{id}/assignments` | teacher | Giao bài cho học sinh lẻ và/hoặc cả nhóm, tuỳ chọn kèm khung giờ riêng (`openAt` + `closeAt`, phải chứa trọn `durationMinutes`, nếu không 400). Gửi lại một lớp kèm khung giờ là sửa giờ cho em chưa bắt đầu (`scheduleUpdatedCount`, `scheduleKeptStartedCount`) |
| DELETE | `/api/exams/{examId}/assignments/{assignmentId}` | teacher | **Bổ sung** — thu hồi bài đã giao. Giao nhầm lớp là chuyện thường, mà endpoint giao bài lại cố ý idempotent nên không có đường lùi |

### 4.7 Làm bài (phía học sinh)

| Method | Path | Vai trò | Mô tả |
|---|---|---|---|
| GET | `/api/my/exams` | student | Bài được giao cho chính mình |
| GET | `/api/my/exams/{assignmentId}` | student | Chi tiết một mục, cho màn giới thiệu trước khi vào thi |
| POST | `/api/exam-attempts` | student | Bắt đầu **hoặc làm tiếp** một lượt. Idempotent |
| GET | `/api/exam-attempts/{attemptId}` | student | Nạp lại phiên đang dở sau F5 hoặc đổi thiết bị |
| GET | `/api/exam-attempts/{attemptId}/time` | student | **Bổ sung** — đồng bộ đồng hồ với giờ server |
| PUT | `/api/exam-attempts/{attemptId}/answers` | student | Lưu nháp. Ngữ nghĩa **MERGE** theo `questionId`, không thay cả bộ |
| POST | `/api/exam-attempts/{attemptId}/submit` | student | Nộp bài và **chấm điểm ở server** |

**409 hay 410 ở ba endpoint làm bài.** 410 nghĩa là "thời gian đã hết", 409 nghĩa là "trạng thái hiện tại không cho làm việc này":

| Tình huống | `POST /exam-attempts` | `GET /exam-attempts/{id}` | `PUT .../answers` |
|---|---|---|---|
| Đề nháp hoặc chưa tới giờ mở | 409 | — | — |
| Khung giờ đã đóng mà em chưa từng vào | 410 | — | — |
| Lượt còn hạn | 200 (làm tiếp, không cấp thêm giờ) | 200 | 200 |
| Lượt đã quá `expiresAt` hoặc đã bị chốt vì hết giờ — dù tiến trình nền hay chính request này chốt | 410 | 410 | 410 |
| Em đã tự nộp trước giờ | 409 | 409 | 409 |

`POST .../submit` sau khi đã chốt (vì hết giờ hoặc em đã nộp) trả lại đúng kết quả cũ với 200 — nộp bài là idempotent.

### 4.8 Kết quả và tổng quan

| Method | Path | Vai trò | Mô tả |
|---|---|---|---|
| GET | `/api/results` | teacher | Bảng kết quả, lọc chéo theo đề / học sinh / nhóm / trạng thái |
| GET | `/api/results/stats` | teacher | Các ô số liệu đầu trang, nhận cùng bộ lọc với bảng kết quả: `totalAttempts`, `gradedCount`, `inProgressCount` (lượt đang làm dở, chưa nộp), `averageScore`, `highestScore`, `completionRate` |
| GET | `/api/results/{attemptId}` | teacher, student | Chi tiết một lượt làm bài, soi từng câu |
| GET | `/api/results/{attemptId}/summary` | student | **Bổ sung** — cho màn "Đã nộp bài". Hiện trang đó gọi `getAttemptDetail`, tức kéo về toàn bộ câu hỏi chỉ để hiện một vòng tròn điểm |
| GET | `/api/my/results` | student | Lịch sử làm bài của chính mình. **Chỉ gồm lượt đã nộp hoặc đã chấm**; bài đang làm dở nằm ở `GET /api/my/exams` |
| GET | `/api/dashboard/teacher` | teacher | Endpoint **gộp** cho trang tổng quan |
| GET | `/api/dashboard/student` | student | Endpoint **gộp** cho trang tổng quan |

> Hai endpoint dashboard **nên gộp**, không nên tách: FE hiện gọi đúng một lần và render cả trang. Tách ra sẽ thành 6 truy vấn độc lập cho một màn hình.
> `GET /api/results/stats` hiện không nhận bộ lọc, nên số liệu là của toàn hệ thống và không đổi theo bảng bên dưới — FE phải ghi chú *"Toàn hệ thống"* để tránh hiểu nhầm. Cân nhắc cho nhận bộ lọc.

---

## 5. Quy tắc nghiệp vụ backend bắt buộc

Ngoài sáu quy tắc bảo mật ở §3, đây là những phép tính và kiểm tra hiện nằm ở FE mà backend phải làm lại.

### Exam Blueprint — nghiệp vụ lõi của đề tài

**Làm tròn phải khớp từng đơn vị.** Công thức lấy nguyên từ `calculateRequiredCounts`:
```
easy   = clamp(round(total * e / 100), 0, total)
medium = min(total - easy, max(0, round(total * m / 100)))
hard   = total - easy - medium        // mức Khó nhận phần dư
```
Mức Khó nhận phần dư nên **tổng luôn bằng `totalQuestions`**. Trong .NET phải dùng `MidpointRounding.AwayFromZero` để khớp `Math.round` của JavaScript, nếu không sẽ lệch 1 câu ở các mốc `.5`.

**BE tự tính, không tin số client gửi.** Client chỉ gửi `(totalQuestions, 3 tỷ lệ, topic)`.

**Validate đề:** `title` không rỗng sau trim · `1 ≤ durationMinutes ≤ 300` · `totalQuestions ≥ 1` · `pointPerQuestion > 0` · `closeAt > openAt` · nếu `lifecycle ≠ 'draft'` thì tổng ba tỷ lệ phải bằng 100 và ngân hàng phải đủ câu.
Bổ sung một kiểm tra FE chỉ cảnh báo ở UI: **`durationMinutes` không được lớn hơn khoảng cách `openAt..closeAt`** — làm bài 90 phút trong cửa sổ mở 30 phút là vô nghĩa.

### Sinh đề và cờ `randomizePerStudent`

Cờ này hiện là **điều khiển giả** — service nhận rồi bỏ qua. BE phải làm thật:

| Cờ | Hành vi | Hạt giống |
|---|---|---|
| Tắt | Một bộ câu duy nhất cho cả đề, mọi học sinh giống nhau | `hash(examId)` |
| Bật | Mỗi học sinh một bộ câu riêng | `hash($"{examId}:{assignmentId}")` |

Hạt giống theo **`assignmentId`**, không phải `attemptId` — vì học sinh F5 hoặc mất mạng rồi vào lại phải thấy đúng bộ câu cũ.

`shuffleQuestions` và `shuffleAnswers` phải nằm **trong cùng bước sinh đề**, dùng chung nguồn ngẫu nhiên đã seed (Fisher-Yates), rồi lưu `Ordinal` xuống DB. Không được xáo lại ở tầng trả response.

### Snapshot nội dung câu hỏi vào attempt

Lưu `content`, `imageUrl`, `point` và đáp án **tại thời điểm sinh đề**. Nếu giáo viên sửa hoặc xoá câu hỏi sau khi học sinh nộp, bài đã chấm không được đổi nội dung hay mất câu.

Mock hiện tra ngược `mockQuestions` theo id lúc chấm — điều đó có nghĩa **sửa một câu hỏi sẽ làm sai lệch điểm của các bài đã chấm trước đó**. Ở BE phải chấm từ snapshot.

### Giao bài

- **Khử trùng lặp ở BE** bằng `HashSet`: `targets = distinct(studentIds hợp lệ ∪ học sinh của groupIds)`. FE có tính số hiển thị nhưng đó chỉ để hiện lên màn hình.
- **Idempotent** theo cặp `(examId, studentId)` — unique index. Giao lại cho người đã có bài thì bỏ qua, giữ nguyên `status`/`attemptId`/`score`.
- **Kiểm tra phạm vi:** học sinh phải thuộc một lớp đã công bố mà giáo viên đó được phân công. Không cho giáo viên A giao bài cho lớp của giáo viên B bằng cách đoán id. Em ngoài phạm vi đếm vào `skippedOutOfScope`, xét **trước** trạng thái tài khoản. **Id không tồn tại (hoặc không phải tài khoản học sinh) đếm vào cùng con số đó** — không có trường riêng cho "không tìm thấy": hai con số tách rời cho phép giáo viên bắn từng id một rồi đọc xem id nào là học sinh thật của lớp người khác. Lớp cũng vậy: lớp không tồn tại và lớp của người khác cùng đếm vào `skippedOutOfScopeGroups`.
- **Học sinh bị khoá:** FE hiển thị nhãn "Đã khóa" nhưng vẫn cho chọn và vẫn tạo assignment. BE **bỏ qua** và trả `skippedLocked` để giáo viên biết. Tài khoản đang chờ xoá cũng bị bỏ qua nhưng đếm riêng vào `skippedRetired` — em đã rời trường thì không có gì để mở khoá. Tài khoản đã xoá hẳn đã bị gỡ khỏi lớp nên rơi vào `skippedOutOfScope`.
- **Chặn giao đề đang là nháp.** FE hoàn toàn không kiểm điều này.
- **Giao vào khung giờ đã đóng** vẫn được (để dành cho lần mở lại), nhưng không sinh thông báo và không bắn `ExamAssigned`: không còn gì để "hoàn thành trước hạn nộp".
- **`randomizePerStudent` chốt từ lần giao đầu tiên.** Đề đã có lượt giao mà request đổi cờ thì **409**; muốn đổi thì thu hồi hết rồi giao lại.

### Lưu nháp và nộp bài

- **Chống ghi đè ngược thứ tự:** autosave chạy mỗi 800ms; mạng chậm khiến gói cũ tới sau gói mới. Server giữ `lastAppliedSeq` cho mỗi attempt và bỏ qua request có `clientSeq` nhỏ hơn. **Không dùng timestamp client** làm mốc.
- **Hết giờ không nhận nháp mới:** `PUT answers` trả **410** khi `serverNow > expiresAt`, và vẫn 410 khi lượt đã bị chốt vì hết giờ từ trước (bảng 409/410 ở §4.7).
- **Validate đáp án gửi lên:** `questionId` phải thuộc đúng attempt đó, `answerId` phải thuộc đúng `questionId` đó. Trả **422** nếu lạ, không âm thầm bỏ qua.
- **`submittedAt` của bài hệ thống nộp thay là hạn của lượt** (`expiresAt`), không phải lúc tiến trình nền quét tới; nhờ vậy `durationUsedMinutes` không bao giờ vượt thời lượng của đề, kể cả khi em bỏ thi và bài được chốt hàng giờ sau. Bài em tự nộp trước giờ giữ giờ thật.
- **Nộp idempotent:** nộp lại cùng `attemptId` trả đúng kết quả cũ (200), không tạo bản ghi thứ hai. FE có cờ chống bấm hai lần, nhưng retry sau timeout mạng vẫn tới server hai lần — chỉ khoá ở DB mới chắc.

### Dữ liệu dẫn xuất — BE tính, không nhận từ client

| Trường | Định nghĩa |
|---|---|
| `Exam.assignedCount` | số assignment của đề |
| `Exam.submittedCount` | số assignment có status ∈ {submitted, graded} |
| `Exam.startedCount` | số assignment đã có lượt làm bài, ở mọi trạng thái |
| `Exam.blueprintFrozen` | `startedCount > 0` — cấu hình đề đã khoá, `PUT` chỉ còn đổi được tên, mô tả, lịch và `showResultAfterSubmit` |
| `Student.completedExams` | COUNT lượt có status ∈ {submitted, graded} |
| `Student.averageScore` | AVG(score) trên các lượt `graded`, làm tròn 1 chữ số |
| `Student.deletionScheduledAt` | `deletionRequestedAt` + 60 ngày, chỉ có khi `status = pendingDeletion`. Server tính sẵn — FE không tự cộng ngày |
| `AddMembersResponse.skippedRetired` | Số tài khoản chờ xoá hoặc đã xoá bị bỏ qua, không đưa vào lớp |
| `AssignExamResponse.skippedRetired` | Số tài khoản **đang chờ xoá** bị bỏ qua khi giao bài — không tính vào `skippedLocked`. Tài khoản đã xoá hẳn không có lớp nên nằm trong `skippedOutOfScope` |
| `AssignExamResponse.skippedOutOfScope` | Số id không giao được vì không thuộc lớp giáo viên phụ trách **hoặc** không tồn tại — cố ý gộp một con số để không dò được id nào là học sinh thật (không còn trường `skippedNotFound` ở endpoint này) |
| `studentStatus` trên `ExamAssignment`, `ResultSummary`, `AttemptDetail` | Trạng thái tài khoản của em (`active` · `locked` · `pendingDeletion` · `deleted`), cùng chuỗi với `Student.status`. Tách khỏi `status` — trường đó là trạng thái lượt làm bài |
| `StudentGroup.studentCount` | COUNT thành viên đang hoạt động hoặc đang khoá — không tính tài khoản chờ xoá |
| `Student.group` (tên) | dữ liệu JOIN, **chỉ đọc** — client gửi lên thì bỏ qua |

`lifecycle` của đề **không nên là cột lưu cứng**. Chỉ lưu `IsDraft` (hoặc `PublishedAt`), ba trạng thái còn lại tính theo giờ server mỗi lần đọc: `now < openAt` → scheduled · `openAt ≤ now ≤ closeAt` → open · `now > closeAt` → closed.

### Tài khoản

- Mật khẩu hash bằng **BCrypt** (work factor ≥ 11). FE hiện chấp nhận mọi mật khẩu không rỗng.
- **`status = 'locked'` chặn đăng nhập** (403) và vô hiệu hoá JWT còn hạn qua `security_stamp`. Khoá, đưa vào diện xoá, đổi mật khẩu và cấp lại mật khẩu đều xoay stamp; mở khoá không làm token cũ sống lại. Token của tài khoản không còn, stamp đã đổi, hoặc trạng thái khác `active` nhận **401** kèm `message` nói rõ lý do trên **mọi** endpoint và cả negotiate của hub — trừ `GET/PUT /api/auth/me` và `change-password`, nơi tài khoản bị khoá hay chờ xoá nhận **403** kèm lý do. Kết nối hub đang mở của tài khoản đó bị server cắt **ngay** lúc stamp xoay, không chờ client gọi gì; nếu vì lý do nào đó vẫn còn, lời gọi hub kế tiếp bị từ chối kèm cùng lý do, và mọi kết nối hub tự đóng khi token của nó hết hạn (xem §7).
- **Ghi đồng thời lên một tài khoản trả 409.** Bảng `Users` có concurrency token: request đọc tài khoản rồi lưu sau khi request khác đã ghi dòng đó nhận **409** kèm `message` ("Dữ liệu này vừa được thay đổi ở nơi khác…"), không ghi đè. Khoá, cấp lại mật khẩu, đổi mật khẩu, đưa vào diện xoá và khôi phục giữ khoá dòng từ lúc đọc tới lúc ghi, nên một loạt `PUT /api/auth/me` bắn song song không làm các thao tác đó trượt — bên nhận 409 là lần lưu hồ sơ trên bản đã cũ.
- **`change-password` sai mật khẩu hiện tại trả 400**, `errors: { currentPassword: ["Mật khẩu hiện tại không đúng."] }` — không phải 401. Client coi 401 trên request có token là phiên đã chết và đăng xuất người dùng; gõ sai một ô thì phiên vẫn còn nguyên.
- Unique index trên `email`, `username`, `code` — so sánh sau khi trim, không phân biệt hoa thường. Trả **409**, không phải 400. Khi sửa phải loại trừ chính bản ghi đang sửa.
- **Trim mọi chuỗi trước khi lưu và trước khi so trùng**, nếu không `'HS1001 '` và `'HS1001'` cùng lọt qua unique index.
- `createdAt` do server đặt = `DateTime.UtcNow`, không nhận từ client.
- Mật khẩu do admin cấp: đặt cờ `mustChangePassword = true`, ép đổi ở lần đăng nhập đầu.
- **Xoá sạch `demoAccounts` và `DEMO_PASSWORD` trước khi lên production.**

### Giới hạn đầu vào

Những đầu vào dưới đây trả **400** kèm câu nói đúng ô sai, không bao giờ 500:

- **Ký tự NUL (U+0000)** trong bất kỳ chuỗi nào của body hay query: PostgreSQL không lưu và không so khớp được. `message = "Dữ liệu gửi lên có ký tự không hợp lệ."`
- **Mật khẩu chỉ gồm khoảng trắng** (tạo học sinh, đổi mật khẩu). Khoảng trắng đầu và cuối của một mật khẩu có chữ vẫn hợp lệ — mật khẩu không bị trim.
- **Phần tử `null` trong mảng** `answers` của câu hỏi hay của gói lưu nháp.
- **Độ dài khớp cột:** mô tả đề 1000 ký tự, email 256, đường dẫn ảnh của câu hỏi và của đáp án 512.

### Tìm kiếm tiếng Việt

Bỏ dấu và không phân biệt hoa thường, đúng như `normalizeText` của FE (NFD + bỏ dấu tổ hợp + `đ → d`). SQL Server: collation `Vietnamese_CI_AI`. PostgreSQL: `unaccent` + `lower`, có index biểu thức.

### Phân trang

Kẹp thay vì báo lỗi: `page < 1` → 1 · `page` vượt tổng số trang → trả trang cuối · `pageSize ≤ 0` → mặc định 10 · chặn trên 200. Response phải nói rõ `page` thực tế đã dùng — FE dựa vào trường đó để đồng bộ ngược lên URL.

---

## 6. Ánh xạ database

```
Subject   (Id, Name, Code)

User      (Id, FullName, Email, Username, PasswordHash, SecurityStamp,
           Role,          -- Admin | Teacher | Student
           SubjectId      FK -> Subject.Id  NULL,     -- NULL với Admin và Student
           Code           NULL,                        -- mã học sinh
           GroupId        FK -> StudentGroup.Id NULL,
           Status,        -- Active | Locked | PendingDeletion | Deleted
           DeletionReason, DeletionRequestedAt, DeletionRequestedByUserId, DeletedAt,
           Phone, AvatarColor, CreatedAt, MustChangePassword)

StudentGroup (Id, Name, Description,
              CreatedByUserId FK -> User.Id,   -- quản trị viên đã lập lớp, chỉ để truy vết
              IsPublished, PublishedAt, CreatedAt)

StudentGroupTeacher (StudentGroupId FK, TeacherId FK -> User.Id, AssignedAt,
                     PRIMARY KEY (StudentGroupId, TeacherId))   -- một lớp nhiều giáo viên

Question  (Id, Content, ImageUrl, Topic, Difficulty, DefaultPoint, Status,
           SubjectId       FK -> Subject.Id  NOT NULL,  -- PHẠM VI: ai được thấy
           CreatedByUserId FK -> User.Id     NOT NULL,  -- QUY TRÁCH: ai được sửa
           Visibility)                                   -- Shared | Private

AnswerOption (Id, QuestionId FK, Content, ImageUrl, IsCorrect, Ordinal)

Exam      (Id, Title, Description, Topic, DurationMinutes, TotalQuestions,
           EasyPercentage, MediumPercentage, HardPercentage, PointPerQuestion,
           ShuffleQuestions, ShuffleAnswers, RandomizePerStudent,
           ShowResultAfterSubmit, OpenAt, CloseAt, IsDraft, PublishedAt,
           CreatedByUserId FK -> User.Id)

ExamAssignment (Id, ExamId FK, StudentId FK, AssignedAt,
                AssignedViaGroupId FK -> StudentGroup.Id NULL,
                OpenAt NULL, CloseAt NULL,        -- khung giờ riêng của lớp; NULL = theo lịch đề
                UNIQUE (ExamId, StudentId))

ExamPaper      (Id, ExamId FK, AssignmentId FK NULL)   -- NULL = đề chung cho cả lớp
ExamPaperQuestion (Id, ExamPaperId FK, QuestionId FK, Ordinal,
                   ContentSnapshot, ImageUrlSnapshot, Point)
ExamPaperAnswer   (Id, ExamPaperQuestionId FK, ContentSnapshot, IsCorrect, Ordinal)

ExamAttempt (Id, AssignmentId FK, StartedAt, ExpiresAt, SubmittedAt,
             Status, Score, MaxScore, CorrectCount, AutoSubmitted, LastAppliedSeq)

StudentAnswer (Id, AttemptId FK, ExamPaperQuestionId FK, SelectedAnswerId NULL)

Notification (Id, RecipientUserId FK, Type, Title, Message, Link,
              TargetType, TargetId, IsRead, CreatedAt)
```

### Vì sao `Question` cần **cả hai** cột phân định

| Cột | Trả lời câu hỏi | Dùng cho |
|---|---|---|
| `SubjectId` | Câu này thuộc ngân hàng nào? | Lọc danh sách, chặn giáo viên môn khác |
| `CreatedByUserId` | Ai chịu trách nhiệm? | Hiển thị "soạn bởi", giới hạn quyền ghi, audit |

Ngân hàng câu hỏi **dùng chung theo tổ bộ môn**: giáo viên Toán A thấy được câu của giáo viên Toán B. Truy vấn phạm vi:

```sql
SELECT * FROM Question q
WHERE q.SubjectId = @currentUserSubjectId
  AND (q.Visibility = 'Shared' OR q.CreatedByUserId = @currentUserId)
  AND q.Status = 'Active';
```

Quyền ghi kiểm riêng ở tầng service: `q.CreatedByUserId == currentUserId || currentUser.Role == Admin`.
Cờ `Visibility = 'Private'` để người tạo giữ lại phần đề thi thật mà vẫn đóng góp phần còn lại vào kho chung — rủi ro thật của ngân hàng dùng chung là **lộ đề** giữa hai lớp học cùng buổi.

`Exam` **không cần** `SubjectId` — suy ra từ `CreatedByUserId → User.SubjectId`.

---

## 7. SignalR — `/hubs/notifications`

Hub phải `[Authorize]`, đọc JWT từ query string `access_token`. Mỗi kết nối tự vào group riêng của người dùng trong `OnConnectedAsync`. Kiểm tra quyền sở hữu exam trước khi cho giáo viên `JoinExamGroup(examId)`.

Phiên bị thu hồi (§5 Tài khoản) đóng bốn cửa: negotiate và request mở kết nối nhận 401; mọi kết nối **đang mở** của tài khoản bị cắt ngay lúc stamp xoay (khoá, cấp lại hay đổi mật khẩu, đưa vào diện xoá, ẩn danh hoá), nên không còn sự kiện nào đẩy xuống được; lời gọi phương thức hub kiểm lại phiên và nhận lỗi kèm lý do; và kết nối tự đóng khi token hết hạn. Lệnh cắt chỉ với tới kết nối do chính tiến trình đang chạy giữ — chạy nhiều bản API sau bộ cân bằng tải thì phải phát lệnh cắt qua backplane, còn ba cửa kia vẫn nguyên.

| Sự kiện | Ai nhận | Khi nào |
|---|---|---|
| `ExamAssigned` | student | Giáo viên giao bài, chỉ khi `notify = true` |
| `ExamSubmitted` | teacher | Học sinh nộp bài. Bắn cả vào group `exam:{examId}` để màn theo dõi tự tăng `submittedCount` |
| `AutoSubmitted` | teacher + student | Hết giờ, hệ thống tự nộp. **Hai payload khác nhau** cho hai vai trò |
| `DeadlineSoon` | student (chưa nộp) + teacher | Bài sắp đóng, do tiến trình nhắc hạn sinh. Học sinh nhận kèm một bản ghi thông báo; giáo viên nhận **mỗi em một sự kiện**, không kèm tên em và không có bản ghi thông báo (chỉ để màn theo dõi tự làm tươi) |
| `ExamUpdated` | student | Giáo viên đổi lịch hoặc thời lượng của đề đã giao; mỗi học sinh bị ảnh hưởng nhận một sự kiện kèm giờ mở/đóng **có hiệu lực của chính em**, và một bản ghi thông báo |
| `UnreadCountChanged` | tất cả | Bắn sau mỗi lần số chưa đọc đổi: giao bài, đổi giờ, nhắc hạn, đánh dấu đã đọc, và **nộp bài hay tự nộp** (cho giáo viên, và cho học sinh khi bị tự nộp). Thay thế trực tiếp cho việc poll `GET /notifications/unread-count` |

> **Sinh thông báo là việc của BE**, trong **cùng transaction** với hành động nghiệp vụ. FE hiện tự `unshift` vào mảng mock — lên BE thì client không được phép tạo thông báo.
> FE đọc `unread-count` **một lần** khi mở phiên và sau mỗi lần nối lại (để bù sự kiện lỡ trong lúc đứt); còn lại chuông chỉ nhận `UnreadCountChanged`. Client không cần gọi phương thức hub nào: mỗi kết nối tự vào group riêng của người dùng, và vào thêm `JoinExamGroup` thì giáo viên nhận `ExamSubmitted` hai lần.
> **Tự động nộp bài và cảnh báo sắp hết hạn phải do BackgroundService/Hangfire** thực hiện dựa trên `ExpiresAt` trong DB, không phải do client bấm giờ.

---

## 8. Không làm ở v1

- **Module thời khoá biểu** — đã hoãn, xem `Anthill_Workflow_Deploy.md`
- **Đăng ký tự do** — tài khoản học sinh chỉ do admin tạo
- **Nhiều loại câu hỏi** — MVP chỉ có trắc nghiệm một đáp án đúng
- **Ngân hàng câu hỏi toàn trường** (nhiều môn dùng chung) — `Visibility` hiện chỉ trong phạm vi một môn
- **Làm lại bài** — mỗi assignment một attempt duy nhất
- **Xuất báo cáo** (Excel/PDF)

---

## 9. Thứ tự dựng backend đề xuất

Theo đúng thứ tự phụ thuộc, mỗi bước chạy được và test được độc lập:

1. **Auth + User** — JWT, BCrypt, 3 vai trò, `[Authorize(Roles=...)]`. Không có bước này thì không test được gì khác.
2. **Student + StudentGroup** — CRUD, unique index, phân quyền admin/teacher
3. **Question + AnswerOption** — CRUD, upload ảnh, phạm vi theo `SubjectId`
4. **Exam + Blueprint** — `blueprint-preview` trước, `POST /exams` sau
5. **Assignment + ExamPaper** — giao bài, sinh đề, `randomizePerStudent`
6. **Attempt** — bắt đầu, lưu nháp, nộp, **chấm ở server**
7. **Result + Dashboard** — truy vấn tổng hợp
8. **Notification + SignalR + BackgroundService** — làm sau cùng vì phụ thuộc mọi thứ trên

Sau mỗi bước, thay ruột nhóm hàm service tương ứng ở FE và kiểm tra bằng chính giao diện đã có — đó là bộ test tích hợp sẵn sàng dùng ngay.
