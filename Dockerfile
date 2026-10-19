# Anthill API — image cho Render (và bất kỳ nơi nào chạy được Docker).
#
# Hai giai đoạn: build trên SDK, chạy trên runtime Alpine nhỏ. Alpine + ReadyToRun là để khởi động nhanh sau
# khi Render đánh thức dịch vụ ngủ (cold start là kẻ thù của gói miễn phí, xem docs/Anthill_Workflow_Deploy.md).

# ---- build ---------------------------------------------------------------------------------------
FROM mcr.microsoft.com/dotnet/sdk:10.0-alpine AS build
WORKDIR /src

# Chép riêng các file .csproj trước để lớp restore được cache: sửa code không phải tải lại gói.
COPY Anthill.Backend.sln ./
COPY src/Anthill.Domain/Anthill.Domain.csproj src/Anthill.Domain/
COPY src/Anthill.Application/Anthill.Application.csproj src/Anthill.Application/
COPY src/Anthill.Infrastructure/Anthill.Infrastructure.csproj src/Anthill.Infrastructure/
COPY src/Anthill.Api/Anthill.Api.csproj src/Anthill.Api/
# PublishReadyToRun phải có mặt ngay ở bước restore: gói biên dịch ReadyToRun chỉ được tải khi restore biết trước
# rằng sẽ publish kiểu này, nếu không `publish --no-restore` dừng với lỗi NETSDK1094.
RUN dotnet restore src/Anthill.Api/Anthill.Api.csproj -r linux-musl-x64 -p:PublishReadyToRun=true

COPY src ./src
RUN dotnet publish src/Anthill.Api/Anthill.Api.csproj \
    -c Release \
    -r linux-musl-x64 --self-contained false \
    -p:PublishReadyToRun=true \
    --no-restore \
    -o /app

# ---- chạy ----------------------------------------------------------------------------------------
FROM mcr.microsoft.com/dotnet/aspnet:10.0-alpine AS final
WORKDIR /app

# ICU để so sánh/sắp xếp chuỗi tiếng Việt đúng (chế độ invariant của Alpine sẽ so từng byte), tzdata cho múi giờ.
RUN apk add --no-cache icu-libs tzdata

ENV DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=false \
    ASPNETCORE_ENVIRONMENT=Production \
    # Render đặt TLS ở reverse proxy: bật đọc X-Forwarded-* để API thấy đúng IP khách (cần cho giới hạn đăng nhập
    # theo IP) và đúng giao thức.
    ASPNETCORE_FORWARDEDHEADERS_ENABLED=true

COPY --from=build /app ./

# Chạy bằng người dùng không phải root (image aspnet có sẵn tài khoản `app`).
USER app

# Render truyền cổng qua biến PORT; chạy cục bộ thì rơi về 8080.
EXPOSE 8080
ENTRYPOINT ["sh", "-c", "exec dotnet Anthill.Api.dll --urls http://0.0.0.0:${PORT:-8080}"]
