# Anthill — Workflow Phát Triển & Chiến Lược Deploy

Tài liệu này ghi lại quy trình làm việc đã chốt và phương án deploy tối ưu cho trải nghiệm người dùng, với ràng buộc **chi phí = 0đ**.

Anthill là một bản demo chạy thật, mở cho người xem dùng thử qua một đường link. Vì vậy hai tiêu chí quan trọng nhất là:

1. Người xem mở link là dùng được ngay, không chờ, không lỗi.
2. Code và kiến trúc đọc vào thấy chuyên nghiệp.

---

## 1. Workflow Tổng Thể

Thứ tự thực hiện: **UI trước → chốt API → làm BE → tích hợp → deploy**.

```text
GĐ1: Hoàn thiện UI/UX (mock data)
  → GĐ2: Chốt API Contract
    → GĐ3: Build Backend
      → GĐ4: Tích hợp FE ↔ BE
        → GĐ5: Deploy + tối ưu trải nghiệm
```

### Giai đoạn 1 — Hoàn thiện UI/UX với mock data

Làm đầy đủ toàn bộ page trong `FRONTEND_INVENTORY.md`, chạy bằng mock. Xong giai đoạn này mới biết chính xác cần những API nào.

**Ba việc phải làm ngay từ giai đoạn này** để giai đoạn 4 không phải viết lại:

- **Tách tầng service.** Không gọi mock trực tiếp trong page. Mỗi domain một file `src/services/*.ts` (`questionService`, `examService`, `resultService`...). Hiện tại thân hàm trả mock, sau này chỉ đổi ruột sang gọi API, **giữ nguyên signature** → page không phải sửa một dòng nào.

- **Mock phải có delay giả lập.** Cho mỗi hàm mock `await sleep(300~800ms)` và thỉnh thoảng random lỗi. Đây là điểm quan trọng nhất của giai đoạn này: nếu mock trả về đồng bộ tức thì, mọi trạng thái `loading` / `empty` / `error` sẽ bị bỏ quên, và khi gắn API thật sẽ lòi ra hàng loạt chỗ màn hình trắng hoặc nhảy layout. Có delay từ đầu thì loading state được thiết kế tự nhiên ngay trong lúc làm UI.

- **Định nghĩa type DTO tập trung** ở `src/types/`. Bộ type này chính là nguyên liệu để chốt API contract ở giai đoạn 2.

### Giai đoạn 2 — Chốt API Contract

Từ các page đã xong, liệt kê ra file `Anthill_API_Contract.md`:

| Cần ghi | Chi tiết |
|---|---|
| Endpoint | Method + path, ví dụ `GET /api/questions` |
| Page tiêu thụ | Page nào gọi, để biết độ ưu tiên |
| Query / Body | Kèm phân trang, filter, sort |
| Response shape | Khớp với type ở `src/types/` |
| Quyền | Teacher / Student / cả hai |
| Mã lỗi | 400 / 401 / 403 / 404 + format lỗi thống nhất |

Chốt luôn một số quy ước dùng chung: format response bọc ngoài (`{ data, meta }` hay trả thẳng), format lỗi, kiểu phân trang (page/pageSize), định dạng ngày (ISO 8601, UTC).

### Giai đoạn 3 — Build Backend

Theo `Anthill_BE_Tech_Structure.md`. Làm theo thứ tự ưu tiên của contract: Auth → Question Bank → Exam → Assign → Take Exam → Result → SignalR.

> **Quyết định đã chốt: dùng PostgreSQL, không dùng SQL Server.**
> Lý do: hosting SQL Server miễn phí gần như không có (trừ Azure), còn PostgreSQL thì nhiều lựa chọn free tốt. Provider EF Core dùng `Npgsql.EntityFrameworkCore.PostgreSQL`. Phải chốt trước khi viết migration đầu tiên, đổi về sau rất tốn công.

### Giai đoạn 4 — Tích hợp FE ↔ BE

Nhờ tầng service ở GĐ1, đây chỉ là thay ruột hàm. Thêm biến môi trường:

```ts
// src/lib/apiClient.ts
const BASE_URL = import.meta.env.VITE_API_BASE_URL
```

Giữ cờ `VITE_USE_MOCK` để vẫn chạy được offline khi BE sập hoặc khi demo local.

### Giai đoạn 5 — Deploy

Xem mục 2 và 3.

---

## 2. Hạ Tầng Deploy (chốt phương án miễn phí)

| Phần | Dịch vụ | Ghi chú |
|---|---|---|
| Frontend | **Vercel** | Static SPA, CDN toàn cầu, deploy tự động từ Git |
| Backend | **Render** (Docker, .NET) | Free tier, **có WebSocket** → SignalR chạy thật |
| Database | **Neon** (PostgreSQL) | Free vĩnh viễn, không bị xoá/pause vĩnh viễn |
| Ảnh câu hỏi | **Cloudinary** free | Bắt buộc, xem mục "bẫy" |
| Region | **Singapore** cho tất cả | Latency thấp nhất từ VN |

**Vì sao Neon chứ không phải Supabase:** Supabase free pause project sau ~7 ngày không hoạt động. Người xem có thể mở link sau 2–3 tuần → vào thấy lỗi. Neon chỉ auto-suspend compute và tự thức lại trong khoảng 500ms.

**Azure để dành cho project sau.** Azure App Service F1 + Azure SQL free tier hợp với hệ sinh thái .NET, nhưng F1 giới hạn 60 phút CPU/ngày và **tắt WebSocket** (SignalR phải fallback sang long polling) → không phù hợp với mục tiêu "mượt" của project này.

**Không dùng Railway / Fly.io:** Railway đã bỏ free tier thật (chỉ còn credit dùng một lần), Fly.io cũng đã siết lại. Đừng xây kế hoạch trên đó.

> Free tier của các dịch vụ này thay đổi liên tục. Trước khi bắt tay làm, mở lại trang pricing xác nhận một lượt.

---

## 3. Chiến Lược Tối Ưu Trải Nghiệm (chống cold start)

Đây là phần quan trọng nhất. Kẻ thù duy nhất của phương án free là **cold start**: Render free ngủ sau 15 phút không có request, đánh thức mất **30–60 giây**. Nếu người xem mở link đúng lúc server ngủ, họ sẽ nghĩ app hỏng và đóng tab.

Xử lý theo **4 tầng, làm đủ cả 4**:

### Tầng 1 — Giữ server luôn thức (quan trọng nhất)

Ping định kỳ endpoint `/health` để container không bao giờ ngủ.

- **UptimeRobot** gọi `/health` mỗi 5 phút — miễn phí, không chiếm tài nguyên của repo, và báo luôn khi service ngừng trả lời.
- **cron-job.org** làm lớp dự phòng, cũng 5 phút một lần.
- **Không dùng GitHub Actions cron cho việc này** khi repo để private: gói Free chỉ có 2.000 phút mỗi tháng và mỗi lần chạy bị làm tròn lên 1 phút, nên ping 10 phút một lần (khoảng 4.300 lần/tháng) vượt hạn mức. Cron của Actions cũng không chạy đúng giờ (có thể trễ 5–15 phút khi runner bận).
- **`/health` phải chạm database** (một câu `SELECT 1`) → giữ ấm cả backend lẫn Neon cùng lúc.
- Quota Render free là 750 giờ/tháng, một tháng có 720–744 giờ → chạy 24/7 một service vẫn nằm trong hạn mức. Chỉ đủ cho **một** service, đừng tạo thêm.

### Tầng 2 — Rút ngắn thời gian boot

Để cả khi lỡ bị ngủ thì thức dậy cũng nhanh:

- Dockerfile multi-stage, runtime image `mcr.microsoft.com/dotnet/aspnet:10.0-alpine`.
- Bật `PublishReadyToRun=true` (biên dịch sẵn, bỏ bớt JIT lúc khởi động).
- Bật `InvariantGlobalization=true` nếu không cần culture-specific.
- **Không chạy `Database.Migrate()` trong `Program.cs`.** Tách migration thành bước riêng của pipeline deploy — chạy lúc startup vừa làm chậm boot vừa dễ hỏng.
- Dùng EF Core compiled model (`dotnet ef dbcontext optimize`) để bỏ chi phí build model lúc chạy request đầu.

### Tầng 3 — Warm-up từ phía Frontend

Vercel trả FE từ CDN trong ~100ms, luôn nhanh bất kể BE. Tận dụng khoảng thời gian đó:

- Ngay khi app khởi động (kể cả ở trang đăng nhập), bắn `fetch(API_BASE + '/health')` kiểu fire-and-forget, không chờ kết quả.
- Người dùng mất 5–10 giây để đọc trang và gõ thông tin đăng nhập — đủ để BE thức dậy trước khi họ bấm nút. Cold start bị che hoàn toàn.

### Tầng 4 — Che độ trễ bằng UX

- **Skeleton screen** thay vì spinner quay tròn. Spinner khiến 2 giây có cảm giác như 5 giây.
- **Optimistic UI** cho các thao tác ghi (tạo/sửa câu hỏi): cập nhật giao diện trước, rollback nếu API lỗi.
- **Prefetch** dữ liệu trang kế khi người dùng hover vào menu.
- Banner "Đang khởi động máy chủ, vui lòng đợi..." chỉ hiện khi request vượt 3 giây — thà nói thật còn hơn để màn hình đứng im.

### Phương án nâng cấp (nếu muốn tuyệt đối không cold start)

**Oracle Cloud Always Free**: VM ARM Ampere (tối đa 4 OCPU / 24GB RAM) miễn phí vĩnh viễn, chạy 24/7, **không hề ngủ** → không cần ping, không cold start. Chạy được cả .NET lẫn PostgreSQL trên cùng một máy.

Đánh đổi: phải tự quản VM (Docker Compose, Nginx reverse proxy, Let's Encrypt), khâu duyệt tài khoản đôi khi khó, và region Singapore hay báo hết capacity cho ARM. Bù lại, tự dựng được thì phần "Deployment" trong CV rất đẹp.

Khuyến nghị: **làm Render trước cho có demo chạy sớm**, khi rảnh thì dựng Oracle song song rồi trỏ domain sang.

---

## 4. Checklist Các Bẫy Phải Tránh

- [ ] **Disk là ephemeral.** Render/Azure free xoá sạch file khi restart. Ảnh câu hỏi lưu vào `wwwroot/uploads` sẽ **mất hết** sau mỗi lần deploy. Bắt buộc đẩy lên Cloudinary/R2, DB chỉ lưu URL. Thiết kế ngay từ đầu, đừng để sau.
- [ ] **Connection pooling với Neon.** EF Core mặc định mở pool lớn, Neon free giới hạn connection. Dùng **pooled connection string** (endpoint có `-pooler`) và set `Maximum Pool Size=10`.
- [ ] **CORS cross-domain.** FE ở `*.vercel.app`, BE ở `*.onrender.com` là hai domain khác nhau. JWT để trong header thì ổn. Nếu dùng refresh token qua cookie phải set `SameSite=None; Secure`.
- [ ] **SignalR cần `AllowCredentials()`** + khai báo origin cụ thể, **không dùng được `AllowAnyOrigin()`** chung với credentials.
- [ ] **Timezone.** Lưu UTC trong DB, quy đổi sang giờ VN ở tầng hiển thị. Bài kiểm tra có deadline nên sai timezone là sai nghiệp vụ.
- [ ] **Secrets.** Connection string, JWT key, Cloudinary key đều qua biến môi trường. Không commit `appsettings.Production.json`.
- [ ] **Preview deployment của Vercel** sinh domain mới mỗi lần push → nhớ cấu hình CORS cho phép pattern preview, hoặc chỉ test tích hợp trên domain production.

---

## 5. Trạng Thái Hiện Tại

- FE: đã nối API thật, không còn tầng mock.
- BE: **đã xong** — đủ luồng từ lập lớp tới làm bài, chấm ở server, kết quả và thông báo thời gian thực, có unit test và bộ kiểm HTTP chạy trên API thật. Chưa deploy.
- Deploy: chưa bắt đầu, phương án đã chốt ở mục 2.

**Việc tiếp theo:** deploy theo mục 2 và 3 (Vercel + Render + Neon), kèm các việc ở checklist mục 4 — trước hết là chuyển ảnh câu hỏi khỏi đĩa của máy chạy API.
