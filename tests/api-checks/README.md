# Kiểm chứng API

Bộ kiểm chạy bằng HTTP thật, đối chiếu những gì unit test không với tới được: mã trạng thái,
phân quyền theo vai trò, phạm vi dữ liệu, và hành vi trong transaction.

## Chạy

Cần API đang chạy, database đã có lược đồ và đã có tài khoản demo (script đăng nhập bằng chúng):

```bash
dotnet run --project src/Anthill.Api          # cửa sổ thứ nhất
bash tests/api-checks/step4-exams.sh          # cửa sổ thứ hai
```

Đổi địa chỉ API bằng biến `HOST` (mặc định `http://localhost:5002`):

```bash
HOST=http://localhost:8080 bash tests/api-checks/step4-exams.sh
```

Thoát với mã 0 khi mọi phép kiểm đều đạt, khác 0 khi có phép hỏng — nên cắm vào CI được.

## Vì sao là script chứ không phải xUnit

Hai lớp kiểm này trả lời hai câu hỏi khác nhau.

`tests/Anthill.Tests/` kiểm **logic thuần**: phép chia blueprint, suy diễn vòng đời, luật validator.
Chúng không cần database và chạy được ở mọi máy — nên đó là chỗ đặt những phép kiểm dày đặc, quét
hết mọi tổ hợp.

Thư mục này kiểm **hành vi của một API đang sống**: 401 khác 403 khác 404, transaction có thật sự
cuộn lại không, phạm vi dữ liệu có rò sang giáo viên khác không. Viết chúng bằng xUnit thì cần
`WebApplicationFactory` cộng một database dùng riêng cho test — đáng làm khi dự án lớn hơn, nhưng
lúc này đổi lại là một tầng hạ tầng phải bảo trì để kiểm đúng những thứ mà mấy chục dòng `curl`
đã nói rõ.

Mỗi script **tự dựng dữ liệu của chính nó** nên không phụ thuộc vào việc database đang có sẵn những
gì, và dọn trong `trap EXIT` kể cả khi hỏng giữa chừng. Dọn chỉ được phần xoá được: đề đã có bài nộp
được giữ lại, học sinh chỉ bị khoá chứ không xoá.

## Các bộ hiện có

25 script, hơn 1600 phép kiểm (24 bộ chạy một lệnh: 1668 phép ở lần chạy gần nhất, cộng `purge-redacts-notifications.sh` chạy hai bước).

| Script | Phủ |
|---|---|
| `step2-students.sh` | học sinh, lớp, xoay SecurityStamp |
| `step3-questions.sh` | ngân hàng câu hỏi (kể cả tên câu hỏi tách khỏi nội dung: không bắt buộc, gộp thành một dòng, trần 200 ký tự, tìm theo tên, bỏ trống khi sửa là xoá tên), bốn lớp kiểm khi upload ảnh |
| `step4-exams.sh` | đề và blueprint, vòng đời đề |
| `step5-assignments.sh` | giao bài theo lớp (danh sách học sinh `studentIds` bị từ chối 400), sinh đề, `randomizePerStudent`, phạm vi, idempotent, thu hồi cả lớp (404 / 403 / số em gỡ, số em giữ lại) |
| `step6-attempts.sh` | làm bài, lưu nháp, chấm ở server, chống rò đáp án, hạn giờ theo đồng hồ server |
| `step7-results.sh` | kết quả và dashboard, chống rò điểm giữa các vai trò |
| `step8-notifications.sh` | thông báo, hộp thư riêng từng người |
| `classes-admin.sh` | lớp do quản trị viên lập, phân công giáo viên, công bố |
| `flow-class-to-student.sh` | luồng xuyên suốt: lập lớp → phân công giáo viên → giao theo lớp → cả lớp làm được bài → giáo viên thấy kết quả |
| `per-class-schedule.sh` | một đề, nhiều lớp, mỗi lớp một khung giờ; đáp án giữ lại tới khi mọi lớp hết giờ |
| `schedule-lock.sh` | khoá đổi giờ từ 120 phút trước giờ mở tới hết giờ đóng, chặn thật ở hai đường: sửa cả đề (PUT) cùng dời lịch đề (PATCH), và gửi lại lớp kèm khung mới (không còn đường dời giờ riêng từng em); kiểm các mốc 123 và 114 phút, giao thêm học sinh vào lớp đang khoá vẫn được, hết giờ đóng thì mở khoá lại (cứu em lỡ giờ, mở lại đề), và cờ `scheduleLocked` trên đề, dòng giao bài và khung giờ của lớp khớp với việc server từ chối |
| `exam-edit-rules.sh` | sửa đề đã giao: chưa ai làm thì sửa hết và bản đề được sinh lại; có em làm rồi thì cấu hình đề bị khoá (409), điểm đã chấm giữ nguyên; thu hồi hết rồi đổi cấu hình thì lần giao sau nhận bản đề mới chứ không phải bản đề chung cũ; nhân bản đề, kể cả giáo viên chưa gán môn bị chặn (403). Cần `psql` để dựng một đồng nghiệp cùng môn và một giáo viên chưa gán môn — xem phần kiểm thử trong README gốc |
| `exam-question-list.sh` | đề soạn theo danh sách câu hỏi: lưu và đọc lại đúng thứ tự, điểm từng câu và đáp án đúng; các cột ma trận dẫn xuất nhất quán (tỷ lệ cộng đúng 100, tổng điểm là tổng thật); nháp rỗng được nhưng xuất bản thì không; câu không tồn tại, bị ẩn, trùng, điểm sai, mục rỗng (`questions: [null]`) bị từ chối; đối chiếu `GET .../blueprint` của đề theo danh sách (cần = chính các câu đã chọn, câu ẩn bị báo); "soạn tự động" rút đúng số câu từng mức, không trùng, báo thiếu và không ghi gì; sửa và nhân bản chép cả danh sách; câu đang nằm trong đề không xoá được; bản đề của học sinh đúng bộ câu, đúng điểm, đúng thứ tự; đã có em làm thì danh sách bị khoá (409, đúng tên trường); đề ma trận chạy như cũ và chuyển được sang danh sách |
| `students-deletion.sh` | xoá mềm: lý do bắt buộc, chỉ quản trị viên, chặn đăng nhập, khôi phục |
| `session-revocation.sh` | token cũ nhận 401 trên mọi endpoint và hub sau khi khoá, mở khoá, cấp lại hay tự đổi mật khẩu, đưa vào diện xoá, khôi phục; token giáo viên và quản trị viên không bị ảnh hưởng; gõ sai mật khẩu hiện tại là 400 và phiên còn sống; `PUT /api/auth/me` bắn song song với lệnh khoá (5 vòng) và cấp lại mật khẩu (3 vòng) không làm lệnh đó biến mất; WebSocket mở trước lúc khoá bị cắt kèm lý do ở lời gọi hub kế tiếp (client WebSocket tối giản viết bằng python thư viện chuẩn) |
| `students-status-guards.sh` | chặn các lối vòng quanh trạng thái tài khoản học sinh (đặt thẳng `deleted`, sửa hay cấp lại mật khẩu cho tài khoản chờ xoá...); giao bài đếm em chờ xoá vào `skippedRetired`, và bảng theo dõi, bảng kết quả, chi tiết bài làm mang `studentStatus` |
| `teacher-student-scope.sh` | giáo viên chỉ thấy học sinh lớp mình dạy: lớp khác, lớp nháp, chưa phân lớp đều ngoài phạm vi; hồ sơ em đã nhận đề của mình vẫn mở được sau khi em chuyển lớp; quản trị viên thấy tất cả |
| `audit-fixes.sh` | những đầu vào từng đi lọt validator rồi vỡ thành 500 (ký tự NUL, mật khẩu toàn khoảng trắng, độ dài vượt cột, phần tử null trong mảng); học sinh không tự đổi họ tên, email và số điện thoại (400), quản trị viên đặt và xoá số điện thoại của học sinh, giáo viên không nhận trường `phone`; khôi phục tài khoản giữ trạng thái khoá; `/health` chạm database; thông báo nộp bài trỏ đúng trang kết quả của lượt đó; giao vào khung giờ đã đóng không sinh thông báo; bài tự nộp ghi giờ nộp bằng hạn của lượt. Có một quãng chờ khoảng 60 giây cho tiến trình tự nộp bài |
| `multiple-answers.sh` | câu nhiều đáp án đúng: soạn (câu một đáp án vẫn đúng một ô đúng, câu nhiều đáp án từ một ô trở lên), bản đề học sinh biết câu nào cho chọn nhiều ô (`allowMultiple`) mà không lộ ô đúng; lưu nháp nhận `answerIds`, vẫn nhận `answerId` kiểu cũ, gửi cả hai bị 400, nhiều ô cho câu một đáp án bị 422, ô không thuộc câu bị 422; chấm "đúng và đủ mới được điểm" (thiếu hay thừa một ô đều 0 điểm); xem lại có `selectedAnswerIds` luôn, `correctAnswerIds` + `isCorrect` chỉ khi được phép; `savedAnswers` là mảng và nạp lại đúng sau khi tải lại phiên |
| `answers-release.sh` | "Mở đáp án": đề chưa giao cho lớp nào → 409; còn trong giờ làm bài (kể cả đã có em nộp xong) → 409; học sinh và quản trị viên bị chặn 403, đề không có → 404; đề tắt "hiện kết quả" thì học sinh nộp xong không thấy điểm lẫn đáp án; hết giờ → 200 bật cờ, học sinh thấy điểm, `canSeeAnswers` và `correctAnswerIds`; mở lần nữa vẫn 200. Có quãng chờ khoảng 30 giây cho hết giờ |
| `profile-avatar.sh` | ảnh đại diện (`PUT /api/auth/me/avatar`): giáo viên và quản trị viên đặt/gỡ ảnh của mình, ảnh có ở `GET /api/auth/me` và phản hồi đăng nhập, file tải về được; bảy loại đường dẫn xấu (URL tuyệt đối, `//host`, data URI, `..`, ngoài `/uploads`, quá 512 ký tự) và thiếu trường đều 400; không token 401; học sinh đặt, gỡ hay tải ảnh lên đều 403; chuỗi rỗng và khoảng trắng hai đầu được xử lý; ảnh của hai người không lẫn nhau |
| `paper-variants.sh` | chia một đề lớn thành nhiều đề nhỏ (`paperCount`): số đề và số câu ngoài khoảng, đề ma trận hay thiếu danh sách, câu cùng mức khác điểm, đề lớn thiếu câu, tỷ lệ không cộng 100 đều bị từ chối, bản nháp được miễn; mỗi lớp chia riêng, mỗi đề đủ số câu từng mức và không có hai câu giống nhau trong một đề; số em mỗi đề trong lớp chênh nhau nhiều nhất 1 và mã đề (`paperLabel`) không theo thứ tự danh sách; mã đề chỉ giáo viên thấy (cả ở chi tiết bài làm `GET /api/results/{id}`; học sinh đọc bài của mình không có `paperLabel`), học sinh gọi `GET /api/exams/{id}/papers` nhận 403; đổi số đề khi chưa ai làm thì chia và phát lại, khi đã có em làm thì 409; em vào lớp sau được xếp vào đề ít em nhất; nhân bản giữ `paperCount` và `paperBonusUnits`; **điểm cộng thêm** (`paperBonusUnits`): đề 3 / 3 / 3 câu điểm 0,9 / 1,1 / 1,33 (9,99) cộng 1 đơn vị thì `totalScore` đúng 10, học sinh nhận đề tổng đúng 10,00 với đúng một câu Khó thành 1,34, đề một đề / cộng nhiều hơn số câu một đề / số âm bị 400, đổi điểm cộng khi đã có em làm thì 409 và chưa ai làm thì sinh lại bản đề |
| `compose-history.sh` | "Soạn tự động" theo chương (`topic`) và theo lịch sử giao bài của lớp (`excludeAssignedToGroupIds`): mỗi câu rút ra đúng chương đã chọn; soạn tiếp chương khác với `excludeQuestionIds` không lặp câu; bỏ đúng những câu lớp đã nhận (đề giao cả lớp chung một bản đề, và đề chia 2 đề chỉ tính các câu thực sự rơi vào đề của lớp), `skippedAlreadyAssigned` đúng số câu bỏ; thiếu câu sau khi lọc không lỗi mà báo `missing`; lớp lạ hay lẫn lớp lạ, quá 50 lớp trả 400; quản trị viên 403; hộp chọn câu `GET /api/questions?excludeAssignedToGroupIds=…`: bỏ đúng câu lớp đã nhận (cùng cách tính với soạn tự động), kết hợp được với chương và mức độ, lớp lạ / lẫn lớp lạ / chuỗi không phải số / quá 50 lớp / quản trị viên đều 400, tham số rỗng thì không lọc |
| `exam-total-score.sh` | điểm theo mức độ khó (Dễ / Trung bình / Khó), chuẩn 10 điểm: từng mức không âm, không quá trần cứng 100 của một câu, tối đa 2 chữ số thập phân, mức có câu thì phải > 0; tổng khác 10 không bị chặn ở đường lưu nào (tạo, sửa, nhân bản, xuất bản) và `totalScore` luôn đúng (trước đây là `exam-score-100.sh`) |
| `schedule-changes.sh` | đổi khung giờ sau khi đã giao: nhiều lớp mỗi lớp một khung, em mới vào lớp theo giờ của lớp, khai lại "theo lịch đề" thì gỡ giờ riêng của em chưa bắt đầu, `assignmentWindows` đúng khung thật, thông báo đổi giờ đúng người, đáp án chỉ mở khi mọi khung đã đóng và mở rồi không đóng lại (H10), thu hồi cả lớp |
| `deadline-reminders.sh` | nhắc học sinh khi giờ làm bài sắp hết: nhắc đúng một lần kèm đường dẫn, quét lại không nhắc lần hai, khung đang mở bị khoá giờ (409), em đã vào thi và lớp có khung vừa khít không bị nhắc. Có quãng chờ nhịp quét của tiến trình nền |
| `purge-redacts-notifications.sh` | H22 — sau khi dọn tài khoản học sinh đã xoá, thông báo nộp bài cũ của giáo viên không còn tên thật. Chạy hai bước (`setup`, khởi động lại API, `verify`), cần `psql`; không nằm trong bộ chạy một lệnh |

`lib.sh` là khung dùng chung: `call`, `expect_status`, `expect_json`, `login`, `new_class`, `summary`.

`step6-attempts.sh` có một quãng chờ **30 giây** thật: nó tạo một đề có cửa sổ mở chỉ 25 giây rồi chờ
hết giờ. `per-class-schedule.sh` cũng chờ tới mốc giờ thật. Không có quãng chờ đó thì không phép nào
chứng minh được rằng hạn nộp tính bằng đồng hồ server — mà đó lại là quy tắc dễ cài sai nhất.

## Phép kiểm hết giờ và tiến trình tự nộp bài

API có một **tiến trình nền tự nộp bài** chạy mỗi 30 giây, nên lượt quá hạn trong `step6-attempts.sh`
có thể bị chốt bởi tiến trình đó hoặc bởi chính gói nháp của script. Phép "lưu nháp sau khi hết giờ
→ 410" từng chập chờn vì hai đường cho hai mã (410 và 409); giờ cả hai đường đều trả 410, và script
gửi thêm một gói nháp thứ hai để lần nào cũng đi qua nhánh "đã bị chốt trước". Chạy với
`AutoSubmit__Enabled=true` hay `false` (profile `http-no-autosubmit`) đều phải đạt.
