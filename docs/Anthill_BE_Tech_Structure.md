# Anthill Backend Technology & Structure

## 1. Backend Context

Backend của Anthill phục vụ hệ thống tạo và quản lý bài kiểm tra trực tuyến. Backend tập trung vào các nghiệp vụ chính:

- Authentication và phân quyền Teacher / Student.
- Quản lý Student.
- Quản lý ngân hàng câu hỏi.
- Hỗ trợ câu hỏi có text và hình ảnh.
- Tạo bài kiểm tra theo Exam Blueprint.
- Sinh bài làm cho Student dựa trên tỷ lệ Easy / Medium / Hard.
- Giao bài kiểm tra cho Student.
- Student làm bài và nộp bài.
- Tự động chấm điểm bài trắc nghiệm.
- Quản lý kết quả và báo cáo.
- Notification realtime bằng SignalR.

Backend không tập trung vào quản lý khóa học hoặc bài học.

---

## 2. Proposed Backend Stack

Đề xuất sử dụng:

- ASP.NET Core Web API.
- Entity Framework Core.
- SQL Server hoặc PostgreSQL.
- JWT Authentication.
- BCrypt Password Hashing.
- Repository Pattern.
- Unit of Work Pattern.
- SignalR.
- Swagger / OpenAPI.
- FluentValidation.
- AutoMapper hoặc mapping thủ công.

Gợi ý stack:

| Nhu cầu | Công nghệ |
|---|---|
| Backend API | ASP.NET Core Web API |
| ORM | Entity Framework Core |
| Database | SQL Server hoặc PostgreSQL |
| Authentication | JWT Bearer |
| Password Hashing | BCrypt |
| Realtime | SignalR |
| API Documentation | Swagger |
| Validation | FluentValidation |
| Excel (đọc file nhập, dựng file mẫu) | ClosedXML, sau cổng `ISpreadsheetService` |
| Architecture | Clean Architecture rút gọn |
| Data Access | Repository + UnitOfWork |

---

## 3. Backend Architecture

Backend dùng kiến trúc:

```text
Clean Architecture rút gọn
+ Repository Pattern
+ Unit of Work Pattern
+ SignalR Realtime
```

Luồng xử lý chính:

```text
Controller
→ Application Service
→ UnitOfWork
→ Repository
→ AppDbContext
→ Database
```

Realtime flow:

```text
Application Service xử lý nghiệp vụ
→ UnitOfWork.SaveChangesAsync()
→ IRealtimeNotificationService
→ SignalR Hub
→ Frontend nhận notification
```

---

## 4. Solution Structure

```text
Anthill_BE/
│
├── Anthill.Backend.sln
│
├── src/
│   ├── Anthill.Api/
│   │   ├── Controllers/
│   │   ├── Hubs/
│   │   ├── Middlewares/
│   │   ├── Extensions/
│   │   ├── Program.cs
│   │   ├── appsettings.json
│   │   └── appsettings.Development.json
│   │
│   ├── Anthill.Application/
│   │   ├── DTOs/
│   │   ├── Interfaces/
│   │   │   ├── Services/
│   │   │   ├── Repositories/
│   │   │   ├── UnitOfWork/
│   │   │   └── Realtime/
│   │   ├── Services/
│   │   ├── Common/
│   │   ├── Validators/
│   │   └── Mappings/
│   │
│   ├── Anthill.Domain/
│   │   ├── Entities/
│   │   ├── Enums/
│   │   ├── Constants/
│   │   └── Common/
│   │
│   └── Anthill.Infrastructure/
│       ├── Persistence/
│       ├── Repositories/
│       ├── UnitOfWork/
│       ├── Realtime/
│       ├── Authentication/
│       ├── FileStorage/
│       ├── Spreadsheets/
│       ├── Services/
│       └── DependencyInjection.cs
│
└── tests/
    └── Anthill.Tests/
```

---

## 5. Anthill.Api Structure

```text
Anthill.Api/
│
├── Controllers/
│   ├── AuthController.cs
│   ├── StudentsController.cs
│   ├── StudentGroupsController.cs
│   ├── TermsController.cs            # danh mục kỳ học (chỉ admin)
│   ├── TeachersController.cs         # tài khoản giáo viên (chỉ admin)
│   ├── ImportsController.cs          # nhập học sinh, giáo viên, lớp từ Excel: file mẫu + nhập (dryRun), multipart
│   ├── QuestionsController.cs
│   ├── ExamsController.cs
│   ├── ExamAssignmentsController.cs
│   ├── ExamAttemptsController.cs
│   ├── ResultsController.cs
│   └── DashboardController.cs
│
├── Hubs/
│   └── NotificationHub.cs
│
├── Filters/
│   ├── ValidationFilter.cs
│   └── UploadSizeLimitAttribute.cs   # chặn file quá lớn trước khi đọc form: ảnh câu hỏi (theo cấu hình) và file Excel (5 MB)
│
├── Requests/
│   └── ImportForms.cs               # thân multipart có IFormFile; chỉ tầng Api biết ASP.NET Core, controller bóc thành Stream
│
├── Middlewares/
│   ├── ExceptionMiddleware.cs
│   └── RequestLoggingMiddleware.cs
│
├── Extensions/
│   ├── ServiceCollectionExtensions.cs
│   └── ApplicationBuilderExtensions.cs
│
└── Program.cs
```

Vai trò của `Anthill.Api`:

- Nhận HTTP request.
- Kiểm tra authentication / authorization.
- Gọi Application Service.
- Trả API response.
- Cấu hình Swagger, CORS, JWT, SignalR.
- Chứa SignalR Hub.

Controller không nên chứa logic nghiệp vụ phức tạp.

---

## 6. Anthill.Application Structure

```text
Anthill.Application/
│
├── DTOs/
│   ├── Auth/
│   ├── Students/                     # StudentDto.Groups: các lớp em đang học (StudentGroupRefDto)
│   ├── StudentGroups/
│   ├── Terms/                        # AcademicTermDto, SaveAcademicTermRequest
│   ├── MyLearning/                   # MyGroupDto, MyTermDto, TranscriptDto: việc học của chính học sinh
│   ├── Files/                        # ExportedFileDto: file do server dựng sẵn để tải về
│   ├── Accounts/                     # lịch sử tài khoản (học sinh và giáo viên dùng chung)
│   ├── Teachers/
│   ├── Imports/                      # kết quả và lựa chọn khi nhập từ Excel
│   ├── Questions/
│   ├── Exams/
│   ├── ExamAssignments/
│   ├── ExamAttempts/
│   ├── Results/
│   └── Dashboard/
│
├── Interfaces/
│   ├── Services/
│   │   ├── IAuthService.cs
│   │   ├── IStudentService.cs
│   │   ├── IStudentGroupService.cs
│   │   ├── ITeacherService.cs
│   │   ├── ITermService.cs               # kỳ học: danh sách, thêm, sửa, xoá (chỉ admin)
│   │   ├── IMyLearningService.cs         # lớp học, kỳ học và bảng điểm của học sinh đang đăng nhập
│   │   ├── IImportServices.cs        # IStudentImportService, ITeacherImportService, IGroupImportService
│   │   ├── IQuestionService.cs
│   │   ├── IExamService.cs
│   │   ├── IExamGeneratorService.cs
│   │   ├── IExamAssignmentService.cs
│   │   ├── IExamAttemptService.cs
│   │   ├── IResultService.cs
│   │   ├── IGradeSheetService.cs         # xuất bảng điểm ra Excel: học sinh của mình, giáo viên của lớp mình dạy
│   │   ├── IDashboardService.cs
│   │   └── IFileService.cs
│   │
│   ├── Repositories/
│   │   ├── IGenericRepository.cs
│   │   ├── IUserRepository.cs
│   │   ├── IStudentGroupRepository.cs
│   │   ├── IAcademicTermRepository.cs    # kỳ học: xếp kỳ hiện tại lên đầu, đếm lớp mỗi kỳ, tên có bị trùng không
│   │   ├── IAccountChangeRepository.cs   # lịch sử học sinh và giáo viên: chỉ thêm và đọc
│   │   ├── IGroupChangeRepository.cs     # lịch sử lớp: chỉ thêm và đọc
│   │   ├── IQuestionRepository.cs
│   │   ├── IExamRepository.cs
│   │   ├── IExamAssignmentRepository.cs
│   │   ├── IExamAttemptRepository.cs
│   │   ├── IStudentAnswerRepository.cs
│   │   └── INotificationRepository.cs
│   │
│   ├── Files/
│   │   └── ISpreadsheetService.cs        # cổng đọc file nhập, dựng file mẫu và dựng bảng xuất (bảng điểm); thư viện thật nằm ở Infrastructure
│   │
│   ├── UnitOfWork/
│   │   └── IUnitOfWork.cs
│   │
│   └── Realtime/
│       └── IRealtimeNotificationService.cs
│
├── Services/
│   ├── AuthService.cs
│   ├── StudentService.cs
│   ├── StudentGroupService.cs        # một giáo viên mỗi lớp, đổi người kèm lý do, lịch sử lớp, bài đã giao cho lớp
│   ├── TeacherService.cs             # tạo / sửa / khoá / cấp lại mật khẩu giáo viên
│   ├── TermService.cs                # kỳ học: tối đa một kỳ hiện tại (tắt kỳ cũ rồi bật kỳ mới trong một transaction), không xoá kỳ còn lớp
│   ├── MyLearningService.cs          # lớp học của tôi, các kỳ em có lớp (mục "Chưa phân kỳ" id 0), bảng điểm lọc theo kỳ
│   ├── StudentImportService.cs       # nhập học sinh từ Excel: mã là khoá nhận diện, cùng luật vào lớp với xếp tay
│   ├── TeacherImportService.cs
│   ├── GroupImportService.cs
│   ├── SchoolImportService.cs        # nhập tổng hợp từ MỘT file: chia dòng rồi chạy ba bộ nhập trên trong một transaction (nhập thử = chạy thật rồi rollback)
│   ├── QuestionService.cs
│   ├── ExamService.cs
│   ├── ExamGeneratorService.cs
│   ├── ExamAssignmentService.cs
│   ├── ExamAttemptService.cs
│   ├── ResultService.cs
│   ├── GradeSheetService.cs          # dựng bảng điểm: điểm quy thang 10, ô trống không tính trung bình, học sinh đi qua cổng xem điểm
│   └── DashboardService.cs
│
├── Common/
│   ├── ApiResponse.cs
│   ├── Result.cs
│   ├── PagedResult.cs
│   ├── PaginationParams.cs
│   ├── SchoolImportPlanner.cs        # thuần logic: từ các dòng "học sinh + lớp + chủ nhiệm" dựng danh sách giáo viên, lớp, học sinh; bắt xung đột
│   ├── ImportText.cs                 # chuẩn hoá chuỗi từ Excel: NFC, gộp khoảng trắng, bỏ dấu để so khớp, số điện thoại mất số 0
│   ├── ImportIssueCollector.cs       # gom dòng lỗi: số dòng hỏng đếm đủ, danh sách trả về cắt ở 500
│   └── ImportLimits.cs               # trần 5 MB và câu từ chối chung cho file quá lớn
│
├── Validators/
│   ├── ReasonRules.cs                # một bộ luật lý do (10–512 ký tự) cho khoá, xoá, gỡ, chuyển lớp và đổi giáo viên
│   ├── SaveAcademicTermRequestValidator.cs   # tên kỳ bắt buộc ≤ 100 ký tự, ngày kết thúc không trước ngày bắt đầu
│   ├── CreateTeacherRequestValidator.cs
│   ├── ChangeTeacherRequestValidator.cs
│   ├── CreateQuestionRequestValidator.cs
│   ├── CreateExamRequestValidator.cs
│   └── SubmitExamRequestValidator.cs
│
└── Mappings/
    └── MappingProfile.cs
```

Vai trò của `Anthill.Application`:

- Chứa DTO request/response.
- Chứa interface service.
- Chứa interface repository.
- Chứa interface UnitOfWork.
- Chứa logic nghiệp vụ chính.
- Validate dữ liệu nghiệp vụ.

---

## 7. Anthill.Domain Structure

```text
Anthill.Domain/
│
├── Entities/
│   ├── User.cs
│   ├── StudentGroup.cs               # cờ IsLocked / LockedAt: lớp khoá là lớp chỉ đọc; AcademicTermId: kỳ học (tuỳ chọn)
│   ├── AcademicTerm.cs               # kỳ học: tên, ngày (không bắt buộc), cờ kỳ hiện tại (chỉ mục duy nhất có điều kiện: tối đa một)
│   ├── StudentGroupMember.cs         # học sinh ↔ lớp, nhiều-nhiều: một em học nhiều lớp cùng lúc
│   ├── AccountChange.cs              # lịch sử khoá / xoá / xếp / gỡ / chuyển lớp của học sinh và khoá của giáo viên, kèm lý do; chỉ thêm
│   ├── GroupChange.cs                # lịch sử lớp: lập lớp, đổi giáo viên, khoá và mở khoá kèm lý do; chỉ thêm
│   ├── Question.cs
│   ├── AnswerOption.cs
│   ├── Exam.cs
│   ├── ExamRule.cs
│   ├── ExamAssignment.cs
│   ├── ExamAttempt.cs
│   ├── ExamAttemptQuestion.cs
│   ├── StudentAnswer.cs
│   └── Notification.cs
│
├── Enums/
│   ├── UserRole.cs
│   ├── UserStatus.cs
│   ├── QuestionDifficulty.cs
│   ├── QuestionStatus.cs
│   ├── AccountChangeKind.cs
│   ├── GroupChangeKind.cs
│   ├── ExamStatus.cs
│   ├── ExamAssignmentStatus.cs
│   ├── ExamAttemptStatus.cs
│   └── NotificationType.cs
│
├── Constants/
│   └── SystemRoles.cs
│
└── Common/
    └── BaseEntity.cs
```

Vai trò của `Anthill.Domain`:

- Chứa entity chính.
- Chứa enum nghiệp vụ.
- Không phụ thuộc EF Core trực tiếp.
- Không chứa logic HTTP, database hoặc framework.

---

## 8. Anthill.Infrastructure Structure

```text
Anthill.Infrastructure/
│
├── Persistence/
│   ├── AppDbContext.cs
│   ├── Configurations/
│   │   ├── UserConfiguration.cs
│   │   ├── AcademicTermConfiguration.cs   # tên duy nhất, chỉ mục duy nhất IsCurrent = TRUE, khoá ngoại từ lớp là Restrict
│   │   ├── StudentGroupConfiguration.cs   # cả bảng nối StudentGroupMember (khoá chính ghép, chỉ mục theo học sinh)
│   │   ├── AccountChangeConfiguration.cs
│   │   ├── GroupChangeConfiguration.cs
│   │   ├── QuestionConfiguration.cs
│   │   ├── AnswerOptionConfiguration.cs
│   │   ├── ExamConfiguration.cs
│   │   ├── ExamRuleConfiguration.cs
│   │   ├── ExamAssignmentConfiguration.cs
│   │   ├── ExamAttemptConfiguration.cs
│   │   ├── StudentAnswerConfiguration.cs
│   │   └── NotificationConfiguration.cs
│   │
│   ├── Migrations/
│   └── Seed/
│       └── DbSeeder.cs
│
├── Repositories/
│   ├── GenericRepository.cs
│   ├── UserRepository.cs
│   ├── StudentGroupRepository.cs
│   ├── AcademicTermRepository.cs
│   ├── AccountChangeRepository.cs
│   ├── GroupChangeRepository.cs
│   ├── QuestionRepository.cs
│   ├── ExamRepository.cs
│   ├── ExamAssignmentRepository.cs
│   ├── ExamAttemptRepository.cs
│   ├── StudentAnswerRepository.cs
│   └── NotificationRepository.cs
│
├── UnitOfWork/
│   └── UnitOfWork.cs
│
├── Realtime/
│   └── SignalRNotificationService.cs
│
├── Authentication/
│   ├── JwtTokenService.cs
│   └── PasswordHashService.cs
│
├── FileStorage/
│   ├── LocalFileStorageService.cs
│   └── FileStorageOptions.cs
│
├── Spreadsheets/
│   └── ClosedXmlSpreadsheetService.cs   # chặn bom zip, không tính công thức, dò hàng tiêu đề, dựng file mẫu có ô chọn
│
├── Services/
│   └── DateTimeService.cs
│
└── DependencyInjection.cs
```

Vai trò của `Anthill.Infrastructure`:

- Kết nối database bằng EF Core.
- Implement repository.
- Implement UnitOfWork.
- Implement SignalR notification service.
- Implement JWT service.
- Implement password hashing.
- Implement upload/lưu file hình ảnh.

---

## 9. Core Entities

### User

```text
User
- Id
- FullName
- Email
- Username
- PasswordHash
- Role
- Status
- CreatedAt
- UpdatedAt
```

### StudentGroup

```text
StudentGroup
- Id
- Name
- Description
- TeacherId
- CreatedAt
- UpdatedAt
```

### Question

```text
Question
- Id
- TeacherId
- Title            (tên ngắn, không bắt buộc, tối đa 200 ký tự)
- Content
- ImageUrl
- Topic
- Difficulty
- DefaultPoint
- AllowMultiple    (câu nhiều đáp án đúng; do giáo viên chọn rõ, không suy từ số đáp án đúng)
- Status
- CreatedAt
- UpdatedAt
```

### AnswerOption

```text
AnswerOption
- Id
- QuestionId
- Content
- ImageUrl
- IsCorrect
- DisplayOrder
```

### Exam

```text
Exam
- Id
- TeacherId
- Title
- Description
- DurationMinutes
- TotalQuestions
- Status
- ShuffleQuestions
- ShuffleAnswers
- ShowResultAfterSubmit
- OpenAt
- CloseAt
- CreatedAt
- UpdatedAt
```

### ExamRule

```text
ExamRule
- Id
- ExamId
- EasyPercentage
- MediumPercentage
- HardPercentage
- TopicFilter
- PointEasy
- PointMedium
- PointHard
```

### ExamAssignment

```text
ExamAssignment
- Id
- ExamId
- StudentId
- AssignedByTeacherId
- Status
- AssignedAt
- DueAt
```

### ExamAttempt

```text
ExamAttempt
- Id
- ExamAssignmentId
- StudentId
- StartedAt
- SubmittedAt
- Status
- Score
- CorrectCount
- TotalQuestions
```

### ExamAttemptQuestion

```text
ExamAttemptQuestion
- Id
- ExamAttemptId
- QuestionId
- DisplayOrder
- Point
```

### StudentAnswer

```text
StudentAnswer
- Id
- AttemptId
- ExamPaperQuestionId
- SelectedAnswerIds   (integer[]: các đáp án đã chọn; rỗng là bỏ trống)
```

Đúng/sai không lưu trên `StudentAnswer`: `AttemptGrading.IsCorrect` so tập đã chọn với tập đáp án đúng của bản đề chụp (`ExamPaperQuestion` + `ExamPaperAnswer`). Câu một đáp án và câu nhiều đáp án dùng chung một quy tắc — tập chọn phải bằng đúng tập đáp án đúng, không có điểm từng phần.

### Notification

```text
Notification
- Id
- ReceiverId
- Title
- Content
- Type
- IsRead
- CreatedAt
```

---

## 10. UnitOfWork Design

Interface đặt ở `Anthill.Application`:

```text
Anthill.Application/Interfaces/UnitOfWork/IUnitOfWork.cs
```

Implementation đặt ở `Anthill.Infrastructure`:

```text
Anthill.Infrastructure/UnitOfWork/UnitOfWork.cs
```

UnitOfWork quản lý các repository:

```text
Users
StudentGroups
Questions
Exams
ExamAssignments
ExamAttempts
StudentAnswers
Notifications
SaveChangesAsync()
BeginTransactionAsync()
CommitTransactionAsync()
RollbackTransactionAsync()
```

UnitOfWork cần dùng khi một nghiệp vụ phải lưu nhiều bảng cùng lúc.

Ví dụ Teacher giao bài kiểm tra:

```text
Tạo ExamAssignment cho Student
+ Tạo ExamAttempt nếu muốn generate sẵn
+ Tạo ExamAttemptQuestion
+ Tạo Notification
→ SaveChangesAsync một lần
```

Ví dụ Student nộp bài:

```text
Lưu StudentAnswer
+ Tính score
+ Cập nhật ExamAttempt
+ Cập nhật ExamAssignment status
+ Tạo Notification cho Teacher
→ SaveChangesAsync một lần
```

---

## 11. Exam Generation Logic

Khi Teacher tạo bài kiểm tra, hệ thống lưu `Exam` và `ExamRule`.

Khi giao bài cho Student hoặc khi Student bắt đầu làm bài, hệ thống sẽ sinh danh sách câu hỏi dựa trên rule.

Ví dụ:

```text
TotalQuestions = 20
EasyPercentage = 30
MediumPercentage = 50
HardPercentage = 20
```

Hệ thống tính ra:

```text
Easy = 6 câu
Medium = 10 câu
Hard = 4 câu
```

Sau đó:

```text
Lấy câu hỏi từ Question Bank của Teacher
→ Lọc theo topic nếu có
→ Lọc theo difficulty
→ Random câu hỏi theo số lượng cần lấy
→ Tạo ExamAttemptQuestion cho từng Student
```

Validation bắt buộc:

- Tổng phần trăm Easy + Medium + Hard phải bằng 100%.
- Số lượng câu hỏi trong ngân hàng phải đủ theo từng difficulty.
- Exam phải có ít nhất 1 câu hỏi.
- Student chỉ được làm bài nếu có ExamAssignment hợp lệ.

---

## 12. Realtime with SignalR

Backend dùng SignalR, không dùng Socket.IO.

Hub đặt tại:

```text
Anthill.Api/Hubs/NotificationHub.cs
```

Realtime service interface đặt tại:

```text
Anthill.Application/Interfaces/Realtime/IRealtimeNotificationService.cs
```

Implementation đặt tại:

```text
Anthill.Infrastructure/Realtime/SignalRNotificationService.cs
```

Realtime dùng cho:

- Gửi thông báo bài kiểm tra mới cho Student.
- Gửi thông báo Student đã nộp bài cho Teacher.
- Cập nhật trạng thái bài làm realtime.
- Gửi thông báo bài kiểm tra gần hết hạn.

Không dùng SignalR để xử lý nghiệp vụ chính. Nghiệp vụ chính vẫn xử lý bằng REST API.

---

## 13. Suggested API Groups

### Auth

```text
POST /api/auth/login
GET  /api/auth/me
POST /api/auth/logout
```

### Students

```text
GET    /api/students
POST   /api/students
GET    /api/students/{id}
PUT    /api/students/{id}
PATCH  /api/students/{id}/status
```

### Student Groups

```text
GET    /api/student-groups
POST   /api/student-groups
PUT    /api/student-groups/{id}
DELETE /api/student-groups/{id}
POST   /api/student-groups/{id}/students
DELETE /api/student-groups/{id}/students/{studentId}
```

### Questions

```text
GET    /api/questions
POST   /api/questions
GET    /api/questions/{id}
PUT    /api/questions/{id}
DELETE /api/questions/{id}
POST   /api/questions/{id}/image
```

### Exams

```text
GET    /api/exams
POST   /api/exams
GET    /api/exams/{id}
PUT    /api/exams/{id}
DELETE /api/exams/{id}
POST   /api/exams/{id}/validate-rule
POST   /api/exams/{id}/assign
```

### Exam Taking

```text
GET  /api/student/exams
POST /api/exam-attempts/{assignmentId}/start
GET  /api/exam-attempts/{attemptId}
POST /api/exam-attempts/{attemptId}/submit
```

### Results

```text
GET /api/results/exams/{examId}
GET /api/results/students/{studentId}
GET /api/results/attempts/{attemptId}
```

### Dashboard

```text
GET /api/dashboard/teacher
GET /api/dashboard/student
```

---

## 14. Development Stages

### Stage 1: Base Setup

- Setup ASP.NET Core Web API.
- Setup Clean Architecture projects.
- Setup EF Core.
- Setup Swagger.
- Setup JWT.
- Setup global exception middleware.

### Stage 2: Auth & Student Management

- Login.
- Role-based authorization.
- Teacher tạo và quản lý Student.
- StudentGroup.

### Stage 3: Question Bank

- CRUD câu hỏi.
- CRUD đáp án.
- Upload hình ảnh cho câu hỏi/đáp án.
- Filter theo topic, difficulty, status.

### Stage 4: Exam Blueprint

- Teacher tạo Exam.
- Teacher cấu hình tỷ lệ Easy / Medium / Hard.
- Validate ngân hàng câu hỏi đủ số lượng.
- Preview số lượng câu hỏi khả dụng.

### Stage 5: Exam Assignment & Generation

- Teacher giao bài cho lớp; bài chỉ giao theo lớp, không giao riêng từng Student.
- Hệ thống sinh câu hỏi cho từng Student.
- Lưu ExamAttemptQuestion.
- Gửi notification realtime cho Student.

### Stage 6: Exam Taking & Auto Grading

- Student bắt đầu làm bài.
- Student nộp bài.
- Backend tự chấm điểm.
- Lưu StudentAnswer.
- Gửi notification realtime cho Teacher.

### Stage 7: Result & Dashboard

- Teacher xem kết quả bài kiểm tra.
- Student xem kết quả.
- Dashboard thống kê.
- Filter và pagination.
