# Kiểm chứng API

Bộ kiểm chạy bằng HTTP thật, đối chiếu những gì unit test không với tới được: mã trạng thái,
phân quyền theo vai trò, phạm vi dữ liệu, và hành vi trong transaction.

## Chạy

Cần API đang chạy và database đã có lược đồ:

```bash
dotnet run --project src/EduQuiz.Api          # cửa sổ thứ nhất
bash tests/api-checks/step4-exams.sh          # cửa sổ thứ hai
```

Đổi địa chỉ API bằng biến `HOST`:

```bash
HOST=https://eduquiz-api.onrender.com bash tests/api-checks/step4-exams.sh
```

Thoát với mã 0 khi mọi phép đều đạt, khác 0 khi có phép hỏng — nên cắm vào CI được.

## Vì sao là script chứ không phải xUnit

Hai lớp kiểm này trả lời hai câu hỏi khác nhau.

`tests/EduQuiz.Tests/` kiểm **logic thuần**: phép chia blueprint, suy diễn vòng đời, luật validator.
Chúng chạy trong một phần mười giây, không cần database, và chạy được ở mọi máy — nên đó là chỗ
đặt những phép kiểm dày đặc, quét hết mọi tổ hợp.

Thư mục này kiểm **hành vi của một API đang sống**: 401 khác 403 khác 404, transaction có thật sự
cuộn lại không, phạm vi dữ liệu có rò sang giáo viên khác không. Viết chúng bằng xUnit thì cần
`WebApplicationFactory` cộng một database dùng riêng cho test — đáng làm khi dự án lớn hơn, nhưng
lúc này đổi lại là một tầng hạ tầng phải bảo trì để kiểm đúng những thứ mà mấy chục dòng `curl`
đã nói rõ.

Mỗi script **tự dựng dữ liệu của chính nó rồi tự dọn** trong `trap EXIT`, kể cả khi hỏng giữa
chừng. Nhờ vậy chạy lại nhiều lần không tích rác, và không phụ thuộc vào việc database đang có
sẵn những gì.

## Các bộ hiện có

| Script | Phủ |
|---|---|
| `step2-students.sh` | §4.2, §4.3 — học sinh, nhóm lớp, xoay SecurityStamp. 37 phép |
| `step3-questions.sh` | §4.5 — ngân hàng câu hỏi, bốn hàng rào upload. 38 phép |
| `step4-exams.sh` | §4.6 — bài kiểm tra và blueprint. 51 phép |
| `step5-assignments.sh` | §4.6 — giao bài, sinh đề, randomizePerStudent. 49 phép |
| `step6-attempts.sh` | §4.7 — làm bài, chấm ở server, chống rò đáp án. 101 phép |
| `step7-results.sh` | §4.8 — kết quả và dashboard, chống rò điểm. 83 phép |
| `step8-notifications.sh` | §4.2, §7 — thông báo và hộp thư riêng. 35 phép |
| `step5-assignments.sh` | §4.6 phần giao bài — phạm vi, idempotent, thu hồi. 42 phép |
| `step6-attempts.sh` | §4.7 — làm bài, lưu nháp, chấm ở server. 85 phép |

`lib.sh` là khung dùng chung: `call`, `expect_status`, `expect_json`, `login`, `summary`.

`step6-attempts.sh` chạy lâu hơn hai bộ kia khoảng **30 giây**: nó tạo một đề có cửa sổ mở chỉ 25
giây rồi chờ hết giờ thật. Không có quãng chờ đó thì không phép nào chứng minh được rằng hạn nộp
tính bằng đồng hồ server — mà đó lại là quy tắc dễ cài sai nhất của cả §4.7.

## Một điều cần biết trước khi chạy

Từ bước 8, API có một **tiến trình nền tự nộp bài** chạy mỗi 30 giây. Nó có thể chạm vào lượt làm
bài mà script vừa tạo và làm một phép kiểm hỏng — điều này đã xảy ra đúng một lần trong quá trình
phát triển và không tái hiện lại được sau đó.

Muốn chạy hoàn toàn tất định thì tắt nó đi khi khởi động API:

```bash
AutoSubmit__Enabled=false dotnet run --project src/EduQuiz.Api
```

Không tắt cũng chạy được — nhưng nếu gặp một phép hỏng lẻ không giải thích nổi ở `step6-attempts.sh`,
đây là chỗ đáng nghi đầu tiên, chứ không phải backend vừa hỏng.
