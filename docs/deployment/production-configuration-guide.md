# HƯỚNG DẪN CẤU HÌNH MÔI TRƯỜNG PRODUCTION
**Dự án:** Culinary Blog – Hệ thống Blog Ẩm thực Trực tuyến (Nhóm 9)\
**Tài liệu:** Production Environment Configuration & Deployment Guide\
**Phiên bản:** v1.0.0 (Week 8)\
**Người thực hiện:** Nguyễn Phạm Phú Nam (MSSV: 2312695 – TV1: Authentication, Security & Base Infrastructure)\
**Ngày hoàn thiện:** 28/10/2026

---

## MỤC LỤC
1. [A. Tổng quan hệ thống kiến trúc Production](#a-tổng-quan-hệ-thống-kiến-trúc-production)
2. [B. Yêu cầu môi trường Production & Đối chiếu SRS](#b-yêu-cầu-môi-trường-production--đối-chiếu-srs)
3. [C. Chuẩn bị biến môi trường và cấu hình chi tiết](#c-chuẩn-bị-biến-môi-trường-và-cấu-hình-chi-tiết)
4. [D. Quy trình triển khai chi tiết từng bước](#d-quy-trình-triển-khai-chi-tiết-từng-bước)
5. [E. Chính sách bảo mật môi trường Production (Hardening)](#e-chính-sách-bảo-mật-môi-trường-production-hardening)
6. [F. Quy trình vận hành, giám sát và khôi phục sự cố](#f-quy-trình-vận-hành-giám-sát-và-khôi-phục-sự-cố)
7. [G. Những phần chưa hoàn thiện & Giới hạn kỹ thuật](#g-những-phần-chưa-hoàn-thiện--giới-hạn-kỹ-thuật)

---

## A. TỔNG QUAN HỆ THỐNG KIẾN TRÚC PRODUCTION

### 1. Sơ đồ luồng lưu lượng (Architecture Flow)
Hệ thống **Culinary Blog** được thiết kế theo mô hình Clean Architecture phân tán, tách biệt giữa tầng giao diện người dùng (Frontend SSR/SSG), tầng xử lý nghiệp vụ trung tâm (Backend API), hệ thống lưu trữ dữ liệu bền vững (PostgreSQL, MinIO S3), bộ nhớ đệm phân tán (Redis) và hàng đợi xử lý tác vụ nền (Hangfire).

```
                      [ Client Browser / Mobile / Bot ]
                                      │
                                      ▼ HTTPS (Port 443 / TLS 1.3)
                         ┌─────────────────────────┐
                         │   Nginx Reverse Proxy   │
                         │  (SSL/TLS, HSTS, Gzip)  │
                         └────────────┬────────────┘
                                      │
              ┌───────────────────────┴───────────────────────┐
              ▼ Host: culinaryblog.com                        ▼ Host: api.culinaryblog.com
   ┌───────────────────────┐                       ┌─────────────────────────┐
   │   Next.js Frontend    │ (SSR / Server Actions)│     ASP.NET Core API    │
   │    (Container: 3000)  ├──────────────────────►│     (Container: 8080)   │
   │  Node 22 / Standalone │  HTTP (Internal Net)  │   .NET 10 Minimal APIs  │
   └───────────────────────┘                       └────────────┬────────────┘
                                                                │
              ┌──────────────────────────┬──────────────────────┴──────────────────────┐
              ▼                          ▼                                             ▼
   ┌──────────────────────┐   ┌──────────────────────┐                     ┌──────────────────────┐
   │  PostgreSQL 16 DB    │   │     Redis 7 Cache    │                     │  MinIO S3 Storage    │
   │  (Container: 5432)   │   │   (Container: 6379)  │                     │  (Container: 9000)   │
   │ - Data & Identity    │   │ - Category/Recipe    │                     │ - Recipe Images      │
   │ - Hangfire Jobs      │   │ - Rate Limit State   │                     │ - Avatars & Media    │
   └──────────────────────┘   └──────────────────────┘                     └──────────────────────┘
              ▲
              │ Background Jobs (Queues: default, image-processing)
   ┌──────────┴───────────┐                       ┌─────────────────────────┐
   │   Hangfire Server    │──────────────────────►│  External SMTP Provider   │
   │ (Integrated in API)  │  SMTP / STARTTLS 587  │ (SendGrid / SES / Post) │
   └──────────────────────┘                       └─────────────────────────┘
```

### 2. Vai trò của từng thành phần
- **Nginx Reverse Proxy:** Đóng vai trò cổng vào duy nhất (Single Gateway) đối ngoại ra Internet. Chịu trách nhiệm thiết lập TLS/HTTPS an toàn, cấu hình tiêu đề bảo mật (HSTS, CSP, X-Frame-Options), nén Gzip/Brotli, định tuyến tên miền (`culinaryblog.com` tới Frontend, `api.culinaryblog.com` tới Backend) và chặn trực tiếp các truy vấn bất thường trước khi chạm vào ứng dụng.
- **Frontend (Next.js 15 App Router):** Chạy trên Node.js 22 LTS dưới chế độ `output: standalone`. Thực hiện Render phía máy chủ (SSR) cho các trang chi tiết bài viết (Recipe Detail) để tối ưu hóa SEO (Schema.org JSON-LD), biên dịch trước (SSG) các trang tĩnh và tương tác với API qua Server Components hoặc Client Components.
- **Backend API (ASP.NET Core / .NET 10):** Xây dựng theo kiến trúc Clean Architecture và mô hình CQRS (MediatR). Xử lý toàn bộ logic nghiệp vụ, xác thực JWT, băm mật khẩu bảo mật (ASP.NET Core Identity PBKDF2-HMAC-SHA512), phân quyền 3 tầng (Role, Resource, Policy), kiểm soát tần suất truy cập (Rate Limiting) và phơi bày các RESTful Endpoints chuẩn RFC 7807 Problem Details.
- **PostgreSQL 16 Database:** Hệ quản trị cơ sở dữ liệu quan hệ lưu trữ tập trung dữ liệu toàn hệ thống gồm: người dùng và vai trò (Identity), bảng phân quyền và token làm mới (`UserRefreshTokens`), công thức nấu ăn (`Recipes`), nguyên liệu, các bước làm, danh mục (`Categories`) và các bảng trạng thái tác vụ nền của Hangfire (`hangfire.*`).
- **Redis 7 Cache:** Bộ nhớ đệm phân tán hỗ trợ giảm tải cho PostgreSQL đối với các truy vấn đọc thường xuyên (Danh mục món ăn `ICategoryCache`, Công thức phổ biến `IRecipeCache`) với thời gian sống (TTL) linh hoạt và hỗ trợ cơ chế vô hiệu hóa cache (Cache Invalidation) khi có thay đổi dữ liệu.
- **MinIO Object Storage (S3-compatible):** Lưu trữ đối tượng phân tán phục vụ toàn bộ tệp tĩnh, ảnh món ăn đại diện, ảnh các bước nấu và avatar người dùng. Phục vụ truy xuất ảnh thông qua URL công khai hoặc định tuyến proxy qua Nginx.
- **Hangfire Worker (In-Process Job Server):** Tích hợp trực tiếp bên trong tiến trình API backend, chạy ngầm với 2 hàng đợi: hàng đợi mặc định (`default`) cho việc gửi email thông báo chào mừng / xác nhận và hàng đợi chuyên biệt (`image-processing`) với 2 worker xử lý tối ưu hóa ảnh đại diện món ăn.

---

## B. YÊU CẦU MÔI TRƯỜNG PRODUCTION & ĐỐI CHIẾU SRS

### 1. Bảng đối chiếu giữa Đặc tả SRS và Hiện trạng Mã nguồn thực tế

| Thành phần | Đặc tả SRS v1.0.0 (Mục tiêu) | Hiện trạng Source Code thực tế | Đánh giá & Hạng mục cần xử lý khi Deploy |
| :--- | :--- | :--- | :--- |
| **Hệ điều hành Server** | Ubuntu 22.04 LTS (x86_64 / arm64) | Tương thích hoàn toàn qua Docker Linux Containers | Đạt. Khuyến nghị sử dụng Ubuntu 22.04 LTS hoặc 24.04 LTS. |
| **Backend Framework** | .NET 10 Minimal APIs (CONS-003) | `net10.0` (TargetFramework trong `Directory.Build.props`) | Khớp chuẩn. Cần cài đặt .NET 10 Runtime / SDK tương ứng trên máy chủ hoặc dùng Docker `mcr.microsoft.com/dotnet/aspnet:10.0`. |
| **Frontend Framework** | Next.js App Router (Next.js 14/15) | Next.js `15.1.4`, React `19.0.0`, Node.js `22-alpine` | Khớp chuẩn. Sử dụng cấu hình `output: standalone` trong `next.config.mjs` để tối ưu dung lượng container. |
| **Cơ sở dữ liệu** | PostgreSQL 16 (extensions: `unaccent`, `pg_trgm`) | `postgres:16-alpine` trong Docker Compose | Khớp chuẩn. Cần chạy migration tạo extensions `unaccent` và `pg_trgm` trước khi kích hoạt tìm kiếm toàn văn tiếng Việt. |
| **Bộ nhớ đệm (Cache)** | Redis 7 (NFR-PERF-001) | `redis:7-alpine` (`--appendonly yes`) | Khớp chuẩn. Đã cấu hình AOF (Append-Only File) để bảo toàn dữ liệu cache khi restart. |
| **Lưu trữ tệp (Storage)**| MinIO Object Storage (S3 compatible) | `bitnamilegacy/minio:2025.7.23-debian-12-r5` | Khớp chuẩn. Cần khởi tạo bucket `culinary-blog` và thiết lập chính sách truy cập công khai cho tệp ảnh. |
| **Tác vụ nền (Jobs)** | Hangfire với PostgreSQL Storage | `Hangfire.PostgreSql` 1.20.10, lưu vào schema `hangfire` | Khớp chuẩn. Hangfire tự động tạo và quản lý bảng trong schema `hangfire`. |
| **Máy chủ Web Proxy** | Nginx Reverse Proxy, Let's Encrypt TLS | Chưa được đóng gói sẵn trong source code | **Hạng mục bổ sung:** Cần cài đặt và cấu hình Nginx trực tiếp trên máy chủ host hoặc dựng thêm service Nginx trong Docker Compose. |
| **Dịch vụ Gửi Email** | SMTP Provider (SRS NFR-SEC-004) | Mã nguồn sử dụng `MailKit` (`SmtpEmailSender`), dev dùng MailHog | **Cần thay thế:** Trong Production phải loại bỏ MailHog và cấu hình thông số SMTP thực tế (SendGrid, AWS SES, Brevo, Gmail SMTP). |

### 2. Yêu cầu phần cứng máy chủ tối thiểu (Hardware Sizing)
- **CPU:** Tối thiểu 2 vCPU (khuyến nghị 4 vCPU nếu lượng truy cập đồng thời cao).
- **RAM:** Tối thiểu 4 GB RAM (khuyến nghị 8 GB RAM để vận hành mượt mà cả PostgreSQL, Redis, MinIO, Backend và Frontend).
- **Dung lượng lưu trữ:** Tối thiểu 40 GB SSD (NVMe càng tốt), bao gồm:
  - 15 GB cho hệ điều hành và Docker Images.
  - 10 GB cho dữ liệu PostgreSQL (volume `pgdata`).
  - 2 GB cho Redis persistence (volume `redisdata`).
  - 10+ GB cho dữ liệu hình ảnh MinIO (volume `miniodata`).
- **Băng thông mạng:** Kết nối mạng ổn định với IP tĩnh công khai (Public Static IP), hỗ trợ băng thông tối thiểu 100 Mbps.

### 3. Ma trận cổng mạng (Port Matrix) và Phân vùng truy cập

| Dịch vụ | Cổng Container | Cổng Host (Đề xuất) | Phân vùng truy cập | Ghi chú an toàn |
| :--- | :---: | :---: | :--- | :--- |
| **Nginx HTTP** | 80 | 80 | Công khai (Public Internet) | Chuyển hướng 301 toàn bộ sang HTTPS (443). |
| **Nginx HTTPS** | 443 | 443 | Công khai (Public Internet) | Tiếp nhận truy cập mã hóa TLS 1.2 / TLS 1.3. |
| **Frontend Next.js**| 3000 | Không publish (Internal) | Nội bộ Docker (`frontend:3000`) | Nginx chuyển tiếp lưu lượng qua cổng này. |
| **Backend API** | 8080 | Không publish (Internal) | Nội bộ Docker (`api:8080`) | Nginx chuyển tiếp lưu lượng `/api/` hoặc subdomain API. |
| **PostgreSQL** | 5432 | Không publish (Internal) | Nội bộ Docker (`postgres:5432`)| **CẤM** mở cổng 5432 ra Internet công khai. |
| **Redis** | 6379 | Không publish (Internal) | Nội bộ Docker (`redis:6379`) | **CẤM** mở cổng 6379 ra Internet công khai. |
| **MinIO S3 API** | 9000 | Không publish (Internal) | Nội bộ Docker (`minio:9000`) | Proxy qua Nginx nếu cần tải ảnh trực tiếp. |
| **MinIO Console** | 9001 | 127.0.0.1:9001 (Nếu cần) | Chỉ localhost quản trị | **CẤM** bind 0.0.0.0. Truy cập qua SSH Tunnel. |

---

## C. CHUẨN BỊ BIẾN MÔI TRƯỜNG VÀ CẤU HÌNH CHI TIẾT

> [!CAUTION]
> **Quy tắc bảo mật secrets:** Tuyệt đối không lưu trữ các giá trị bí mật thực tế (mật khẩu CSDL, khóa ký JWT, khóa MinIO, thông tin SMTP) trong mã nguồn Git. Tất cả cấu hình Production phải được truyền qua biến môi trường của hệ điều hành hoặc file `.env.production` được phân quyền `chmod 600`.

### 1. Bảng danh mục biến môi trường Production
Dưới đây là bảng thông số cấu hình chính thức, đối chiếu chính xác theo các khóa cấu hình mà tầng Infrastructure và API của dự án đang đọc:

| Tên biến môi trường (Docker / Linux) | Khóa cấu hình tương ứng | Mục đích sử dụng | Giá trị mẫu (Placeholder) |
| :--- | :--- | :--- | :--- |
| `ASPNETCORE_ENVIRONMENT` | Môi trường ASP.NET Core | Bắt buộc gán `Production` | `Production` |
| `ASPNETCORE_URLS` | Cổng lắng nghe API | Khai báo cổng nội bộ container | `http://+:8080` |
| `ConnectionStrings__Postgres` | `ConnectionStrings:Postgres` | Kết nối CSDL PostgreSQL chính | `Host=postgres;Port=5432;Database=culinary_prod;Username=culinary_app;Password=<STRONG_DB_PASS>` |
| `Redis__ConnectionString` | `Redis:ConnectionString` | Kết nối Redis đệm phân tán | `redis:6379,abortConnect=false` |
| `MinIO__Endpoint` | `MinIO:Endpoint` | Địa chỉ máy chủ lưu trữ S3 | `minio:9000` |
| `MinIO__AccessKey` | `MinIO:AccessKey` | Khóa truy cập MinIO API | `<STRONG_MINIO_ACCESS_KEY>` |
| `MinIO__SecretKey` | `MinIO:SecretKey` | Mật khẩu truy cập MinIO API | `<STRONG_MINIO_SECRET_KEY>` |
| `MinIO__UseSSL` | `MinIO:UseSSL` | Kích hoạt SSL nội bộ MinIO | `false` (nếu Nginx phía ngoài đã SSL) |
| `MinIO__BucketName` | `MinIO:BucketName` | Tên Bucket chứa ảnh ẩm thực | `culinary-blog` |
| `MinIO__PublicBaseUrl` | `MinIO:PublicBaseUrl` | URL công khai để client tải ảnh | `https://media.culinaryblog.com/culinary-blog` |
| `Jwt__Key` | `Jwt:Key` | Khóa ký JWT bí mật (>= 32 ký tự) | `<CRYPTOGRAPHICALLY_SECURE_SECRET_MIN_32_CHARS>` |
| `Jwt__Issuer` | `Jwt:Issuer` | Định danh nguồn phát hành Token | `CulinaryBlogProduction` |
| `Jwt__Audience` | `Jwt:Audience` | Định danh đối tượng tiếp nhận | `CulinaryBlogApp` |
| `Google__ClientId` | `Google:ClientId` | Client ID xác thực Google OAuth | `<GOOGLE_CLIENT_ID>.apps.googleusercontent.com` |
| `Cors__AllowedOrigins__0` | `Cors:AllowedOrigins:[0]` | Danh sách trắng Domain Frontend | `https://culinaryblog.com` |
| `Smtp__Host` | `Smtp:Host` | Địa chỉ máy chủ SMTP thật | `smtp.sendgrid.net` (hoặc smtp.mailgun.org) |
| `Smtp__Port` | `Smtp:Port` | Cổng kết nối SMTP an toàn | `587` |
| `Smtp__UseSsl` | `Smtp:UseSsl` | Kích hoạt mã hóa STARTTLS | `true` |
| `Smtp__Username` | `Smtp:Username` | Tài khoản đăng nhập SMTP | `apikey` |
| `Smtp__Password` | `Smtp:Password` | Mật khẩu / Khóa API gửi mail | `<STRONG_SMTP_API_KEY>` |
| `Smtp__FromAddress` | `Smtp:FromAddress` | Địa chỉ email hiển thị người gửi | `no-reply@culinaryblog.com` |
| `Smtp__FromName` | `Smtp:FromName` | Tên người gửi hiển thị | `Culinary Blog Team` |
| `Sitemap__PublicBaseUrl` | `Sitemap:PublicBaseUrl` | Domain gốc để sinh link sitemap | `https://culinaryblog.com` |
| `Sitemap__PublicSitemapUrl` | `Sitemap:PublicSitemapUrl` | URL file sitemap gửi Google Ping | `https://culinaryblog.com/sitemap.xml` |
| `Seed__Enabled` | `Seed:Enabled` | Tắt tự động nạp dữ liệu mẫu | `false` |
| `NODE_ENV` | Môi trường Next.js Frontend | Tối ưu hóa hiệu năng production | `production` |
| `PORT` | Cổng ứng dụng Next.js | Cổng lắng nghe trong container | `3000` |
| `HOSTNAME` | Host binding Next.js | Cho phép lắng nghe mọi card mạng | `0.0.0.0` |
| `NEXT_PUBLIC_API_URL` | Biến Frontend công khai | URL gọi API phía trình duyệt | `https://api.culinaryblog.com` |
| `API_INTERNAL_URL` | Biến Frontend nội bộ | URL gọi API từ Server Components | `http://api:8080` |
| `NEXT_PUBLIC_GOOGLE_CLIENT_ID`| Biến Google Client ID Frontend | Khởi tạo Google Identity Services | `<GOOGLE_CLIENT_ID>.apps.googleusercontent.com` |

### 2. Mẫu tệp cấu hình `.env.production` (Template an toàn)
Tạo file `/opt/culinary-blog/.env.production` trên server Linux:

```bash
# -------------------------------------------------------------
# CULINARY BLOG PRODUCTION ENVIRONMENT VARIABLES
# LƯU Ý: Phân quyền chmod 600 cho file này sau khi lưu.
# -------------------------------------------------------------

# PostgreSQL Database Configuration
POSTGRES_DB=culinary_prod
POSTGRES_USER=culinary_admin
POSTGRES_PASSWORD=P@ssw0rd_Generated_Random_Db_32Chars_!

# MinIO Object Storage Configuration
MINIO_ROOT_USER=minio_storage_admin
MINIO_ROOT_PASSWORD=P@ssw0rd_Generated_Random_Minio_32Chars_!
MINIO_PUBLIC_URL=https://media.culinaryblog.com/culinary-blog

# JWT Authentication Secret Key (Chuỗi ngẫu nhiên tối thiểu 256 bits)
JWT_SECRET=super_secret_jwt_signing_key_production_environment_2026_xyz!

# Google Authentication
GOOGLE_CLIENT_ID=123456789012-abcdefghijklmnopqrstuvwxyz.apps.googleusercontent.com

# SMTP Provider Credentials
SMTP_HOST=smtp.sendgrid.net
SMTP_PORT=587
SMTP_USER=apikey
SMTP_PASS=SG.example_key_value_do_not_hardcode

# Public Web Domain
FRONTEND_DOMAIN=https://culinaryblog.com
API_DOMAIN=https://api.culinaryblog.com
```

---

## D. QUY TRÌNH TRIỂN KHAI CHI TIẾT TỪNG BƯỚC

### Bước 1: Chuẩn bị máy chủ Linux (Ubuntu 22.04 LTS / 24.04 LTS)
1. Kết nối SSH vào máy chủ và cập nhật các gói hệ thống:
   ```bash
   sudo apt-get update && sudo apt-get upgrade -y
   sudo apt-get install -y curl wget git ufw htop ca-certificates gnupg lsb-release
   ```
2. Thiết lập Tường lửa (UFW Firewall) để chỉ mở các cổng thiết yếu:
   ```bash
   sudo ufw default deny incoming
   sudo ufw default allow outgoing
   sudo ufw allow 22/tcp comment 'SSH Port'
   sudo ufw allow 80/tcp comment 'HTTP Nginx'
   sudo ufw allow 443/tcp comment 'HTTPS Nginx'
   sudo ufw enable
   sudo ufw status verbose
   ```

### Bước 2: Cài đặt Docker Engine và Docker Compose Plugin chính thức
Cài đặt theo tài liệu chuẩn của Docker để nhận các bản vá bảo mật mới nhất:
```bash
sudo install -m 0755 -d /etc/apt/keyrings
curl -fsSL https://download.docker.com/linux/ubuntu/gpg | sudo gpg --dearmor -o /etc/apt/keyrings/docker.gpg
sudo chmod a+r /etc/apt/keyrings/docker.gpg

echo \
  "deb [arch="$(dpkg --print-architecture)" signed-by=/etc/apt/keyrings/docker.gpg] https://download.docker.com/linux/ubuntu \
  "$(. /etc/os-release && echo "$VERSION_CODENAME")" stable" | \
  sudo tee /etc/apt/sources.list.d/docker.list > /dev/null

sudo apt-get update
sudo apt-get install -y docker-ce docker-ce-cli containerd.io docker-buildx-plugin docker-compose-plugin
sudo systemctl enable --now docker
```

### Bước 3: Lấy mã nguồn và thiết lập thư mục triển khai
```bash
sudo mkdir -p /opt/culinary-blog
sudo chown -R $USER:$USER /opt/culinary-blog
git clone -b main https://github.com/Minh2312688/Nhom9_Culinary_Blog.git /opt/culinary-blog
cd /opt/culinary-blog

# Thiết lập file cấu hình môi trường
cp .env.example .env.production
chmod 600 .env.production
nano .env.production
```

### Bước 4: Thiết lập Docker Compose chuyên dụng cho Production (`docker-compose.prod.yml`)
Trong môi trường Production, tuyệt đối không sử dụng tệp `docker-compose.yml` dành cho phát triển (vốn publish cổng CSDL ra máy chủ). Tạo file `docker-compose.prod.yml`:

```yaml
version: '3.8'

services:
  postgres:
    image: postgres:16-alpine
    container_name: culinaryblog-prod-postgres
    restart: always
    environment:
      POSTGRES_DB: ${POSTGRES_DB}
      POSTGRES_USER: ${POSTGRES_USER}
      POSTGRES_PASSWORD: ${POSTGRES_PASSWORD}
    volumes:
      - pgdata:/var/lib/postgresql/data
    healthcheck:
      test: ["CMD-SHELL", "pg_isready -U ${POSTGRES_USER} -d ${POSTGRES_DB}"]
      interval: 10s
      timeout: 5s
      retries: 5
    networks:
      - culinaryblog-internal

  redis:
    image: redis:7-alpine
    container_name: culinaryblog-prod-redis
    restart: always
    command: ["redis-server", "--appendonly", "yes"]
    volumes:
      - redisdata:/data
    healthcheck:
      test: ["CMD", "redis-cli", "ping"]
      interval: 10s
      timeout: 3s
      retries: 5
    networks:
      - culinaryblog-internal

  minio:
    image: bitnamilegacy/minio:2025.7.23-debian-12-r5
    container_name: culinaryblog-prod-minio
    restart: always
    environment:
      MINIO_ROOT_USER: ${MINIO_ROOT_USER}
      MINIO_ROOT_PASSWORD: ${MINIO_ROOT_PASSWORD}
    volumes:
      - miniodata:/bitnami/minio/data
    networks:
      - culinaryblog-internal

  api:
    build:
      context: ./backend
      dockerfile: Dockerfile
    container_name: culinaryblog-prod-api
    restart: always
    environment:
      ASPNETCORE_ENVIRONMENT: Production
      ASPNETCORE_URLS: http://+:8080
      Seed__Enabled: "false"
      ConnectionStrings__Postgres: Host=postgres;Port=5432;Database=${POSTGRES_DB};Username=${POSTGRES_USER};Password=${POSTGRES_PASSWORD}
      Redis__ConnectionString: redis:6379
      MinIO__Endpoint: minio:9000
      MinIO__AccessKey: ${MINIO_ROOT_USER}
      MinIO__SecretKey: ${MINIO_ROOT_PASSWORD}
      MinIO__UseSSL: "false"
      MinIO__BucketName: culinary-blog
      MinIO__PublicBaseUrl: ${MINIO_PUBLIC_URL}
      Jwt__Key: ${JWT_SECRET}
      Jwt__Issuer: CulinaryBlogProduction
      Jwt__Audience: CulinaryBlogApp
      Google__ClientId: ${GOOGLE_CLIENT_ID}
      Cors__AllowedOrigins__0: ${FRONTEND_DOMAIN}
      Smtp__Host: ${SMTP_HOST}
      Smtp__Port: ${SMTP_PORT}
      Smtp__UseSsl: "true"
      Smtp__Username: ${SMTP_USER}
      Smtp__Password: ${SMTP_PASS}
      Smtp__FromAddress: no-reply@culinaryblog.com
      Smtp__FromName: "Culinary Blog"
      Sitemap__PublicBaseUrl: ${FRONTEND_DOMAIN}
      Sitemap__PublicSitemapUrl: ${FRONTEND_DOMAIN}/sitemap.xml
    depends_on:
      postgres:
        condition: service_healthy
      redis:
        condition: service_healthy
      minio:
        condition: service_started
    networks:
      - culinaryblog-internal
      - culinaryblog-proxy

  frontend:
    build:
      context: ./frontend
      dockerfile: Dockerfile
      args:
        NEXT_PUBLIC_API_URL: ${API_DOMAIN}
        NEXT_PUBLIC_GOOGLE_CLIENT_ID: ${GOOGLE_CLIENT_ID}
    container_name: culinaryblog-prod-frontend
    restart: always
    environment:
      NODE_ENV: production
      PORT: 3000
      HOSTNAME: 0.0.0.0
      API_INTERNAL_URL: http://api:8080
    depends_on:
      - api
    networks:
      - culinaryblog-internal
      - culinaryblog-proxy

volumes:
  pgdata:
  redisdata:
  miniodata:

networks:
  culinaryblog-internal:
    driver: bridge
  culinaryblog-proxy:
    driver: bridge
```

### Bước 5: Chuẩn bị cơ sở dữ liệu và Thực thi Migrations
> [!IMPORTANT]
> Trong môi trường `Production`, `Program.cs` đã tắt cơ chế tự động migrate khi khởi động để bảo đảm tính toàn vẹn dữ liệu. Quá trình nâng cấp schema CSDL phải được kích hoạt độc lập trước khi mở kết nối cho ứng dụng.

1. Khởi động CSDL PostgreSQL trước:
   ```bash
   docker compose --env-file .env.production -f docker-compose.prod.yml up -d postgres redis minio
   ```
2. Thực thi EF Core Migrations thông qua .NET SDK container hoặc script riêng:
   ```bash
   # Chạy migrations cập nhật schema CSDL
   docker run --rm \
     --network culinaryblog_culinaryblog-internal \
     -v $(pwd)/backend:/src \
     -w /src \
     mcr.microsoft.com/dotnet/sdk:10.0 \
     dotnet ef database update \
       --project src/CulinaryBlog.Infrastructure \
       --startup-project src/CulinaryBlog.API \
       --connection "Host=postgres;Port=5432;Database=culinary_prod;Username=culinary_admin;Password=P@ssw0rd_Generated_Random_Db_32Chars_!"
   ```
3. Xác minh extensions PostgreSQL bắt buộc (`unaccent` và `pg_trgm`):
   ```bash
   docker exec -it culinaryblog-prod-postgres psql -U culinary_admin -d culinary_prod -c "CREATE EXTENSION IF NOT EXISTS unaccent; CREATE EXTENSION IF NOT EXISTS pg_trgm;"
   ```

### Bước 6: Khởi tạo Storage Bucket trên MinIO
Truy cập MinIO nội bộ để tạo bucket `culinary-blog` và cấp quyền tải ảnh:
```bash
docker run --rm --network culinaryblog_culinaryblog-internal \
  --entrypoint /bin/sh minio/mc -c "
    mc alias set myminio http://minio:9000 minio_storage_admin P@ssw0rd_Generated_Random_Minio_32Chars_!;
    mc mb --ignore-existing myminio/culinary-blog;
    mc anonymous set download myminio/culinary-blog;
  "
```

### Bước 7: Build và Khởi chạy toàn bộ hệ thống
```bash
docker compose --env-file .env.production -f docker-compose.prod.yml build
docker compose --env-file .env.production -f docker-compose.prod.yml up -d
docker compose --env-file .env.production -f docker-compose.prod.yml ps
```

### Bước 8: Cấu hình Nginx Reverse Proxy và Chứng chỉ số SSL/TLS
Cài đặt Nginx trên máy chủ host và tạo file cấu hình `/etc/nginx/sites-available/culinaryblog.conf`:

```nginx
# Upstream nội bộ trỏ tới Docker container
upstream frontend_upstream {
    server 127.0.0.1:3000; # Hoặc IP container nếu dùng bridge
    keepalive 32;
}

upstream backend_upstream {
    server 127.0.0.1:8080;
    keepalive 32;
}

# 1. Chuyển hướng toàn bộ HTTP (Port 80) sang HTTPS (Port 443)
server {
    listen 80;
    listen [::]:80;
    server_name culinaryblog.com www.culinaryblog.com api.culinaryblog.com;
    return 301 https://$host$request_uri;
}

# 2. Cấu hình Frontend Web (culinaryblog.com)
server {
    listen 443 ssl http2;
    listen [::]:443 ssl http2;
    server_name culinaryblog.com www.culinaryblog.com;

    ssl_certificate /etc/letsencrypt/live/culinaryblog.com/fullchain.pem;
    ssl_certificate_key /etc/letsencrypt/live/culinaryblog.com/privkey.pem;
    ssl_protocols TLSv1.2 TLSv1.3;
    ssl_ciphers HIGH:!aNULL:!MD5;
    ssl_prefer_server_ciphers on;

    # Security Headers
    add_header Strict-Transport-Security "max-age=31536000; includeSubDomains; preload" always;
    add_header X-Content-Type-Options "nosniff" always;
    add_header X-Frame-Options "DENY" always;
    add_header Referrer-Policy "strict-origin-when-cross-origin" always;

    gzip on;
    gzip_types text/plain text/css application/json application/javascript text/xml image/svg+xml;

    location / {
        proxy_pass http://frontend_upstream;
        proxy_http_version 1.1;
        proxy_set_header Upgrade $http_upgrade;
        proxy_set_header Connection 'upgrade';
        proxy_set_header Host $host;
        proxy_set_header X-Real-IP $remote_addr;
        proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
        proxy_set_header X-Forwarded-Proto $scheme;
        proxy_cache_bypass $http_upgrade;
    }
}

# 3. Cấu hình Backend API (api.culinaryblog.com)
server {
    listen 443 ssl http2;
    listen [::]:443 ssl http2;
    server_name api.culinaryblog.com;

    ssl_certificate /etc/letsencrypt/live/culinaryblog.com/fullchain.pem;
    ssl_certificate_key /etc/letsencrypt/live/culinaryblog.com/privkey.pem;
    ssl_protocols TLSv1.2 TLSv1.3;
    ssl_ciphers HIGH:!aNULL:!MD5;

    client_max_body_size 10M; # Giới hạn dung lượng tải ảnh lên

    location / {
        proxy_pass http://backend_upstream;
        proxy_http_version 1.1;
        proxy_set_header Host $host;
        proxy_set_header X-Real-IP $remote_addr;
        proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
        proxy_set_header X-Forwarded-Proto $scheme;
    }
}
```

Kích hoạt trang và sinh chứng chỉ SSL Let's Encrypt tự động:
```bash
sudo ln -s /etc/nginx/sites-available/culinaryblog.conf /etc/nginx/sites-enabled/
sudo nginx -t
sudo systemctl reload nginx

# Cấp chứng chỉ TLS miễn phí qua Certbot
sudo apt-get install -y certbot python3-certbot-nginx
sudo certbot --nginx -d culinaryblog.com -d www.culinaryblog.com -d api.culinaryblog.com
```

### Bước 9: Xác minh và Kiểm thử khói (Smoke Test)
1. Kiểm tra Liveness probe Backend:
   ```bash
   curl -i https://api.culinaryblog.com/health/live
   # Kết quả mong đợi: HTTP/2 200 OK, Body: "Healthy"
   ```
2. Kiểm tra System Base Status:
   ```bash
   curl -i https://api.culinaryblog.com/api/v1/system/base-status
   # Kết quả: Trả về JSON trạng thái với environment: "Production"
   ```
3. Kiểm tra Frontend:
   ```bash
   curl -i https://culinaryblog.com
   # Kết quả: HTTP/2 200 OK
   ```

---

## E. CHÍNH SÁCH BẢO MẬT MÔI TRƯỜNG PRODUCTION (HARDENING)

1. **Mã hóa đường truyền (In-Transit Encryption):**
   - Áp dụng giao thức TLS 1.2 và TLS 1.3 trên Nginx cho tất cả kết nối từ người dùng.
   - Bắt buộc HSTS (`Strict-Transport-Security`) với thời gian tối thiểu 1 năm (`max-age=31536000`).
   - Cấu hình `RequireHttpsMetadata = true` trong `AddJwtBearer` của Backend để từ chối các kết nối JWT không mã hóa.
2. **Kiểm soát nguồn gốc chia sẻ tài nguyên (CORS Policy):**
   - Đạt chuẩn SRS NFR-SEC-005: Bộ lọc `CorsPolicyValidator` của hệ thống kiểm tra chặt chẽ mảng `Cors:AllowedOrigins`.
   - **Tuyệt đối không dùng ký tự đại diện (`*`)** trên Production. Chỉ cho phép các Origin hợp lệ đã đăng ký trong danh sách trắng (`https://culinaryblog.com`).
3. **Quản lý Phiên và Token (JWT & Refresh Token Rotation):**
   - Access Token có thời gian sống ngắn (15 phút) nhằm thu hẹp cửa sổ rủi ro khi token bị đánh cắp.
   - Refresh Token có thời hạn 7 ngày, được lưu trữ dưới dạng băm trong bảng `UserRefreshTokens`.
   - Áp dụng cơ chế **Refresh Token Rotation**: Mỗi lần cấp lại Access Token sẽ thu hồi Refresh Token cũ và phát hành một cặp token hoàn toàn mới.
   - Đăng xuất an toàn: Thu hồi token lập tức ở server qua `/api/v1/auth/logout` và xóa sạch sessionStorage ở client.
4. **Phòng chống tấn công brute-force & từ chối dịch vụ (Rate Limiting):**
   - Đã tích hợp `PartitionedRateLimiter` của ASP.NET Core theo chuẩn SRS NFR-SEC-003:
     - Endpoints xác thực (`/api/v1/auth/login`, `/register`): Giới hạn **10 requests/phút/IP** (Sliding Window 6 phân đoạn).
     - Endpoints tải tệp lên (`/api/v1/recipes/images`): Giới hạn **5 requests/phút/IP**.
     - API chung: Giới hạn **100 requests/phút/IP**.
     - Khi vượt ngưỡng, hệ thống trả về HTTP 429 Too Many Requests kèm tiêu đề `Retry-After: 60`.
5. **Nguyên tắc phân quyền tối thiểu (Principle of Least Privilege):**
   - Cả hai container Docker (`backend/Dockerfile` và `frontend/Dockerfile`) đều chạy dưới người dùng không có quyền quản trị (`USER $APP_UID` và `USER nextjs`).
   - CSDL PostgreSQL tách biệt user ứng dụng (`culinary_app`) khỏi superuser (`postgres`).
6. **Bảo mật ghi vết (Zero Leakage Logging):**
   - Hệ thống nghiêm cấm và không bao giờ ghi vết các dữ liệu nhạy cảm ra log: Mật khẩu người dùng, JWT Access Token, Refresh Token, Reset Password Token, Khóa bí mật CSDL/MinIO.
   - Endpoint Quên mật khẩu trả thông điệp chung (Generic Response) và dummy delay để chống kỹ thuật dò quét email người dùng (Account Enumeration).

---

## F. QUY TRÌNH VẬN HÀNH, GIÁM SÁT VÀ KHÔI PHỤC SỰ CỐ

### 1. Giám sát Sức khỏe hệ thống (Health Monitoring)
- **Liveness Check (`/health/live`):** Phục vụ bộ cân bằng tải / Kubernetes / Docker Swarm kiểm tra tiến trình API có còn sống hay không. Không kiểm tra CSDL để tránh restart dây chuyền khi CSDL quá tải tạm thời.
- **Dependency Probes:** Các lớp kiểm tra kết nối sâu bên trong `Infrastructure/Health/`:
  - `PostgresProbe`: Thực thi truy vấn `SELECT 1` kiểm tra CSDL.
  - `RedisProbe`: Gửi lệnh `PING` kiểm tra bộ nhớ đệm.
  - `MinioProbe`: Kiểm tra bucket `culinary-blog` có sẵn sàng hay không.
- **Kiểm tra trạng thái container:**
  ```bash
  docker compose -f docker-compose.prod.yml ps
  ```

### 2. Xem và Quản lý Logs an toàn
```bash
# Xem log thời gian thực của API Backend
docker compose -f docker-compose.prod.yml logs -f --tail=100 api

# Xem log của Frontend
docker compose -f docker-compose.prod.yml logs -f --tail=100 frontend

# Kiểm tra log lỗi Nginx
sudo tail -f /var/log/nginx/error.log
```
> [!TIP]
> Cần cấu hình Docker Daemon log rotation trong `/etc/docker/daemon.json` (`"max-size": "50m"`, `"max-file": "5"`) để tránh làm tràn ổ cứng máy chủ.

### 3. Quy trình Sao lưu dữ liệu định kỳ (Automated Backups)
Thiết lập Crontab chạy hàng ngày lúc 01:00 AM trên máy chủ host:
```bash
# 1. Sao lưu CSDL PostgreSQL nén
0 1 * * * docker exec culinaryblog-prod-postgres pg_dump -U culinary_admin -Fc culinary_prod > /backup/postgres/culinary_$(date +\%Y\%m\%d).dump

# 2. Xóa các bản sao lưu cũ hơn 30 ngày
0 2 * * * find /backup/postgres/ -type f -mtime +30 -name "*.dump" -delete

# 3. Đồng bộ dữ liệu hình ảnh MinIO sang thư mục backup
0 3 * * * docker run --rm --network culinaryblog_culinaryblog-internal -v /backup/minio:/backup minio/mc mirror myminio/culinary-blog /backup/
```

### 4. Quy trình Khôi phục & Rollback khi bản phát hành lỗi
Khi triển khai phiên bản mới gặp sự cố nghiêm trọng, thực hiện rollback theo quy trình sau:
1. **Dừng container hiện tại:**
   ```bash
   docker compose -f docker-compose.prod.yml down
   ```
2. **Khôi phục CSDL từ bản sao lưu gần nhất (nếu migration gây lỗi):**
   ```bash
   docker exec -i culinaryblog-prod-postgres dropdb -U culinary_admin culinary_prod
   docker exec -i culinaryblog-prod-postgres createdb -U culinary_admin culinary_prod
   docker exec -i culinaryblog-prod-postgres pg_restore -U culinary_admin -d culinary_prod < /backup/postgres/culinary_latest.dump
   ```
3. **Chuyển mã nguồn về commit ổn định trước đó (Git Checkout Tag/SHA):**
   ```bash
   git checkout <LAST_STABLE_COMMIT_OR_TAG>
   ```
4. **Rebuild và khởi động lại dịch vụ:**
   ```bash
   docker compose --env-file .env.production -f docker-compose.prod.yml up --build -d
   ```

### 5. Những điều TUYỆT ĐỐI KHÔNG LÀM trên Production
- **KHÔNG** chỉnh sửa trực tiếp mã nguồn hoặc file trong container đang chạy.
- **KHÔNG** chạy lệnh `dotnet ef database drop` hoặc xóa volume `pgdata`.
- **KHÔNG** mở cổng PostgreSQL (5432) hoặc Redis (6379) ra ngoài Internet để debug.
- **KHÔNG** lưu trữ file `.env.production` trên các kho lưu trữ công khai hoặc commit vào Git.
- **KHÔNG** bật `Seed__Enabled: "true"` trên môi trường Production để tránh ghi đè dữ liệu mẫu vào dữ liệu thực tế của người dùng.

---

## G. NHỮNG PHẦN CHƯA HOÀN THIỆN & GIỚI HẠN KỸ THUẬT

Nhằm đảm bảo tính minh bạch và trung thực học thuật, các điểm phụ thuộc kỹ thuật chưa được kiểm chứng thực tế trên môi trường live được ghi nhận chi tiết:

1. **Dịch vụ Chuyển phát Email SMTP Live (Email Delivery):**
   - Hiện tại hệ thống đang kết nối MailKit thông qua cấu hình `SmtpEmailSender`. Trong môi trường phát triển cục bộ, hệ thống sử dụng MailHog hoặc bắt ngoại lệ log cảnh báo.
   - Trên Production, hệ thống phụ thuộc vào việc đăng ký và xác minh tên miền với một nhà cung cấp SMTP chính thức (SendGrid, Mailgun, Amazon SES). Chưa có tài khoản SMTP live nào được cấu hình trong giai đoạn này.
2. **Chứng chỉ TLS và Tên miền Internet thực tế:**
   - Cấu hình Nginx trong hướng dẫn này là mẫu cấu hình thực tế chuẩn mực. Tuy nhiên, việc kích hoạt thực tế đòi hỏi một Tên miền Internet thật (Domain) đã trỏ bản ghi DNS Type A về địa chỉ IP tĩnh của máy chủ VPS để Certbot có thể hoàn thành thử thách ACME challenge cấp chứng chỉ TLS.
3. **Đăng nhập Google Live (Google OAuth GIS):**
   - Module `GoogleTokenValidator.cs` ở backend và form login ở frontend đã sẵn sàng tiếp nhận token từ Google Identity Services.
   - Tuy nhiên, tính năng này chưa thể chạy live nếu chưa đăng ký tài khoản Google Cloud Console, tạo OAuth 2.0 Client ID và khai báo Authorized JavaScript Origins chính thức.
4. **Cơ chế Xoay khóa JWT tự động tại Runtime (Dynamic Key Rotation):**
   - Hiện tại hệ thống sử dụng khóa ký tĩnh được cấu hình qua biến môi trường `Jwt__Key`. Chưa có cơ chế tự động xoay khóa (Key Rotation) định kỳ theo lịch trình tự động tại runtime mà không cần khởi động lại ứng dụng.
5. **Giao diện Quản trị Hangfire Dashboard (`/hangfire`):**
   - Bộ lọc `HangfireDashboardAuthorizationFilter` hiện yêu cầu Bearer Token chứa Role `Admin` trong Header. Do trình duyệt web mặc định không tự động đính kèm Bearer token vào thẻ điều hướng thông thường, người quản trị cần có công cụ hỗ trợ hoặc mở rộng thêm cơ chế xác thực phiên riêng cho Dashboard trong các tuần tiếp theo.
6. **Hệ thống Thu thập Log tập trung (Centralized Observability):**
   - Hiện tại hệ thống ghi log ra stdout/console. Chưa tích hợp cụm thu thập log tập trung (Seq, ELK hoặc Grafana Loki) trên Production.

---

## KẾT LUẬN
Tài liệu hướng dẫn này cung cấp đầy đủ thông số kỹ thuật, quy trình triển khai, mẫu cấu hình và chính sách bảo mật thực tế bám sát 100% hiện trạng mã nguồn của dự án **Culinary Blog – Nhóm 9**. Bất kỳ thành viên nào trong nhóm hoặc kỹ sư vận hành đều có thể dựa vào hướng dẫn này để chuẩn bị và triển khai hệ thống lên máy chủ Linux một cách an toàn, nhất quán và đáng tin cậy.
