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
- Đảm bảo các cổng `3000`, `5000`, `5432`, `6379`, `9000`, `9001` chưa bị ứng dụng
  khác sử dụng. PostgreSQL, Redis và MinIO được publish ra máy host để tiện phát
  triển; không nên mở các cổng này ra Internet.

## 2. Cấu hình lần đầu

PowerShell:

```powershell
Copy-Item .env.example .env
notepad .env
```

Thay `POSTGRES_PASSWORD`, `MINIO_ROOT_PASSWORD` và `JWT_SECRET` bằng giá trị riêng.
`JWT_SECRET` phải có ít nhất 32 ký tự. Không gửi hoặc commit file `.env`.
Giữ `NEXT_PUBLIC_API_URL=http://localhost:5000` khi mở web trên chính máy đang
chạy Docker. Giá trị này được nhúng vào frontend lúc build.

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
| PostgreSQL | `localhost:5432` | Database `culinary_blog` |
| Redis | `localhost:6379` | Cache |
| MinIO API | http://localhost:9000 | Lưu ảnh |
| MinIO Console | http://localhost:9001 | Đăng nhập bằng credentials MinIO trong `.env` |

Ứng dụng dùng **một PostgreSQL database** cho Identity và dữ liệu blog. Ở
Development, API tự áp dụng EF migration khi khởi động; `Seed__Enabled=true`
bật dữ liệu mẫu gồm 20 danh mục và 100 công thức. Nếu API chưa sẵn sàng, đợi
PostgreSQL/Redis khởi động rồi xem log ở bước dưới.

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
`NEXT_PUBLIC_API_URL` là cấu hình công khai và cần đặt đúng trước khi build.

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
