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
- Người dùng xem được hồ sơ của mình và đổi mật khẩu (phải nhập đúng mật khẩu hiện tại). Thông tin liên hệ do nhà trường (Admin) quản lý: Teacher chỉ tự sửa được họ tên, còn email, số điện thoại và môn giảng dạy do Admin đổi; Student không tự sửa trường nào (họ tên, email, số điện thoại, mã học sinh, lớp đều do Admin giữ — Stage 2). Giao diện hiện các trường đó ở dạng chỉ xem kèm dòng giải thích, và server từ chối giá trị khác đi.
- Hệ thống kiểm tra quyền truy cập theo role ở server; giao diện chỉ làm nhiệm vụ điều hướng.
- Khóa tài khoản, đổi mật khẩu hoặc Admin cấp lại mật khẩu làm mọi phiên đăng nhập đang mở của tài khoản đó mất hiệu lực ngay; giao diện đưa người dùng về trang đăng nhập.

Tài khoản Teacher và danh mục môn học hiện được tạo sẵn trong dữ liệu khởi tạo; hệ thống chưa có chức năng quản lý tài khoản Teacher (nên cũng chưa có màn hình để Admin đổi email, số điện thoại hay môn của Teacher).

---

### Stage 2: Student Account Management

Admin sở hữu tài khoản Student. Teacher chỉ xem hồ sơ Student, không tạo, sửa, khóa hay xóa.

Chức năng chính:

- Admin tạo tài khoản Student kèm mật khẩu ban đầu.
- Admin xem danh sách Student, có phân trang, lọc theo lớp và trạng thái tài khoản.
- Admin tìm kiếm Student theo họ tên, email, username hoặc mã học sinh (không phân biệt dấu và chữ hoa/thường).
- Admin chỉnh sửa thông tin Student (họ tên, email, số điện thoại không bắt buộc, tên đăng nhập, mã học sinh) và xếp Student vào lớp. Số điện thoại của Student chỉ Admin thấy và sửa; Teacher không nhận trường này.
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
- Teacher chọn loại câu: một đáp án đúng hoặc nhiều đáp án đúng.
- Teacher chọn đáp án đúng (một đáp án, hoặc một số đáp án nếu là câu nhiều đáp án đúng).
- Teacher có thể thêm hình ảnh vào câu hỏi.
- Teacher có thể thêm hình ảnh vào đáp án nếu cần.
- Teacher tìm kiếm và lọc câu hỏi theo từ khóa, chủ đề, mức độ khó, trạng thái.

Quy tắc:

- Câu hỏi đã nằm trong một đề đã sinh thì không xóa được, chỉ ẩn được.
- Ẩn câu hỏi chỉ loại câu đó khỏi các lần sinh đề sau; đề đã sinh không thay đổi.
- Sửa câu hỏi không làm thay đổi điểm của các bài đã chấm, vì bài làm được chấm theo bản chụp của đề đã sinh.
- Hình ảnh phải được tải lên server trước qua API upload (PNG, JPEG, GIF, WebP); câu hỏi và đáp án chỉ lưu đường dẫn ảnh, server không nhận ảnh nhúng dạng data URI. Ảnh được tải lên ngay khi Teacher chọn; trong lúc ảnh còn đang tải, câu hỏi chưa lưu được.

Mức độ khó của câu hỏi:

| Difficulty | Ý nghĩa |
|---|---|
| Easy | Câu dễ |
| Medium | Câu trung bình |
| Hard | Câu khó |

Thông tin câu hỏi gồm:

- Tên câu hỏi: nhãn ngắn do Teacher đặt để nhận ra câu trong ngân hàng và trong đề. **Trình soạn câu hỏi bắt buộc nhập** (ô sáng viền vàng khi còn trống); API vẫn nhận tên trống để tương thích câu cũ, khi đó hệ thống hiển thị phần đầu của nội dung. Student không thấy tên này khi làm bài.
- Nội dung câu hỏi.
- Hình ảnh minh họa cho câu hỏi nếu có.
- Chủ đề câu hỏi.
- Mức độ khó.
- Danh sách đáp án, mỗi đáp án có thể kèm hình ảnh.
- Loại câu: một đáp án đúng hoặc nhiều đáp án đúng.
- Đáp án đúng.
- Điểm mặc định.
- Trạng thái câu hỏi (Active / Hidden).
- Chế độ chia sẻ (Shared / Private).

Trình soạn câu hỏi và các biểu mẫu tạo/sửa khác (bài kiểm tra, học sinh):

- **Ô còn thiếu sáng viền vàng ngay từ đầu** (tên câu hỏi, đề bài, chủ đề, điểm, nội dung từng đáp án, ô chữ cái khi chưa chọn đáp án đúng, nút thêm đáp án khi chưa đủ số tối thiểu), không còn hộp liệt kê "còn N mục cần hoàn thiện". Tab nào còn ô thiếu thì có chấm vàng. Nút lưu khoá cho tới khi đủ.
- **Bấm nút lưu chỉ mở hộp xác nhận** có bản xem trước như học sinh; trong hộp chọn "Lưu" hoặc "Lưu và tạo câu tiếp theo" (khi sửa câu cũ chỉ có "Lưu thay đổi"). Đóng hộp là quay lại sửa tiếp.
- **Rời trang khi còn thay đổi chưa lưu thì hỏi lại**: bấm liên kết, nút quay lại, nút Back của trình duyệt đều hiện hộp "Thoát mà chưa lưu?" với hai nút Huỷ / Bỏ qua, chỉ "Bỏ qua" mới rời đi; đóng tab hay tải lại thì dùng hộp hỏi của trình duyệt. Lưu xong hay chủ động chuyển trang (ví dụ sang soạn thêm câu hỏi cho đề đang soạn) thì không hỏi.

Loại câu hỏi hỗ trợ:

- Multiple Choice: chọn 1 đáp án đúng. Câu có đúng một đáp án đúng.
- Multiple Answers: chọn nhiều đáp án đúng. Teacher bật rõ loại này khi soạn câu; câu có ít nhất một đáp án đúng, và Student thấy nhãn "Chọn tất cả đáp án đúng".

Chấm điểm câu nhiều đáp án đúng: Student phải chọn **đúng và đủ** tất cả đáp án đúng mới được điểm của câu đó. Chọn thiếu, chọn thừa hoặc không chọn thì câu đó 0 điểm; chưa có điểm từng phần.

Có thể mở rộng sau:

- True/False.
- Short Answer.
- Chấm điểm từng phần cho câu nhiều đáp án đúng.

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

Điểm của bài kiểm tra — thang chuẩn là 10 điểm, không có thang khác (trước đây là 100, đổi sang 10 ngày 15/10/2026):

- Điểm gắn theo mức độ khó chứ không theo từng câu: Teacher nhập điểm của một câu Easy, một câu Medium, một câu Hard (không âm, tối đa hai chữ số thập phân, mức nào có câu thì điểm phải lớn hơn 0). Câu hỏi thật chỉ xuất hiện khi sinh đề nên không có điểm riêng từng câu.
- Tổng = số câu Easy × điểm Easy + số câu Medium × điểm Medium + số câu Hard × điểm Hard. Teacher chọn số câu bất kỳ nên tổng không phải lúc nào cũng ra đúng 10; hệ thống luôn hiện tổng dạng "n / 10" và nói còn thiếu hay đang thừa bao nhiêu.
- Chưa đủ 10 vẫn lưu được, nhưng khi Teacher chốt (lưu bài, xuất bản, lưu và giao bài) hệ thống hỏi xác nhận: xác nhận thì lưu như vậy, không thì quay lại sửa số câu hoặc điểm mỗi câu. Bản nháp không bị hỏi.
- Nút gợi ý chia điểm điền sẵn cách chia đủ 10 nếu có, câu khó điểm cao hơn câu dễ (trọng số mặc định 1 : 1,5 : 2), ưu tiên điểm tròn, nửa điểm hay phần tư điểm; Teacher vẫn tự chỉnh được.
- Có những cách chia số câu không thể ra đúng 10 khi điểm chỉ có hai chữ số thập phân (ví dụ 12 / 12 / 6 câu: tổng luôn là bội của 0,06). Khi đó hệ thống nói rõ, điền cách chia sát 10 nhất và gợi ý số câu gần nhất chia được đúng. (Đề **chia nhiều đề** thì khác: mọi đề nhỏ luôn đúng 10 nhờ điểm cộng thêm, xem phần chia đề.)
- Bài kiểm tra có từ trước quy tắc này (kể cả đề cũ chấm trên 100) giữ nguyên điểm cũ và không bị hỏi xác nhận chừng nào chưa đổi cấu hình đề.
- Mọi lượt làm bài của một bài kiểm tra cùng tổng điểm; kết quả và thống kê (điểm trung bình, điểm cao nhất, xếp loại) quy về thang 10 theo điểm tối đa của chính từng đề.

Soạn đề theo danh sách câu hỏi (cách soạn mặc định của đề mới):

- Teacher thấy ngay nội dung đề lúc soạn: mỗi câu hiện như một thẻ (nội dung, ảnh, các đáp án với đáp án đúng đánh dấu, mức độ, chủ đề, điểm). Duyệt xong mới lưu, nên bộ câu học sinh nhận đúng là bộ câu Teacher đã nhìn thấy — khác với đề theo ma trận, nơi câu hỏi chỉ được rút lúc giao bài.
- Đưa câu vào đề bằng ba cách: chọn từ ngân hàng (tìm theo nội dung, mức độ, chủ đề, và có thể ẩn những câu mà lớp đã nhận ở đề trước); "Soạn tự động" (chọn chương, nhập số câu mỗi mức, hệ thống rút ngẫu nhiên và bỏ vào đề để Teacher duyệt, không rút trùng câu đã có, thiếu thì báo thiếu bao nhiêu); hoặc tạo câu hỏi mới ở ngân hàng rồi quay lại đề.
- "Soạn tự động" theo chương: chủ đề của câu hỏi được dùng như **chương**. Teacher **phải chọn rõ chương** (không có mặc định "mọi chủ đề"; chọn "Mọi chương" là một lựa chọn có chủ ý) để đề cho một chương không lọt câu của chương khác, và mỗi ô số câu hiện số câu dùng được của chương đó. Đề gồm nhiều chương: soạn chương này xong, bấm "Soạn tự động" lần nữa và chọn chương sau — câu mới được **thêm vào cuối đề**, không ghi đè và không rút lại câu đã có; ô chương về trống sau mỗi lần soạn để chọn chương kế tiếp, tab Nội dung hiện số câu theo từng chương.
- "Soạn tự động" có thể **không lặp lại câu mà lớp đã nhận**: Teacher tích các lớp sẽ làm đề, hệ thống bỏ những câu đã nằm trong các đề từng giao cho các lớp đó (câu thực sự học sinh của lớp đã nhận, không phải mọi câu của đề lớn chia nhiều đề). Báo rõ đã bỏ bao nhiêu câu, và số câu rút được có thể ít hơn số yêu cầu.
- Soạn không đủ số câu yêu cầu thì **báo nổi bật, không để thông báo thoáng qua**: một khung vàng nằm ngay trên danh sách câu, ghi rút được bao nhiêu câu và còn thiếu bao nhiêu câu ở mức nào (kèm lý do và cách xử lý), ở lại cho tới khi Teacher bấm "Đã hiểu" hoặc soạn lần khác — vì thông báo mờ sau vài giây dễ khiến Teacher tưởng đề đã đủ số câu mình muốn.
- Hộp "Thêm từ ngân hàng" có cùng bộ lọc **"Lớp đã nhận"**: Teacher tích các lớp sẽ làm đề, danh sách chỉ còn những câu các lớp đó chưa nhận (cùng cách tính "đã nhận" với Soạn tự động). Mục này đóng sẵn để không chiếm chỗ; chỉ giáo viên có lớp mới thấy.
- Khi "Soạn tự động" với **"Mọi chương"**, hệ thống **không** tự cân số câu giữa các chương: mỗi câu có cơ hội như nhau, Teacher muốn số lượng từng chương thì soạn từng chương một (đã chốt 17/10/2026).
- Mỗi câu có điểm riêng. Tổng điểm hiện nổi bật ("n / 10" kèm thiếu hay thừa bao nhiêu) để Teacher tự cân đối; có nút chia đều và nút chia theo mức độ (1 : 1,5 : 2). Thêm hay bớt câu không tự đổi điểm các câu khác: điểm chỉ được chia lại khi Teacher bấm một trong hai nút. Luật xác nhận khi tổng khác 10 như phần trên.
- Có thể chọn lớp nhận bài ngay trong form tạo đề (kèm khung giờ riêng từng lớp); bấm Lưu một lần là một hộp xác nhận cho cả xuất bản lẫn giao.
- Đã có em bắt đầu làm thì danh sách câu, điểm từng câu, số đề, thời lượng và tuỳ chọn trộn bị khoá như cấu hình của đề ma trận; muốn đổi thì nhân bản đề.
- Câu hỏi đang nằm trong một đề không xoá được (ẩn thì được, nhưng đề chứa câu bị ẩn không xuất bản hay giao được cho tới khi thay câu).
- Đề theo danh sách cả lớp dùng chung một bộ câu; học sinh vẫn được trộn thứ tự câu và đáp án riêng nếu bật tuỳ chọn trộn. Muốn mỗi em một đề khác nhau để chống gian lận thì chia đề lớn thành nhiều đề nhỏ (mục ngay dưới).
- Đề tạo trước đó theo ma trận (số câu và tỷ lệ độ khó) giữ nguyên cách hoạt động.

Chia đề lớn thành nhiều đề nhỏ (đề soạn theo danh sách):

- Ngay trong form tạo bài kiểm tra, ở tab Cấu hình, ngay dưới thông tin chung, Teacher tích ô "Chia thành nhiều đề cho mỗi lớp"; không tích (mặc định) thì cả lớp làm chung một đề, tích vào mới mở khung cấu hình chia đề. Teacher tự soạn đề lớn bằng danh sách câu hỏi như trên, rồi chọn số đề nhỏ mỗi lớp nhận (2 đến 10). Từ 2 đề trở lên, Teacher nhập số câu của một đề nhỏ và tỷ lệ Easy / Medium / Hard của đề nhỏ; đó là chuẩn chung, nên mọi đề nhỏ có cùng số câu ở từng mức độ.
- **Teacher không nhập điểm khi chia đề: hệ thống tự tính.** Với số câu ba mức của một đề nhỏ, hệ thống tìm ba điểm (hai chữ số thập phân, câu khó không ít điểm hơn câu dễ, tỷ trọng điểm ba mức sát 1 : 1,5 : 2 nhất) sao cho **một đề nhỏ cộng đúng 10 điểm** mà số câu từng mức vẫn giữ đúng tỷ lệ Teacher đặt; các câu cùng mức cùng điểm nên mọi đề nhỏ có chung tổng điểm. Đổi số câu hay tỷ lệ thì điểm được tính lại ngay. **Mọi cấu hình đều ra đúng 10** (đã chốt 17/10/2026): với số câu mà điểm theo mức không chia ra đúng (số câu ba mức có ước chung không chia hết 1000, như 3 / 3 / 3 hay 9 / 9 / 9, vì điểm chỉ có hai chữ số thập phân), phần lẻ được **cộng thêm 0,01 điểm cho vài câu mức cao nhất của mỗi đề nhỏ** (ví dụ 3 / 3 / 3 câu, điểm 0,9 / 1,1 / 1,33 = 9,99, cộng 0,01 cho một câu Khó → 10). Hệ thống tự chọn các câu nhận điểm cộng thêm trong từng đề của từng em, nên mọi đề vẫn cùng tổng; màn chia đề ghi rõ "+0.01 cho N câu" ở mức đó. Khi bật chia đề lần đầu, số câu mỗi đề được đặt sẵn ở mức chia đều đề lớn.
- Hai ưu tiên khi chia, theo thứ tự: mỗi đề đủ số câu, rồi đúng tỷ lệ độ khó. Đề lớn đủ câu khác nhau cho mọi đề thì các đề không trùng nhau; không đủ thì được trùng câu giữa các đề: mỗi câu được dùng đều nhau (chênh nhiều nhất một lần) và trong một đề không bao giờ có hai câu giống nhau. Mức nào đề lớn có ít câu hơn số một đề cần thì không xuất bản được. Màn hình soạn tính trước và nói rõ: mỗi mức đề lớn có bao nhiêu câu, mỗi đề cần bao nhiêu, bao nhiêu câu trùng lặp giữa các đề, điểm mỗi câu và tổng điểm của từng mức (cộng lại đúng 10), cùng ghi chú bao nhiêu câu không vào đề nào.
- Mỗi lớp được chia riêng (xáo ngẫu nhiên độc lập), nên cùng là đề A nhưng hai lớp có thể gồm các câu khác nhau.
- Student nhận đề ngẫu nhiên nhưng cân bằng: số em mỗi đề trong một lớp chênh nhau nhiều nhất một, và không nhận theo thứ tự danh sách (không xoay A, B, C, D) để không suy ra được đề của mình từ vị trí. Em được thêm vào lớp sau cũng được xếp vào đề đang ít em nhất.
- Mỗi em có một bản đề riêng (câu và đáp án trộn riêng nếu bật trộn). Student không thấy mã đề; mã đề A, B, C… chỉ hiện cho Teacher và Admin: cột "Mã đề" ở trang theo dõi, thẻ "Chia đề" ở tab Cấu hình của trang chi tiết (số em mỗi đề của từng lớp), và cạnh tên em ở trang chi tiết bài làm.
- Teacher xem được các câu của từng đề nhỏ: ở tab Nội dung của trang chi tiết, chọn "Từng đề nhỏ" rồi chọn lớp và mã đề (hoặc bấm mã đề ở bảng theo dõi, hay số em của một đề ở thẻ "Chia đề"). Trang hiện số câu từng mức, điểm, số em đang cầm đề và những câu trùng với đề khác của lớp; đáp án vẫn ẩn cho tới khi bấm "Xem đáp án". Đề nhỏ chỉ có sau khi giao bài (mỗi lớp được chia riêng lúc giao), nên chưa giao lớp nào thì chỉ xem được đề lớn.
- Đã có em bắt đầu làm thì số đề, số câu mỗi đề, tỷ lệ và điểm cộng thêm bị khoá như phần còn lại của cấu hình; chưa ai làm thì đổi được và các đề được chia lại, phát lại.
- Chia nhiều đề hiện chỉ có với đề soạn theo danh sách; đề theo ma trận chưa chia được.

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
- Khóa đổi giờ: bài kiểm tra đã giao cho Student thì không đổi được lịch mở/đóng từ lúc còn dưới 120 phút nữa là mở cho tới hết giờ đóng. Giờ riêng của một lớp hay của một Student chọn đích danh cũng bị khóa trong khoảng đó, tính theo giờ mở của chính khung giờ ấy. Còn từ 120 phút trở lên, hoặc đã qua giờ đóng, thì đổi bình thường; qua giờ đóng là đường cứu Student lỡ giờ hoặc mở lại bài. Bài chưa giao cho ai thì không bị khóa; bài đã giao thì đưa về bản nháp cũng không thoát khóa. Giao thêm Student vào một lớp đang bị khóa giờ vẫn được, các em theo khung giờ hiện có của lớp.
- Teacher không xóa được bài kiểm tra đã có Student nộp bài.
- Teacher chỉ thấy và quản lý bài kiểm tra do chính mình tạo. Admin xem được mọi bài kiểm tra nhưng không tạo hay sửa bài kiểm tra.

Sửa bài kiểm tra đã xuất bản — mốc phân chia là **lượt làm bài đầu tiên**, không phải lúc giao hay lúc xuất bản:

- Chưa Student nào bắt đầu làm: Teacher sửa được mọi trường (riêng lịch mở/đóng theo luật khóa đổi giờ ở trên). Đề đã sinh cho các Student được giao sẽ được sinh lại theo cấu hình mới; ngân hàng không đủ câu cho cấu hình mới thì hệ thống từ chối và giữ nguyên cấu hình cũ. Bài kiểm tra đã thu hồi hết các lượt giao thì đề cũ bị bỏ, lần giao sau sinh theo cấu hình mới.
- Đã có Student bắt đầu làm: cấu hình đề bị khóa — số câu, ba tỷ lệ độ khó, chủ đề, điểm từng mức độ, thời gian làm bài và hai tùy chọn trộn không đổi được nữa, và không đưa bài kiểm tra về bản nháp được. Tên, mô tả và tùy chọn cho xem điểm ngay vẫn sửa được; lịch mở/đóng đổi theo luật khóa đổi giờ ở trên. Hệ thống báo rõ trường nào bị khóa.
- Muốn dùng cấu hình khác cho một bài đã khóa, Teacher nhân bản bài kiểm tra: bản sao là một bản nháp mới cùng cấu hình, chưa giao cho ai, sửa được thoải mái. Teacher chưa được gán môn không nhân bản được, giống như không tạo được bài kiểm tra.
- Điểm đã chấm không bao giờ thay đổi vì bất kỳ lần sửa nào: bài làm được chấm trên bản đề đã chụp lúc sinh đề.

---

### Stage 6: Assign Exam to Class

Teacher giao bài kiểm tra cho một hoặc nhiều lớp. Bài chỉ giao theo lớp, không giao riêng từng Student.

Chức năng chính:

- Teacher chọn bài kiểm tra cần giao. Chỉ giao được bài đã xuất bản.
- Teacher chọn một hoặc nhiều lớp, chỉ trong những lớp đã công bố và được phân công cho mình. Student mới vào lớp nhận bài khi Teacher giao lại lớp.
- Hệ thống tạo bài làm cho từng Student dựa trên Exam Blueprint.
- Mỗi Student có thể nhận bộ câu hỏi giống nhau hoặc được random khác nhau tùy cấu hình. Lựa chọn này áp dụng cho cả bài kiểm tra và bị khóa từ lần giao đầu tiên: bài kiểm tra đã có lượt giao thì server từ chối đổi lựa chọn (409). Muốn đổi, Teacher thu hồi hết rồi giao lại.
- Giao bài có tính idempotent: giao lại cho Student đã được giao không tạo bài trùng và không ảnh hưởng tới bài Student đã làm.
- Hệ thống báo rõ số Student được giao mới và số bị bỏ qua theo từng lý do: đã được giao, tài khoản đang khóa, đã rời trường (tài khoản đang chờ xóa), không thuộc lớp của Teacher, không tồn tại. "Đang khóa" và "đã rời trường" được đếm riêng vì dẫn tới hai việc khác nhau: mở khóa rồi giao lại, hoặc không cần làm gì. Tài khoản đã xóa hẳn không còn thuộc lớp nào nên được tính là không thuộc lớp của Teacher.
- Student mới được giao nhận thông báo trong ứng dụng.
- Teacher có thể thu hồi bài đã giao cho cả một lớp: hệ thống gỡ bài khỏi những Student của lớp chưa bắt đầu làm; Student đã bắt đầu giữ nguyên bài và điểm. Không có thu hồi riêng từng Student.
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
- Student chọn đáp án cho từng câu hỏi (câu nhiều đáp án đúng cho chọn nhiều ô, bấm lại để bỏ chọn); đáp án được tự động lưu nháp lên server.
- Student có thể chuyển qua lại giữa các câu và đánh dấu câu cần xem lại trong thời gian làm bài.
- Student nộp bài.
- Hệ thống tự động nộp bài nếu hết thời gian. Kể cả khi Student đã đóng trình duyệt, tiến trình nền ở server vẫn tự nộp và chấm những lượt đã quá hạn (quét khoảng 30 giây một lần).
- Câu hỏi gửi xuống cho Student trong lúc làm bài không bao giờ kèm đáp án đúng.

Sau khi nộp bài:

- Hệ thống chấm điểm ngay tại server với câu hỏi trắc nghiệm; client không gửi điểm lên.
- Hệ thống lưu lại đáp án Student đã chọn (một hoặc nhiều đáp án cho mỗi câu).
- Student xem được điểm và số câu đúng ngay sau khi chấm nếu Teacher bật "cho xem điểm ngay sau khi nộp". Nếu không bật, điểm không được gửi xuống cho Student.
- Đáp án đúng và kết quả đúng/sai của từng câu chỉ được gửi xuống cho Student khi Student đã được xem điểm **và** mọi lớp được giao bài kiểm tra đó đều đã qua giờ đóng bài (kể cả lượt còn đang làm dở). Nhờ vậy lớp làm trước không thể chuyển đáp án cho lớp làm sau.
- Teacher có nút "Mở đáp án" ở trang chi tiết bài kiểm tra, dùng khi đề đang tắt "Hiện kết quả sau khi nộp": bấm thì học sinh đã nộp xem được điểm và đáp án từng câu. **Nút chỉ bấm được khi đã hết giờ** — lớp đóng bài muộn nhất đã qua giờ đóng. Còn lớp đang làm bài, hoặc còn lớp chưa tới giờ làm, hoặc đề chưa giao cho lớp nào thì nút bị khoá (server cũng từ chối). Không có lối mở sớm hơn giờ đóng. Đề đã bật "Hiện kết quả sau khi nộp" thì đáp án tự mở khi lớp cuối hết giờ, không cần bấm. Việc báo cho học sinh biết đáp án đã mở sẽ làm sau, qua email của học sinh.
- Học sinh **luôn được thông báo** khi nhận bài mới: không còn ô chọn "báo cho học sinh" ở lúc giao bài (em nào bị dời giờ cũng luôn được báo).
- **Trang chi tiết bài kiểm tra** có bốn tab: **Theo dõi** (danh sách lớp → học sinh → bài làm), **Cấu hình** (lịch mở bài, các tuỳ chọn làm bài, ma trận với đề theo ma trận, khung giờ theo lớp và thu hồi bài của một lớp), **Nội dung** (các thao tác mở đáp án cho học sinh, sửa, nhân bản, xoá đề; và các câu hỏi của đề kèm điểm từng câu, có tìm kiếm, lọc theo mức độ, cuộn trong khung nên dùng được với đề vài trăm câu) và **Giao bài**. Tên đề là tiêu đề trang; thời lượng, số câu, điểm và ngày tạo nằm ngay dưới. Ở tab Nội dung **đáp án đúng ẩn mặc định**, giáo viên bấm "Xem đáp án" mới thấy (rời tab là ẩn lại), để mở đề trước lớp không lộ đáp án.
- **Trang theo dõi của một bài kiểm tra** đi theo thứ tự đề → lớp → học sinh → bài làm: vào đề là thấy **danh sách lớp** (kể cả khi chỉ có một lớp), bấm một lớp mới hiện học sinh của lớp đó, bấm học sinh (đã nộp) mở bài làm. Bảng học sinh lọc được theo trạng thái làm bài (chưa bắt đầu, đang làm, chờ chấm, đã chấm, hết hạn, kèm số học sinh) và tìm theo tên, mã hay nhóm. Bảng cũng sắp xếp được: mặc định em **nộp sớm nhất** ở trên cùng; có thể đổi sang nộp mới nhất, điểm cao nhất hay điểm thấp nhất. Em chưa nộp hay chưa có điểm luôn nằm dưới ở mọi thứ tự.
- **Chọn lớp để giao bài** (lúc tạo đề và ở tab Giao bài của đề) có ô tìm kiếm, sắp xếp theo ngày tạo (mới nhất, lâu nhất) hay theo tên, và phân trang; lớp đã chọn được giữ khi đổi trang hay đổi từ khoá.
- **Ảnh đại diện:** giáo viên và quản trị viên tự đổi hoặc gỡ ảnh của mình ở trang Hồ sơ cá nhân (ảnh PNG, JPG, GIF, WEBP tối đa 4 MB); chưa có ảnh thì hệ thống vẽ chữ cái đầu của tên. Học sinh không đổi được (hồ sơ học sinh do nhà trường quản lý).
- **Danh sách mở chi tiết khi bấm vào bất cứ chỗ nào của hàng** (nhóm lớp, bài kiểm tra, học sinh, kết quả; ngân hàng câu hỏi thì mở cửa sổ xem trước), trừ các nút và liên kết có việc riêng. Nút "Quay lại" của các trang chi tiết nằm ở thanh tiêu đề.

---

### Stage 8: Result & Report Management

Teacher có thể xem kết quả làm bài của Student.

Chức năng chính:

- Teacher xem kết quả của những bài kiểm tra do chính mình tạo.
- Teacher xem danh sách bài làm theo từng bài kiểm tra: mở bài kiểm tra, thấy các lớp đã làm bài, chọn một lớp để xem học sinh của lớp đó.
- Teacher xem trạng thái từng Student.
- Teacher xem điểm số của từng Student.
- Teacher xem số câu đúng / sai.
- Teacher xem thời gian nộp bài.
- Teacher xem chi tiết đáp án Student đã chọn, kể cả những câu bỏ trống. Câu nhiều đáp án đúng hiện đủ các ô đã chọn và các ô đúng, để thấy chọn thiếu hay thừa ô nào.
- Không có trang bảng kết quả chung. Teacher đi tới bài làm của một học sinh theo hai lối: (1) Nhóm lớp → lớp → học sinh → bài làm của học sinh đó; (2) Bài kiểm tra → mở bài kiểm tra → lớp đã làm bài → học sinh → bài làm. Lớp đang xem ở lối (2) nằm trên URL nên quay lại từ trang bài làm vẫn đứng đúng lớp đó.
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

- Backend có SignalR hub đẩy các sự kiện giao bài, đổi lịch, nộp bài, tự nộp bài, bài sắp đóng và thay đổi số thông báo chưa đọc.
- Frontend giữ một kết nối SignalR suốt phiên đăng nhập: chuông thông báo, danh sách bài của học sinh, bảng kết quả và trang theo dõi đề của giáo viên tự cập nhật khi có sự kiện, không cần tải lại trang. Mất kết nối quá vài giây thì hiện dải "Đang kết nối lại".
- Thông báo "bài kiểm tra sắp đóng" và "giáo viên đổi lịch bài kiểm tra" đều do hệ thống tạo ra.

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
