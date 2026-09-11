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
được giữ lại, học sinh chỉ bị khoá chứ không xoá. Riêng `step5` tới `step8` còn để lại lớp thử, vì
bước dọn gỡ học sinh khỏi lớp và xoá lớp bằng token giáo viên, mà hai thao tác đó chỉ quản trị viên
được làm.

## Các bộ hiện có

14 script, tổng cộng 916 phép kiểm.

| Script | Phủ |
|---|---|
| `step2-students.sh` | học sinh, lớp, xoay SecurityStamp |
| `step3-questions.sh` | ngân hàng câu hỏi, bốn lớp kiểm khi upload ảnh |
| `step4-exams.sh` | đề và blueprint, vòng đời đề |
| `step5-assignments.sh` | giao bài, sinh đề, `randomizePerStudent`, phạm vi, idempotent, thu hồi |
| `step6-attempts.sh` | làm bài, lưu nháp, chấm ở server, chống rò đáp án, hạn giờ theo đồng hồ server |
| `step7-results.sh` | kết quả và dashboard, chống rò điểm giữa các vai trò |
| `step8-notifications.sh` | thông báo, hộp thư riêng từng người |
| `classes-admin.sh` | lớp do quản trị viên lập, phân công giáo viên, công bố |
| `flow-class-to-student.sh` | luồng xuyên suốt: lập lớp → phân công giáo viên → giao theo lớp → cả lớp làm được bài → giáo viên thấy kết quả |
| `per-class-schedule.sh` | một đề, nhiều lớp, mỗi lớp một khung giờ; đáp án giữ lại tới khi mọi lớp hết giờ |
| `students-deletion.sh` | xoá mềm: lý do bắt buộc, chỉ quản trị viên, chặn đăng nhập, khôi phục |
| `session-revocation.sh` | token cũ nhận 401 trên mọi endpoint và hub sau khi khoá, mở khoá, cấp lại hay tự đổi mật khẩu, đưa vào diện xoá, khôi phục; token giáo viên và quản trị viên không bị ảnh hưởng; gõ sai mật khẩu hiện tại là 400 và phiên còn sống; `PUT /api/auth/me` bắn song song với lệnh khoá (5 vòng) và cấp lại mật khẩu (3 vòng) không làm lệnh đó biến mất; WebSocket mở trước lúc khoá bị cắt kèm lý do ở lời gọi hub kế tiếp (client WebSocket tối giản viết bằng python thư viện chuẩn) |
| `students-status-guards.sh` | chặn các lối vòng quanh trạng thái tài khoản học sinh (đặt thẳng `deleted`, sửa hay cấp lại mật khẩu cho tài khoản chờ xoá...) |
| `teacher-student-scope.sh` | giáo viên chỉ thấy học sinh lớp mình dạy: lớp khác, lớp nháp, chưa phân lớp đều ngoài phạm vi; hồ sơ em đã nhận đề của mình vẫn mở được sau khi em chuyển lớp; quản trị viên thấy tất cả |

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
