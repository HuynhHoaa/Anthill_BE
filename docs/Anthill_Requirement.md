# Anthill Requirement

## 1. Context

Anthill là hệ thống kiểm tra trắc nghiệm trực tuyến dành cho một tổ chức giáo dục — trường học hoặc trung tâm dạy thêm. Toàn bộ việc kiểm tra của đơn vị diễn ra trên cùng một hệ thống; tài khoản, lớp học và quyền hạn thuộc về tổ chức chứ không thuộc về từng người dùng. Hệ thống phục vụ 3 nhóm người dùng: **Admin**, **Teacher** và **Student**.

Admin là hiệu trưởng hoặc giám đốc trung tâm: lập lớp học, xếp học sinh vào lớp, phân công giáo viên phụ trách lớp và quản lý tài khoản học sinh. Teacher chỉ làm việc với những lớp đã công bố và được phân công cho mình: soạn ngân hàng câu hỏi theo môn, phân loại câu hỏi theo mức độ khó, tạo bài kiểm tra theo tỷ lệ câu dễ / trung bình / khó, giao bài cho lớp hoặc từng Student (mỗi lớp có thể có khung giờ làm bài riêng) và theo dõi kết quả. Student đăng nhập bằng tài khoản do Admin cấp để xem bài kiểm tra được giao, làm bài và xem kết quả sau khi nộp.

Phạm vi hệ thống không tập trung vào quản lý khóa học hay bài học. Trọng tâm chính là:

- Quản lý lớp học và phân công giáo viên.
- Quản lý tài khoản Student.
- Quản lý ngân hàng câu hỏi.
- Tạo bài kiểm tra từ ngân hàng câu hỏi.
- Giao bài kiểm tra cho lớp hoặc Student, với lịch làm bài riêng cho từng lớp.
- Student làm bài kiểm tra.
- Hệ thống tự chấm điểm ở server.
- Teacher theo dõi kết quả làm bài của Student.

---

## 2. Functional Requirement

### Stage 1: Authentication & Role Management

Hệ thống hỗ trợ đăng nhập theo vai trò người dùng.

Các role chính:

| Role | Mô tả |
|---|---|
| Admin | Hiệu trưởng / giám đốc trung tâm. Lập và công bố lớp, xếp Student vào lớp, phân công Teacher, quản lý tài khoản Student |
| Teacher | Tạo câu hỏi, tạo bài kiểm tra, giao bài cho lớp được phân công, xem kết quả |
| Student | Xem bài kiểm tra được giao, làm bài, nộp bài, xem kết quả |

Chức năng chính:

- Admin và Teacher đăng nhập bằng email.
- Student đăng nhập bằng username hoặc mã học sinh, với tài khoản do Admin tạo/cấp.
- Trang đăng nhập có lựa chọn vai trò; server đối chiếu với vai trò lưu trong database và từ chối nếu không khớp.
- Tài khoản bị khóa, đang chờ xóa hoặc đã xóa không đăng nhập được.
- Hệ thống điều hướng dashboard theo role.
- Người dùng có thể xem và cập nhật thông tin cá nhân cơ bản, và đổi mật khẩu (phải nhập đúng mật khẩu hiện tại).
- Hệ thống kiểm tra quyền truy cập theo role ở server; giao diện chỉ làm nhiệm vụ điều hướng.
- Khóa tài khoản, đổi mật khẩu hoặc Admin cấp lại mật khẩu làm mọi phiên đăng nhập đang mở của tài khoản đó mất hiệu lực ngay; giao diện đưa người dùng về trang đăng nhập.

Tài khoản Teacher và danh mục môn học hiện được tạo sẵn trong dữ liệu khởi tạo; hệ thống chưa có chức năng quản lý tài khoản Teacher.

---

### Stage 2: Student Account Management

Admin sở hữu tài khoản Student. Teacher chỉ xem hồ sơ Student, không tạo, sửa, khóa hay xóa.

Chức năng chính:

- Admin tạo tài khoản Student kèm mật khẩu ban đầu.
- Admin xem danh sách Student, có phân trang, lọc theo lớp và trạng thái tài khoản.
- Admin tìm kiếm Student theo họ tên, email, username hoặc mã học sinh (không phân biệt dấu và chữ hoa/thường).
- Admin chỉnh sửa thông tin Student và xếp Student vào lớp.
- Admin khóa hoặc mở khóa tài khoản Student.
- Admin cấp lại mật khẩu cho Student quên mật khẩu. Mật khẩu mới chỉ hiển thị đúng một lần.
- Admin xóa tài khoản Student theo cơ chế xóa mềm mô tả bên dưới.
- Email, username và mã học sinh không được trùng với tài khoản khác.

Xóa tài khoản Student:

- Không có thao tác xóa ngay. Admin đưa tài khoản vào danh sách chờ xóa và **bắt buộc** nhập lý do (10–512 ký tự).
- Tài khoản chờ xóa không đăng nhập được, phiên đang mở bị hủy ngay, và không còn xuất hiện ở các danh sách vận hành như xếp lớp hay giao bài.
- Trong vòng **60 ngày** kể từ ngày yêu cầu xóa, Admin có thể khôi phục tài khoản. Danh sách hiển thị hạn khôi phục của từng tài khoản.
- Hết 60 ngày, tiến trình nền tự động xóa dữ liệu cá nhân (họ tên, email, username, mã học sinh, số điện thoại) và gỡ tài khoản khỏi lớp. Lượt làm bài và điểm số được **giữ nguyên**, hiển thị dưới tên "Học sinh đã xoá", để bảng điểm của lớp không bị thiếu dòng. Lý do xóa được giữ lại. Sau bước này tài khoản không khôi phục được nữa.

Thông tin Student gồm:

- Họ và tên.
- Email.
- Username.
- Mã học sinh.
- Lớp.
- Trạng thái tài khoản.
- Ngày tạo tài khoản.

Trạng thái tài khoản:

| Status | Ý nghĩa |
|---|---|
| Active | Đang hoạt động |
| Locked | Tạm khóa, không đăng nhập được, dữ liệu nguyên vẹn |
| PendingDeletion | Đang chờ xóa, khôi phục được trong 60 ngày |
| Deleted | Dữ liệu cá nhân đã bị xóa, chỉ còn lại kết quả làm bài |

---

### Stage 3: Class Management & Teacher Assignment

Lớp học là đơn vị hành chính của tổ chức, do Admin lập. Mỗi Student thuộc tối đa một lớp.

Chức năng chính:

- Admin tạo lớp, đổi tên và mô tả lớp. Tên lớp không được trùng trong toàn hệ thống.
- Lớp mới tạo ở trạng thái nháp (Draft). Admin công bố lớp (Publish) khi đã sẵn sàng, và có thể đưa lớp đã công bố trở lại nháp (Unpublish). Giao diện cảnh báo khi công bố một lớp chưa có Student hoặc chưa có Teacher, nhưng không cấm.
- Admin thêm nhiều Student vào lớp trong một lần. Thêm Student đang thuộc lớp khác nghĩa là chuyển lớp.
- Admin gỡ Student khỏi lớp; tài khoản Student giữ nguyên.
- Admin phân công một hoặc nhiều Teacher phụ trách lớp, và gỡ phân công. Tài khoản Teacher đang bị khóa không phân công được. Gỡ Teacher khỏi lớp không ảnh hưởng tới bài đã giao.
- Admin chỉ xóa được lớp không còn Student. Xóa lớp không xóa tài khoản Student.
- Teacher chỉ thấy những lớp **đã công bố và được phân công cho mình**, ở chế độ chỉ đọc (thông tin lớp và danh sách Student). Lớp nháp hoặc lớp không được phân công không hiển thị với Teacher và không giao bài được.

Trạng thái lớp:

| Status | Ý nghĩa |
|---|---|
| Draft | Admin đang dựng lớp, Teacher chưa thấy |
| Published | Đã công bố, Teacher được phân công thấy và giao bài được |

---

### Stage 4: Question Bank Management

Teacher có thể tạo và quản lý ngân hàng câu hỏi. Ngân hàng câu hỏi là nguồn dữ liệu chính để hệ thống tạo bài kiểm tra cho Student.

Ngân hàng câu hỏi chia theo môn học: mỗi Teacher thuộc một môn và làm việc trong ngân hàng của môn đó, dùng chung với đồng nghiệp cùng môn.

Chức năng chính:

- Teacher tạo câu hỏi mới trong ngân hàng môn của mình.
- Teacher chỉnh sửa câu hỏi do chính mình soạn.
- Teacher xóa hoặc ẩn câu hỏi do chính mình soạn.
- Teacher chọn chế độ chia sẻ cho câu hỏi: Shared (đồng nghiệp cùng môn cũng thấy) hoặc Private (chỉ người soạn thấy).
- Teacher phân loại câu hỏi theo chủ đề.
- Teacher phân loại câu hỏi theo mức độ khó.
- Teacher thêm đáp án cho câu hỏi.
- Teacher chọn đáp án đúng.
- Teacher có thể thêm hình ảnh vào câu hỏi.
- Teacher có thể thêm hình ảnh vào đáp án nếu cần.
- Teacher tìm kiếm và lọc câu hỏi theo từ khóa, chủ đề, mức độ khó, trạng thái.

Quy tắc:

- Câu hỏi đã nằm trong một đề đã sinh thì không xóa được, chỉ ẩn được.
- Ẩn câu hỏi chỉ loại câu đó khỏi các lần sinh đề sau; đề đã sinh không thay đổi.
- Sửa câu hỏi không làm thay đổi điểm của các bài đã chấm, vì bài làm được chấm theo bản chụp của đề đã sinh.
- Hình ảnh phải được tải lên server trước qua API upload (PNG, JPEG, GIF, WebP); câu hỏi và đáp án chỉ lưu đường dẫn ảnh, server không nhận ảnh nhúng dạng data URI. Giao diện soạn câu hỏi hiện vẫn đọc ảnh thành data URI và chưa gọi API upload, nên chưa lưu được câu hỏi kèm ảnh.

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
- Danh sách đáp án, mỗi đáp án có thể kèm hình ảnh.
- Đáp án đúng.
- Điểm mặc định.
- Trạng thái câu hỏi (Active / Hidden).
- Chế độ chia sẻ (Shared / Private).

Loại câu hỏi ưu tiên trong MVP:

- Multiple Choice: chọn 1 đáp án đúng.

Có thể mở rộng sau:

- Multiple Answers: chọn nhiều đáp án đúng.
- True/False.
- Short Answer.

---

### Stage 5: Exam Creation & Exam Blueprint

Teacher có thể tạo bài kiểm tra dựa trên ngân hàng câu hỏi đã có.

Khi tạo bài kiểm tra, Teacher không cần chọn thủ công từng câu hỏi. Thay vào đó, Teacher có thể cấu hình một **Exam Blueprint** để hệ thống tự sinh đề kiểm tra.

Thông tin cấu hình bài kiểm tra gồm:

- Tên bài kiểm tra.
- Mô tả bài kiểm tra.
- Thời gian làm bài.
- Tổng số câu hỏi.
- Chủ đề câu hỏi được sử dụng (bỏ trống nghĩa là lấy từ mọi chủ đề).
- Tỷ lệ câu dễ.
- Tỷ lệ câu trung bình.
- Tỷ lệ câu khó.
- Điểm cho mỗi câu.
- Có trộn thứ tự câu hỏi hay không.
- Có trộn thứ tự đáp án hay không.
- Có cho Student xem điểm ngay sau khi nộp hay không.
- Thời gian mở bài (lịch chung).
- Thời gian đóng bài (lịch chung).

Ví dụ cấu hình:

| Tổng số câu | Easy | Medium | Hard |
|---:|---:|---:|---:|
| 20 | 30% | 50% | 20% |

Khi đó hệ thống sẽ lấy:

- 6 câu Easy.
- 10 câu Medium.
- 4 câu Hard.

Trong lúc Teacher chỉnh tỷ lệ, hệ thống đối chiếu blueprint với ngân hàng câu hỏi và cho biết mỗi mức độ khó cần bao nhiêu câu, ngân hàng đang có bao nhiêu, còn thiếu bao nhiêu. Số câu của từng mức do server tính.

Điều kiện kiểm tra:

- Tổng tỷ lệ Easy + Medium + Hard phải bằng 100%.
- Số lượng câu hỏi trong ngân hàng phải đủ theo từng mức độ khó.
- Nếu số câu hỏi không đủ, hệ thống cần báo lỗi rõ ràng cho Teacher.
- Thời gian làm bài không được dài hơn khoảng từ lúc mở đến lúc đóng bài.

Vòng đời bài kiểm tra:

| Lifecycle | Ý nghĩa |
|---|---|
| Draft | Bản nháp, chưa giao được |
| Scheduled | Đã xuất bản, chưa tới giờ mở |
| Open | Đang trong thời gian mở bài |
| Closed | Đã qua giờ đóng bài |

- Hệ thống chỉ lưu Draft hoặc đã xuất bản; Scheduled / Open / Closed được tính từ lịch chung so với giờ server.
- Bản nháp được lưu dù tổng tỷ lệ chưa bằng 100% hoặc ngân hàng chưa đủ câu. Muốn xuất bản thì phải thỏa mọi điều kiện kiểm tra ở trên.
- Teacher có thể chỉ dời lịch mở/đóng mà không phải gửi lại toàn bộ cấu hình.
- Teacher không xóa được bài kiểm tra đã có Student nộp bài.
- Teacher chỉ thấy và quản lý bài kiểm tra do chính mình tạo. Admin xem được mọi bài kiểm tra nhưng không tạo hay sửa bài kiểm tra.

---

### Stage 6: Assign Exam to Class / Student

Teacher có thể giao bài kiểm tra cho một hoặc nhiều lớp, hoặc cho từng Student.

Chức năng chính:

- Teacher chọn bài kiểm tra cần giao. Chỉ giao được bài đã xuất bản.
- Teacher chọn lớp và/hoặc từng Student, chỉ trong những lớp đã công bố và được phân công cho mình.
- Hệ thống tạo bài làm cho từng Student dựa trên Exam Blueprint.
- Mỗi Student có thể nhận bộ câu hỏi giống nhau hoặc được random khác nhau tùy cấu hình. Lựa chọn này áp dụng cho cả bài kiểm tra; đổi lựa chọn sau khi đã giao không sinh lại các đề đã phát.
- Giao bài có tính idempotent: giao lại cho Student đã được giao không tạo bài trùng và không ảnh hưởng tới bài Student đã làm.
- Hệ thống báo rõ số Student được giao mới và số bị bỏ qua theo từng lý do: đã được giao, tài khoản đang khóa, không thuộc lớp của Teacher, không tồn tại.
- Student mới được giao nhận thông báo trong ứng dụng.
- Teacher có thể thu hồi bài đã giao cho một Student khi Student đó chưa bắt đầu làm bài.
- Teacher theo dõi trạng thái làm bài của từng Student, kèm khung giờ làm bài thực tế của Student đó.

Lịch làm bài riêng cho từng lớp:

- Cùng một bài kiểm tra có thể giao cho nhiều lớp, mỗi lớp một khung giờ riêng (giờ mở – giờ đóng). Ví dụ lớp 12A1 làm tiết 2 sáng thứ Ba, lớp 12A2 làm tiết 4 chiều thứ Năm.
- Không đặt khung giờ riêng thì Student dùng lịch chung của bài kiểm tra; sửa lịch chung sẽ áp dụng cho những lượt giao không có khung giờ riêng.
- Khung giờ riêng phải có đủ cả giờ mở lẫn giờ đóng và dài ít nhất bằng thời gian làm bài.
- Giao lại một lớp kèm khung giờ mới sẽ cập nhật khung giờ cho những Student của lớp đó chưa bắt đầu làm bài. Student đã bắt đầu giữ nguyên hạn nộp đã chốt.
- Teacher không tăng được thời gian làm bài vượt quá khung giờ riêng của một lớp còn Student chưa bắt đầu làm.

Trạng thái bài làm:

| Status | Ý nghĩa |
|---|---|
| Assigned | Đã giao bài, chưa bắt đầu |
| InProgress | Student đang làm bài |
| Submitted | Student đã nộp bài |
| Graded | Đã chấm điểm |
| Expired | Khung giờ đã đóng mà Student chưa bắt đầu làm |

---

### Stage 7: Student Exam Taking

Student có thể xem danh sách bài kiểm tra được giao và thực hiện bài làm.

Chức năng chính:

- Student xem danh sách bài kiểm tra được giao cho chính mình, chia theo nhóm: đang mở, sắp tới, đã xong, hết hạn.
- Student xem thông tin bài kiểm tra: tên bài, thời gian làm, giờ mở và hạn nộp theo khung giờ của mình.
- Student bắt đầu làm bài. Được bắt đầu hay không do server quyết định theo khung giờ của Student đó.
- Mỗi Student chỉ có một lượt làm bài cho mỗi bài được giao (chưa hỗ trợ làm lại).
- Hạn nộp của lượt làm bài là thời điểm bắt đầu cộng thời gian làm bài, nhưng không muộn hơn giờ đóng bài.
- Đồng hồ đếm ngược tính theo giờ server, không phụ thuộc giờ trên máy Student.
- Tải lại trang hoặc đổi thiết bị thì Student quay lại đúng lượt đang làm: cùng bộ câu hỏi, cùng thứ tự, cùng hạn nộp, cùng các đáp án đã chọn.
- Hệ thống hiển thị câu hỏi dạng text hoặc kèm hình ảnh.
- Student chọn đáp án cho từng câu hỏi; đáp án được tự động lưu nháp lên server.
- Student có thể chuyển qua lại giữa các câu và đánh dấu câu cần xem lại trong thời gian làm bài.
- Student nộp bài.
- Hệ thống tự động nộp bài nếu hết thời gian. Kể cả khi Student đã đóng trình duyệt, tiến trình nền ở server vẫn tự nộp và chấm những lượt đã quá hạn (quét khoảng 30 giây một lần).
- Câu hỏi gửi xuống cho Student trong lúc làm bài không bao giờ kèm đáp án đúng.

Sau khi nộp bài:

- Hệ thống chấm điểm ngay tại server với câu hỏi trắc nghiệm; client không gửi điểm lên.
- Hệ thống lưu lại đáp án Student đã chọn.
- Student xem được điểm và số câu đúng ngay sau khi chấm nếu Teacher bật "cho xem điểm ngay sau khi nộp". Nếu không bật, điểm không được gửi xuống cho Student.
- Đáp án đúng và kết quả đúng/sai của từng câu chỉ được gửi xuống cho Student khi Student đã được xem điểm **và** mọi lớp được giao bài kiểm tra đó đều đã qua giờ đóng bài (kể cả lượt còn đang làm dở). Nhờ vậy lớp làm trước không thể chuyển đáp án cho lớp làm sau.

---

### Stage 8: Result & Report Management

Teacher có thể xem kết quả làm bài của Student.

Chức năng chính:

- Teacher xem kết quả của những bài kiểm tra do chính mình tạo.
- Teacher xem danh sách bài làm theo từng bài kiểm tra.
- Teacher xem trạng thái từng Student.
- Teacher xem điểm số của từng Student.
- Teacher xem số câu đúng / sai.
- Teacher xem thời gian nộp bài.
- Teacher xem chi tiết đáp án Student đã chọn, kể cả những câu bỏ trống.
- Teacher lọc kết quả theo bài kiểm tra, Student, lớp, trạng thái hoặc từ khóa. Bộ lọc nằm trên URL nên có thể chia sẻ đường dẫn.
- Các số liệu thống kê đầu trang tính theo cùng bộ lọc với bảng kết quả.
- Teacher có dashboard tổng quan: số liệu về Student trong lớp mình, câu hỏi, bài kiểm tra và các bài gần đây.

Student có thể xem:

- Điểm bài kiểm tra (nếu Teacher cho phép).
- Số câu đúng (nếu Teacher cho phép).
- Tổng số câu.
- Thời gian nộp bài.
- Chi tiết đáp án theo quy tắc ở Stage 7.
- Dashboard tổng quan: bài đang mở, bài sắp tới, kết quả gần nhất. Điểm trung bình chỉ tính trên những bài Student được xem điểm.

Admin có dashboard riêng về tài khoản Student: tổng số Student, số đang hoạt động, số đang khóa, số lớp, số tài khoản đang chờ xóa và các tài khoản mới tạo. Admin không xem bảng kết quả làm bài.

---

### Stage 9: Notification & Realtime Updates

Hệ thống lưu thông báo trong ứng dụng cho từng người dùng và có thể gửi thông báo realtime bằng SignalR.

Thông báo đang được tạo:

- Teacher giao bài kiểm tra mới cho Student: Student nhận thông báo.
- Student nộp bài kiểm tra: Teacher ra đề nhận thông báo.
- Hệ thống tự động nộp bài khi hết thời gian: cả Student và Teacher nhận thông báo.

Chức năng chính:

- Mỗi người dùng chỉ xem thông báo của chính mình, mới nhất trước.
- Xem số thông báo chưa đọc, đánh dấu một thông báo hoặc tất cả là đã đọc.
- Thông báo được tạo ở backend, cùng lúc với hành động nghiệp vụ sinh ra nó; không có API cho client tự tạo thông báo.

Realtime:

- Backend có SignalR hub đẩy các sự kiện giao bài, nộp bài, tự nộp bài và thay đổi số thông báo chưa đọc.
- Frontend hiện chưa kết nối SignalR; số thông báo chưa đọc được tải lại mỗi khi chuyển trang.
- Thông báo "bài kiểm tra gần hết hạn" và "Teacher cập nhật bài kiểm tra" đã có kiểu dữ liệu nhưng chưa được hệ thống tạo ra.

Realtime chỉ dùng để cập nhật thông báo và trạng thái nhanh. Các nghiệp vụ chính như tạo bài kiểm tra, nộp bài, chấm điểm vẫn xử lý bằng REST API.

---

## 3. Non-functional Requirement

- Giao diện rõ ràng, dễ sử dụng cho Admin, Teacher và Student.
- Website responsive trên desktop và tablet.
- UI sử dụng tiếng Việt.
- Hệ thống có phân quyền theo role, kiểm tra ở server.
- Dữ liệu câu hỏi, bài kiểm tra và kết quả phải được lưu rõ ràng.
- Câu hỏi có thể chứa text và hình ảnh.
- Hệ thống cần kiểm tra dữ liệu khi Teacher tạo Exam Blueprint.
- Hệ thống cần đảm bảo Student chỉ thấy bài kiểm tra được giao cho mình và kết quả của chính mình.
- Hệ thống cần đảm bảo Teacher chỉ làm việc trong phạm vi của mình: lớp đã công bố và được phân công, ngân hàng câu hỏi của môn mình, bài kiểm tra và kết quả do mình tạo.
- Admin sở hữu tài khoản Student và lớp học; Teacher không sửa được tài khoản Student.
- Truy cập tài nguyên ngoài phạm vi trả về "không tìm thấy" (404) thay vì "không có quyền" (403), để không xác nhận tài nguyên đó tồn tại.
- Danh sách Student, câu hỏi và bài kiểm tra nên có tìm kiếm, lọc và phân trang.
- Password được hash.
- API cần có JWT authentication; khóa tài khoản hoặc đổi mật khẩu làm các token đã cấp mất hiệu lực ngay.
- Điểm số, thời gian làm bài và hạn nộp do server quyết định; không tin dữ liệu điểm hay thời gian gửi từ client.
- Dữ liệu cá nhân của Student bị xóa phải biến mất thật, nhưng kết quả làm bài của lớp không được mất.
- Realtime dùng SignalR cho notification và cập nhật trạng thái.
- Source code cần chia module rõ ràng, dễ bảo trì và mở rộng.

---

## 4. Proposed Solutions

### Frontend

Đang sử dụng:

- ReactJS + TypeScript.
- Vite.
- React Router.
- Tailwind CSS.
- lucide-react cho icon.
- `fetch` qua một lớp `apiClient` dùng chung; không dùng thư viện gọi dữ liệu hay thư viện form.
- Validate form bằng code tự viết, lỗi theo từng trường lấy từ server.

### Backend

Đang sử dụng:

- ASP.NET Core Web API (.NET 10).
- Entity Framework Core.
- PostgreSQL.
- JWT Authentication.
- Repository Pattern.
- Unit of Work Pattern.
- FluentValidation.
- SignalR.
- Swagger.
- Background service cho tự động nộp bài quá hạn và xóa dữ liệu tài khoản hết thời gian chờ.

### Database chính

Các bảng chính:

- User (dùng chung cho Admin, Teacher, Student).
- Subject.
- StudentGroup.
- StudentGroupTeacher.
- Question.
- AnswerOption.
- Exam.
- ExamAssignment.
- ExamPaper.
- ExamPaperQuestion.
- ExamPaperAnswer.
- ExamAttempt.
- StudentAnswer.
- Notification.

### Cách triển khai phù hợp

Nên triển khai theo từng giai đoạn:

1. Authentication và phân quyền.
2. Quản lý tài khoản Student.
3. Quản lý lớp học và phân công Teacher.
4. Quản lý ngân hàng câu hỏi.
5. Tạo Exam Blueprint.
6. Giao bài kiểm tra cho lớp / Student, lịch riêng từng lớp.
7. Student làm bài và nộp bài.
8. Tự động chấm điểm và hiển thị kết quả.
9. Notification và realtime bằng SignalR.

---

## 5. Applied Theory

Các lý thuyết và kỹ thuật có thể áp dụng:

- Role-Based Access Control: phân quyền Admin, Teacher và Student.
- Data-scoped Authorization: giới hạn dữ liệu theo phân công lớp, môn học và người tạo, không chỉ theo role.
- CRUD Operations: quản lý Student, lớp học, câu hỏi, bài kiểm tra.
- Question Bank Management: quản lý ngân hàng câu hỏi theo môn, chủ đề và độ khó.
- Exam Blueprint: tạo đề kiểm tra dựa trên cấu hình tỷ lệ câu hỏi.
- Randomized Question Selection: chọn câu hỏi ngẫu nhiên từ ngân hàng câu hỏi.
- Auto-Grading: tự động chấm điểm bài trắc nghiệm ở server, dựa trên bản chụp đề đã sinh.
- Idempotent API: giao bài, bắt đầu làm bài và nộp bài gọi lại nhiều lần vẫn cho cùng kết quả.
- Background Jobs: tự nộp bài quá hạn, xóa dữ liệu tài khoản hết thời gian chờ.
- Soft Delete & Anonymization: xóa mềm có thời gian chờ, sau đó ẩn danh hóa nhưng giữ kết quả.
- Relational Database Design: thiết kế quan hệ giữa User, StudentGroup, Question, Exam, Assignment, Attempt, Answer.
- RESTful API Design: thiết kế API rõ ràng cho frontend sử dụng.
- Repository Pattern: tách logic truy cập dữ liệu.
- Unit of Work Pattern: quản lý transaction khi xử lý nhiều thao tác database.
- SignalR Realtime Communication: gửi thông báo và cập nhật trạng thái realtime.
- Component-Based UI: chia giao diện frontend thành các component tái sử dụng.

---

## 6. Products / Expected Deliverables

Sản phẩm kỳ vọng gồm:

- Website quản lý bài kiểm tra trực tuyến cho trường học / trung tâm.
- Trang đăng nhập.
- Dashboard cho Admin.
- Dashboard cho Teacher.
- Dashboard cho Student.
- Trang quản lý tài khoản Student (tạo, sửa, khóa, cấp lại mật khẩu, xóa mềm và khôi phục).
- Trang quản lý lớp học: xếp Student, phân công Teacher, công bố lớp.
- Trang lớp học chỉ đọc cho Teacher.
- Trang ngân hàng câu hỏi.
- Form tạo câu hỏi có hỗ trợ hình ảnh.
- Trang tạo bài kiểm tra bằng Exam Blueprint.
- Chức năng cấu hình tỷ lệ câu Easy / Medium / Hard.
- Chức năng giao bài kiểm tra cho lớp / Student, có lịch riêng từng lớp.
- Trang Student làm bài kiểm tra.
- Chức năng tự động chấm điểm.
- Trang xem kết quả bài kiểm tra.
- Trang thông báo cho cả ba role.
- Notification realtime bằng SignalR.
- Database schema hoặc ERD.
- Source code frontend.
- Source code backend.
- Tài liệu mô tả yêu cầu hệ thống.
