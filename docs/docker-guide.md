# Hướng dẫn chạy Culinary Blog bằng Docker

Hướng dẫn này dùng Docker Compose để chạy giao diện, API, PostgreSQL, Redis và
MinIO. Cấu hình trong `docker-compose.yml` dành cho máy cá nhân/nhóm phát triển,
không phải cấu hình triển khai production.

## 1. Chuẩn bị

- Cài Docker Desktop trên Windows/macOS hoặc Docker Engine + Compose plugin trên Linux.
- Mở Docker Desktop (Windows nên bật WSL 2), rồi kiểm tra:

```powershell
docker --version
docker compose version
docker info
```

- Clone repository và mở terminal tại thư mục gốc có `docker-compose.yml`.
- Đảm bảo các cổng `3000`, `5000`, `5433`, `6379`, `8025`, `1025`, `9000`,
  `9001` và `5341` chưa bị ứng dụng khác sử dụng. PostgreSQL, Redis và MinIO
  được publish ra máy host để tiện phát triển; không nên mở các cổng này ra
  Internet.

## 2. Cấu hình lần đầu

PowerShell:

```powershell
Copy-Item .env.example .env
notepad .env
```

Thay `POSTGRES_PASSWORD`, `MINIO_ROOT_PASSWORD` và `JWT_SECRET` bằng giá trị riêng.
`JWT_SECRET` phải có ít nhất 32 ký tự. Không gửi hoặc commit file `.env`.
Giữ `NEXT_PUBLIC_API_URL=http://localhost:5000` khi mở web trên chính máy đang
chạy Docker và `NEXT_PUBLIC_MEDIA_URL=http://localhost:9000/culinary-blog` để
trình duyệt tải ảnh từ MinIO. Hai giá trị công khai này được nhúng vào frontend
lúc build và thêm chính xác origin vào Content Security Policy.
Frontend nhận `API_INTERNAL_URL=http://api:8080` lúc runtime để Server
Components gọi API qua mạng nội bộ Compose. Biến này không có tiền tố
`NEXT_PUBLIC_` và không được chuyển xuống browser; khi frontend chạy trực tiếp
trên host, dùng `API_INTERNAL_URL=http://localhost:5000`.

### Cấu hình Google Login & Email (MailHog demo vs Brevo gửi thật)

- **Google Login (`NEXT_PUBLIC_GOOGLE_CLIENT_ID`):** `.env.example` và `frontend/.env.example` đã điền sẵn Web Client ID phát triển công khai cho `http://localhost:3000`. Docker Compose truyền biến này vào cả lúc build Frontend và cho API (`Google__ClientId`). Nếu chạy Next.js ngoài Docker (`npm run dev`), sao chép `frontend/.env.example` thành `frontend/.env.local`. Trên Google Cloud Console, OAuth Client ID cần khai báo đúng **Authorized JavaScript origins** (ví dụ `http://localhost:3000`); nếu ứng dụng OAuth đang ở trạng thái Testing thì chỉ các Test Users được cấp quyền hoặc cần tự tạo Client ID riêng mới đăng nhập được trên máy mới.
- **Email Quên mật khẩu / Welcome Email (`SMTP_*`):**
  - **Chế độ demo / offline (mặc định):** Khi không khai báo `SMTP_*` trong `.env`, Docker Compose tự dùng **MailHog** (`mailhog:1025`, xem thư giả lập tại `http://127.0.0.1:8025`). Email trong MailHog **không** gửi tới Gmail thật.
  - **Gửi email thật (Brevo):** Bỏ ghi chú và điền các biến `SMTP_HOST`, `SMTP_PORT`, `SMTP_USE_SSL`, `SMTP_USERNAME`, `SMTP_PASSWORD` (SMTP Key cá nhân — không commit vào Git), `SMTP_FROM_ADDRESS`, `SMTP_FROM_NAME` trong `.env` cục bộ rồi chạy `docker compose up -d api`. Brevo bật kiểm soát IP nguồn (`Authorized IP addresses`), nên khi đổi mạng hoặc chạy từ máy thành viên khác mà API log `525: 5.7.1 Unauthorized IP address`, cần vào **Brevo → Security → Authorized IP addresses** thêm IP công cộng hiện tại của máy đó.

## 3. Khởi chạy toàn bộ ứng dụng

```powershell
docker compose config
docker compose up --build -d
docker compose ps
```

Lần đầu Docker tải các base image và khôi phục NuGet/npm packages nên có thể mất
thời gian. Khi các container đã chạy:

| Thành phần | Địa chỉ từ máy host | Ghi chú |
|---|---|---|
| Frontend | http://localhost:3000 | Giao diện web |
| API | http://localhost:5000 | API và endpoint kiểm tra `/` |
| PostgreSQL | `localhost:5433` | Database `culinary_blog` |
| Redis | `localhost:6379` | Cache |
| MinIO API | http://localhost:9000 | Lưu ảnh |
| MinIO Console | http://localhost:9001 | Đăng nhập bằng credentials MinIO trong `.env` |
| MailHog UI | http://127.0.0.1:8025 | Hộp thư giả lập, chỉ dùng development |
| Seq | http://127.0.0.1:5341 | Dashboard log, chỉ dùng development |

Ứng dụng dùng **một PostgreSQL database** cho Identity và dữ liệu blog. Các
container, volumes và network dùng tiền tố `culinary_blog_`; volume được
Compose scope theo project name để các project khác nhau không dùng chung dữ
liệu. Ở Development, API tự áp dụng EF migration khi khởi động;
`Seed__Enabled=true` bật seed danh mục và 20 công thức từ
`tools/sample-data/20_cong_thuc_mon_an.json`, đồng thời tải ảnh trong
`tools/sample-data/images/` lên MinIO. Nếu API chưa sẵn sàng, đợi
PostgreSQL/Redis/MinIO healthy rồi xem log ở bước dưới. Healthcheck chờ API mở
cổng sau migration/seed; Compose chỉ khởi động frontend khi API healthy.
Seed không xóa dữ liệu cũ; database đã có 100 công thức giả lập từ phiên bản
trước sẽ giữ nguyên chúng. Dùng `docker compose down -v` rồi khởi động lại nếu
cần tạo database development mới chỉ với bộ dữ liệu JSON.

Nutrition trong JSON là giá trị ước tính để tham khảo trên mỗi khẩu phần, không
thay thế tư vấn chuyên môn. Seeder bổ sung dinh dưỡng còn thiếu nhưng không ghi
đè giá trị dinh dưỡng đã có.

Có thể áp dụng migration thủ công từ host sau khi PostgreSQL Compose sẵn sàng.
Connection string `Postgres` cần dùng host port `5433`; lấy database/user/password
từ `.env` và không ghi secret vào shell history:

```powershell
Set-Location backend
$postgres = @{}
Get-Content ..\.env | ForEach-Object {
    if ($_ -match '^\s*(POSTGRES_DB|POSTGRES_USER|POSTGRES_PASSWORD)=(.*)$') {
        $postgres[$matches[1]] = $matches[2].Trim().Trim('"').Trim("'")
    }
}
if (-not $postgres.POSTGRES_DB -or -not $postgres.POSTGRES_USER -or -not $postgres.POSTGRES_PASSWORD) {
    throw "Missing PostgreSQL settings in ..\.env"
}
$env:ConnectionStrings__Postgres = "Host=localhost;Port=5433;Database=$($postgres.POSTGRES_DB);Username=$($postgres.POSTGRES_USER);Password=$($postgres.POSTGRES_PASSWORD)"
dotnet ef database update --project src\CulinaryBlog.Infrastructure --startup-project src\CulinaryBlog.API --context ApplicationDbContext
Remove-Item Env:ConnectionStrings__Postgres
```

Migration `20261014123827_AddRecipePublishedAt` thêm `PublishedAt` nullable.
Recipe đã Published trước migration vẫn giữ `NULL`; không backfill bằng
`CreatedAt`. UTC publication time chỉ được ghi khi recipe thực sự chuyển sang
Published.

## 4. Theo dõi, kiểm tra và chẩn đoán

```powershell
docker compose ps
docker compose logs -f api
docker compose logs --tail 100 frontend
docker compose logs --tail 100 postgres
```

Nhấn `Ctrl+C` để dừng theo dõi log, không làm dừng container. Kiểm tra API:

```powershell
Invoke-RestMethod http://localhost:5000/
```

Vào PostgreSQL bằng `psql` trong container (nhập mật khẩu từ `.env` khi được hỏi):

```powershell
docker compose exec postgres psql -U culinary -d culinary_blog
```

Trong `psql`, xem bảng bằng `\dt`, thoát bằng `\q`. Nếu đổi `POSTGRES_USER` hoặc
`POSTGRES_DB` trong `.env`, truyền đúng các giá trị mới cho lệnh `psql`.

## 5. Các thao tác Docker thường dùng

```powershell
# Build lại image và khởi động sau khi sửa source/Dockerfile
docker compose up --build -d

# Khởi động/dừng mà vẫn giữ container và dữ liệu
docker compose start
docker compose stop

# Dừng và xóa container/network; volumes dữ liệu vẫn được giữ
docker compose down

# Chỉ build một service
docker compose build api
docker compose build frontend

# Khởi động lại một service
docker compose restart api
```

Muốn xóa đúng một service, ví dụ API: `docker compose rm -sf api`. Lệnh này không
xóa database hoặc volumes.

### Sao lưu và khôi phục PostgreSQL

Tạo file backup SQL trên máy host:

```powershell
docker compose exec -T postgres pg_dump -U culinary culinary_blog > culinary_blog.sql
```

Khôi phục vào database đã tồn tại:

```powershell
Get-Content .\culinary_blog.sql | docker compose exec -T postgres psql -U culinary -d culinary_blog
```

Hãy thử khôi phục trên database riêng trước khi áp dụng lên dữ liệu cần giữ.
Thay `culinary` hoặc tên database nếu `.env` của bạn dùng giá trị khác.

## 6. Build và chuyển image cho người khác

Compose là cách khuyến nghị để chia sẻ cả ứng dụng: gửi mã nguồn qua Git, mỗi
người tự tạo `.env` từ `.env.example`, rồi chạy `docker compose up --build -d`.
Không gửi file `.env`, database volume hoặc credentials thật.

Có thể build riêng API và frontend:

```powershell
docker build -t culinaryblog-api:local -f backend/Dockerfile backend
docker build --build-arg NEXT_PUBLIC_API_URL=http://localhost:5000 `
  --build-arg NEXT_PUBLIC_MEDIA_URL=http://localhost:9000/culinary-blog `
  -t culinaryblog-frontend:local -f frontend/Dockerfile frontend
```

Để chuyển image offline, xuất và gửi các file `.tar` qua kênh phù hợp:

```powershell
docker save -o culinaryblog-api.tar culinaryblog-api:local
docker save -o culinaryblog-frontend.tar culinaryblog-frontend:local
```

Người nhận tải image bằng `docker load -i culinaryblog-api.tar` và tương tự cho
frontend. Khi chia sẻ qua registry, cần đăng nhập registry và push image theo
tên/namespace do nhóm thống nhất. Không đưa secrets vào build arguments; riêng
`NEXT_PUBLIC_API_URL` và `NEXT_PUBLIC_MEDIA_URL` là cấu hình công khai và cần
đặt đúng trước khi build.

## 7. Dữ liệu tồn tại và đặt lại

PostgreSQL, Redis và MinIO lưu dữ liệu trong Docker volumes. `docker compose down`
không xóa dữ liệu. Muốn làm mới database, hãy backup trước và chỉ xóa volume
PostgreSQL khi chắc chắn có thể mất dữ liệu:

```powershell
docker compose down
docker volume ls
docker volume rm nhom9_culinary_blog_pgdata
docker compose up --build -d
```

Tên volume có thể khác tùy tên thư mục/project; xác nhận bằng `docker volume ls`
trước khi xóa. `docker compose down -v` xóa **toàn bộ** volumes của Compose (cả
PostgreSQL, Redis, MinIO); không chạy nếu cần giữ bất kỳ dữ liệu nào.

## 8. Khắc phục lỗi phổ biến

- **`docker` không nhận diện:** mở Docker Desktop, mở terminal mới và xác nhận
  Docker CLI đã có trong `PATH`; nếu cần, khởi động lại máy sau khi cài.
- **Docker daemon không chạy:** mở Docker Desktop, đợi trạng thái Engine sẵn sàng,
  sau đó thử `docker info`.
- **Port đã được dùng:** tìm và dừng ứng dụng đang chiếm cổng hoặc đổi port bên trái
  dấu `:` trong `ports` của service tương ứng; API URL frontend phải trỏ tới port
  API mới và frontend cần build lại.
- **API lỗi kết nối database:** kiểm tra `docker compose ps` và
  `docker compose logs --tail 100 postgres api`; trong mạng Compose, API dùng host
  `postgres`, không dùng `localhost`.
- **Frontend không gọi được API:** mở DevTools của trình duyệt; kiểm tra API URL,
  API đang chạy và CORS cho phép origin frontend. `NEXT_PUBLIC_API_URL` được chốt
  khi build, vì vậy sửa `.env` cần chạy `docker compose build --no-cache frontend`
  rồi `docker compose up -d frontend`.
- **Không tải được image/package:** kiểm tra mạng, proxy/VPN và thử build lại bằng
  `docker compose build --pull`; không thêm registry credentials vào Dockerfile.
- **Migration báo không tương thích:** database cũ từng chạy `AuthDbContext` không
  tự chuyển sang migration hợp nhất. Sao lưu database cũ và làm theo
  [hướng dẫn PostgreSQL](./postgresql-setup.md); không xóa volume để che lỗi.

## 9. Lưu ý khi triển khai thật

Compose này bật Development, seed dữ liệu và publish các cổng dịch vụ nội bộ;
chỉ dùng cho local/demo. Khi triển khai production, cần cấu hình HTTPS, secrets
qua secret manager, CORS theo domain thật, backup/restore, giám sát và kế hoạch
migration riêng. Không dùng credentials mẫu hoặc expose PostgreSQL/Redis/MinIO
công khai.
