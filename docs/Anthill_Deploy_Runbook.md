# Anthill — sổ tay deploy (Vercel + Render + Neon + Cloudinary)

Kế hoạch và lý do chọn từng dịch vụ nằm ở `Anthill_Workflow_Deploy.md`. File này là **các bước làm thật**, theo
đúng thứ tự, cho người chưa từng deploy bản này. Toàn bộ chạy được trên gói miễn phí.

Phần code đã sẵn: `Dockerfile` và `render.yaml` ở gốc repo BE, nơi cất ảnh Cloudinary, `/health`, và `vercel.json`
ở repo FE. Việc còn lại là tạo tài khoản và dán các giá trị bên dưới vào đúng chỗ.

## 0. Cần chuẩn bị

- Tài khoản: **Neon**, **Cloudinary**, **Render**, **Vercel**, **UptimeRobot** (và **cron-job.org** làm dự phòng).
- Hai repo GitHub (BE và FE). Repo private vẫn deploy được: Render và Vercel đều có ứng dụng GitHub riêng, cấp quyền
  cho đúng hai repo là đủ.
- Máy có `psql` và `dotnet` 10 (để tạo bảng cho database một lần).

## 1. Database trên Neon

1. Tạo project, region **Singapore** (gần Việt Nam nhất), Postgres 17 trở lên.
2. Trong **Connection details**, bật **Pooled connection** và copy chuỗi dạng
   `postgresql://user:matkhau@ep-xxxx-pooler.ap-southeast-1.aws.neon.tech/neondb?sslmode=require`.
   Đây chính là giá trị của `DATABASE_URL` ở bước 3. API tự đổi sang dạng Npgsql, bật SSL và đặt
   `Maximum Pool Size=10` cho khỏi chạm trần kết nối của gói miễn phí.
3. Tạo bảng (một lần, và lặp lại mỗi khi có migration mới). Chạy từ thư mục gốc repo BE:

   ```bash
   dotnet ef migrations script --idempotent \
     --project src/Anthill.Infrastructure --startup-project src/Anthill.Api -o schema.sql
   psql "<chuỗi kết nối KHÔNG-pooler của Neon>" -f schema.sql
   ```

   Dùng chuỗi **không có `-pooler`** cho bước này (tạo bảng và tạo extension `unaccent` nên đi thẳng, không qua bộ
   gộp kết nối). Script có tính `idempotent`: chạy lại trên database đã có bảng thì chỉ áp những migration còn thiếu.
   API **không** tự chạy migration lúc khởi động: hai bản chạy cùng lúc sẽ cùng sửa lược đồ.

## 2. Ảnh câu hỏi trên Cloudinary

1. Đăng ký, vào **Dashboard** lấy ba giá trị: **Cloud name**, **API Key**, **API Secret**.
2. Không cần tạo preset hay thư mục: API tự đặt ảnh vào thư mục `anthill/` trên tài khoản, tên file là GUID.
3. Đường dẫn ảnh trong database vẫn là `/uploads/202610/<guid>.png`; API chuyển hướng (302) sang địa chỉ giao hàng
   của Cloudinary, nên đổi tài khoản Cloudinary sau này không làm hỏng ảnh cũ, miễn là ảnh được chép sang cùng thư mục.

## 3. API trên Render

1. **New → Blueprint**, chọn repo BE; Render đọc `render.yaml`.
2. Điền các biến `sync: false` khi được hỏi:

   | Biến | Giá trị |
   |---|---|
   | `DATABASE_URL` | chuỗi **pooled** của Neon (bước 1.2) |
   | `Cors__AllowedOrigins__0` | địa chỉ frontend trên Vercel, ví dụ `https://anthill.vercel.app`, **không** có dấu `/` ở cuối. Chưa biết thì tạm điền sau ở bước 5 |
   | `FileStorage__Cloudinary__CloudName` | Cloud name |
   | `FileStorage__Cloudinary__ApiKey` | API Key |
   | `FileStorage__Cloudinary__ApiSecret` | API Secret |

   `Jwt__SecretKey` Render tự sinh. `FileStorage__Provider=Cloudinary` và `Seed__DemoData=true` đã nằm sẵn trong
   `render.yaml` (xem chú thích trong file).
3. Đợi build xong (lần đầu 4–6 phút) rồi mở `https://<tên>.onrender.com/health`: phải thấy `{"status":"ok", ...}`.
4. Lần chạy đầu API nạp ba tài khoản demo (`Seed__DemoData=true`; mật khẩu `123456`, công khai trong README). Muốn
   dừng việc đó thì đổi biến này thành `false`.

API từ chối khởi động nếu thiếu `Jwt__SecretKey`, thiếu `Cors__AllowedOrigins__0` ở Production, hoặc chọn Cloudinary
mà thiếu một trong ba khoá — câu lỗi trong log nói đúng biến phải đặt.

## 4. Frontend trên Vercel

1. **Add New → Project**, chọn repo FE. Framework: **Vite**. Build `npm run build`, output `dist`.
2. Biến môi trường (Production): `VITE_API_BASE_URL=https://<tên>.onrender.com` (không có `/` ở cuối).
3. `vercel.json` trong repo đã khai rewrite về `index.html` để F5 trên `/giao-vien/...` không ra 404.
4. Deploy, mở địa chỉ Vercel, đăng nhập thử bằng tài khoản demo.

## 5. Nối hai đầu

Quay lại Render, đặt `Cors__AllowedOrigins__0` đúng địa chỉ Vercel (nếu chưa làm ở bước 3) rồi **Manual Deploy**. Trình
duyệt báo lỗi CORS khi gọi API gần như luôn là do giá trị này sai chữ (thừa `/`, `http` thay vì `https`, gõ nhầm tên miền).

## 6. Chống ngủ (cold start)

Render miễn phí ngủ sau 15 phút không có request và mất 30–60 giây để dậy. Người xem mở link đúng lúc đó sẽ tưởng
trang hỏng.

- **UptimeRobot**: monitor kiểu HTTP(s), URL `https://<tên>.onrender.com/health`, chu kỳ **5 phút**.
- **cron-job.org**: cùng URL, cùng chu kỳ, làm lớp dự phòng.
- Không dùng GitHub Actions để ping: repo private chỉ có 2.000 phút/tháng.
- Gói miễn phí 750 giờ/tháng chỉ đủ cho **một** dịch vụ chạy liên tục; đừng tạo thêm.

Frontend cũng gọi `/health` ngay khi tải trang để đánh thức API sớm (`src/lib/warmup.ts`).

## 7. Kiểm tra sau deploy

- [ ] `GET /health` trả `ok` và có thông tin database.
- [ ] Đăng nhập được cả ba vai trò; học sinh bị buộc đổi mật khẩu thì đổi được.
- [ ] Tạo câu hỏi có ảnh: tải ảnh lên, ảnh hiện trong ngân hàng câu hỏi và trong bài làm.
- [ ] Giao bài cho một lớp, học sinh vào làm, nộp, giáo viên xem kết quả.
- [ ] Thông báo thời gian thực (chuông, bảng kết quả) cập nhật không cần tải lại trang.
- [ ] F5 trên một trang sâu của frontend không ra 404.
- [ ] Để ngủ hơn 15 phút rồi mở lại: UptimeRobot có báo gián đoạn không, thời gian dậy bao lâu.

## 8. Khi có chuyện

| Dấu hiệu | Nguyên nhân thường gặp |
|---|---|
| Trang trắng, console báo CORS | `Cors__AllowedOrigins__0` sai chữ so với địa chỉ thật của frontend |
| Mọi request 500 sau khi deploy | Chưa chạy `schema.sql` (database chưa có bảng) — xem log Render |
| API không lên, log nói thiếu biến | Đọc đúng tên biến trong câu lỗi; lỗi này được in ngay lúc khởi động |
| Ảnh vỡ sau deploy | `FileStorage__Provider` không phải `Cloudinary`, hoặc ảnh cũ nằm ở đĩa của lần chạy trước |
| Đăng nhập bị từ chối vì quá nhiều lần thử (429) | Giới hạn theo IP; trên Render cần `ASPNETCORE_FORWARDEDHEADERS_ENABLED=true` (đã có trong image) để API thấy IP thật của khách |
| Neon báo hết kết nối | Đang dùng chuỗi không-pooler cho API; đổi sang chuỗi `-pooler` |
