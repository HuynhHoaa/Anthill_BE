# EduQuiz — Hợp Đồng API

Tài liệu này được **rút ra từ tầng service của frontend**, không phải thiết kế trên giấy. Frontend đã hoàn thiện và chạy bằng mock qua 9 file trong `EduQuiz.Frontend/src/services/`; mỗi hàm ở đó tương ứng một endpoint, và mọi quy tắc nghiệp vụ đang nằm sẵn trong thân hàm.

Cách dùng: dựng backend theo tài liệu này, rồi thay ruột các hàm service ở FE bằng lời gọi `apiClient` — **chữ ký hàm giữ nguyên nên không trang nào phải sửa**.

Ngày lập: 06/09/2026 · Nguồn: 9 service, ~50 hàm · Kết quả: **68 endpoint**

> Đọc kèm: `EduQuiz_Requirement.md` (nghiệp vụ), `EduQuiz_BE_Tech_Structure.md` (kiến trúc BE), `EduQuiz_Workflow_Deploy.md` (lộ trình và hạ tầng).

---

## 1. Quy ước chung

| Hạng mục | Quy ước |
|---|---|
| Base URL | `/api` · biến môi trường FE: `VITE_API_BASE_URL` |
| Xác thực | JWT Bearer. **Mọi endpoint đều yêu cầu token trừ `POST /api/auth/login`** |
| Đường dẫn | Tiếng Anh, số nhiều, kebab-case: `/api/exam-attempts`, `/api/student-groups` |
| Phân trang | Query `page` (1-based), `pageSize`. Trả `{ items, total, page, pageSize }` |
| Ngày giờ | **ISO 8601 UTC** ở mọi request và response |
| Lỗi | `{ message: string, errors?: Record<string,string[]> }` — FE đọc trường `message` |
| Tìm kiếm | Không dấu, không phân biệt hoa thường (xem §6) |

### Điểm chuyển đổi cần biết trước khi tích hợp

**Ngày giờ.** FE hiện dùng chuỗi `'DD/MM/YYYY HH:mm'` theo giờ máy người dùng. API phải dùng ISO 8601 UTC. Chỗ chuyển đổi đặt ở `src/lib/apiClient.ts` — parse khi nhận, format khi gửi. Không sửa rải rác trong page.

**Ảnh.** FE hiện đọc file bằng `FileReader.readAsDataURL` và nhét thẳng data URI vào payload. API **không nhận data URI** — phải upload trước qua `POST /api/uploads/images` rồi gửi URL.

**Phân trang lách.** Năm chỗ ở FE đang truyền `pageSize` rất lớn (500) để lấy trọn tập dữ liệu rồi tự tính thống kê. BE nên chặn trên `pageSize` tối đa 200 và cung cấp endpoint thống kê riêng.

---

## 2. Ba vai trò

> **Admin sở hữu TÀI KHOẢN** (danh tính, mật khẩu, khoá/mở).
> **Teacher sở hữu VIỆC DẠY** (nhóm lớp, ngân hàng câu hỏi, đề, giao bài, kết quả).
> **Student làm bài và xem kết quả của chính mình.**

| Khu vực | admin | teacher | student |
|---|:---:|:---:|:---:|
| Tài khoản học sinh (tạo/sửa/khoá/reset mật khẩu) | ✅ | đọc | — |
| Nhóm lớp và thành viên | đọc | ✅ | — |
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
Quy tắc: giáo viên sở hữu đề luôn xem được; học sinh chỉ xem khi *(attempt là của mình)* **và** *(status = graded)* **và** *(exam.showResultAfterSubmit = true)*. Khi không đủ điều kiện, response phải **không chứa** `correctAnswerId`/`isCorrect` — không phải ẩn ở giao diện.

---

## 4. Danh sách endpoint

### 4.1 Xác thực và hồ sơ

| Method | Path | Vai trò | Mô tả |
|---|---|---|---|
| POST | `/api/auth/login` | — | Đăng nhập bằng email (teacher/admin) hoặc username/mã HS (student). Trả JWT + hồ sơ |
| POST | `/api/auth/refresh` | — | Cấp lại access token. **Bổ sung** — cần thiết vì học sinh làm bài 45–60 phút, hết token giữa chừng là mất bài |
| GET | `/api/auth/me` | tất cả | Hồ sơ người đang đăng nhập, suy từ JWT |
| PUT | `/api/auth/me` | tất cả | Cập nhật thông tin liên hệ của chính mình |
| POST | `/api/auth/change-password` | tất cả | Đổi mật khẩu, xác minh mật khẩu hiện tại |
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

> **Khác biệt lớn so với FE:** mock lọc thông báo theo `audience === role` (dùng chung cho cả vai trò). Ở BE phải gắn với **`RecipientUserId`** cụ thể. `markAsRead` hiện tìm theo id trên toàn mảng — bất kỳ ai cũng đọc được thông báo của người khác; BE trả **404** (không phải 403) khi id không thuộc người gọi, để không dò được id hợp lệ.

### 4.3 Học sinh

| Method | Path | Vai trò | Mô tả |
|---|---|---|---|
| GET | `/api/students` | admin, teacher | Danh sách có phân trang, lọc theo từ khoá / nhóm / trạng thái |
| GET | `/api/students/{id}` | admin, teacher | Hồ sơ một học sinh |
| POST | `/api/students` | admin | Tạo tài khoản kèm mật khẩu ban đầu |
| PUT | `/api/students/{id}` | admin | Cập nhật hồ sơ, nhóm, trạng thái. **Không nhận mật khẩu** |
| PATCH | `/api/students/{id}/status` | admin | Khoá / mở khoá |
| POST | `/api/students/{id}/reset-password` | admin | **Bổ sung** — cấp lại mật khẩu khi học sinh quên |
| GET | `/api/students/availability` | admin | **Bổ sung** — kiểm tra email/username/code đã dùng chưa, thay cho việc FE tải 500 bản ghi về so sánh tại chỗ |

### 4.4 Nhóm lớp

| Method | Path | Vai trò | Mô tả |
|---|---|---|---|
| GET | `/api/student-groups` | teacher, admin | Danh sách nhóm kèm sĩ số |
| GET | `/api/student-groups/{id}` | teacher, admin | Chi tiết một nhóm |
| POST | `/api/student-groups` | teacher | Tạo nhóm. Chủ sở hữu lấy từ JWT |
| PUT | `/api/student-groups/{id}` | teacher | Đổi tên và mô tả |
| DELETE | `/api/student-groups/{id}` | teacher | Xoá nhóm **rỗng**. Không kéo theo tài khoản học sinh |
| GET | `/api/student-groups/{id}/students` | teacher | Thành viên của nhóm |
| GET | `/api/student-groups/{id}/assignable-students` | teacher | Học sinh có thể thêm vào nhóm |
| POST | `/api/student-groups/{id}/students` | teacher | Thêm **nhiều** học sinh trong một lần gọi |
| DELETE | `/api/student-groups/{id}/students/{studentId}` | teacher | Gỡ khỏi nhóm (đặt `GroupId = NULL`) |

> `PUT /student-groups/{id}` ở FE phải lan tên nhóm mới xuống mọi bản ghi học sinh. Ở BE việc đó **biến mất hoàn toàn** nếu chuẩn hoá đúng — học sinh chỉ giữ khoá ngoại `GroupId`, tên luôn đọc qua JOIN.
> `POST .../students` nhận mảng: FE hiện gọi lặp từng em rồi tự đếm thành công/thất bại. Gộp thành batch để tránh N request.

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
| PUT | `/api/exams/{id}` | teacher | Sửa toàn bộ cấu hình |
| PATCH | `/api/exams/{id}/schedule` | teacher | **Chỉ** đổi lịch mở/đóng — hẹp và an toàn hơn PUT |
| DELETE | `/api/exams/{id}` | teacher | Xoá đề kèm toàn bộ bản ghi giao bài |
| POST | `/api/exams/blueprint-preview` | teacher | **Trái tim của tính năng.** Nhận cấu hình đang gõ dở, trả số câu cần / số câu có / hợp lệ hay không |
| GET | `/api/exams/{id}/blueprint` | teacher | Blueprint của đề đã lưu |
| POST | `/api/exams/{id}/publish` | teacher | **Bổ sung** — chuyển nháp sang xuất bản. FE hiện làm việc này bằng cách gửi PUT với `lifecycle` bỏ trống |
| GET | `/api/exams/{id}/assignments` | teacher | Danh sách học sinh đã giao kèm trạng thái làm bài |
| POST | `/api/exams/{id}/assignments` | teacher | Giao bài cho học sinh lẻ và/hoặc cả nhóm |
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

### 4.8 Kết quả và tổng quan

| Method | Path | Vai trò | Mô tả |
|---|---|---|---|
| GET | `/api/results` | teacher | Bảng kết quả, lọc chéo theo đề / học sinh / nhóm / trạng thái |
| GET | `/api/results/stats` | teacher | Bốn ô số liệu đầu trang |
| GET | `/api/results/{attemptId}` | teacher, student | Chi tiết một lượt làm bài, soi từng câu |
| GET | `/api/results/{attemptId}/summary` | student | **Bổ sung** — cho màn "Đã nộp bài". Hiện trang đó gọi `getAttemptDetail`, tức kéo về toàn bộ câu hỏi chỉ để hiện một vòng tròn điểm |
| GET | `/api/my/results` | student | Lịch sử làm bài của chính mình |
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
- **Kiểm tra phạm vi:** học sinh phải thuộc nhóm do chính giáo viên đó quản lý. Không cho giáo viên A giao bài cho lớp của giáo viên B bằng cách đoán id.
- **Học sinh bị khoá:** FE hiển thị nhãn "Đã khóa" nhưng vẫn cho chọn và vẫn tạo assignment. BE nên **bỏ qua** và trả `skippedLocked` để giáo viên biết.
- **Chặn giao đề đang là nháp.** FE hoàn toàn không kiểm điều này.

### Lưu nháp và nộp bài

- **Chống ghi đè ngược thứ tự:** autosave chạy mỗi 800ms; mạng chậm khiến gói cũ tới sau gói mới. Server giữ `lastAppliedSeq` cho mỗi attempt và bỏ qua request có `clientSeq` nhỏ hơn. **Không dùng timestamp client** làm mốc.
- **Hết giờ không nhận nháp mới:** `PUT answers` trả **410** khi `serverNow > expiresAt`.
- **Validate đáp án gửi lên:** `questionId` phải thuộc đúng attempt đó, `answerId` phải thuộc đúng `questionId` đó. Trả **422** nếu lạ, không âm thầm bỏ qua.
- **Nộp idempotent:** nộp lại cùng `attemptId` trả đúng kết quả cũ (200), không tạo bản ghi thứ hai. FE có cờ chống bấm hai lần, nhưng retry sau timeout mạng vẫn tới server hai lần — chỉ khoá ở DB mới chắc.

### Dữ liệu dẫn xuất — BE tính, không nhận từ client

| Trường | Định nghĩa |
|---|---|
| `Exam.assignedCount` | số assignment của đề |
| `Exam.submittedCount` | số assignment có status ∈ {submitted, graded} |
| `Student.completedExams` | COUNT lượt có status ∈ {submitted, graded} |
| `Student.averageScore` | AVG(score) trên các lượt `graded`, làm tròn 1 chữ số |
| `Student.deletionScheduledAt` | `deletionRequestedAt` + 60 ngày, chỉ có khi `status = pendingDeletion`. Server tính sẵn — FE không tự cộng ngày |
| `AddMembersResponse.skippedRetired` | Số tài khoản chờ xoá hoặc đã xoá bị bỏ qua, không đưa vào lớp |
| `StudentGroup.studentCount` | COUNT thành viên đang hoạt động hoặc đang khoá — không tính tài khoản chờ xoá |
| `Student.group` (tên) | dữ liệu JOIN, **chỉ đọc** — client gửi lên thì bỏ qua |

`lifecycle` của đề **không nên là cột lưu cứng**. Chỉ lưu `IsDraft` (hoặc `PublishedAt`), ba trạng thái còn lại tính theo giờ server mỗi lần đọc: `now < openAt` → scheduled · `openAt ≤ now ≤ closeAt` → open · `now > closeAt` → closed.

### Tài khoản

- Mật khẩu hash bằng **BCrypt** (work factor ≥ 11). FE hiện chấp nhận mọi mật khẩu không rỗng.
- **`status = 'locked'` phải chặn đăng nhập** (403) và vô hiệu hoá JWT còn hạn qua `security_stamp`. Hiện nó chỉ là nhãn hiển thị.
- Unique index trên `email`, `username`, `code` — so sánh sau khi trim, không phân biệt hoa thường. Trả **409**, không phải 400. Khi sửa phải loại trừ chính bản ghi đang sửa.
- **Trim mọi chuỗi trước khi lưu và trước khi so trùng**, nếu không `'HS1001 '` và `'HS1001'` cùng lọt qua unique index.
- `createdAt` do server đặt = `DateTime.UtcNow`, không nhận từ client.
- Mật khẩu do admin cấp: đặt cờ `mustChangePassword = true`, ép đổi ở lần đăng nhập đầu.
- **Xoá sạch `demoAccounts` và `DEMO_PASSWORD` trước khi lên production.**

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
           Status,        -- Active | Locked
           Phone, School, AvatarColor, CreatedAt, MustChangePassword)

StudentGroup (Id, Name, Description, OwnerTeacherId FK -> User.Id, CreatedAt)

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

| Sự kiện | Ai nhận | Khi nào |
|---|---|---|
| `ExamAssigned` | student | Giáo viên giao bài, chỉ khi `notify = true` |
| `ExamSubmitted` | teacher | Học sinh nộp bài. Bắn cả vào group `exam:{examId}` để màn theo dõi tự tăng `submittedCount` |
| `AutoSubmitted` | teacher + student | Hết giờ, hệ thống tự nộp. **Hai payload khác nhau** cho hai vai trò |
| `DeadlineSoon` | student (chưa nộp) + teacher | Bài sắp đóng |
| `ExamUpdated` | student | Giáo viên sửa đề đã giao (đổi lịch, đổi thời lượng) |
| `UnreadCountChanged` | tất cả | Thay thế trực tiếp cho việc poll `GET /notifications/unread-count` |

> **Sinh thông báo là việc của BE**, trong **cùng transaction** với hành động nghiệp vụ. FE hiện tự `unshift` vào mảng mock — lên BE thì client không được phép tạo thông báo.
> Endpoint `unread-count` hiện bị gọi lại **mỗi lần đổi trang** (`useAsync` phụ thuộc `location.pathname` ở cả hai layout) nên là endpoint bị gọi nhiều nhất hệ thống. `UnreadCountChanged` là thứ thay thế nó.
> **Tự động nộp bài và cảnh báo sắp hết hạn phải do BackgroundService/Hangfire** thực hiện dựa trên `ExpiresAt` trong DB, không phải do client bấm giờ.

---

## 8. Không làm ở v1

- **Module thời khoá biểu** — đã hoãn, xem `EduQuiz_Workflow_Deploy.md`
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
