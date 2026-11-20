# Chạy PostgreSQL cho Culinary Blog

## Khởi tạo bằng Docker Compose (khuyến nghị)

Từ thư mục gốc trong PowerShell:

```powershell
Copy-Item .env.example .env
```

Sửa `.env`: đặt `POSTGRES_DB`, `POSTGRES_USER`, `POSTGRES_PASSWORD` và `JWT_SECRET` riêng cho máy local; không commit file này.

```powershell
docker compose up --build -d
docker compose ps
docker compose logs api
```

PostgreSQL tạo database và role `POSTGRES_USER` khi volume được khởi tạo lần
đầu. API chạy ở Development, tự áp dụng migration của `ApplicationDbContext`.
`Seed__Enabled=true` tạo danh mục và 51 công thức từ
`tools/sample-data/20_cong_thuc_mon_an.json`, đồng thời tải ảnh từ
`tools/sample-data/images/` lên MinIO. `RandomDataSeeder` không được gọi tự
động khi API khởi động. API: `http://localhost:5000`.

PostgreSQL được publish trên máy host tại `localhost:5433` (cổng container vẫn
là `5432`, và API trong Compose kết nối bằng `postgres:5432`). Cổng host `5433`
tránh xung đột với PostgreSQL cục bộ đang dùng `5432`.

Nếu PostgreSQL đang dùng volume cũ, đổi `POSTGRES_DB` trong `.env` không tự tạo database mới. Để giữ dữ liệu cũ, tạo database mới qua `psql` trước, đổi `POSTGRES_DB` sang tên mới, rồi khởi động lại API:

```powershell
docker compose exec postgres psql -U culinary -d postgres
```

Trong dấu nhắc `psql` (đổi tên/database/owner theo `.env`):

```sql
CREATE DATABASE culinary_blog_unified OWNER culinary;
\q
```

Không xóa volume PostgreSQL để xử lý migration cũ nếu chưa backup. Cơ sở dữ liệu từng migrate bằng `AuthDbContext` không được nâng cấp tự động bởi migration hợp nhất; giữ database đó nguyên trạng và chuyển sang database mới trước.

## Tạo tài khoản đăng nhập ứng dụng

Seed tạo một tài khoản author kỹ thuật để liên kết công thức, nhưng không gán mật khẩu. Tạo tài khoản đăng nhập riêng qua API:

```powershell
$register = @{
  email = "chef@example.com"
  password = "Use-A-Strong-Local-Password1!"
  displayName = "Local Chef"
} | ConvertTo-Json

Invoke-RestMethod `
  -Method Post `
  -Uri "http://localhost:5000/api/v1/auth/register" `
  -ContentType "application/json" `
  -Body $register
```

Đăng nhập để xác nhận tài khoản:

```powershell
$login = @{
  email = "chef@example.com"
  password = "Use-A-Strong-Local-Password1!"
} | ConvertTo-Json

Invoke-RestMethod `
  -Method Post `
  -Uri "http://localhost:5000/api/v1/auth/login" `
  -ContentType "application/json" `
  -Body $login
```

Mật khẩu phải có ít nhất 8 ký tự, chữ hoa, chữ thường, chữ số và ký tự đặc biệt. Thay email/mật khẩu mẫu; không dùng thông tin này cho môi trường thật.

## Kiểm tra schema và dữ liệu

Thay user/database bằng giá trị trong `.env`:

```powershell
docker compose exec postgres psql -U culinary -d culinary_blog -c 'SELECT COUNT(*) AS categories FROM "Categories";'
docker compose exec postgres psql -U culinary -d culinary_blog -c 'SELECT COUNT(*) AS recipes FROM "Recipes" WHERE "IsDeleted" = false;'
docker compose exec postgres psql -U culinary -d culinary_blog -c 'SELECT COUNT(*) AS app_users FROM "AspNetUsers";'
docker compose exec postgres psql -U culinary -d culinary_blog -c 'SELECT * FROM "__EFMigrationsHistory" ORDER BY "MigrationId";'
```

Kỳ vọng sau sample seed trên database sạch: có ít nhất 20 categories và 51
công thức mẫu; tài khoản mới đăng ký xuất hiện trong `AspNetUsers`. Seeder không xóa dữ liệu cũ,
nên tổng số recipe có thể cao hơn 51. Dataset báo cáo ngẫu nhiên 100 recipe
không phải seed mặc định của API. Identity và dữ liệu công thức cùng nằm trong
một database, dùng một migration history.

`Recipe.Difficulty` được EF chuyển từ enum sang `character varying(20)` và lưu
tên mức độ (`Easy`, `Medium`, `Hard`, `Expert`), không lưu số enum. Migration
hợp nhất giữ nguyên cột chuỗi này. Test mapping tương ứng:
`RecipeDifficulty_UsesStringStorageConversion`.

## EF Core thủ công

API tự migrate khi chạy Development. Nếu cần chạy EF CLI, đặt connection string trong biến môi trường; factory design-time không chứa mật khẩu mặc định:

```powershell
$env:ConnectionStrings__Postgres = "Host=localhost;Port=5433;Database=culinary_blog;Username=culinary;Password=<POSTGRES_PASSWORD>"
dotnet ef migrations list `
  --project backend/src/CulinaryBlog.Infrastructure `
  --startup-project backend/src/CulinaryBlog.API `
  --context ApplicationDbContext
dotnet ef database update `
  --project backend/src/CulinaryBlog.Infrastructure `
  --startup-project backend/src/CulinaryBlog.API `
  --context ApplicationDbContext
Remove-Item Env:ConnectionStrings__Postgres
```

Chỉ tạo migration mới trên `ApplicationDbContext`. Không chạy `EnsureCreated()` hoặc migration lịch sử `AuthDbContext`.

## Kiểm thử migration trên PostgreSQL

Migration regression tests dùng PostgreSQL 16 qua Testcontainers; cần Docker
daemon đang chạy. Test nâng cấp bắt đầu từ schema `InitialRecipeSchema`, thêm
dữ liệu legacy rồi áp dụng các migration còn lại. Test xác nhận dữ liệu
RowVersion và Identity/role được giữ, `SearchVector` được backfill và trigger
tiếp tục hoạt động, ảnh cũ nhận `VariantStatus=Pending`, và recipe Published
lịch sử vẫn có `PublishedAt=NULL`.

Chạy riêng các kiểm thử migration:

```powershell
dotnet test backend\tests\CulinaryBlog.Integration.Tests\CulinaryBlog.Integration.Tests.csproj `
  --filter "FullyQualifiedName~PostgresMigrationTests"
```

Testcontainers dùng database cô lập và không thay đổi database Compose hoặc
database trên máy host. Chi tiết trạng thái coverage: [recipe-core-implementation-spec.md](./recipe-core-implementation-spec.md).

Hướng dẫn chạy API, frontend và kiểm thử toàn dự án: [run-project.md](./run-project.md).
