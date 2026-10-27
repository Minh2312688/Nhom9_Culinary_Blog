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

Có thể áp dụng migration tường minh từ terminal backend khi PostgreSQL đã sẵn
sàng. Với Compose, connection string `Postgres` phải trỏ tới `localhost:5433`;
lấy database/user/password từ `.env` và không ghi secret vào shell history hoặc
tài liệu:

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

Migration `20261014123827_AddRecipePublishedAt` thêm `Recipes.PublishedAt`
dưới dạng nullable. Recipe đã Published trước migration vẫn có `NULL`; không
suy diễn ngày từ `CreatedAt`. Chỉ lần chuyển trạng thái thực sự sang Published
mới ghi UTC publication time.

## 2. Chạy giao diện

Mở terminal mới:

```powershell
Set-Location frontend
npm ci
npm run dev
```

Mở `http://localhost:3000`. Frontend dùng API tại `http://localhost:5000` theo
mặc định. Server Components dùng `API_INTERNAL_URL` để gọi API khi render; khi
chạy frontend trực tiếp trên host, đặt biến này thành `http://localhost:5000`
(Compose đặt nội bộ thành `http://api:8080`). Không chuyển giá trị server-only
này xuống browser qua `NEXT_PUBLIC_API_URL`.

Public author profile là `/authors/{id}`, được tải từ
`GET /api/v1/authors/{id}?page=1&pageSize=12`. API chỉ trả display name, avatar,
bio và danh sách recipe Published có phân trang; không trả email hoặc roles.
Trang `/recipes/{slug}` render dữ liệu API ở server và thêm `author` /
`datePublished` vào Recipe JSON-LD khi dữ liệu nguồn có sẵn; trường thiếu, kể cả
ngày xuất bản lịch sử còn NULL, được bỏ qua. Để tạo tài khoản thử nghiệm, dùng
chức năng đăng ký trên trang web.

## 3. Chạy thử Welcome Email

### Cách A: Docker Compose (MailHog đi kèm, khuyến nghị cho demo nhóm)

`docker-compose.yml` đã bao gồm MailHog (`mailhog/mailhog:v1.0.1`): Web UI tại
`127.0.0.1:8025`, SMTP nội bộ `mailhog:1025` (API container nhận qua biến môi
trường `Smtp__Host=mailhog`, `Smtp__Port=1025`, `Smtp__UseSsl=false`, không
credential — override riêng cho container, không sửa `appsettings.Development.json`;
API chạy trực tiếp ngoài Docker vẫn dùng `localhost:1025`).

Đăng ký tài khoản mới để kích hoạt Welcome Email (qua frontend
`http://localhost:3000` hoặc curl tới API Compose):

```powershell
$email = "compose-$(Get-Date -Format yyyyMMddHHmmss)@example.com"
Invoke-RestMethod -Uri "http://localhost:5000/api/v1/auth/register" -Method Post -ContentType "application/json" -Body (@{ email = $email; password = "P@ssword123"; displayName = "TV4 Compose" } | ConvertTo-Json)
```

Mở `http://127.0.0.1:8025` để xem email trong MailHog. Mỗi lần thử, đăng ký
bằng email mới.

**Cảnh báo xung đột cổng:** không chạy đồng thời script
`.\tools\run-welcome-email-demo.ps1` (Cách B) khi Compose đang chạy — cả hai đều
chiếm cổng `1025`/`8025` trên `127.0.0.1`.

Dừng dịch vụ, giữ nguyên dữ liệu trong volumes (xem mục 5):
`docker compose down` — **không** thêm `-v`.

Hangfire Dashboard (`/hangfire`): chỉ nhận `Authorization: Bearer <JWT có role
Admin>`; không nhận token qua query string, chưa có luồng đăng nhập browser an
toàn, nên Admin chưa mở được Dashboard qua trình duyệt — đang chờ nhóm/TV1 chốt.

### Cách B: Script local (môi trường Development ngoài Docker)

Đảm bảo Docker Desktop đang chạy và đã có `.env` (nếu chưa, làm theo bước 1).
Từ thư mục gốc repository, chạy một lệnh:

```powershell
.\tools\run-welcome-email-demo.ps1
```

Script khởi động/tái sử dụng PostgreSQL và MailHog, tạo/tái sử dụng database
demo riêng `frjob001_welcome_demo`, rồi chạy API local ở `http://localhost:5051`.
Giữ terminal này mở trong lúc thử nghiệm. Chỉ khởi động API chưa gửi email; phải
đăng ký một tài khoản mới để kích hoạt Welcome Email.

Mở terminal PowerShell khác và gửi yêu cầu đăng ký thử (email tự có thời gian
để có thể chạy lại):

```powershell
$email = "demo-$(Get-Date -Format yyyyMMddHHmmss)@example.com"; Invoke-RestMethod -Uri "http://localhost:5051/api/v1/auth/register" -Method Post -ContentType "application/json" -Body (@{ email = $email; password = "DemoPass123!"; displayName = "TV4 Demo" } | ConvertTo-Json)
```

Mở `http://127.0.0.1:8025` để xem email trong MailHog. Mỗi lần thử gửi một email
mới, đăng ký bằng địa chỉ email mới; không cần khởi động lại API khi nó vẫn đang
chạy.

Dừng API bằng `Ctrl+C` ở terminal chạy script. PostgreSQL, MailHog và dữ liệu
demo được giữ lại để lần sau chạy cùng lệnh. Nếu muốn xóa riêng demo, sau khi
dừng API chạy từ thư mục gốc:

```powershell
docker rm -f frjob001-mailhog-demo
$u = docker exec culinaryblog-postgres printenv POSTGRES_USER; docker exec culinaryblog-postgres psql -U $u -d postgres -v ON_ERROR_STOP=1 -c 'DROP DATABASE IF EXISTS frjob001_welcome_demo WITH (FORCE);'
```

Lệnh dọn trên chỉ xóa MailHog và database demo `frjob001_welcome_demo`; không
dừng PostgreSQL, không xóa database chính, và không xóa Docker volumes. Chạy lại
script sẽ tạo lại chúng.

## 4. Kiểm thử

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

## 5. Dừng dịch vụ

```powershell
docker compose down
```

Lệnh trên giữ lại dữ liệu PostgreSQL và MinIO trong volumes. Không thêm `-v`
nếu chưa chủ động sao lưu/xóa dữ liệu.

## Khắc phục nhanh

- GitHub báo conflict ở `Program.cs` hoặc `srs-routes.spec.ts`: giữ một cặp
  `UseAuthentication()`/`UseAuthorization()` duy nhất và đóng từng khối
  Playwright `test(...)` bằng `});`. Sau đó chạy
  `npx playwright test e2e/srs-routes.spec.ts --list` từ `frontend` để kiểm
  tra file test được nạp.
- E2E route cần chạy cùng `npm run dev -- --port 3001`. Lần chạy đầu có thể
  biên dịch lazy nhiều trang, vì vậy test quét route dùng timeout 90 giây.
- Docker chưa chạy: mở Docker Desktop rồi kiểm tra `docker info`.
- API không khởi động: chạy `docker compose logs --tail 100 api`; kiểm tra
  `JWT_SECRET` và trạng thái PostgreSQL bằng `docker compose ps`.
- Cổng đã được dùng: giải phóng cổng 5000, 8025, 1025, 5433, 6379, 9000 hoặc 9001 trước
  khi chạy Compose.
- Database từng được tạo với migration `AuthDbContext`: migration hợp nhất không
  tự chuyển database cũ. Giữ nguyên dữ liệu, tạo database mới theo
  [postgresql-setup.md](./postgresql-setup.md); không xóa volume để chữa lỗi.
- Không thể upload ảnh: kiểm tra MinIO healthy và credentials trong `.env` khớp
  với MinIO đang chạy. Các chức năng đọc/ghi công thức không cần upload ảnh.
