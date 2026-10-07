# Chạy Culinary Blog trên Windows

## Yêu cầu

- .NET SDK 10
- Node.js và npm
- Docker Desktop đang chạy

## 1. Cấu hình và chạy API + database

Từ thư mục gốc của repository:

```powershell
Copy-Item .env.example .env
```

Trong `.env`, đổi `POSTGRES_PASSWORD`, `MINIO_ROOT_PASSWORD` và `JWT_SECRET`
sang giá trị riêng cho máy local. `JWT_SECRET` tối thiểu 32 ký tự. Không commit
`.env`.

```powershell
docker compose up --build -d
docker compose ps
curl.exe http://localhost:5000/
```

Khi API chạy trong `Development`, ứng dụng tự áp dụng migration của
`ApplicationDbContext`. Seed mặc định tạo 20 danh mục và 100 công thức. Identity,
refresh token và dữ liệu blog cùng nằm trong PostgreSQL này.

## 2. Chạy giao diện

Mở terminal mới:

```powershell
Set-Location frontend
npm ci
npm run dev
```

Mở `http://localhost:3000`. Frontend dùng API tại `http://localhost:5000` theo
mặc định. Để tạo tài khoản thử nghiệm, dùng chức năng đăng ký trên trang web.

## 3. Kiểm thử

Từ thư mục gốc, chạy backend tests:

```powershell
dotnet test backend\CulinaryBlog.sln --maxcpucount:1
```

Sáu test MinIO thật chỉ chạy khi có cấu hình. Sau khi Compose đã khởi chạy
MinIO, mở terminal backend và đặt access key/secret bằng đúng giá trị
`MINIO_ROOT_USER`/`MINIO_ROOT_PASSWORD` trong `.env`:

```powershell
$env:MINIO_TEST_ENDPOINT = "localhost:9000"
$env:MINIO_TEST_ACCESS_KEY = "<MINIO_ROOT_USER trong .env>"
$env:MINIO_TEST_SECRET_KEY = "<MINIO_ROOT_PASSWORD trong .env>"
dotnet test backend\tests\CulinaryBlog.Integration.Tests\CulinaryBlog.Integration.Tests.csproj `
  --filter "FullyQualifiedName~MinioStorageIntegrationTests"
```

Test dùng bucket riêng `culinary-blog-it`; nếu bỏ các biến trên, test MinIO sẽ
được đánh dấu `Skipped`, không tính là đã kiểm chứng.

Build frontend:

```powershell
Set-Location frontend
npm run build
```

Chạy E2E SRS trong hai terminal. Terminal thứ nhất:

```powershell
Set-Location frontend
npm run dev -- --port 3001
```

Terminal thứ hai:

```powershell
Set-Location frontend
npx playwright install chromium
npx playwright test e2e/srs-routes.spec.ts
```

Nếu đã có Microsoft Edge, có thể dùng trình duyệt đó thay vì tải Chromium:

```powershell
$env:PLAYWRIGHT_CHROMIUM_EXECUTABLE_PATH = "C:\Program Files (x86)\Microsoft\Edge\Application\msedge.exe"
npx playwright test e2e/srs-routes.spec.ts
```

E2E hiện giả lập phản hồi API để chạy độc lập; chúng xác minh route, render và
request payload, không thay thế integration test với PostgreSQL/MinIO.

## 4. Dừng dịch vụ

```powershell
docker compose down
```

Lệnh trên giữ lại dữ liệu PostgreSQL và MinIO trong volumes. Không thêm `-v`
nếu chưa chủ động sao lưu/xóa dữ liệu.

## Khắc phục nhanh

- Docker chưa chạy: mở Docker Desktop rồi kiểm tra `docker info`.
- API không khởi động: chạy `docker compose logs --tail 100 api`; kiểm tra
  `JWT_SECRET` và trạng thái PostgreSQL bằng `docker compose ps`.
- Cổng đã được dùng: giải phóng cổng 5000, 5432, 6379, 9000 hoặc 9001 trước
  khi chạy Compose.
- Database từng được tạo với migration `AuthDbContext`: migration hợp nhất không
  tự chuyển database cũ. Giữ nguyên dữ liệu, tạo database mới theo
  [postgresql-setup.md](./postgresql-setup.md); không xóa volume để chữa lỗi.
- Không thể upload ảnh: kiểm tra MinIO healthy và credentials trong `.env` khớp
  với MinIO đang chạy. Các chức năng đọc/ghi công thức không cần upload ảnh.
