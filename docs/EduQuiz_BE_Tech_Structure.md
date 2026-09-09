# EduQuiz Backend Technology & Structure

## 1. Backend Context

Backend của EduQuiz phục vụ hệ thống tạo và quản lý bài kiểm tra trực tuyến. Backend tập trung vào các nghiệp vụ chính:

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
EduQuiz.Backend/
│
├── EduQuiz.Backend.sln
│
├── src/
│   ├── EduQuiz.Api/
│   │   ├── Controllers/
│   │   ├── Hubs/
│   │   ├── Middlewares/
│   │   ├── Extensions/
│   │   ├── Program.cs
│   │   ├── appsettings.json
│   │   └── appsettings.Development.json
│   │
│   ├── EduQuiz.Application/
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
│   ├── EduQuiz.Domain/
│   │   ├── Entities/
│   │   ├── Enums/
│   │   ├── Constants/
│   │   └── Common/
│   │
│   └── EduQuiz.Infrastructure/
│       ├── Persistence/
│       ├── Repositories/
│       ├── UnitOfWork/
│       ├── Realtime/
│       ├── Authentication/
│       ├── FileStorage/
│       ├── Services/
│       └── DependencyInjection.cs
│
└── tests/
    └── EduQuiz.Tests/
```

---

## 5. EduQuiz.Api Structure

```text
EduQuiz.Api/
│
├── Controllers/
│   ├── AuthController.cs
│   ├── StudentsController.cs
│   ├── StudentGroupsController.cs
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

Vai trò của `EduQuiz.Api`:

- Nhận HTTP request.
- Kiểm tra authentication / authorization.
- Gọi Application Service.
- Trả API response.
- Cấu hình Swagger, CORS, JWT, SignalR.
- Chứa SignalR Hub.

Controller không nên chứa logic nghiệp vụ phức tạp.

---

## 6. EduQuiz.Application Structure

```text
EduQuiz.Application/
│
├── DTOs/
│   ├── Auth/
│   ├── Students/
│   ├── StudentGroups/
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
│   │   ├── IQuestionService.cs
│   │   ├── IExamService.cs
│   │   ├── IExamGeneratorService.cs
│   │   ├── IExamAssignmentService.cs
│   │   ├── IExamAttemptService.cs
│   │   ├── IResultService.cs
│   │   ├── IDashboardService.cs
│   │   └── IFileService.cs
│   │
│   ├── Repositories/
│   │   ├── IGenericRepository.cs
│   │   ├── IUserRepository.cs
│   │   ├── IStudentGroupRepository.cs
│   │   ├── IQuestionRepository.cs
│   │   ├── IExamRepository.cs
│   │   ├── IExamAssignmentRepository.cs
│   │   ├── IExamAttemptRepository.cs
│   │   ├── IStudentAnswerRepository.cs
│   │   └── INotificationRepository.cs
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
│   ├── StudentGroupService.cs
│   ├── QuestionService.cs
│   ├── ExamService.cs
│   ├── ExamGeneratorService.cs
│   ├── ExamAssignmentService.cs
│   ├── ExamAttemptService.cs
│   ├── ResultService.cs
│   └── DashboardService.cs
│
├── Common/
│   ├── ApiResponse.cs
│   ├── Result.cs
│   ├── PagedResult.cs
│   └── PaginationParams.cs
│
├── Validators/
│   ├── CreateQuestionRequestValidator.cs
│   ├── CreateExamRequestValidator.cs
│   └── SubmitExamRequestValidator.cs
│
└── Mappings/
    └── MappingProfile.cs
```

Vai trò của `EduQuiz.Application`:

- Chứa DTO request/response.
- Chứa interface service.
- Chứa interface repository.
- Chứa interface UnitOfWork.
- Chứa logic nghiệp vụ chính.
- Validate dữ liệu nghiệp vụ.

---

## 7. EduQuiz.Domain Structure

```text
EduQuiz.Domain/
│
├── Entities/
│   ├── User.cs
│   ├── StudentGroup.cs
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

Vai trò của `EduQuiz.Domain`:

- Chứa entity chính.
- Chứa enum nghiệp vụ.
- Không phụ thuộc EF Core trực tiếp.
- Không chứa logic HTTP, database hoặc framework.

---

## 8. EduQuiz.Infrastructure Structure

```text
EduQuiz.Infrastructure/
│
├── Persistence/
│   ├── AppDbContext.cs
│   ├── Configurations/
│   │   ├── UserConfiguration.cs
│   │   ├── StudentGroupConfiguration.cs
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
├── Services/
│   └── DateTimeService.cs
│
└── DependencyInjection.cs
```

Vai trò của `EduQuiz.Infrastructure`:

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
- Content
- ImageUrl
- Topic
- Difficulty
- DefaultPoint
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
- PointPerQuestion
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
- ExamAttemptQuestionId
- SelectedAnswerOptionId
- IsCorrect
```

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

Interface đặt ở `EduQuiz.Application`:

```text
EduQuiz.Application/Interfaces/UnitOfWork/IUnitOfWork.cs
```

Implementation đặt ở `EduQuiz.Infrastructure`:

```text
EduQuiz.Infrastructure/UnitOfWork/UnitOfWork.cs
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
EduQuiz.Api/Hubs/NotificationHub.cs
```

Realtime service interface đặt tại:

```text
EduQuiz.Application/Interfaces/Realtime/IRealtimeNotificationService.cs
```

Implementation đặt tại:

```text
EduQuiz.Infrastructure/Realtime/SignalRNotificationService.cs
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

- Teacher giao bài cho Student/Group.
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
