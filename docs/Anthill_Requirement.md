# Anthill Requirement

## 1. Context

Anthill là hệ thống hỗ trợ giáo viên tạo và quản lý bài kiểm tra trực tuyến. Hệ thống tập trung vào 2 nhóm người dùng chính: **Teacher** và **Student**.

Teacher có thể tạo ngân hàng câu hỏi, phân loại câu hỏi theo mức độ khó, tạo bài kiểm tra theo tỷ lệ câu dễ / trung bình / khó, giao bài kiểm tra cho Student và theo dõi kết quả làm bài. Student đăng nhập vào hệ thống để xem bài kiểm tra được giao, làm bài và xem kết quả sau khi nộp.

Phạm vi hệ thống không tập trung vào quản lý khóa học hay bài học. Trọng tâm chính là:

- Quản lý Student.
- Quản lý ngân hàng câu hỏi.
- Tạo bài kiểm tra từ ngân hàng câu hỏi.
- Giao bài kiểm tra cho Student.
- Student làm bài kiểm tra.
- Hệ thống tự chấm điểm.
- Teacher theo dõi kết quả làm bài của Student.

---

## 2. Functional Requirement

### Stage 1: Authentication & Role Management

Hệ thống hỗ trợ đăng nhập theo vai trò người dùng.

Các role chính:

| Role | Mô tả |
|---|---|
| Teacher | Tạo câu hỏi, tạo bài kiểm tra, giao bài, quản lý Student, xem kết quả |
| Student | Xem bài kiểm tra được giao, làm bài, nộp bài, xem kết quả |

Chức năng chính:

- Teacher đăng nhập vào hệ thống.
- Student đăng nhập vào hệ thống bằng tài khoản được tạo/cấp bởi Teacher.
- Hệ thống điều hướng dashboard theo role.
- Người dùng có thể xem và cập nhật thông tin cá nhân cơ bản.
- Hệ thống kiểm tra quyền truy cập theo role.

---

### Stage 2: Student Management

Teacher có thể quản lý danh sách Student trong hệ thống.

Chức năng chính:

- Teacher tạo tài khoản Student.
- Teacher xem danh sách Student.
- Teacher chỉnh sửa thông tin Student.
- Teacher khóa hoặc mở khóa tài khoản Student.
- Teacher tìm kiếm Student theo tên, email hoặc mã học sinh.
- Teacher có thể nhóm Student theo lớp/nhóm học để dễ giao bài kiểm tra.

Thông tin Student gồm:

- Họ và tên.
- Email hoặc username.
- Mã học sinh.
- Lớp/nhóm.
- Trạng thái tài khoản.
- Ngày tạo tài khoản.

---

### Stage 3: Question Bank Management

Teacher có thể tạo và quản lý ngân hàng câu hỏi. Ngân hàng câu hỏi là nguồn dữ liệu chính để hệ thống tạo bài kiểm tra cho Student.

Chức năng chính:

- Teacher tạo câu hỏi mới.
- Teacher chỉnh sửa câu hỏi.
- Teacher xóa hoặc ẩn câu hỏi.
- Teacher phân loại câu hỏi theo chủ đề.
- Teacher phân loại câu hỏi theo mức độ khó.
- Teacher thêm đáp án cho câu hỏi.
- Teacher chọn đáp án đúng.
- Teacher có thể thêm hình ảnh vào câu hỏi.
- Teacher có thể thêm hình ảnh vào đáp án nếu cần.
- Teacher tìm kiếm và lọc câu hỏi theo chủ đề, mức độ khó, trạng thái.

Mức độ khó của câu hỏi:

| Difficulty | Ý nghĩa |
|---|---|
| Easy | Câu dễ |
| Medium | Câu trung bình |
| Hard | Câu khó |

Thông tin câu hỏi gồm:

- Nội dung câu hỏi.
- Hình ảnh minh họa cho câu hỏi nếu có.
- Chủ đề câu hỏi.
- Mức độ khó.
- Danh sách đáp án.
- Đáp án đúng.
- Điểm mặc định.
- Trạng thái câu hỏi.

Loại câu hỏi ưu tiên trong MVP:

- Multiple Choice: chọn 1 đáp án đúng.

Có thể mở rộng sau:

- Multiple Answers: chọn nhiều đáp án đúng.
- True/False.
- Short Answer.

---

### Stage 4: Exam Creation & Exam Blueprint

Teacher có thể tạo bài kiểm tra dựa trên ngân hàng câu hỏi đã có.

Khi tạo bài kiểm tra, Teacher không cần chọn thủ công từng câu hỏi. Thay vào đó, Teacher có thể cấu hình một **Exam Blueprint** để hệ thống tự sinh đề kiểm tra.

Thông tin cấu hình bài kiểm tra gồm:

- Tên bài kiểm tra.
- Mô tả bài kiểm tra.
- Thời gian làm bài.
- Tổng số câu hỏi.
- Chủ đề hoặc nhóm câu hỏi được sử dụng.
- Tỷ lệ câu dễ.
- Tỷ lệ câu trung bình.
- Tỷ lệ câu khó.
- Điểm cho mỗi câu.
- Có trộn thứ tự câu hỏi hay không.
- Có trộn thứ tự đáp án hay không.
- Thời gian mở bài.
- Thời gian đóng bài.

Ví dụ cấu hình:

| Tổng số câu | Easy | Medium | Hard |
|---:|---:|---:|---:|
| 20 | 30% | 50% | 20% |

Khi đó hệ thống sẽ lấy:

- 6 câu Easy.
- 10 câu Medium.
- 4 câu Hard.

Điều kiện kiểm tra:

- Tổng tỷ lệ Easy + Medium + Hard phải bằng 100%.
- Số lượng câu hỏi trong ngân hàng phải đủ theo từng mức độ khó.
- Nếu số câu hỏi không đủ, hệ thống cần báo lỗi rõ ràng cho Teacher.

---

### Stage 5: Assign Exam to Student

Teacher có thể giao bài kiểm tra cho một hoặc nhiều Student.

Chức năng chính:

- Teacher chọn bài kiểm tra cần giao.
- Teacher chọn Student hoặc nhóm Student.
- Hệ thống tạo bài làm cho từng Student dựa trên Exam Blueprint.
- Mỗi Student có thể nhận bộ câu hỏi giống nhau hoặc được random khác nhau tùy cấu hình.
- Teacher theo dõi trạng thái làm bài của từng Student.

Trạng thái bài làm:

| Status | Ý nghĩa |
|---|---|
| Assigned | Đã giao bài |
| InProgress | Student đang làm bài |
| Submitted | Student đã nộp bài |
| Graded | Đã chấm điểm |
| Expired | Hết hạn làm bài |

---

### Stage 6: Student Exam Taking

Student có thể xem danh sách bài kiểm tra được giao và thực hiện bài làm.

Chức năng chính:

- Student xem danh sách bài kiểm tra được giao.
- Student xem thông tin bài kiểm tra: tên bài, thời gian làm, hạn nộp.
- Student bắt đầu làm bài.
- Hệ thống hiển thị câu hỏi dạng text hoặc kèm hình ảnh.
- Student chọn đáp án cho từng câu hỏi.
- Student có thể chuyển qua lại giữa các câu trong thời gian làm bài.
- Student nộp bài.
- Hệ thống tự động nộp bài nếu hết thời gian.

Sau khi nộp bài:

- Hệ thống tự động chấm điểm với câu hỏi trắc nghiệm.
- Hệ thống lưu lại đáp án Student đã chọn.
- Student có thể xem điểm nếu Teacher cho phép hiển thị kết quả ngay.

---

### Stage 7: Result & Report Management

Teacher có thể xem kết quả làm bài của Student.

Chức năng chính:

- Teacher xem danh sách bài làm theo từng bài kiểm tra.
- Teacher xem trạng thái từng Student.
- Teacher xem điểm số của từng Student.
- Teacher xem số câu đúng / sai.
- Teacher xem thời gian nộp bài.
- Teacher xem chi tiết đáp án Student đã chọn.
- Teacher lọc kết quả theo Student, nhóm, trạng thái hoặc điểm số.

Student có thể xem:

- Điểm bài kiểm tra.
- Số câu đúng.
- Tổng số câu.
- Thời gian nộp bài.
- Chi tiết đáp án nếu Teacher cho phép.

---

### Stage 8: Notification & Realtime Updates

Hệ thống có thể gửi thông báo realtime bằng SignalR.

Các trường hợp nên có thông báo:

- Teacher giao bài kiểm tra mới cho Student.
- Student nộp bài kiểm tra.
- Teacher cập nhật bài kiểm tra.
- Bài kiểm tra gần hết hạn.
- Hệ thống tự động nộp bài khi hết thời gian.

Realtime chỉ dùng để cập nhật thông báo và trạng thái nhanh. Các nghiệp vụ chính như tạo bài kiểm tra, nộp bài, chấm điểm vẫn xử lý bằng REST API.

---

## 3. Non-functional Requirement

- Giao diện rõ ràng, dễ sử dụng cho Teacher và Student.
- Website responsive trên desktop và tablet.
- UI sử dụng tiếng Việt.
- Hệ thống có phân quyền theo role.
- Dữ liệu câu hỏi, bài kiểm tra và kết quả phải được lưu rõ ràng.
- Câu hỏi có thể chứa text và hình ảnh.
- Hệ thống cần kiểm tra dữ liệu khi Teacher tạo Exam Blueprint.
- Hệ thống cần đảm bảo Student chỉ thấy bài kiểm tra được giao cho mình.
- Hệ thống cần đảm bảo Teacher chỉ quản lý Student, câu hỏi và bài kiểm tra thuộc phạm vi của mình.
- Danh sách Student, câu hỏi và bài kiểm tra nên có tìm kiếm, lọc và phân trang.
- Password cần được hash nếu dùng backend thật.
- API cần có JWT authentication.
- Realtime dùng SignalR cho notification và cập nhật trạng thái.
- Source code cần chia module rõ ràng, dễ bảo trì và mở rộng.

---

## 4. Proposed Solutions

### Frontend

Đề xuất sử dụng:

- ReactJS hoặc ReactJS + TypeScript.
- React Router.
- Tailwind CSS.
- Axios.
- React Hook Form.
- Zod hoặc Yup để validate form.
- @microsoft/signalr cho realtime notification.

### Backend

Đề xuất sử dụng:

- ASP.NET Core Web API.
- Entity Framework Core.
- SQL Server hoặc PostgreSQL.
- JWT Authentication.
- Repository Pattern.
- Unit of Work Pattern.
- SignalR.
- Swagger.

### Database chính đề xuất

Các bảng chính:

- User.
- StudentGroup.
- Question.
- AnswerOption.
- Exam.
- ExamRule.
- ExamAssignment.
- ExamAttempt.
- StudentAnswer.
- Notification.

### Cách triển khai phù hợp

Nên triển khai theo từng giai đoạn:

1. Authentication và phân quyền.
2. Quản lý Student.
3. Quản lý ngân hàng câu hỏi.
4. Tạo Exam Blueprint.
5. Giao bài kiểm tra cho Student.
6. Student làm bài và nộp bài.
7. Tự động chấm điểm và hiển thị kết quả.
8. Notification realtime bằng SignalR.

---

## 5. Applied Theory

Các lý thuyết và kỹ thuật có thể áp dụng:

- Role-Based Access Control: phân quyền Teacher và Student.
- CRUD Operations: quản lý Student, câu hỏi, bài kiểm tra.
- Question Bank Management: quản lý ngân hàng câu hỏi theo chủ đề và độ khó.
- Exam Blueprint: tạo đề kiểm tra dựa trên cấu hình tỷ lệ câu hỏi.
- Randomized Question Selection: chọn câu hỏi ngẫu nhiên từ ngân hàng câu hỏi.
- Auto-Grading: tự động chấm điểm bài trắc nghiệm.
- Relational Database Design: thiết kế quan hệ giữa User, Question, Exam, Attempt, Answer.
- RESTful API Design: thiết kế API rõ ràng cho frontend sử dụng.
- Repository Pattern: tách logic truy cập dữ liệu.
- Unit of Work Pattern: quản lý transaction khi xử lý nhiều thao tác database.
- SignalR Realtime Communication: gửi thông báo và cập nhật trạng thái realtime.
- Component-Based UI: chia giao diện frontend thành các component tái sử dụng.

---

## 6. Products / Expected Deliverables

Sản phẩm kỳ vọng gồm:

- Website quản lý bài kiểm tra trực tuyến.
- Trang đăng nhập.
- Dashboard cho Teacher.
- Dashboard cho Student.
- Trang quản lý Student.
- Trang quản lý nhóm/lớp Student.
- Trang ngân hàng câu hỏi.
- Form tạo câu hỏi có hỗ trợ hình ảnh.
- Trang tạo bài kiểm tra bằng Exam Blueprint.
- Chức năng cấu hình tỷ lệ câu Easy / Medium / Hard.
- Chức năng giao bài kiểm tra cho Student.
- Trang Student làm bài kiểm tra.
- Chức năng tự động chấm điểm.
- Trang xem kết quả bài kiểm tra.
- Notification realtime bằng SignalR.
- Database schema hoặc ERD.
- Source code frontend.
- Source code backend.
- Tài liệu mô tả yêu cầu hệ thống.
