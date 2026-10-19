# Anthill — Hợp Đồng API

Tài liệu này được **rút ra từ tầng service của frontend** trước khi viết backend, không phải thiết kế trên giấy: mỗi hàm trong 9 file `src/services/` của repo frontend tương ứng một endpoint, và các quy tắc nghiệp vụ được chép ra từ chính thân hàm.

Backend đã được dựng theo tài liệu này, và frontend đã chuyển mọi hàm service sang gọi `apiClient` — **chữ ký hàm giữ nguyên nên không trang nào phải sửa**. Tài liệu được cập nhật theo code; chỗ nào lệch thì code là chuẩn.

Ngày lập: 06/09/2026 · Nguồn: 9 service, ~50 hàm · Hiện tại: **92 endpoint** (kể cả `GET /api/exams/{id}/papers`, `POST /api/exams/{id}/duplicate`, `POST /api/exams/{id}/release-answers`, `GET /api/exams/{id}/questions`, `POST /api/exams/compose-questions`, `PUT /api/auth/me/avatar`, `POST /api/student-groups/{id}/students/{studentId}/transfer`, `GET /api/students/{id}/history`, `GET /api/students/{id}/exams`, `GET /api/questions/subjects`, bảy endpoint giáo viên `/api/teachers`, sáu endpoint nhập Excel `/api/imports/*` và bốn endpoint của lớp: `GET /api/student-groups/assignable-teachers`, `PUT .../{id}/teacher`, `GET .../{id}/history`, `GET .../{id}/exams`; ba endpoint phân công nhiều giáo viên cũ đã bỏ)

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

> **Admin sở hữu TỔ CHỨC** (tài khoản học sinh và giáo viên, lớp, xếp học sinh vào lớp, giáo viên phụ trách lớp, nhập danh sách từ Excel).
> **Teacher sở hữu VIỆC DẠY** (ngân hàng câu hỏi, đề, giao bài, kết quả) trong những lớp đã công bố mà mình được phân công.
> **Student làm bài và xem kết quả của chính mình.**

| Khu vực | admin | teacher | student |
|---|:---:|:---:|:---:|
| Tài khoản học sinh (tạo/sửa/khoá/reset mật khẩu, yêu cầu xoá/khôi phục) | ✅ — khoá, xoá phải kèm lý do | đọc, chỉ học sinh mình phụ trách (§4.3) | — |
| Lớp, thành viên (xếp, gỡ, chuyển lớp — gỡ và chuyển phải kèm lý do), công bố; giáo viên phụ trách (mỗi lớp đúng một, đổi người phải kèm lý do), lịch sử lớp, bài đã giao cho lớp | ✅ | đọc, chỉ lớp đã công bố mình được phân công (§4.4) | — |
| Tài khoản giáo viên (tạo, sửa, khoá kèm lý do, cấp lại mật khẩu), lịch sử khoá | ✅ (§4.9) | — | — |
| Nhập học sinh, giáo viên, lớp từ file Excel (kể cả tải file mẫu) | ✅ (§4.10) | — | — |
| Lịch sử thay đổi, mọi bài được giao (kể cả bài chưa làm) của một học sinh | đọc (§4.3) | — | — |
| Ngân hàng câu hỏi | ✅ mọi môn: xem cả câu riêng tư, tạo (phải chọn môn), sửa, ẩn, xoá (§4.5) | ✅ trong môn của mình | — |
| Bài kiểm tra, giao bài | — | ✅ | — |
| Kết quả toàn lớp | — | ✅ | — |
| Bài làm của một học sinh (soi từng câu) | đọc, chỉ đọc (§4.8) | ✅ bài của đề mình tạo | ✅ bài của mình |
| Làm bài, kết quả của mình | — | — | ✅ |
| Hồ sơ, thông báo của mình | ✅ | ✅ | ✅ |

---

## 3. Sáu quy tắc bảo mật không được vi phạm

Đây là phần quan trọng nhất của tài liệu. Sáu điều dưới đây hiện **đang đúng ở FE chỉ vì mọi thứ chạy trong một trình duyệt**; lên API thật mà không làm lại ở server thì mỗi điều là một lỗ hổng.

**① Đáp án đúng không được rời server trong lúc làm bài.**
`attemptService` cố ý ghi đè `isCorrect = false` trước khi trả câu hỏi xuống. Ở BE phải dùng **hai DTO tách biệt**: `AttemptAnswerOptionDto { id, content, imageUrl }` cho lúc làm bài (không khai báo trường `isCorrect`, kể cả gán false) và `ResultAnswerOptionDto { id, content, imageUrl, isCorrect }` cho lúc xem kết quả. Học sinh mở DevTools là thấy toàn bộ response — gán false không đủ, phải **không có trường đó**.

**② Chấm điểm ở server, luôn luôn.**
`submitAttempt` hiện tính điểm ngay trong FE rồi ghi vào mock. Client chỉ được gửi `{ questionId, answerIds }` (câu một đáp án thì mảng có một phần tử); server đối chiếu `AnswerOption.IsCorrect` trong DB, đếm, tính điểm. **Không bao giờ nhận `score`/`correctCount` từ client.**

**③ Hạn giờ tính bằng đồng hồ server.**
`expiresAt = startedAt + durationMinutes`, lưu vào bảng Attempt lúc bắt đầu, mọi kiểm tra so với `DateTime.UtcNow`. Client đổi giờ máy chỉ làm lệch cái đồng hồ trên màn hình, không mở rộng được thời gian thật.
Và: **phải có background job** quét các attempt `inprogress` quá hạn để tự chấm — vì học sinh tắt trình duyệt thì không ai gọi `submit` cả.

**④ Danh tính suy từ JWT, không nhận từ client.**
`listMyExams`, `listMyResults`, `getStudentDashboard`, `startAttempt`, `saveAnswers`, `submitAttempt` — không endpoint nào nhận `studentId`. Kiểm tra quyền sở hữu trên từng `attemptId`/`assignmentId` trước khi làm bất cứ việc gì.

**⑤ Role đọc từ DB, không tin trường `role` client gửi khi login.**
Mock hiện lấy hồ sơ theo đúng role client chọn (`getDefaultProfile(payload.role)`). Bê nguyên logic đó lên BE là **leo thang quyền tức thì** — gửi `role: 'admin'` là thành admin. Trường `role` trong request chỉ dùng để đối chiếu, hoặc bỏ hẳn.

**⑥ Quyền xem đáp án thực thi ở server.**
Quy tắc: giáo viên sở hữu đề luôn xem được. Học sinh xem **điểm** (`score`, `maxScore`, `correctCount`) khi *(attempt là của mình)* **và** *(status = graded)* **và** *(exam.showResultAfterSubmit = true)*. **Đáp án từng câu** (`correctAnswerIds`, `isCorrect`) cần thêm điều kiện thứ tư: *(mọi lượt giao của đề đã qua giờ đóng có hiệu lực — lịch riêng của lớp nếu có, ngược lại lịch chung của đề)* — một đề giao cho nhiều lớp với nhiều khung giờ, và em lớp làm trước không được cầm đáp án chuyền cho lớp chưa thi. Khi không đủ điều kiện, response phải **không chứa** các trường đó — không phải ẩn ở giao diện. `GET /api/results/{attemptId}` trả kèm `answersVisibleAt` (mốc đáp án sẽ mở; vắng mặt khi đã mở, hoặc khi không có mốc nào để hẹn).

---

## 4. Danh sách endpoint

### 4.1 Xác thực và hồ sơ

| Method | Path | Vai trò | Mô tả |
|---|---|---|---|
| POST | `/api/auth/login` | — | Đăng nhập bằng email (teacher/admin) hoặc username/mã HS (student). Trả JWT + hồ sơ |
| POST | `/api/auth/refresh` | — | Cấp lại access token. **Bổ sung** — cần thiết vì học sinh làm bài 45–60 phút, hết token giữa chừng là mất bài |
| GET | `/api/auth/me` | tất cả | Hồ sơ người đang đăng nhập, suy từ JWT |
| PUT | `/api/auth/me` | tất cả | Cập nhật hồ sơ của chính mình. **Thông tin liên hệ do nhà trường quản lý:** **quản trị viên sửa được `fullName` và `phone`, không đổi được `email`** (gửi `email` khác đi trả **400** kèm `errors.email`; email của mọi tài khoản đổi ở trang quản lý của quản trị viên chứ không tự đổi); **giáo viên chỉ sửa được `fullName`** (gửi `email` hay `phone` khác đi trả **400** kèm `errors.email` / `errors.phone`); **học sinh không sửa được trường nào** (cả `fullName`, `email`, `phone`). Gửi lại đúng giá trị đang có thì hợp lệ — form luôn gửi đủ ba trường. Môn giảng dạy không có trong request nên không đổi được từ đây. `email` tối đa 256 ký tự |
| PUT | `/api/auth/me/avatar` | admin, teacher | Đặt hay gỡ **ảnh đại diện** của chính mình. Body `{ "avatarUrl": string \| null }`, trường **phải có mặt** (thiếu là **400**, để client quên gửi không âm thầm gỡ ảnh); `null` hay chuỗi rỗng là gỡ. Ảnh tải lên trước ở `POST /api/uploads/images`, rồi gửi đường dẫn nhận về; chỉ nhận đường dẫn tương đối bắt đầu bằng `/uploads/`, không chứa `..`, tối đa 512 ký tự — URL tuyệt đối, `//host`, data URI trả **400**. Học sinh trả **403** (hồ sơ học sinh do nhà trường quản lý, và học sinh cũng không tải ảnh lên được). Cố ý **không** áp khoá tài khoản demo (§H19). Trả `UserProfileDto` có `avatarUrl` (vắng khi chưa có ảnh); `GET /api/auth/me` và phản hồi đăng nhập cũng mang trường này. File ảnh cũ không bị xoá khi đổi hay gỡ |
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
| POST | `/api/students` | admin | Tạo tài khoản kèm mật khẩu ban đầu. `phone` không bắt buộc (sai định dạng trả 400). `groupId` tuỳ chọn: xếp em vào lớp đó ngay khi tạo (kèm một dòng lịch sử `addedToGroup`); lớp đã khoá trả **409** |
| PUT | `/api/students/{id}` | admin | Cập nhật hồ sơ. **Không nhận mật khẩu**, **không đổi lớp, không đổi trạng thái**: `status` khác giá trị hiện tại trả **400**; `groupId`, nếu gửi, phải là **một trong các lớp em đang học** (hoặc 0 khi em chưa học lớp nào), khác đi trả **400** — xếp thêm, chuyển và gỡ lớp đi qua §4.4, khoá đi qua `PATCH .../status`; không gửi hai trường đó thì giữ nguyên. `phone`: không gửi thì giữ nguyên số đang có, chuỗi rỗng xoá trắng. `StudentDto.phone` chỉ có khi người gọi là quản trị viên — giáo viên không nhận trường này |
| PATCH | `/api/students/{id}/status` | admin | Khoá / mở khoá — chỉ nhận `active` hoặc `locked`; hai trạng thái xoá đi qua hai endpoint riêng bên dưới. **Khoá bắt buộc `reason`** (10–512 ký tự sau khi bỏ khoảng trắng hai đầu, thiếu hoặc ngắn trả **400** `errors.reason`); mở khoá không cần. Lý do vào lịch sử của học sinh |
| POST | `/api/students/{id}/reset-password` | admin | **Bổ sung** — cấp lại mật khẩu khi học sinh quên |
| POST | `/api/students/{id}/request-deletion` | admin | **Bổ sung** — đưa vào diện chờ xoá (`pendingDeletion`), lý do bắt buộc (≥ 10 ký tự). Không đăng nhập được, token cũ chết ngay. Sau 60 ngày tiến trình nền ẩn danh hoá, gỡ khỏi lớp và chuyển sang `deleted`; lượt làm bài và điểm giữ nguyên |
| POST | `/api/students/{id}/restore` | admin | **Bổ sung** — khôi phục tài khoản đang chờ xoá, trả về **đúng trạng thái trước khi vào diện xoá**: em đang bị khoá vẫn `locked` (cột `StatusBeforeDeletion`); bản ghi chờ xoá từ trước khi có cột này về `active`. Đã ẩn danh hoá rồi thì **409** |
| GET | `/api/students/availability` | admin | **Bổ sung** — kiểm tra email/username/code đã dùng chưa, thay cho việc FE tải 500 bản ghi về so sánh tại chỗ |
| GET | `/api/students/{id}/history` | admin | **Bổ sung** — lịch sử thay đổi của học sinh, mới nhất trước: `{ id, kind, reason?, fromGroup?, toGroup?, actorName?, createdAt }`. `kind`: `locked` · `unlocked` · `deletionRequested` · `restored` · `addedToGroup` · `removedFromGroup` · `movedGroup`. Tên lớp ghi đúng như lúc xảy ra (không phải tên hiện tại). Chỉ thêm, không sửa, không xoá |
| GET | `/api/students/{id}/exams` | admin | **Bổ sung** — mọi bài kiểm tra đã giao cho học sinh, **kể cả bài chưa làm và bài đã hết hạn mà em không làm**: `{ assignmentId, examId, title, teacherName, group?, status, openAt, closeAt, attemptId?, startedAt?, submittedAt?, score?, maxScore?, correctCount?, assignedAt }`. `openAt`/`closeAt` là khung giờ thật của em (lịch riêng của lớp nếu có). Điểm luôn có khi đã chấm, không bị chặn bởi `showResultAfterSubmit` (cờ đó chỉ nói về điều học sinh được thấy). Em nghỉ học mà không ai gỡ khỏi lớp vẫn hiện đủ các bài sau của lớp với trạng thái `assigned`/`expired` |

`Student.status`: `active` · `locked` · `pendingDeletion` · `deleted`.

`StudentDto.groups`: **các lớp em đang học** — `[{ id, name, isLocked }]`, mảng rỗng là chưa học lớp nào. Một em học được nhiều lớp cùng lúc (thay cho cặp `groupId` / `group` của mô hình một lớp, đã bỏ). Giáo viên chỉ nhận những lớp **mình dạy** (đã công bố): lớp của đồng nghiệp mà em cùng học không phải chuyện giáo viên này được biết. `UserProfileDto.groups` (học sinh đăng nhập) cùng hình dạng, đủ mọi lớp.

`StudentDto.hasStartedExams`: học sinh đã có ít nhất một lượt làm bài, ở bất kỳ trạng thái nào (kể cả đang làm dở). Chỉ để giao diện hiện nhãn "Đang làm bài"; **không còn luật nào dựa vào cờ này** — em đã làm bài vẫn chuyển, gỡ và xếp lớp được (§5, "Lớp và thành viên").

> **Phạm vi đọc của giáo viên.** Danh sách là "lớp tôi đang dạy": học sinh đang học **ít nhất một** lớp **đã công bố** mà giáo viên **được phân công** — đúng tập lớp `GET /api/student-groups` trả cho họ. Bộ lọc `groupId` / `status` / `search` chạy bên trong phạm vi đó: `groupId` của lớp khác hay lớp nháp, và `groupId=0` ("chưa phân nhóm"), đều ra trang rỗng, không lỗi. Hồ sơ rộng hơn danh sách một vế: mở được cả học sinh đã nhận một đề do chính giáo viên đó tạo, vì kết quả thuộc về đề chứ không thuộc về lớp hiện tại — em chuyển lớp hay đã bị xoá dữ liệu cá nhân thì liên kết từ bảng kết quả vẫn phải mở được. Ngoài phạm vi trả **404** cùng thông điệp với id không tồn tại. Admin không bị giới hạn.

### 4.4 Nhóm lớp

| Method | Path | Vai trò | Mô tả |
|---|---|---|---|
| GET | `/api/student-groups` | admin, teacher | Danh sách lớp kèm sĩ số. Query `search`, `status` (`draft` · `published` · `locked` · `all`, mặc định `all`; giá trị khác trả 400). Giáo viên chỉ nhận lớp **đã công bố** mà mình **được phân công** |
| GET | `/api/student-groups/{id}` | admin, teacher | Chi tiết một lớp. Lớp ngoài phạm vi của giáo viên trả **404** |
| POST | `/api/student-groups` | admin | Lập lớp, thân `{ name, description?, teacherId, termId? }` (`termId` là kỳ học của lớp, tuỳ chọn; mã kỳ không có trả **400**). **`teacherId` bắt buộc**: mỗi lớp có đúng một giáo viên, chọn ngay lúc lập (id không phải giáo viên trả **400**, tài khoản giáo viên đang bị khoá trả **409**). Lớp mới ra đời ở dạng **nháp** — giáo viên chưa thấy — kèm một dòng lịch sử `created` |
| PUT | `/api/student-groups/{id}` | admin | Đổi tên, mô tả và kỳ học: thân `{ name, description?, termId? }`, `termId` bỏ trống hay `null` là **bỏ phân kỳ** (form luôn gửi đủ các trường); mã kỳ không có trả **400**. Lớp đã khoá trả **409** |
| DELETE | `/api/student-groups/{id}` | admin | Xoá lớp **rỗng** (còn học sinh hoặc tài khoản chờ xoá thì **409**; lớp đã khoá mà rỗng vẫn xoá được). Lớp còn **lượt giao bài** đi qua nó (đã làm, đang làm hoặc hết hạn) cũng trả **409**: khoá ngoại từ bài giao là `SetNull`, nên xoá sẽ làm các bài đó mất tên lớp và kỳ học; hãy khoá lớp thay vì xoá (xoá đề chưa ai làm thì gỡ được bài giao). Không kéo theo tài khoản học sinh |
| GET | `/api/student-groups/{id}/students` | admin, teacher | Thành viên của lớp |
| GET | `/api/student-groups/{id}/assignable-students` | admin | Học sinh **chưa học lớp này** có thể xếp thêm vào lớp — kể cả em đang học lớp khác (một em học được nhiều lớp). Không liệt kê tài khoản chờ xoá hay đã xoá |
| POST | `/api/student-groups/{id}/students` | admin | Xếp **nhiều** học sinh vào lớp trong một lần gọi; không cần lý do (không ai bị đổi chỗ hay mất bài: em chỉ có **thêm** một lớp, các lớp khác giữ nguyên). Lớp đã khoá trả **409**. Trả `added`, `skippedAlreadyMember` (đã học lớp này), `skippedNotFound`, `skippedRetired` (chờ xoá / đã xoá), `studentCount` |
| DELETE | `/api/student-groups/{id}/students/{studentId}` | admin | Gỡ khỏi **lớp này** (xoá dòng thành viên; các lớp khác của em giữ nguyên). **Thân `{ reason }` bắt buộc** (10–512 ký tự; thiếu thân hoặc lý do ngắn trả **400**). Làm được kể cả khi em đã làm bài; lớp đã khoá trả **409**. Thu hồi bài lớp này đã giao mà em chưa làm và chưa hết hạn, trả `{ revokedExams, keptExams }` (xem §5, "Lớp và thành viên") |
| POST | `/api/student-groups/{id}/students/{studentId}/transfer` | admin | **Bổ sung** — chuyển một em từ lớp `{id}` sang lớp khác: thân `{ targetGroupId, reason }`, lý do bắt buộc (10–512 ký tự). Là **gỡ khỏi lớp `{id}` rồi xếp vào lớp đích trong một transaction**, làm được kể cả khi em đã làm bài; các lớp khác của em giữ nguyên. **409** khi tài khoản đang chờ xoá / đã xoá, em đã học lớp đích rồi (muốn rời lớp thì gỡ), hoặc lớp nguồn hay lớp đích đã khoá; **404** khi em không thuộc lớp `{id}` hoặc lớp đích không có; lớp đích trùng lớp nguồn trả **400**. Trả `{ revokedExams, keptExams }` (của lớp nguồn) |
| POST | `/api/student-groups/{id}/publish` | admin | **Bổ sung** — công bố lớp nháp; từ đây giáo viên được phân công mới thấy lớp. Đã công bố rồi thì **409**; lớp chưa có giáo viên phụ trách cũng **409** |
| POST | `/api/student-groups/{id}/unpublish` | admin | **Bổ sung** — đưa lớp về nháp (lùi lại khi công bố nhầm). Không đụng tới học sinh hay bài đã giao. Vốn là nháp thì **409**; lớp đã khoá cũng **409** |
| POST | `/api/student-groups/{id}/lock` | admin | **Bổ sung** — khoá lớp (không có "kết thúc lớp": lớp hết dùng thì khoá). Thân `{ reason }` **bắt buộc** (10–512 ký tự; thiếu thân hoặc lý do ngắn trả **400**). Lớp khoá là lớp **chỉ đọc**: xếp thêm, gỡ, chuyển vào / ra, giao bài, sửa lớp, đổi giáo viên, công bố / gỡ công bố đều **409**. Thành viên, bài đã giao, lượt làm bài và bảng điểm giữ nguyên; các em vẫn học các lớp khác. Đã khoá rồi trả **409**. Trả `StudentGroupDto` (`isLocked`, `lockedAt`). Lý do vào lịch sử của lớp |
| POST | `/api/student-groups/{id}/unlock` | admin | **Bổ sung** — mở khoá lớp, không cần lý do. Lớp không bị khoá trả **409**. Dòng `unlocked` vào lịch sử của lớp |
| GET | `/api/student-groups/{id}/grade-sheet` | teacher | **Bổ sung** — xuất **bảng điểm lớp** ra Excel (`.xlsx`, `Content-Disposition: attachment`). Chỉ giáo viên **đang dạy lớp đã công bố**: quản trị viên và học sinh nhận **403**; lớp không có, lớp nháp hay lớp của giáo viên khác trả **404**. Lớp đã khoá vẫn xuất được. Nội dung ở §5, "Xuất bảng điểm" |
| GET | `/api/student-groups/assignable-teachers` | admin | **Bổ sung** — mọi giáo viên để chọn người phụ trách, kể cả tài khoản đang bị khoá (kèm trạng thái, để người dùng hiểu vì sao không chọn được) |
| PUT | `/api/student-groups/{id}/teacher` | admin | **Bổ sung** — đổi giáo viên phụ trách, thân `{ teacherId, reason? }`. Mỗi lớp có đúng một giáo viên nên đây là **thay** chứ không thêm; lớp cũ còn nhiều giáo viên thì lần đổi này dọn về một. **`reason` bắt buộc** (10–512 ký tự, thiếu trả **400**) khi đang có người khác phụ trách; lớp chưa có giáo viên hay chọn lại đúng người đang phụ trách thì không cần. Tài khoản giáo viên bị khoá trả **409**. Bài đã giao gắn với học sinh nên không bị ảnh hưởng |
| GET | `/api/student-groups/{id}/history` | admin | **Bổ sung** — lịch sử của lớp, mới nhất trước: `{ id, kind, reason?, fromTeacher?, toTeacher?, actorName?, createdAt }`. `kind`: `created` · `teacherChanged` · `locked` (kèm `reason`) · `unlocked`. Tên giáo viên chép đúng như lúc xảy ra. Chỉ thêm, không sửa, không xoá |
| GET | `/api/student-groups/{id}/exams/{examId}/results` | admin, teacher | **Bổ sung** — từng em của lớp làm một bài ra sao, cùng hình dạng với `GET /api/exams/{id}/assignments` (trạng thái, điểm, `startedAt`, `submittedAt`, `attemptId`) nhưng **chỉ những em được giao qua lớp này**. Quản trị viên đọc mọi lớp; giáo viên đọc lớp đã công bố mình dạy, kể cả bài của giáo viên trước (lớp khác trả **404**). Từ `attemptId` mở bài làm bằng `GET /api/results/{attemptId}` |
| GET | `/api/student-groups/{id}/exams` | admin, teacher | **Bổ sung** — các bài kiểm tra đã giao cho lớp, mới nhất trước, kể cả đề cũ đã đóng. Quản trị viên đọc mọi lớp; **giáo viên đọc lớp đã công bố mình dạy** (lớp khác, lớp nháp hay không có trả **404**) và thấy cả đề do giáo viên trước của lớp soạn (`teacherName` là người soạn). Mỗi dòng: `{ examId, title, teacherName, openAt, closeAt, lifecycle, assignedCount, startedCount, submittedCount, averageScore?, maxScore?, assignedAt }` (`averageScore` = tổng điểm các bài đã chấm chia cho `assignedCount`: em chưa làm tính 0 điểm). `openAt`/`closeAt` là khung giờ của lớp (lịch riêng của lớp nếu có). Số liệu chỉ tính em được giao **qua lớp này** |

> Lớp thuộc về **tổ chức**, không thuộc về giáo viên nào: quản trị viên lập lớp (chọn ngay **một** giáo viên phụ trách), xếp học sinh rồi công bố. Giáo viên chỉ đọc và giao bài cho lớp đã công bố mình được phân công. Bảng `StudentGroupTeacher` vẫn là bảng nhiều-nhiều (schema không đổi) nhưng dịch vụ luôn giữ đúng một dòng cho mỗi lớp.
> Đổi tên lớp không phải ghi lại bản ghi học sinh nào — học sinh và lớp nối nhau qua bảng `StudentGroupMember` (nhiều-nhiều), tên luôn đọc qua JOIN.
> `POST .../students` nhận mảng: FE từng gọi lặp từng em rồi tự đếm thành công/thất bại. Gộp thành batch để tránh N request.

### 4.4a Kỳ học

Quản trị viên quản lý danh mục kỳ học; giáo viên và học sinh không gọi các endpoint này (giáo viên thấy `termName` trên lớp của mình, học sinh qua `/api/my/terms`, §4.7). Nghiệp vụ ở §5, "Kỳ học".

| Method | Path | Vai trò | Mô tả |
|---|---|---|---|
| GET | `/api/terms` | admin | Danh sách kỳ: `{ id, name, startsOn?, endsOn?, isCurrent, groupCount }`. Kỳ hiện tại đứng đầu, rồi kỳ bắt đầu muộn hơn trước (kỳ không ghi ngày xếp sau) |
| POST | `/api/terms` | admin | Thêm kỳ, thân `{ name, startsOn?, endsOn?, isCurrent? }`. Tên bắt buộc, tối đa 100 ký tự; trùng tên (không phân biệt hoa thường, bỏ khoảng trắng thừa) trả **409**; ngày kết thúc trước ngày bắt đầu trả **400**. `isCurrent = true` thì kỳ hiện tại cũ tự tắt trong cùng transaction |
| PUT | `/api/terms/{id}` | admin | Sửa kỳ, cùng thân. Không có kỳ trả **404** |
| DELETE | `/api/terms/{id}` | admin | Xoá kỳ **không còn lớp**; còn lớp trả **409** (chuyển lớp sang kỳ khác hoặc bỏ phân kỳ trước). Không có kỳ trả **404** |

### 4.5 Ngân hàng câu hỏi

| Method | Path | Vai trò | Mô tả |
|---|---|---|---|
| GET | `/api/questions` | teacher, admin | Danh sách, lọc theo từ khoá / chủ đề / độ khó / trạng thái. Thêm `excludeAssignedToGroupIds` (mã lớp ngăn cách bằng dấu phẩy, ví dụ `18,19`) để **bỏ những câu mà học sinh các lớp đó đã nhận ở đề trước** — dùng cho hộp "Thêm từ ngân hàng" của trình soạn đề. Chỉ giáo viên, chỉ lớp mình được phân công và đã công bố; lớp lạ, chuỗi không phải số, quá 50 lớp, hay quản trị viên dùng là **400** (không âm thầm bỏ qua). Bỏ trống là không lọc. Cùng cách tính "đã nhận" với `excludeAssignedToGroupIds` của soạn tự động |
| GET | `/api/questions/subjects` | admin | **Bổ sung** — các môn học `{ id, name, code }`, theo tên. Cho ô chọn môn khi quản trị viên tạo câu hỏi và ô lọc môn của ngân hàng. Giáo viên nhận **403** (họ bị khoá vào môn của mình) |
| GET | `/api/questions/topics` | teacher, admin | Tập chủ đề đang có (cho dropdown và gợi ý nhập). Quản trị viên thêm `subjectId` để thu hẹp về một môn |
| GET | `/api/questions/summary` | teacher, admin | Thống kê ngân hàng. Gộp 3 lời gọi FE đang bắn song song. Quản trị viên thêm `subjectId` để thu hẹp về một môn |
| GET | `/api/questions/{id}` | teacher, admin | Một câu hỏi kèm đáp án |
| POST | `/api/questions` | teacher, admin | Tạo. Đáp án lồng trong body, BE cấp id. Giáo viên lấy môn từ tài khoản; **quản trị viên phải gửi `subjectId`** (thiếu hoặc môn không có thật trả **400**) |
| PUT | `/api/questions/{id}` | teacher, admin | Sửa toàn bộ. Môn và người soạn **không đổi** (kể cả khi body có `subjectId`): câu hỏi không chuyển môn giữa chừng, người sửa không cướp quyền tác giả |
| DELETE | `/api/questions/{id}` | teacher, admin | Xoá. **409** khi câu đã nằm trong một đề (kèm gợi ý ẩn). Giáo viên chỉ xoá câu của mình; quản trị viên xoá câu của bất kỳ ai. Giao diện giáo viên không có nút xoá (ẩn đã đủ), giao diện quản trị viên có |
| PATCH | `/api/questions/{id}/status` | teacher, admin | Bật / ẩn nhanh |
| POST | `/api/uploads/images` | teacher, admin | Upload ảnh, trả URL |

> **Phạm vi của quản trị viên.** Quản trị viên không thuộc môn nào nên đọc cả ngân hàng của mọi môn, **kể cả câu `private` của giáo viên** (hàng rào lộ đề giữa hai lớp cùng buổi không áp cho người không đứng lớp, và quyền ghi vốn đã mở trên mọi câu). Thêm `subjectId` vào danh sách, `topics` hay `summary` chỉ thu hẹp về một môn — **không** làm mất các câu riêng tư. `QuestionDto` mang thêm `subjectId`, `subject` (tên môn; rỗng khi chưa nạp), `createdBy`, `createdByUserId`, `visibility` và `canEdit` do server tính (quản trị viên luôn `true`; giáo viên chỉ `true` với câu của mình).
> **Sửa câu hỏi:** FE **thay cả mảng đáp án** chứ không sửa tại chỗ, có chú thích rõ *"để các bài làm đã chấm giữ nguyên đáp án cũ"*. BE phải giữ đúng ý này — bằng versioning hoặc bằng snapshot vào attempt (xem §6).
> **Xoá câu hỏi:** FE xoá thẳng, không kiểm tra câu đó đã nằm trong đề hay bài làm nào. BE **bắt buộc** phải kiểm.
> **Tên câu hỏi (`title`):** nhãn ngắn do giáo viên đặt, tách khỏi `content` (đề bài). Server không bắt buộc (trình soạn trên giao diện bắt buộc nhập từ 17/10), một dòng (khoảng trắng và xuống dòng được gộp), tối đa 200 ký tự — vượt thì **400**. Chưa đặt thì API trả chuỗi rỗng và giao diện dùng phần đầu của `content`. `PUT` thay toàn bộ nên bỏ trống `title` là xoá tên đã đặt. `search` khớp cả tên. Học sinh không nhận trường này: bản đề chụp lại chỉ giữ nội dung.
> **Loại câu (`allowMultiple`):** cờ do giáo viên chọn rõ khi soạn, không suy ra từ số đáp án đúng. `false` (mặc định, cũng là giá trị khi `POST`/`PUT` bỏ trống trường) = câu một đáp án: đúng **một** đáp án đúng, nhiều hơn trả **400**. `true` = câu nhiều đáp án đúng: **từ một** đáp án đúng trở lên. Cả hai loại đều bắt buộc có ít nhất một đáp án đúng. `QuestionDto` luôn mang `allowMultiple`; bản đề chụp lại giữ cờ này (`ExamPaperQuestion.AllowMultiple`) nên đổi loại câu trong ngân hàng không làm đổi đề đã sinh.

### 4.6 Bài kiểm tra

| Method | Path | Vai trò | Mô tả |
|---|---|---|---|
| GET | `/api/exams` | teacher, admin | Danh sách, tìm kiếm + lọc theo vòng đời |
| GET | `/api/exams/{id}` | teacher, admin | Chi tiết một đề (`usesQuestionList` cho biết kiểu) |
| GET | `/api/exams/{id}/questions` | teacher, admin | Danh sách câu hỏi của đề theo danh sách, theo thứ tự, mỗi dòng `{ ordinal, point, question }` với `question` là `QuestionDto` đầy đủ (đáp án đúng, ảnh, trạng thái). Rỗng với đề theo ma trận |
| GET | `/api/exams/{id}/papers` | teacher, admin | Cách đề được **chia thành nhiều đề nhỏ**: mỗi mức đề lớn có bao nhiêu câu, mỗi đề cần bao nhiêu, bao nhiêu câu trùng ở nhiều đề hay không vào đề nào, và với từng lớp đã giao thì mỗi đề nhỏ (mã A, B…) có bao nhiêu em cùng mã câu của nó. Đề không chia: `paperCount = 1`, hai mảng rỗng. Học sinh **403**. Xem "Chia một đề thành nhiều đề" |
| POST | `/api/exams/compose-questions` | teacher | **"Soạn tự động"**: nhận `{ easy, medium, hard, topic?, excludeQuestionIds?, excludeAssignedToGroupIds? }`, rút ngẫu nhiên câu đang bật từ ngân hàng của môn giáo viên, loại các câu đã có. `topic` là **chương** cần soạn (khớp tên chính xác, không phân biệt hoa thường); soạn đề nhiều chương thì gọi lần lượt từng chương, truyền các câu đã có vào `excludeQuestionIds`. `excludeAssignedToGroupIds` (tối đa 50 lớp) bỏ thêm những câu mà học sinh của các lớp đó **đã nhận** ở các đề trước — bản đề riêng của từng em và bản chung của đề giao cho lớp; đề chia nhiều đề chỉ tính các câu thực sự rơi vào đề của lớp. Lớp không thuộc quyền giáo viên (chưa được phân công, chưa công bố, hay không tồn tại) trả **400**, không âm thầm bỏ qua. Trả `{ questions, missing, skippedAlreadyAssigned }`; thiếu câu không phải lỗi (`missing.hard = 3` nghĩa là còn thiếu 3 câu Khó), `skippedAlreadyAssigned` là số câu đủ điều kiện nhưng bị bỏ vì lớp đã nhận. **Không ghi gì** — đề chỉ đổi khi lưu |
| POST | `/api/exams` | teacher | Tạo đề. Có hai kiểu: **theo danh sách câu hỏi** (gửi `questions`: từng câu kèm điểm riêng, giáo viên đã duyệt trước khi lưu) hoặc **theo ma trận** (gửi `totalQuestions` + ba tỷ lệ, đề không chứa câu hỏi cụ thể). Đề theo danh sách có thể gửi thêm `paperCount` (2–10) để chia thành nhiều đề nhỏ cho mỗi lớp. Xem "Đề theo danh sách câu hỏi" và "Chia một đề thành nhiều đề" |
| PUT | `/api/exams/{id}` | teacher | Sửa toàn bộ cấu hình. Chưa em nào bắt đầu làm: sửa được mọi trường, bản đề đã sinh cho các lượt giao được **sinh lại** theo cấu hình mới (ngân hàng không đủ câu thì từ chối, không đổi gì); đề không còn lượt giao nào (đã thu hồi hết) thì bản đề cũ bị bỏ, lần giao sau sinh theo cấu hình mới. Đã có em bắt đầu (`blueprintFrozen`): đổi số câu, ba tỷ lệ, chủ đề, điểm mỗi câu (đề theo danh sách: danh sách câu hỏi hoặc điểm từng câu; đề chia nhiều đề: số đề), thời lượng, tuỳ chọn trộn hoặc đưa về nháp trả **409**; tên, mô tả và `showResultAfterSubmit` vẫn sửa được. Lịch có luật khoá riêng (xem `scheduleLocked`): đề đã giao thì đổi lịch trả **409** từ lúc còn dưới 120 phút nữa là mở cho tới hết giờ đóng, gửi lại đúng lịch đang có thì không bị chặn. Điểm đã chấm không bao giờ đổi |
| PATCH | `/api/exams/{id}/schedule` | teacher | **Chỉ** đổi lịch mở/đóng — hẹp và an toàn hơn PUT. **409** khi đề đã giao và lịch đang khoá (từ dưới 120 phút trước giờ mở tới hết giờ đóng) |
| DELETE | `/api/exams/{id}` | teacher | Xoá đề kèm toàn bộ bản ghi giao bài và bản đề đã sinh. Đã có học sinh nộp bài thì **409** — để đề tự đóng theo lịch, kết quả của các em vẫn tra cứu được |
| POST | `/api/exams/blueprint-preview` | teacher | **Trái tim của tính năng.** Nhận cấu hình đang gõ dở, trả số câu cần / số câu có / hợp lệ hay không |
| GET | `/api/exams/{id}/blueprint` | teacher | Blueprint của đề đã lưu, đối chiếu với tồn kho hiện tại. Đề theo danh sách: `required` là số câu mỗi mức trong chính danh sách, `available` là số câu trong đó còn bật, `errors` nói bao nhiêu câu đang bị ẩn |
| POST | `/api/exams/{id}/publish` | teacher | **Bổ sung** — chuyển nháp sang xuất bản. FE hiện làm việc này bằng cách gửi PUT với `lifecycle` bỏ trống. Kiểm lại hai điều kiện mà nháp được miễn: tổng ba tỷ lệ và ngân hàng đủ câu. Tổng điểm không phải điều kiện (xem "Điểm của đề") |
| POST | `/api/exams/{id}/release-answers` | teacher | **Bổ sung** — "Mở đáp án": bật `showResultAfterSubmit` của đề để học sinh đã nộp xem được điểm và đáp án từng câu. **Chỉ khi hết giờ**: lớp đóng bài muộn nhất (kể cả lượt đang chạy) đã qua giờ đóng; còn lớp đang làm hay chưa tới giờ làm thì **409** "Chưa hết giờ…", đề chưa giao cho lớp nào cũng **409**. Đã bật rồi thì 200, không đổi gì. Chỉ chủ đề (giáo viên khác 404, học sinh và quản trị viên 403). Cờ này vẫn sửa được qua `PUT` kể cả khi cấu hình đề đã khoá, nhưng đáp án từng câu luôn chờ lớp cuối hết giờ |
| POST | `/api/exams/{id}/duplicate` | teacher | **Bổ sung** — nhân bản đề của chính mình thành một bản **nháp** mới, cùng cấu hình, tên thêm " (bản sao)", chưa giao cho ai. Lối ra khi cấu hình đã khoá. Đề của giáo viên khác trả 404; giáo viên chưa được gán môn trả 403 như khi tạo đề |
| GET | `/api/exams/{id}/assignments` | teacher | Danh sách học sinh đã giao kèm trạng thái làm bài. Mỗi dòng mang khung giờ thật của em (`openAt`, `closeAt`, `hasScheduleOverride`, `assignedViaGroupId`) và, với đề chia nhiều đề, `paperLabel` (mã đề của em; **chỉ giáo viên và quản trị viên thấy**); `expired` tính theo khung giờ đó |
| POST | `/api/exams/{id}/assignments` | teacher | Giao bài cho một hay nhiều **lớp** (`groupIds`, hoặc `classes` khi mỗi lớp có khung giờ riêng). **Bài chỉ giao theo lớp**: body mang `studentIds` khác rỗng trả **400**; em mới vào lớp nhận bài khi gửi lại lớp. Tuỳ chọn kèm khung giờ (`openAt` + `closeAt`, phải chứa trọn `durationMinutes`, nếu không 400). Gửi lại một lớp kèm khung giờ là sửa giờ cho em chưa bắt đầu (`scheduleUpdatedCount`, `scheduleKeptStartedCount`); **409** nếu khung giờ lớp đang theo đã khoá, gửi lại đúng khung cũ hoặc giao thêm em vào lớp thì không bị chặn |
| DELETE | `/api/exams/{examId}/assignments/classes/{groupId}` | teacher | **Bổ sung** — thu hồi bài đã giao cho **cả một lớp**: gỡ mọi em của lớp chưa bắt đầu làm, em đã bắt đầu giữ nguyên bài và điểm. Trả `{ revokedCount, keptStartedCount }`. **404** khi lớp chưa được giao đề này **hoặc không phải lớp đã công bố do chính giáo viên này dạy** (cùng một câu, không dò được lớp của người khác), **409** khi lớp đã khoá (lớp chỉ đọc) hoặc khi mọi em đã bắt đầu. Giao nhầm lớp là chuyện thường, mà endpoint giao bài lại cố ý idempotent nên không có đường lùi; không có thu hồi riêng từng học sinh |

### 4.7 Làm bài (phía học sinh)

| Method | Path | Vai trò | Mô tả |
|---|---|---|---|
| GET | `/api/my/exams` | student | Bài được giao cho chính mình. Mỗi dòng ghi lớp đã giao (`groupId`, `groupName`; vắng với bài giao lẻ cũ). Tham số `?groupId=` chỉ lấy bài của một lớp (trang một lớp), kể cả bài đã nộp và bài hết hạn |
| GET | `/api/my/groups` | student | **Lớp học của tôi**: các lớp **đã công bố** em đang học (lớp nháp không hiện; lớp đã khoá vẫn hiện để xem lại). Mỗi lớp: `id`, `name`, `description`, `teacherName`, `subjectName?` (môn của giáo viên phụ trách), `studentCount`, `isLocked`, `termId?` và `termName?` (kỳ học của lớp, vắng khi lớp chưa phân kỳ), và số bài đã giao qua lớp theo trạng thái: `assignedCount`, `completedCount`, `openCount` (đang mở mà chưa nộp, kể cả đang làm dở), `upcomingCount`, `expiredCount` (hết hạn chưa làm) |
| GET | `/api/my/groups/{id}` | student | Một lớp của em, cùng dạng trên. **404** khi em không học lớp đó, lớp còn là bản nháp hoặc không tồn tại (không phân biệt) |
| GET | `/api/my/terms` | student | **Các kỳ em có lớp** (lớp đã công bố): `[{ id, name, startsOn?, endsOn?, isCurrent, groupCount }]`, `groupCount` là số lớp của **em** trong kỳ. Kỳ hiện tại đứng đầu; các lớp chưa phân kỳ gộp thành một mục cuối danh sách với `id = 0`, tên "Chưa phân kỳ". Giao diện dùng nó làm ô lọc (ẩn khi em chỉ có một mục) |
| GET | `/api/my/transcript` | student | **Bảng điểm**: `{ overallAverage?, subjects[] }`. Query `termId?`: bỏ trống là mọi kỳ, mã kỳ là chỉ các bài giao qua lớp thuộc kỳ đó, `0` là bài của lớp chưa phân kỳ (kể cả bài giao riêng cũ); mã kỳ không có thì danh sách môn rỗng, không phải lỗi. Mỗi môn (môn của giáo viên ra đề; "Chưa gán môn" khi chưa có): `subjectName`, `groupNames[]`, `average?` (thang 10), `countedExams`, `totalExams`, `hiddenExams`, `pendingExams`, `upcomingExams`, `missedExams`, `exams[]` (`examId`, `assignmentId`, `title`, `groupId?`, `groupName?`, `status`, `score?` thang 10, `counted`, `attemptId?`, `submittedAt?`, `closeAt`). **Điểm trung bình môn** tính trên bài đã nộp và được xem điểm, cộng bài **hết hạn mà em không làm (0 điểm)**; **không** tính bài còn làm được (đang mở, đang làm dở, sắp tới) và bài đã nộp nhưng giáo viên chưa cho xem điểm — nên không lộ điểm bị giấu qua trung bình. `overallAverage` là trung bình các điểm trung bình môn đã có |
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

**Hình dạng đáp án khi làm bài (hỗ trợ câu nhiều đáp án đúng).**

| Hướng | Trường | Ý nghĩa |
|---|---|---|
| Gửi `PUT .../answers`, `POST .../submit` | `answers: [{ questionId, answerIds }]` | Mỗi phần tử là **toàn bộ** lựa chọn hiện tại của câu đó: mảng rỗng là bỏ chọn hết. Tối đa 10 id, mỗi id dương, trùng thì gộp. Chưa có dòng nào cho câu thì coi như bỏ trống |
| Gửi (kiểu cũ, vẫn nhận) | `answerId` (số hoặc `null`) | Tương đương `answerIds` một phần tử (hoặc rỗng). Gửi cả `answerId` lẫn `answerIds` trong cùng một phần tử trả **400** |
| Nhận (nạp lại phiên, trả sau khi lưu) | `savedAnswers: { [questionId]: number[] }` | Chỉ có câu đã chọn ít nhất một đáp án |
| Nhận | `questions[].allowMultiple` | Cho giao diện chọn kiểu ô tròn hay ô vuông. Tuyệt đối **không** kèm đáp án nào đúng |

- Câu một đáp án nhận nhiều hơn một id trả **422**, id không thuộc câu đó trả **422** (như trước).
- **Chấm điểm câu nhiều đáp án: tất cả hoặc không gì cả.** Câu chỉ đúng khi tập đáp án đã chọn **bằng đúng** tập đáp án đúng — chọn thiếu hay chọn thừa đều 0 điểm; bỏ trống thì 0 điểm; chưa có điểm từng phần. Câu một đáp án chấm như cũ.
- Cột `StudentAnswer.SelectedAnswerIds` là `integer[]`, một dòng cho mỗi (lượt, câu); bỏ chọn hết thì dòng có mảng rỗng hoặc không có dòng.

### 4.8 Kết quả và tổng quan

| Method | Path | Vai trò | Mô tả |
|---|---|---|---|
| GET | `/api/results` | teacher | Bảng kết quả, lọc chéo theo đề / học sinh / nhóm / trạng thái |
| GET | `/api/results/stats` | teacher | Các ô số liệu đầu trang, nhận cùng bộ lọc với bảng kết quả: `totalAttempts`, `gradedCount`, `inProgressCount` (lượt đang làm dở, chưa nộp), `averageScore`, `highestScore`, `completionRate` |
| GET | `/api/results/{attemptId}` | teacher, student, admin | Chi tiết một lượt làm bài, soi từng câu. Giáo viên mở được bài của đề **mình tạo** và bài của em được giao **qua lớp mình đang dạy** (lớp đã công bố, kể cả đề của giáo viên trước); bài khác 404. Với đề chia nhiều đề, giáo viên nhận thêm `paperLabel` (mã đề của em); học sinh **không bao giờ** nhận trường này. Quản trị viên mở được bài của **mọi** học sinh, xem đầy đủ như giáo viên sở hữu đề (đáp án đúng, mã đề) và chỉ đọc: không có endpoint ghi nào cho vai trò này trên bài làm |
| GET | `/api/results/{attemptId}/summary` | student | **Bổ sung** — cho màn "Đã nộp bài". Hiện trang đó gọi `getAttemptDetail`, tức kéo về toàn bộ câu hỏi chỉ để hiện một vòng tròn điểm |
| GET | `/api/my/results` | student | Lịch sử làm bài của chính mình. **Chỉ gồm lượt đã nộp hoặc đã chấm**; bài đang làm dở nằm ở `GET /api/my/exams` |
| GET | `/api/my/results/export` | student | **Bổ sung** — xuất **bảng điểm của chính mình** ra Excel (`.xlsx`): mọi bài đã nộp, mới nhất trước. Danh tính lấy từ token; `termId?` lọc theo kỳ giống `/api/my/transcript` (`0` là lớp chưa phân kỳ, có thêm dòng "Kỳ học: …" ở phần đầu file); giáo viên và quản trị viên nhận **403**. Đi qua đúng cổng xem điểm của `GET /api/my/results` — bài chưa được công bố điểm ghi "Chưa công bố" và **không có số nào** trong file. Nội dung ở §5, "Xuất bảng điểm" |
| GET | `/api/dashboard/teacher` | teacher | Endpoint **gộp** cho trang tổng quan |
| GET | `/api/dashboard/student` | student | Endpoint **gộp** cho trang tổng quan |

> **Phía giao diện giáo viên không còn trang bảng kết quả chung** (đã bỏ): bài làm được mở từ lớp → học sinh, hoặc từ bài kiểm tra → lớp → học sinh. `GET /api/results` vẫn được dùng để liệt kê bài làm của một học sinh (`studentId`); `GET /api/results/stats` hiện chưa có nơi nào ở giao diện gọi.
> **Chi tiết một lượt (`GET /api/results/{attemptId}`) với câu nhiều đáp án đúng.** Mỗi câu mang `allowMultiple` và `selectedAnswerIds` (mảng, rỗng khi bỏ trống — luôn có, vì đó là lựa chọn của chính em). Khi đủ điều kiện xem đáp án (rule ⑥) mới có thêm `correctAnswerIds` (đủ mọi đáp án đúng theo thứ tự hiển thị) và `isCorrect`; không đủ điều kiện thì hai trường này **vắng mặt hẳn** như trước. Giao diện đối chiếu `selectedAnswerIds` với `correctAnswerIds` để chỉ ra ô chọn thiếu, ô chọn thừa.
> Hai endpoint dashboard **nên gộp**, không nên tách: FE hiện gọi đúng một lần và render cả trang. Tách ra sẽ thành 6 truy vấn độc lập cho một màn hình.
> `GET /api/results/stats` hiện không nhận bộ lọc, nên số liệu là của toàn hệ thống và không đổi theo bảng bên dưới — FE phải ghi chú *"Toàn hệ thống"* để tránh hiểu nhầm. Cân nhắc cho nhận bộ lọc.

### 4.9 Giáo viên

Chỉ quản trị viên. Nhà trường cấp tài khoản, gán môn, khoá khi giáo viên nghỉ; giáo viên không tự đổi email, số điện thoại hay môn của mình (xem §5, "Tài khoản").

| Method | Path | Vai trò | Mô tả |
|---|---|---|---|
| GET | `/api/teachers` | admin | Danh sách có phân trang: `search` (họ tên không dấu, email, số điện thoại), `status` (`active` · `locked` · `all`; giá trị khác trả 400), `subjectId`, `page`, `pageSize`. Mỗi dòng có `groupCount` (số lớp đang phụ trách, kể cả nháp) |
| GET | `/api/teachers/{id}` | admin | Một giáo viên kèm `groups` (các lớp đang phụ trách). Id của học sinh hay quản trị viên trả **404** |
| POST | `/api/teachers` | admin | Tạo tài khoản, thân `{ fullName, email, phone, subjectId, password }`. **`phone` bắt buộc** (9–15 chữ số; thiếu hay sai định dạng trả **400** `errors.phone`). **Email là tên đăng nhập**; trùng bất kỳ định danh nào đang có (email, tên đăng nhập, mã) trả **409**, môn không có thật trả **400**. Giáo viên bị ép đổi mật khẩu ở lần đăng nhập đầu |
| PUT | `/api/teachers/{id}` | admin | Sửa họ tên, email, số điện thoại, môn (cùng thân với POST, không có mật khẩu). **Đổi môn trả 409** khi giáo viên đã soạn câu hỏi hoặc đề: ngân hàng và việc sinh đề đều chia theo môn của người soạn |
| PATCH | `/api/teachers/{id}/status` | admin | Khoá / mở khoá — `active` hoặc `locked`. **Khoá bắt buộc `reason`** (10–512 ký tự); khoá xoay `SecurityStamp` nên mọi phiên đang mở chết ngay và kết nối hub bị cắt. Lớp giáo viên đang phụ trách vẫn giữ tên họ cho tới khi quản trị viên đổi người |
| POST | `/api/teachers/{id}/reset-password` | admin | Cấp mật khẩu ngẫu nhiên mới, ép đổi ở lần đăng nhập đầu, đăng xuất mọi phiên. Mật khẩu ở dạng đọc được **đúng một lần** |
| GET | `/api/teachers/{id}/history` | admin | Lịch sử khoá / mở khoá kèm lý do và người thực hiện, mới nhất trước (cùng hình dạng `AccountChange` với học sinh, §4.3) |

Không có xoá: giáo viên sở hữu câu hỏi, đề và lịch sử giao bài, xoá dòng là làm thủng chúng. Giáo viên nghỉ việc thì **khoá** tài khoản (kèm lý do) rồi đổi giáo viên phụ trách của các lớp họ đang dạy (`PUT .../teacher`, §4.4).

### 4.10 Nhập từ Excel

Chỉ quản trị viên. Mỗi kỳ có vài nghìn học sinh, không ai nhập tay từng em: tải file mẫu, điền, rồi nhập một lần. Có **bốn** loại: **tổng hợp (cả trường, một file)**, học sinh, giáo viên, lớp. Trường thường giữ một danh sách duy nhất nên nhập tổng hợp là đường chính; ba loại riêng dùng khi chỉ thêm một thứ (ví dụ vài học sinh vào lớp đã có). Thân request của `POST` là `multipart/form-data` (trường `file` là `.xlsx`, tối đa **5 MB**).

| Method | Path | Mô tả |
|---|---|---|
| GET | `/api/imports/students/template` | File mẫu `.xlsx`: trang nhập liệu (hàng tiêu đề, ô Lớp có danh sách chọn các lớp đang có), trang "Danh sách" và trang "Hướng dẫn" |
| POST | `/api/imports/students` | Nhập học sinh. Cột: `Mã học sinh*`, `Họ và tên*`, `Lớp`, `Email`, `Số điện thoại`, `Tên đăng nhập`. Trường form kèm theo: `dryRun`, `groupId?` (lớp cho các dòng để trống ô Lớp), `password?` (mật khẩu ban đầu chung của đợt). Tối đa **5000** dòng |
| GET | `/api/imports/teachers/template` | File mẫu giáo viên (ô Môn có danh sách chọn) |
| POST | `/api/imports/teachers` | Nhập giáo viên. Cột: `Họ và tên*`, `Email*`, `Số điện thoại*`, `Môn giảng dạy`. Trường form: `dryRun`, `subjectId?` (môn cho các dòng để trống), `password?`. Tối đa **1000** dòng |
| GET | `/api/imports/groups/template` | File mẫu lớp (ô giáo viên có danh sách chọn các email đang hoạt động) |
| POST | `/api/imports/groups` | Nhập lớp. Cột: `Tên lớp*`, `Giáo viên phụ trách*` (email), `Mô tả`, `Kỳ học` (không bắt buộc). Trường form: `dryRun`. Tối đa **500** dòng |
| GET | `/api/imports/school/template` | File mẫu **nhập tổng hợp**: MỘT trang "Học sinh" (ô Môn có danh sách chọn), trang "Danh sách" (môn) và trang "Hướng dẫn" |
| POST | `/api/imports/school` | **Nhập tổng hợp từ một file**: mỗi dòng một học sinh, kèm lớp và giáo viên chủ nhiệm của lớp. Cột: `Mã học sinh*`, `Họ và tên*`, `Lớp*`, `Giáo viên chủ nhiệm*`, `Email giáo viên*`, `Số điện thoại giáo viên*`, `Môn giảng dạy`, `Email học sinh`, `Số điện thoại học sinh`, `Tên đăng nhập`, `Kỳ học` (không bắt buộc, kỳ của lớp). Trường form: `dryRun`, `subjectId?` (môn cho giáo viên chủ nhiệm không ghi môn; **màn nhập trên web không gửi trường này**, môn đọc từ cột Môn giảng dạy của file), `password?` (mật khẩu ban đầu chung cho **mọi** tài khoản mới, giáo viên lẫn học sinh), `publishGroups?` (công bố ngay các lớp vừa tạo). Tối đa **20000** dòng. Phản hồi hình dạng riêng, xem bên dưới |

Phản hồi của ba `POST` nhập riêng cùng một hình dạng: `{ dryRun, totalRows, created, placed, unchanged, failed, issues: [{ row, column?, message }], issuesTruncated, accounts: [{ fullName, username, code?, group? }], termsCreated? }` (`termsCreated`: tên các kỳ học mới được tạo cùng lớp, chỉ có ở nhập lớp). `row` là số dòng thật trong file Excel (hàng tiêu đề là 1). `issues` cắt ở 500 dòng đầu nhưng `failed` đếm đủ. `accounts` chỉ có khi nhập thật. Lỗi cả file (không phải Excel, thiếu cột bắt buộc, quá 5 MB hay quá số dòng, mật khẩu thiếu hoặc ngắn khi có tài khoản mới) trả **400** kèm `message`; lỗi từng dòng đi vào `issues` và **không** làm hỏng cả mẻ. Quy tắc chi tiết ở §5, "Nhập từ Excel".

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

### Đề theo danh sách câu hỏi

Đề có thể soạn bằng **danh sách câu hỏi cụ thể** thay vì ma trận: giáo viên chọn từng câu từ ngân hàng (hoặc bấm "Soạn tự động"), đặt điểm cho từng câu, **duyệt xong mới lưu**. Học sinh nhận đúng những câu đó.

- Gửi `questions: [{ questionId, point }]` (thứ tự mảng là thứ tự trong đề; mảng rỗng được với bản nháp). Có mặt `questions` thì các trường ma trận (`totalQuestions`, ba tỷ lệ, `pointEasy/Medium/Hard`, `topic`) **bị bỏ qua**: server dẫn xuất lại từ danh sách để mọi màn hình đọc chúng vẫn chạy. `usesQuestionList = true` trong `ExamDto`.
- Mỗi lần `PUT` một đề như vậy **phải gửi lại** `questions` (thiếu thì 400): danh sách là một phần của "thay toàn bộ cấu hình".
- Điểm: mỗi câu `0 ≤ point ≤ 100`, tối đa 2 chữ số thập phân; tổng ≤ 9999,99. `totalScore` là tổng điểm từng câu (chụp lúc lưu vào `Exam.ListTotalScore`). Chuẩn là 10 (trước đây 100) nhưng không bắt buộc, như đề ma trận. Trần 100 của **một câu** là trần cứng của server, cố ý khác tổng chuẩn để đề cũ chấm trên 100 vẫn sửa được.
- Mỗi câu phải còn trong ngân hàng mà giáo viên đọc được (cùng môn; câu riêng tư của đồng nghiệp thì không). Bản nháp được giữ cả câu đang bị ẩn; xuất bản (`lifecycle` bỏ trống, hoặc `POST /{id}/publish`) thì phải có ít nhất một câu, mọi câu đang bật, tổng điểm > 0.
- **Bản đề** sinh lúc giao bài rút thẳng từ danh sách, theo thứ tự (khi tắt trộn), mỗi `ExamPaperQuestion.Point` lấy từ điểm riêng của câu. Mọi học sinh cùng một bộ câu (trừ khi đề được chia thành nhiều đề — xem mục sau); `randomizePerStudent: true` trả **400**.
- Đổi danh sách (thêm, bớt, thay, xếp lại, đổi điểm) theo đúng ranh giới "lượt làm bài đầu tiên" như đề ma trận: chưa ai làm thì ghi và sinh lại bản đề; đã có em làm thì **409** gọi tên "danh sách câu hỏi và điểm từng câu".
- Câu hỏi đang nằm trong danh sách của một đề **không xoá được** (409), kể cả đề nháp: ẩn thay vì xoá. Ẩn một câu đang nằm trong đề đã giao khiến bản đề không sinh lại được (vẫn giữ bản cũ), và đề đó không xuất bản/giao thêm được cho tới khi thay câu.
- Đề cũ theo ma trận giữ nguyên cách chạy. Gửi `questions` lên một đề ma trận chưa ai làm thì chuyển nó sang kiểu danh sách.
- `POST /{id}/duplicate` chép cả danh sách.

### Chia một đề thành nhiều đề

Một đề theo danh sách có thể được chia thành **N đề nhỏ** cho mỗi lớp, để các em ngồi cạnh nhau cầm đề khác nhau. Danh sách câu hỏi giáo viên đã duyệt là **đề lớn**; mỗi đề nhỏ rút từ đó theo một chuẩn chung.

- **`paperCount`** (trong `SaveExamRequest` và `ExamDto`): 1–10, mặc định 1 (cả lớp một đề). Ngoài khoảng là **400** "Số đề phải từ 1 đến 10."; từ 2 trở lên mà không gửi `questions` là **400** "Chia thành nhiều đề chỉ dùng được với đề soạn theo danh sách câu hỏi."
- **`paperBonusUnits`** (trong `SaveExamRequest` và `ExamDto`): số đơn vị **0,01 điểm** cộng thêm cho các câu **mức cao nhất** (Khó; hết câu Khó thì tràn xuống Trung bình rồi Dễ) của **mỗi** đề nhỏ, để đề nhỏ cộng **đúng 10** với mọi cấu hình. Lý do: các câu cùng mức cùng điểm và điểm chỉ có hai chữ số thập phân, nên tổng điểm theo mức luôn là bội của `g × 0,01` (`g` là ước chung lớn nhất của ba số câu) — 3 / 3 / 3 câu không bao giờ ra đúng 10,00. Mặc định 0, trong 0–500; phải là 0 khi `paperCount` ≤ 1 (**400** "Điểm cộng thêm chỉ dùng được khi chia thành nhiều đề."); không lớn hơn `totalQuestions` (**400** "Điểm cộng thêm không thể nhiều đơn vị hơn số câu của một đề."). Điểm của một câu trong bản đề của em là điểm mức, cộng 0,01 nếu câu đó nằm trong `paperBonusUnits` câu đầu theo thứ tự Khó → Trung bình → Dễ **trong bản đề của chính em** (nên các em có thể có hai câu cùng mức hơn kém nhau 0,01, nhưng mọi bản đề cùng tổng). Ví dụ 3 / 3 / 3 câu, điểm 0,9 / 1,1 / 1,33 = 9,99, `paperBonusUnits = 1` → một câu Khó mỗi đề là 1,34, tổng đúng 10,00. Giao diện tự tính điểm theo mức và `paperBonusUnits`; giáo viên không nhập.
- **Từ 2 đề**, `totalQuestions` (1–500) và ba tỷ lệ `easyPercentage` / `mediumPercentage` / `hardPercentage` là **cấu hình của MỘT đề nhỏ** do giáo viên nhập; server **không** dẫn xuất lại chúng từ danh sách như với đề một đề. Số câu mỗi mức tính bằng đúng công thức làm tròn của ma trận (mức Khó nhận phần dư). `pointEasy` / `pointMedium` / `pointHard` vẫn dẫn xuất từ danh sách, và **`totalScore` là tổng điểm của một đề nhỏ** (số câu mỗi mức × điểm mức đó, cộng `paperBonusUnits` × 0,01).
- **Điều kiện lưu và xuất bản** (bản nháp được miễn tổng tỷ lệ và số câu đủ; khoảng giá trị của từng trường luôn được kiểm): ba tỷ lệ cộng đúng 100; mỗi mức, đề lớn có ít nhất bằng số câu **một** đề cần ở mức đó (thiếu thì **400** "Mức Dễ: mỗi đề cần X câu nhưng đề lớn chỉ có Y câu Dễ…"); các câu **cùng mức phải cùng điểm** (**400** "Đề nhiều đề: các câu Dễ phải cùng điểm…") để mọi đề nhỏ có chung tổng điểm.
- **Chia câu** (`PaperPartitioner`; `PaperPlanner` tính trước cùng công thức, nên con số giáo viên thấy lúc soạn là con số lúc sinh đề). Ưu tiên theo thứ tự: mỗi đề đủ số câu → đúng tỷ lệ độ khó → tới tác giả/chủ đề. Mỗi mức: nhu cầu là N × số câu mỗi đề; các câu được dùng **đều nhau** (hai câu cùng mức chênh nhau nhiều nhất một lần) và trong một đề **không bao giờ có hai câu giống nhau**. Đề lớn đủ câu cho mọi đề (số câu mức đó ≥ N × số câu mỗi đề) thì các đề không trùng nhau; không đủ thì **được trùng** (không phải lỗi); dư thì một số câu không vào đề nào.
- **Mỗi lớp xáo riêng**: bảng `ExamPaperVariants (ExamId, GroupId, VariantIndex, QuestionIds integer[])` giữ bộ câu của từng đề nhỏ ở từng lớp, nên "đề A" của hai lớp có thể gồm các câu khác nhau.
- **Phát đề**: lúc giao bài, mỗi em có một `ExamPaper` riêng (`AssignmentId`, `VariantIndex`) chép từ đề nhỏ của em, thứ tự câu và đáp án trộn riêng khi tuỳ chọn trộn bật. Em nhận đề **ngẫu nhiên nhưng cân bằng** (bộ sinh số ngẫu nhiên mật mã, xáo danh sách trước khi chia): số em mỗi đề trong một lớp chênh nhau nhiều nhất 1 và **không theo thứ tự danh sách** (không xoay A, B, C, D), để học sinh không suy ra đề của mình từ vị trí. Em vào lớp sau được xếp vào đề đang ít em nhất.
- **`paperLabel`** ("A", "B"…) trong `ExamAssignmentDto` (`GET /api/exams/{id}/assignments`): mã đề của em, vắng với đề không chia. **Chỉ giáo viên và quản trị viên thấy**; học sinh không bao giờ nhận mã đề của mình hay của bạn.
- **`GET /api/exams/{id}/papers`** → `ExamPapersDto`: `{ paperCount, levels: [{ level, poolSize, perPaper, sufficient, sharedQuestions, unusedQuestions }], classes: [{ groupId?, groupName, students, papers: [{ variantIndex, label, students, questionCount, easy, medium, hard, questionIds }] }] }`. Đề không chia: `paperCount: 1` và hai mảng rỗng. Giáo viên đọc đề của mình, quản trị viên đọc mọi đề, học sinh **403**, đề của giáo viên khác **404**.
- **Khoá**: đổi `paperCount` ("số đề"), `totalQuestions`, ba tỷ lệ hay `paperBonusUnits` ("điểm cộng thêm của đề nhỏ") khi đã có em bắt đầu làm là **409** gọi đúng tên trường; chưa ai làm thì ghi và các bản đề đã phát được chia lại, phát lại.
- `randomizePerStudent: true` với đề theo danh sách vẫn **400**: chia đề là cách chống gian lận thay cho nó. `POST /{id}/duplicate` chép cả `paperCount` và `paperBonusUnits`.
- **H10 (đáp án đã lộ)**: thêm học sinh vào một lớp đã có đề nhỏ, khi mọi cửa sổ làm bài của đề đã đóng (đáp án đã tới tay các em trước), là **409** — vì các câu của đề lớn đã lộ cùng đáp án, nên đổi cách chia cũng không giúp gì; câu chữ riêng cho đề chia nhiều đề, khuyên nhân bản đề. Lớp chưa có đề nhỏ nào (giao mới) không bị chặn, và còn lớp nào chưa hết giờ thì chưa bị chặn.
- Chia nhiều đề chỉ có với đề soạn theo danh sách (giáo viên tự duyệt đề lớn); đề theo ma trận không chia nhiều đề.

**Validate đề (theo ma trận):** `title` không rỗng sau trim · `1 ≤ durationMinutes ≤ 300` · `totalQuestions ≥ 1` · `pointEasy`, `pointMedium`, `pointHard` không âm, tối đa 100 và tối đa 2 chữ số thập phân (mức có câu thì phải > 0) · `closeAt > openAt` · nếu `lifecycle ≠ 'draft'` thì tổng ba tỷ lệ phải bằng 100 và ngân hàng phải đủ câu. Tổng điểm khác 100 **không** bị chặn (mục dưới).
Bổ sung một kiểm tra FE chỉ cảnh báo ở UI: **`durationMinutes` không được lớn hơn khoảng cách `openAt..closeAt`** — làm bài 90 phút trong cửa sổ mở 30 phút là vô nghĩa.

### Điểm của đề: chuẩn 10, gắn theo mức độ khó

Tổng điểm **chuẩn** của một đề là 10 (đổi từ 100 ngày 15/10/2026), nhưng server không bắt buộc (xem dưới). Điểm gắn theo **mức độ khó**, không theo từng câu cụ thể: đề chỉ là ma trận (xem trên), câu hỏi thật chỉ xuất hiện khi sinh bản đề và khác nhau giữa các học sinh, nên giáo viên đặt ba số `pointEasy`, `pointMedium`, `pointHard` (điểm của MỘT câu ở mức đó).

```
tổng điểm = nDễ × pointEasy + nTrungBình × pointMedium + nKhó × pointHard      (nDễ, nTB, nKhó: số câu từ ma trận)
```

- Số câu mỗi mức cố định theo ma trận nên **mọi học sinh cùng một tổng điểm**, kể cả khi `randomizePerStudent` bật. `ExamPaperQuestion.point` lấy theo mức của câu lúc sinh bản đề.
- **Server không chặn tổng khác 10** ở bất kỳ đường lưu nào (tạo, sửa, nhân bản, xuất bản). Giáo viên chọn số câu bất kỳ — 56 câu chẳng hạn — và nhiều cấu hình không chia ra đúng 10,00 với hai chữ số thập phân; bắt đủ 10 thì họ kẹt ở những số câu đó. Server giữ đúng con số thật (phép cộng dùng `decimal`, không làm tròn) và báo lại ở `totalScore`; giao diện hiện "n / 10" và hỏi xác nhận trước khi chốt một đề chưa đủ, nên luật nhắc nhở nằm ở FE chứ không ở API.
- Đề có từ trước luật này (chấm trên 100, hay 9 câu × 1 điểm) giữ nguyên điểm cũ; không có phép chuyển dữ liệu nào đổi tổng của chúng. Đề đã có người làm vẫn bị khoá cấu hình như cũ (§5), điểm đã chấm không bao giờ đổi.
- **Không phải cấu hình nào cũng chia được đúng 10.** Tổng điểm luôn là bội của ước chung lớn nhất của ba số câu (khi điểm có hai chữ số thập phân), nên 30 câu chia 12/12/6 không thể ra đúng 10,00. Frontend báo điều đó, điền cách chia sát 10 nhất và gợi ý số câu gần nhất chia được đúng.
- Response `ExamDto` có `pointEasy`, `pointMedium`, `pointHard` và `totalScore` (tính sẵn). Học sinh chỉ nhận `totalScore` ở `StudentExamItemDto` (thẻ bài), không nhận ba điểm từng mức. Điểm của từng câu trong bản đề (`point`) vẫn hiện lúc làm bài và ở kết quả để biết câu nào nặng hơn; từ đó học sinh suy ra được mức điểm của câu, và đó là chủ ý.
- Điểm tổng hợp (`averageScore`, `highestScore`, `Student.averageScore`) **quy về thang 10** (`score / maxScore × 10`, hằng `ExamScoring.TotalScore`) trước khi gộp, để đề có `maxScore` khác 10 (kể cả đề cũ chấm trên 100) vẫn so được với nhau; làm tròn hai chữ số thập phân.

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

### Lớp và thành viên

Lớp là của tổ chức, nên mọi việc đổi chỗ học sinh đều do quản trị viên làm và đều để lại dấu vết.

- **Lý do bắt buộc** (10–512 ký tự sau khi bỏ khoảng trắng hai đầu, cùng một bộ luật ở mọi nơi) cho: **khoá** tài khoản (học sinh và giáo viên), **yêu cầu xoá** tài khoản, **gỡ khỏi lớp**, **chuyển lớp**, **khoá lớp**, **đổi giáo viên phụ trách** lớp đang có người khác. Mở khoá (tài khoản hay lớp), khôi phục, xếp thêm một em vào một lớp, và chọn giáo viên cho lớp chưa có ai không cần lý do — không ai bị đổi chỗ hay mất gì. Thiếu hoặc ngắn trả **400** với `errors.reason`.
- **Mỗi lớp có đúng một giáo viên phụ trách**, chọn ngay lúc lập lớp (`teacherId` bắt buộc). Lớp và giáo viên vào cùng một lần lưu, nên không có khoảnh khắc nào lớp tồn tại mà chưa có người phụ trách. Đổi người bằng `PUT .../teacher`: thay cả tập (không thêm), lý do bắt buộc khi đang có người khác, hai quản trị viên cùng đổi một lớp xếp hàng (khoá dòng lớp `FOR UPDATE`) nên không bao giờ còn hai giáo viên. Tài khoản giáo viên bị khoá không được giao lớp (**409**); công bố lớp chưa có giáo viên bị từ chối (**409**). Lớp cũ còn nhiều giáo viên hay chưa có ai vẫn đọc được và được chuẩn hoá ở lần đổi kế tiếp. Giáo viên nghỉ việc thì khoá tài khoản rồi đổi giáo viên của các lớp họ phụ trách.
- **Một học sinh học nhiều lớp cùng lúc.** Quan hệ học sinh – lớp là nhiều-nhiều (`StudentGroupMember`, khoá chính `(StudentGroupId, StudentId)`): xếp em vào một lớp chỉ **thêm** một dòng, không đụng tới các lớp khác; gỡ chỉ xoá dòng của lớp đó. **Chuyển lớp = gỡ khỏi lớp nguồn + xếp vào lớp đích** trong một transaction (khoá dòng tài khoản `FOR UPDATE` như gỡ). Mô hình một lớp từng có hai luật dựa vào "một em một chỗ" — **khoá chuyển lớp khi em đã bắt đầu làm bài** và **em đã làm bài chỉ quay lại được đúng lớp cũ** (`skippedLocked`) — cả hai đã **bỏ**: bài làm gắn với lớp được giao (`AssignedViaGroupId`), không với "lớp hiện tại" của em, nên đổi lớp không làm lệch điểm lớp nào.
- **Em nghỉ học thì gỡ khỏi lớp**, kèm lý do. Gỡ luôn làm được kể cả khi em đã làm bài, và em xếp lại vào lớp nào cũng được. Bài đã làm và điểm giữ nguyên.
- **Khoá lớp** (`POST .../lock`, lý do bắt buộc; không có "kết thúc lớp"). Cờ `StudentGroup.IsLocked` + `LockedAt`, độc lập với nháp / công bố. Lớp khoá là **chỉ đọc**: mọi thao tác ghi lên lớp (xếp thêm, gỡ, chuyển vào hoặc ra, giao bài, sửa, đổi giáo viên, công bố / gỡ công bố) trả **409** kèm câu nêu tên lớp; riêng khoá, mở khoá và xoá lớp rỗng vẫn làm được. Khoá **không** đụng tới thành viên, bài đã giao, lượt làm bài hay bảng điểm, và các em đang làm bài giao trước đó vẫn làm nốt. Lý do lưu ở `GroupChange` (`Locked` kèm lý do, `Unlocked`).
- **Không xử lý riêng ca "một đề giao cho hai lớp mà em học cả hai".** Nghiệp vụ không có ca này: một em không học hai lớp cùng môn, nên một đề (của giáo viên một môn) không đi tới cùng một em qua hai lớp. Khoá duy nhất `(ExamId, StudentId)` của `ExamAssignment` vẫn có, chỉ không ai dựa vào nó để phân xử.
- **Em nghỉ mà không ai gỡ.** Hồ sơ học sinh phía quản trị (`GET /api/students/{id}/exams`) liệt kê cả những bài sau của lớp em chưa làm hoặc đã hết hạn mà em không làm, để người quản lý thấy em vắng mà không cần gỡ.
- **Thu hồi bài khi rời lớp.** Gỡ hoặc chuyển lớp xoá khỏi em những bài do **lớp rời đi** giao (`ExamAssignment.AssignedViaGroupId` = lớp rời; bài giao qua lớp khác của em giữ nguyên) mà em **chưa có lượt làm** và **chưa hết hạn** (`ResolveStatus = Assigned`); đếm vào `revokedExams`. Bài em đã bắt đầu và bài đã hết hạn mà em không làm được **giữ** (`keptExams`): một là kết quả đã có, hai là bằng chứng vắng mặt. Em **không tự nhận** bài đã giao cho lớp mới — giáo viên giao lại cho lớp nếu muốn em làm (đúng như "Giao bài" bên dưới).
- **Không có đường đổi lớp lách.** `PUT /api/students/{id}` không thêm, bớt hay đổi lớp của em (`groupId` gửi lên phải là một lớp em đang học, nếu không 400). Rời hay đổi lớp đều có lý do: `transfer` hoặc gỡ; chỉ việc **thêm** một lớp mới không cần lý do (không ai mất gì).
- **Lịch sử.** Mỗi lần khoá, mở khoá, yêu cầu xoá, khôi phục, xếp, gỡ, chuyển lớp ghi một dòng `AccountChange` (một bảng chung cho học sinh và giáo viên; giáo viên chỉ có khoá và mở khoá) trong **cùng transaction** với thay đổi — không có thay đổi mà thiếu dấu vết. Bảng chỉ thêm, không sửa, không xoá; tên lớp chép lại đúng như lúc xảy ra và hai cột mã lớp **không có khoá ngoại** (lớp trống xoá được mà lịch sử phải còn). Giống `DeletionReason`, lịch sử được giữ lại cả khi dữ liệu cá nhân của em bị ẩn danh hoá. Lớp có bảng `GroupChange` riêng (lập lớp, đổi giáo viên phụ trách kèm lý do, khoá và mở khoá lớp) theo cùng nguyên tắc: tên giáo viên chép lại, hai cột mã giáo viên không có khoá ngoại, lịch sử đi theo lớp khi lớp trống bị xoá.
- **Thứ tự khoá hàng** khi gỡ/chuyển: dòng tài khoản học sinh trước (`FOR UPDATE`), rồi các dòng `ExamAssignment` của em theo `Id` tăng dần — cùng thứ tự với lúc em bấm "Vào thi", nên hai việc xếp hàng chứ không chờ vòng tròn lẫn nhau.

### Kỳ học

Kỳ học gom lớp theo thời gian để học sinh lọc "Lớp học của tôi" và "Bảng điểm": học kỳ ở đại học (một năm ba kỳ), hay cấp lớp ở phổ thông (mỗi kỳ là "Lớp 11", "Lớp 12"). Kỳ **chỉ là nhãn gom lớp**: không đổi luật nào của lớp (khoá, chuyển lớp, giáo viên phụ trách, giao bài), nên giáo viên không có thao tác mới nào.

- **Danh mục do quản trị viên quản lý** (`/api/terms`, §4.4a), tên tự do vì mỗi trường chia kỳ một kiểu; ngày bắt đầu và kết thúc không bắt buộc, chỉ để xếp kỳ mới lên trước. Bảng `AcademicTerm` (`Name` duy nhất không phân biệt hoa thường).
- **Tối đa một kỳ hiện tại.** Chỉ mục duy nhất có điều kiện (`IsCurrent = TRUE`) giữ luật ở database; đặt kỳ khác làm hiện tại thì kỳ cũ được tắt và lưu **trước**, rồi mới bật kỳ mới, cùng một transaction. Được phép không có kỳ hiện tại. Giao diện học sinh mở sẵn kỳ hiện tại; hết kỳ không có gì tự khoá, lớp của kỳ cũ vẫn mở như cũ cho tới khi quản trị viên khoá.
- **Lớp có kỳ là tuỳ chọn** (`StudentGroup.AcademicTermId`, khoá ngoại `Restrict`): lớp chưa phân kỳ hoạt động đầy đủ và hiện ở mục "Chưa phân kỳ" của học sinh; lớp cũ trước khi có tính năng này nằm ở đó. Gán khi lập lớp, khi sửa lớp (lớp đã khoá vẫn bị chặn như mọi thao tác sửa) hoặc bằng cột "Kỳ học" của nhập Excel. **Không xoá được kỳ còn lớp** (**409**): chuyển lớp sang kỳ khác hoặc bỏ phân kỳ trước.
- **Học sinh lọc theo kỳ của lớp đã giao bài** (`ExamAssignment.AssignedViaGroup.AcademicTermId`), nên một bài thuộc kỳ của lớp giao nó. `GET /api/my/terms` chỉ liệt kê kỳ em có lớp đã công bố; `GET /api/my/transcript?termId=` và `GET /api/my/results/export?termId=` lọc cùng một luật, nên file xuất khớp bảng điểm đang xem. Điểm trung bình môn và điểm chung tính **trong kỳ đang lọc**, theo đúng luật tính điểm đã có.
- **Nhập Excel**: cột "Kỳ học" ghi kỳ của lớp (một lần cho mỗi lớp, hai kỳ khác nhau trong một lớp thì dòng sau bị từ chối). Kỳ trùng tên với kỳ có sẵn (không phân biệt hoa thường và dấu) thì lớp nối vào kỳ cũ; chưa có thì được tạo mới cùng lớp, không ghi ngày và không phải kỳ hiện tại, bước xem trước liệt kê (`termsCreated`). Kỳ có sẵn đọc không theo dõi (`AsNoTracking`) nên lớp mới chỉ gán khoá ngoại, không gán cả đối tượng kỳ (gán đối tượng là EF chèn lại kỳ cũ như một kỳ mới và đụng khoá chính).

### Nhập từ Excel

Mỗi kỳ có vài nghìn học sinh nên quản trị viên nhập bằng file Excel (§4.10). Mọi đường nhập dùng **cùng luật** với thao tác bằng tay — nhập không phải cửa hậu.

- **Hai bước, một đường mã.** `dryRun=true` kiểm tra và đếm mà không ghi gì; `dryRun=false` ghi thật **và kiểm tra lại từ đầu** (server không tin kết quả xem trước). Cả hai chạy cùng một hàm nên bản xem trước không thể hứa điều bản nhập thật không làm. Nhập thật là **một lần `SaveChanges`** (một transaction): hoặc cả mẻ vào, hoặc không dòng nào.
- **Dòng hỏng không làm hỏng cả mẻ.** Dòng hợp lệ được nhập; dòng hỏng được liệt kê kèm số dòng Excel, tên cột và lý do. File có dòng hỏng sửa xong **gửi lại nguyên file** là an toàn: dòng đã nhập được nhận ra (`unchanged`), không bị báo trùng, và không đòi mật khẩu khi không còn tài khoản nào để tạo.
- **File tải lên là dữ liệu không tin cậy.** Chỉ nhận `.xlsx`; kiểm gói zip trước khi đưa cho thư viện (không quá 1000 mục, không quá 100 MB sau khi giải nén — chặn "bom zip"); **không bao giờ tính công thức** (ô công thức chưa có giá trị đã lưu coi như ô trống); trang ẩn bị bỏ qua; hàng tiêu đề được dò trong 10 dòng đầu, tên cột không phân biệt hoa thường, dấu, khoảng trắng hay dấu `*`, và có vài tên gọi khác (`MSHS`...). Chuỗi chuẩn hoá NFC, số nguyên in đúng như gõ (mã `10001` không thành `1E+04`), số điện thoại 9 chữ số bị Excel nuốt số 0 thì được thêm lại.
- **Học sinh.** Mã học sinh là khoá nhận diện; **mã, tên đăng nhập và email dùng chung một không gian tên** với mọi tài khoản (như `availability`), nên mã trùng email của một tài khoản khác cũng bị từ chối. Tên đăng nhập để trống thì dùng mã; email để trống thì cấp email tạm `<tên đăng nhập>@student.anthill.local` (không phải hộp thư thật). Mã đã có thì dòng đó nói về **học sinh đã có**: hồ sơ **không bao giờ bị ghi đè** từ file; khác hẳn họ tên → lỗi (nghi gõ nhầm mã); tài khoản chờ xoá hay đã xoá → lỗi; được xếp **thêm** vào lớp ghi trên dòng (hoặc lớp chung `groupId`) nếu chưa học lớp đó — em đang học lớp khác vẫn được, em đã làm bài cũng vậy (cùng luật với xếp bằng tay: không rời lớp nào); em đã học lớp đó rồi → `unchanged`; lớp đã khoá → lỗi dòng (và chọn lớp chung đã khoá → 400). Chuyển hay gỡ lớp không làm bằng file (không có chỗ cho lý do). Tên lớp khớp không phân biệt hoa thường và dấu; lớp không có → lỗi dòng. Mỗi em được xếp ghi một dòng `AccountChange` `addedToGroup`.
- **Mật khẩu ban đầu chung cho cả đợt**, nhập một lần (≥ 6 ký tự), băm **một lần** (băm riêng từng em hàng nghìn lần BCrypt mất nhiều phút), mọi tài khoản mới bị ép đổi ở lần đăng nhập đầu. Chỉ bắt buộc khi thật sự có tài khoản mới để tạo. Server không trả mật khẩu về.
- **Giáo viên.** Email là khoá nhận diện (cũng là tên đăng nhập). **Số điện thoại bắt buộc** (thiếu → lỗi dòng "Chưa có số điện thoại", sai → lỗi định dạng; cột thiếu hẳn trong file → cả file bị từ chối). Môn khớp theo tên hoặc mã môn (không phân biệt dấu); ô trống dùng môn chung `subjectId`; không có môn → lỗi dòng. Email đã thuộc đúng giáo viên cùng họ tên → `unchanged`; khác họ tên hoặc là tài khoản khác → lỗi.
- **Lớp.** Tên lớp là khoá nhận diện (không phân biệt hoa thường và dấu). Mỗi dòng phải có email của một giáo viên **đang hoạt động** (tài khoản bị khoá → lỗi). Lớp mới là **nháp**, kèm dòng lịch sử `created`, giống lập lớp tay. Lớp đã có cùng giáo viên → `unchanged`; đã có mà giáo viên khác → lỗi (đổi giáo viên phải ghi lý do, làm ở trang lớp).
- **Giới hạn.** Học sinh 5000 dòng, giáo viên 1000, lớp 500 mỗi lần, nhập cả trường (một file) **20000** dòng (đo trên máy thử: xem trước 20000 dòng / 200 lớp khoảng 12 giây, phản hồi 1,7 MB); file ≤ 5 MB (vượt thì 400 với cùng một câu, do `UploadSizeLimit` chặn trước khi đọc form). Đo trên máy thử: nhập thật 5000 học sinh khoảng 1,6 giây, nhập thử khoảng 0,15 giây.

### Xuất bảng điểm

Hai endpoint xuất file `.xlsx` (một trang tính "Bảng điểm": vài dòng tiêu đề, hàng đầu cột, dữ liệu, dòng tổng; hàng đầu cột và các cột định danh được cố định khi cuộn). Dùng chung một bộ dựng bảng (`ISpreadsheetService.BuildTable`), dữ liệu không đi qua CSV.

- **Học sinh — `GET /api/my/results/export`.** Mỗi bài đã nộp hoặc đã chấm một dòng, mới nhất trước: `STT, Bài kiểm tra, Chủ đề, Lớp (lớp được giao), Nộp lúc (giờ Việt Nam), Số câu đúng, Điểm, Điểm tối đa, Điểm thang 10, Xếp loại`; dòng cuối là điểm trung bình thang 10. **Điểm đi qua đúng cổng của `GET /api/my/results`** (`CanRevealScore`: đã chấm và giáo viên bật `showResultAfterSubmit`): bài chưa được xem điểm ghi **"Chưa công bố"** ở cột Điểm, các cột số còn lại để trống, và không tính vào trung bình — xuất file không phải đường vòng để đọc điểm sớm (§3 quy tắc ⑥). Đề cũ chấm trên thang khác được quy về thang 10 theo điểm tối đa của chính đề đó. Tuỳ chọn `?termId=` lọc theo kỳ như bảng điểm trên màn hình (kỳ không có bài nào cho file chỉ có tiêu đề, không phải lỗi).
- **Giáo viên — `GET /api/student-groups/{id}/grade-sheet`.** Mỗi học sinh **đang học lớp** một dòng (theo mã, bỏ tài khoản đã xoá), mỗi đề **đã giao cho lớp** (`AssignedViaGroupId` = lớp này) một cột, theo thứ tự soạn đề: `STT, Mã học sinh, Họ và tên, <các đề>, Điểm trung bình`. Tiêu đề cột chỉ là **tên bài kiểm tra**, không kèm thông tin đề lớn / đề nhỏ hay số câu; không có cột hay dòng đếm số bài đã làm. Ô điểm là điểm thang 10 (làm tròn 2 chữ số); **bài được giao mà em không nộp (chưa làm, đang làm dở, hết giờ không nộp) là 0 điểm** và vẫn tính vào trung bình; **"—"** = em không có bài đó (vào lớp sau khi đề đã giao), ô này không tính. Giáo viên phụ trách thấy điểm mọi bài của lớp (không qua cổng của học sinh), **kể cả đề do giáo viên trước của lớp soạn**. Một dòng cuối: trung bình lớp theo từng đề (và trung bình các điểm trung bình). Điểm trung bình của một em là trung bình mọi đề em được giao, **kể cả đề 0 điểm**; trung bình lớp theo từng đề là trung bình mọi em được giao đề đó. (Bản đầu để ô trống và bỏ khỏi trung bình; người dùng sửa 19/10 vì như vậy một em bỏ cả đề vẫn có điểm trung bình bằng điểm đề còn lại.) Điểm trung bình trong hồ sơ học sinh và tab Bài kiểm tra trong lớp cũng chia cho số bài **đã giao** (bài chưa làm tính 0). Riêng bảng điểm của học sinh (§4.7) chỉ tính bài đã nộp và được xem điểm, cộng bài hết hạn mà em không làm; bài còn làm được hay sắp tới chưa được tính.
- **Bảng điểm là của lớp, không của người soạn đề.** Giáo viên phụ trách một lớp chứ không phải một đề; lớp đổi giáo viên giữa chừng (đổi kèm lý do, §5) thì người mới cần thấy điểm các đề người cũ đã giao để nắm sức học cả lớp. Đây là ngoại lệ có chủ đích của quy tắc "chỉ xem kết quả đề do chính mình tạo" áp cho các endpoint kết quả khác (`GET /api/results...`): file chỉ chứa điểm từng em. Từ tab "Bài kiểm tra trong lớp" giáo viên đang dạy lớp cũng mở được bài làm của các em ở những đề đó (`GET /api/student-groups/{id}/exams/{examId}/results` rồi `GET /api/results/{attemptId}`), vì cùng lý do.
- **Nội dung do người dùng gõ là dữ liệu không tin cậy.** Mọi chuỗi (tên học sinh, tên đề, tên lớp) được ghi là ô **văn bản**, không bao giờ là công thức, dù bắt đầu bằng `=`, `+`, `-` hay `@`; kiểm bằng file đọc lại (không có ô công thức nào). Tên file chỉ gồm chữ ASCII thường, số và dấu gạch (`bang-diem-<mã học sinh>.xlsx`, `bang-diem-lop-<tên lớp>.xlsx`).
- **Quyền.** Chỉ học sinh xuất bảng điểm của mình và chỉ giáo viên đang dạy lớp đã công bố xuất bảng điểm lớp; quản trị viên không xuất (403 ở cả hai). Lớp không phải của giáo viên, lớp nháp, lớp không có → **404** (403 sẽ xác nhận id có thật).

### Nhập tổng hợp (một file, ba nơi quản lý)

`POST /api/imports/school` tạo giáo viên chủ nhiệm, tạo lớp (kèm chủ nhiệm), tạo học sinh và xếp học sinh vào lớp **trong một lần**; sau đó mỗi thứ được quản lý ở trang riêng (Giáo viên, Nhóm lớp, Học sinh). Phản hồi: `{ dryRun, totalRows, subjectsCreated[], preview[], teachersCreated, teachersUnchanged, groupsCreated, groupsUnchanged, groupsPublished, studentsCreated, studentsPlaced, studentsUnchanged, failed, issues[], issuesTruncated, accounts[] }` — số theo từng loại bản ghi, còn `failed` đếm theo **dòng** của file.

- **Không viết lại luật.** `SchoolImportPlanner` (thuần logic, có unit test) chia các dòng thành ba danh sách; rồi ba bộ nhập riêng (giáo viên → lớp → học sinh) xử lý từng danh sách theo **đúng luật của chúng**: email dùng chung một không gian tên, giáo viên bị khoá không nhận lớp, hồ sơ có sẵn không bị ghi đè, mã học sinh là khoá, học sinh có sẵn chỉ được xếp thêm lớp... Nhập tổng hợp không có cửa hậu nào mà nhập riêng không có. Khác biệt: giáo viên đã có trong hệ thống lấy môn theo hồ sơ, còn môn của giáo viên **mới** đọc từ file (xem "Môn học đọc theo file" bên dưới). Khi mọi giáo viên (hay mọi lớp) của file đều hỏng thì vẫn trả 200 kèm danh sách lỗi từng dòng, không phải 400 "file trống".
- **Danh sách xem trước (`preview`).** Chỉ có khi nhập thử, để quản trị viên soát trước khi nhập (nhập thật trả `[]`; giao diện hiện mỗi lớp một tab, đầu tab ghi giáo viên phụ trách, thân tab là danh sách học sinh cuộn trong khung và có ô tìm trong mọi lớp): mỗi phần tử là một lớp `{ name, exists, teacherName, teacherEmail, teacherExists, subject, students[{ row, code, fullName, exists }] }`, `exists` nghĩa là đã có sẵn trong hệ thống (nhập chỉ xếp thêm, không tạo hay sửa). Chỉ gồm những gì nhập được: lớp bị chặn và dòng hỏng không có ở đây mà nằm ở `issues`, nên danh sách là đúng những gì nhập thật sẽ làm.
- **Môn học đọc theo file.** Cột Môn giảng dạy của từng giáo viên mới là nguồn duy nhất của môn: trường soạn file theo môn thật của mình và môn không có trang quản lý riêng. Môn ghi trong file mà hệ thống chưa có (so không phân biệt hoa thường và dấu, cả tên lẫn mã) được **tạo mới** cùng lần nhập, mã sinh từ tên (chữ cái đầu các từ: "Anh văn" → `AV`; một từ thì bốn chữ đầu: "Toán" → `TOAN`; trùng mã thì thêm số). `subjectsCreated` liệt kê tên các môn mới để bước xem trước báo người dùng soát chính tả; môn chỉ được giữ khi có giáo viên **mới** dùng nó (giáo viên của dòng đó hỏng vì lý do khác thì môn không để lại). Giáo viên đã có trong hệ thống luôn lấy môn theo hồ sơ, ô Môn trên dòng (trống, khác hay môn lạ) bị bỏ qua và không tạo môn. Giáo viên mới không ghi môn → dòng lỗi "Chưa có môn giảng dạy" (trừ khi gọi API kèm `subjectId`).
- **Kỳ học đọc theo file.** Cột Kỳ học (không bắt buộc) ghi kỳ của lớp, giống cách ghi giáo viên chủ nhiệm: một lần cho mỗi lớp, các dòng sau để trống hay lặp lại đều được. Kỳ chưa có được tạo cùng lớp (xem §5, "Kỳ học"); phần xem trước ghi kỳ của từng lớp ở đầu tab và liệt kê kỳ mới (`termsCreated`, `preview[].termName`). Hai kỳ khác nhau trong một lớp là xung đột: dòng sau bị từ chối ở cột "Kỳ học", và kỳ của dòng bị từ chối không được tạo.
- **Ô gộp (merge) được đọc đúng.** Trường hay gộp ô cột Lớp và các cột giáo viên cho cả nhóm dòng; Excel chỉ giữ giá trị ở ô trên cùng bên trái, nên bộ đọc áp giá trị của vùng gộp cho mọi dòng nó phủ (áp cho cả ba loại nhập riêng). Dòng ngoài vùng gộp không thừa hưởng gì; vùng gộp kéo dài hàng triệu dòng không tốn bộ nhớ.
- **Lớp và giáo viên suy ra từ chính các dòng.** Cùng tên lớp (không phân biệt hoa thường và dấu) là một lớp; cùng email là một giáo viên. Thông tin giáo viên chủ nhiệm chỉ cần điền **một lần** cho mỗi lớp (các dòng sau để trống hay điền lặp lại đều được). Hai ô cùng có giá trị mà khác nhau là xung đột và dòng đến sau bị từ chối: hai email chủ nhiệm khác nhau ở một lớp, hoặc cùng email mà khác tên / số điện thoại / môn. Lớp mà mọi dòng đều bị từ chối thì không được tạo.
- **Một transaction cho cả ba bước.** Bước sau cần thấy kết quả bước trước (lớp tham chiếu giáo viên mới tạo, học sinh tham chiếu lớp mới tạo) mà "nhập thử" không ghi gì thì không thấy được. Nên **nhập thử cũng chạy thật rồi rollback**: con số xem trước là con số sẽ xảy ra (nhập thử dùng mật khẩu giả, không trả `accounts`). Lỗi hạ tầng (mật khẩu thiếu hoặc quá ngắn khi có tài khoản mới, môn chung không có...) trả **400** và rollback cả ba bước, không ghi gì; lỗi từng dòng thì không.
- **Lỗi lan theo quan hệ, báo một lần ở gốc.** Giáo viên hỏng (ví dụ số điện thoại sai) thì lớp của người đó không tạo được; lớp hỏng (thiếu chủ nhiệm, đã có giáo viên khác, giáo viên bị khoá...) thì các học sinh của lớp đó chưa được xếp. Nguyên nhân gốc báo ở dòng đầu tiên của lớp hay của giáo viên; các học sinh còn lại báo "Lớp … chưa nhập được (xem dòng N), nên học sinh này chưa được xếp" — không trút hàng trăm dòng cùng một lỗi. Tên cột trong lỗi là tên cột của file tổng hợp.
- **Lớp mới ở dạng nháp**, trừ khi `publishGroups=true`: khi đó các lớp vừa tạo được công bố ngay (`groupsPublished`), giáo viên chủ nhiệm thấy lớp sau khi nhập. Đổi giáo viên của lớp đã có vẫn làm ở trang lớp và phải ghi lý do.
- **Gửi lại nguyên file an toàn**: giáo viên, lớp và học sinh đã nhập được nhận ra (`...Unchanged`), không báo trùng, không đòi mật khẩu khi không còn tài khoản mới.

### Giao bài

- **Chỉ giao theo lớp:** `targets = distinct(học sinh của các lớp được chọn)`; request mang `studentIds` khác rỗng trả **400** vì bài kiểm tra không giao riêng từng học sinh. Khử trùng lặp ở BE bằng `HashSet`. FE có tính số hiển thị nhưng đó chỉ để hiện lên màn hình.
- **Idempotent** theo cặp `(examId, studentId)` — unique index. Giao lại cho người đã có bài thì bỏ qua, giữ nguyên `status`/`attemptId`/`score`.
- **Kiểm tra phạm vi:** học sinh phải thuộc một lớp đã công bố mà giáo viên đó được phân công. Không cho giáo viên A giao bài cho lớp của giáo viên B bằng cách đoán id. Em ngoài phạm vi đếm vào `skippedOutOfScope`, xét **trước** trạng thái tài khoản. **Id không tồn tại (hoặc không phải tài khoản học sinh) đếm vào cùng con số đó** — không có trường riêng cho "không tìm thấy": hai con số tách rời cho phép giáo viên bắn từng id một rồi đọc xem id nào là học sinh thật của lớp người khác. Lớp cũng vậy: lớp không tồn tại và lớp của người khác cùng đếm vào `skippedOutOfScopeGroups`.
- **Học sinh bị khoá:** FE hiển thị nhãn "Đã khóa" nhưng vẫn cho chọn và vẫn tạo assignment. BE **bỏ qua** và trả `skippedLocked` để giáo viên biết. Tài khoản đang chờ xoá cũng bị bỏ qua nhưng đếm riêng vào `skippedRetired` — em đã rời trường thì không có gì để mở khoá. Tài khoản đã xoá hẳn đã bị gỡ khỏi lớp nên rơi vào `skippedOutOfScope`.
- **Chặn giao đề đang là nháp.** FE hoàn toàn không kiểm điều này.
- **Giao vào khung giờ đã đóng** vẫn được (để dành cho lần mở lại), nhưng không sinh thông báo và không bắn `ExamAssigned`: không còn gì để "hoàn thành trước hạn nộp".
- **`randomizePerStudent` chốt từ lần giao đầu tiên.** Đề đã có lượt giao mà request đổi cờ thì **409**; muốn đổi thì thu hồi hết rồi giao lại.

### Lưu nháp và nộp bài

- **Chống ghi đè ngược thứ tự:** autosave chạy mỗi 800ms; mạng chậm khiến gói cũ tới sau gói mới. Server giữ `lastAppliedSeq` cho mỗi attempt và bỏ qua request có `clientSeq` nhỏ hơn. **Không dùng timestamp client** làm mốc.
- **Hết giờ không nhận nháp mới:** `PUT answers` trả **410** khi `serverNow > expiresAt`, và vẫn 410 khi lượt đã bị chốt vì hết giờ từ trước (bảng 409/410 ở §4.7).
- **Validate đáp án gửi lên:** `questionId` phải thuộc đúng attempt đó, mọi id trong `answerIds` phải thuộc đúng `questionId` đó, và câu một đáp án chỉ nhận tối đa một id. Trả **422** nếu lạ, không âm thầm bỏ qua.
- **`submittedAt` của bài hệ thống nộp thay là hạn của lượt** (`expiresAt`), không phải lúc tiến trình nền quét tới; nhờ vậy `durationUsedMinutes` không bao giờ vượt thời lượng của đề, kể cả khi em bỏ thi và bài được chốt hàng giờ sau. Bài em tự nộp trước giờ giữ giờ thật.
- **Nộp idempotent:** nộp lại cùng `attemptId` trả đúng kết quả cũ (200), không tạo bản ghi thứ hai. FE có cờ chống bấm hai lần, nhưng retry sau timeout mạng vẫn tới server hai lần — chỉ khoá ở DB mới chắc.

### Dữ liệu dẫn xuất — BE tính, không nhận từ client

| Trường | Định nghĩa |
|---|---|
| `Exam.assignedCount` | số assignment của đề |
| `Exam.submittedCount` | số assignment có status ∈ {submitted, graded} |
| `Exam.startedCount` | số assignment đã có lượt làm bài, ở mọi trạng thái |
| `Exam.blueprintFrozen` | `startedCount > 0` — cấu hình đề đã khoá, `PUT` chỉ còn đổi được tên, mô tả và `showResultAfterSubmit` |
| `Exam.scheduleLocked` | `assignedCount > 0` và còn dưới 120 phút nữa là tới `openAt` của đề, hoặc đang mở (tới hết `closeAt`). Lịch chung không đổi được; hết giờ đóng thì mở khoá lại |
| `ExamAssignment.scheduleLocked`, `AssignmentWindow.scheduleLocked` | Như trên nhưng tính theo khung giờ thật của em hoặc của lớp |
| `Student.completedExams` | COUNT lượt có status ∈ {submitted, graded} |
| `Student.averageScore` | Tổng (score / maxScore × 10) của các lượt `graded` chia cho **số bài đã giao** cho em (thang 10, làm tròn 2 chữ số): bài đã giao mà chưa làm tính **0 điểm**, giống bảng điểm xuất ra Excel. Chỉ có nghĩa khi `assignedExams > 0` (chưa được giao bài nào thì giao diện hiện "chưa có", không hiện 0). **Giáo viên chỉ thấy số liệu của các bài giao qua lớp mình đang dạy hoặc do mình soạn** (`averageScore`, `completedExams`, `assignedExams`, `hasStartedExams`): một em học nhiều lớp thì điểm các môn của đồng nghiệp không lọt vào con số giáo viên này thấy; quản trị viên thấy mọi bài |
| `Student.assignedExams` | COUNT bản ghi giao bài của em |
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
- **Tài khoản giáo viên** do quản trị viên tạo (§4.9): email chính là tên đăng nhập (`Username` = `Email`), không có mã học sinh, bắt buộc có môn. Giáo viên không tự đổi email, số điện thoại hay môn. Khoá bắt buộc lý do, xoay `SecurityStamp` và cắt kết nối hub ngay; không có xoá.
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
           Status,        -- Active | Locked | PendingDeletion | Deleted
           DeletionReason, DeletionRequestedAt, DeletionRequestedByUserId, DeletedAt,
           Phone, AvatarColor, AvatarUrl NULL, CreatedAt, MustChangePassword)

StudentGroup (Id, Name, Description,
              CreatedByUserId FK -> User.Id,   -- quản trị viên đã lập lớp, chỉ để truy vết
              IsPublished, PublishedAt,
              IsLocked DEFAULT false, LockedAt NULL,   -- lớp khoá = chỉ đọc (khoá ≠ nháp / công bố)
              AcademicTermId FK -> AcademicTerm.Id NULL,   -- kỳ học (tuỳ chọn, Restrict); INDEX (AcademicTermId)
              CreatedAt)

AcademicTerm (Id, Name,            -- UNIQUE
              StartsOn NULL, EndsOn NULL,   -- ngày, không bắt buộc
              IsCurrent DEFAULT false,      -- UNIQUE INDEX có điều kiện WHERE IsCurrent = TRUE: tối đa một kỳ hiện tại
              CreatedAt, UpdatedAt)

StudentGroupMember (StudentGroupId FK -> StudentGroup.Id, StudentId FK -> User.Id, JoinedAt,
                    PRIMARY KEY (StudentGroupId, StudentId))   -- học sinh học NHIỀU lớp: một dòng cho mỗi (lớp, em); INDEX (StudentId)

StudentGroupTeacher (StudentGroupId FK, TeacherId FK -> User.Id, AssignedAt,
                     PRIMARY KEY (StudentGroupId, TeacherId))   -- dịch vụ luôn giữ đúng một dòng cho mỗi lớp (lớp cũ có thể còn nhiều, được dọn khi đổi giáo viên)

AccountChange (Id, AccountId FK -> User.Id RESTRICT,     -- học sinh hoặc giáo viên
               Kind,          -- Locked | Unlocked | DeletionRequested | Restored | AddedToGroup | RemovedFromGroup | MovedGroup (lưu chuỗi)
               Reason NULL,   -- ≤ 512; bắt buộc với Locked, DeletionRequested, RemovedFromGroup, MovedGroup
               FromGroupId, FromGroupName, ToGroupId, ToGroupName,   -- KHÔNG có khoá ngoại: lớp xoá được, lịch sử phải còn
               ActorUserId FK -> User.Id SET NULL, CreatedAt,
               INDEX (AccountId, CreatedAt))                     -- chỉ thêm, không sửa, không xoá

GroupChange   (Id, GroupId FK -> StudentGroup.Id CASCADE,        -- lịch sử đi theo lớp khi lớp trống bị xoá
               Kind,          -- Created | TeacherChanged | Locked | Unlocked (lưu chuỗi)
               Reason NULL,   -- ≤ 512; bắt buộc với TeacherChanged khi lớp đang có người khác phụ trách, và với Locked
               FromTeacherId, FromTeacherName, ToTeacherId, ToTeacherName,   -- KHÔNG có khoá ngoại: tên được chép lại
               ActorUserId FK -> User.Id SET NULL, CreatedAt,
               INDEX (GroupId, CreatedAt))                       -- chỉ thêm, không sửa, không xoá

Question  (Id, Title, Content, ImageUrl, Topic, Difficulty, DefaultPoint, Status,
           AllowMultiple,                                -- câu nhiều đáp án đúng, do giáo viên chọn
           SubjectId       FK -> Subject.Id  NOT NULL,  -- PHẠM VI: ai được thấy
           CreatedByUserId FK -> User.Id     NOT NULL,  -- QUY TRÁCH: ai được sửa
           Visibility)                                   -- Shared | Private

AnswerOption (Id, QuestionId FK, Content, ImageUrl, IsCorrect, Ordinal)

Exam      (Id, Title, Description, Topic, DurationMinutes, TotalQuestions,
           EasyPercentage, MediumPercentage, HardPercentage, PointEasy, PointMedium, PointHard,
           UsesQuestionList, ListTotalScore,        -- đề theo danh sách (các cột ma trận ở trên là dẫn xuất)
           ShuffleQuestions, ShuffleAnswers, RandomizePerStudent,
           ShowResultAfterSubmit, OpenAt, CloseAt, IsDraft, PublishedAt,
           CreatedByUserId FK -> User.Id)

ExamQuestion (Id, ExamId FK -> Exam.Id CASCADE, QuestionId FK -> Question.Id RESTRICT,
              Ordinal, Point numeric(6,2),
              UNIQUE (ExamId, QuestionId))          -- chỉ có với đề theo danh sách

ExamAssignment (Id, ExamId FK, StudentId FK, AssignedAt,
                AssignedViaGroupId FK -> StudentGroup.Id NULL,
                OpenAt NULL, CloseAt NULL,        -- khung giờ riêng của lớp; NULL = theo lịch đề
                UNIQUE (ExamId, StudentId))

ExamPaper      (Id, ExamId FK, AssignmentId FK NULL)   -- NULL = đề chung cho cả lớp
ExamPaperQuestion (Id, ExamPaperId FK, QuestionId FK, Ordinal,
                   ContentSnapshot, ImageUrlSnapshot, Point, AllowMultiple)
ExamPaperAnswer   (Id, ExamPaperQuestionId FK, ContentSnapshot, IsCorrect, Ordinal)

ExamAttempt (Id, AssignmentId FK, StartedAt, ExpiresAt, SubmittedAt,
             Status, Score, MaxScore, CorrectCount, AutoSubmitted, LastAppliedSeq)

StudentAnswer (Id, AttemptId FK, ExamPaperQuestionId FK,
               SelectedAnswerIds integer[],            -- các đáp án đã chọn (id của ExamPaperAnswer); rỗng = bỏ trống
               UNIQUE (AttemptId, ExamPaperQuestionId))

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
| `ExamAssigned` | student | Giáo viên giao bài, chỉ khi `notify = true` (giao diện luôn gửi `true`: học sinh nhận bài mới luôn được báo, không còn ô chọn) |
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
