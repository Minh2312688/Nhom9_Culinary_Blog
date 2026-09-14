# Culinary Blog - TV1 Giai đoạn 1

Starter base cho Thành viên 1 theo phân công: **Clean Architecture + MediatR Pipeline + Docker Compose (PostgreSQL, Redis, MinIO)**.

## 1. Phạm vi đúng của Giai đoạn 1

Giai đoạn này chỉ dựng nền tảng để các thành viên phát triển song song. Chưa triển khai FR-AUTH-001..007 và chưa làm UI đăng nhập/đăng ký/profile.

Backend có 4 tầng:

```text
CulinaryBlog.Domain
        ^
        |
CulinaryBlog.Application
        ^
        |
CulinaryBlog.Infrastructure
        ^
        |
CulinaryBlog.API (Presentation)
```

`API` được phép reference `Application` + `Infrastructure`. `Infrastructure` reference `Application`. `Application` reference `Domain`. `Domain` không reference layer khác.

## 2. Yêu cầu máy phát triển

- .NET 10 SDK
- Docker Engine/Desktop + Docker Compose v2
- Git 2.40+

## 3. Chạy local bằng Docker

Từ thư mục gốc:

```bash
cp .env.example .env
docker compose config
docker compose up -d --build
docker compose ps
```

Windows PowerShell:

```powershell
Copy-Item .env.example .env
docker compose config
docker compose up -d --build
docker compose ps
```

API:

```text
http://localhost:5000/
http://localhost:5000/api/v1/system/base-status
```

MinIO Console:

```text
http://localhost:9001
```

## 4. Chạy backend không Docker

```bash
cd backend
dotnet restore CulinaryBlog.sln
dotnet build CulinaryBlog.sln -c Debug
dotnet run --project src/CulinaryBlog.API/CulinaryBlog.API.csproj
```

> PostgreSQL/Redis/MinIO chưa được gọi trực tiếp trong Phase 1, nên API base có thể chạy để kiểm tra Clean Architecture + MediatR trước khi các infrastructure adapter được tích hợp.

## 5. MediatR Pipeline

Đã đăng ký theo thứ tự:

1. `LoggingBehavior<TRequest,TResponse>`
2. `ValidationBehavior<TRequest,TResponse>`
3. Handler

Endpoint `/api/v1/system/base-status` đi qua MediatR để kiểm tra pipeline nền tảng.

## 6. Kiểm tra trước khi commit/merge

```bash
cd backend
dotnet restore CulinaryBlog.sln
dotnet build CulinaryBlog.sln -c Release
cd ..
docker compose config
```

Checklist:

- Build không warning/error.
- API container chạy cổng 5000.
- PostgreSQL healthy.
- Redis healthy.
- MinIO mở được cổng 9000/9001.
- `/api/v1/system/base-status` trả HTTP 200.
- Không commit `.env` hoặc secret thật.

## 7. Bước kế tiếp - Giai đoạn 2

Trên chính base này mới triển khai Auth:

- FR-AUTH-001 Register
- FR-AUTH-002 Local Login
- FR-AUTH-003 Google OAuth 2.0
- FR-AUTH-004 Refresh Token Rotation
- FR-AUTH-005 Logout
- FR-AUTH-006 View Profile
- FR-AUTH-007 Update Profile
- Rate Limiting cho `/auth/*`
- Auth UI trên Next.js base do nhóm frontend dựng

Xem thêm `docs/architecture/PHASE-1-BOUNDARIES.md` để tránh conflict khi merge.