# Nhom9_Culinary_Blog

> **Đồ án môn:** Phát triển Ứng dụng Web Nâng cao  
> **Chủ đề:** Culinary Blog – Blog Ẩm thực và Chia sẻ Công thức Nấu ăn

---

## 1. Thành viên Nhóm 9

| STT | Họ và tên | MSSV | Email | SĐT |
|---:|---|---:|---|---|
| 1 | Dương Văn Minh | 2312688 | 2312688@dlu.edu.vn | 0352183984 |
| 2 | Nguyễn Phạm Phú Nam | 2312695 | 2312695@dlu.edu.vn | 0917707902 |
| 3 | Mai Quý Phước | 2312716 | 2312716@dlu.edu.vn | 097100756 |
| 4 | Trần Hữu Phan Lâm | 2312656 | 2312656@dlu.edu.vn | 0562144990 |

---

## 2. Mục tiêu dự án

Nhóm xây dựng một nền tảng blog ẩm thực và chia sẻ công thức nấu ăn với các nhóm chức năng chính:

- Đăng ký, đăng nhập Local JWT và đăng nhập Google.
- Quản lý hồ sơ cá nhân.
- Xem danh sách, chi tiết và danh mục công thức.
- Tìm kiếm, lọc, sắp xếp và phân trang công thức.
- Tác giả tạo, chỉnh sửa, xóa và quản lý trạng thái công thức.
- Quản lý nguyên liệu và các bước chế biến.
- Quản lý danh mục món ăn.
- Upload và quản lý hình ảnh.
- Background Jobs.
- Logging, Health Check và Observability.
- Caching và tối ưu truy vấn.
- Responsive UI cho Desktop / Tablet / Mobile.

---

## 3. Công nghệ và kiến trúc

### Backend

- .NET 10.
- ASP.NET Core Minimal API.
- Clean Architecture.
- CQRS + MediatR.
- FluentValidation.
- Entity Framework Core 10.
- PostgreSQL.
- ASP.NET Core Identity.
- JWT Authentication.
- Google Identity Services / Google ID Token verification.
- Rate Limiting.
- Docker / Docker Compose.

Các thành phần được tích hợp theo tiến độ module tương ứng:

- Redis.
- MinIO.
- Hangfire.
- Serilog.
- OpenTelemetry.
- PostgreSQL Full-Text Search.

### Frontend

- Next.js App Router.
- React.
- TypeScript.
- Tailwind CSS.
- React Hook Form.
- Zod.
- Các thư viện khác được tích hợp theo từng module.

### Clean Architecture

```text
Domain
  ↑
Application
  ↑
Infrastructure
  ↑
API / Presentation
```

Nguyên tắc chính:

- `Domain` chứa entity và mô hình nghiệp vụ cốt lõi.
- `Application` chứa use case, Command/Query, Handler, Validator, DTO và abstraction.
- `Infrastructure` hiện thực abstraction và tích hợp EF Core, PostgreSQL, Identity, caching, file storage...
- `API` nhận HTTP Request, map endpoint và cấu hình Dependency Injection.
- Domain không phụ thuộc trực tiếp Infrastructure hoặc API.

---

## 4. Cấu trúc thư mục chính

```text
Nhom9_Culinary_Blog/
│
├── backend/
│   ├── src/
│   │   ├── CulinaryBlog.Domain/
│   │   │   └── Entity và mô hình nghiệp vụ cốt lõi
│   │   ├── CulinaryBlog.Application/
│   │   │   └── Command, Query, Handler, Validator, DTO và abstraction
│   │   ├── CulinaryBlog.Infrastructure/
│   │   │   └── EF Core, PostgreSQL, Identity, Repository và service implementation
│   │   └── CulinaryBlog.API/
│   │       └── Minimal API, Endpoint, Middleware và cấu hình ứng dụng
│   └── tests/
│       ├── CulinaryBlog.Application.Tests/
│       ├── CulinaryBlog.ArchitectureTests/
│       └── CulinaryBlog.Integration.Tests/
│
├── frontend/
│   └── Ứng dụng Next.js
│
├── docs/
│   └── Tài liệu dự án và tài liệu rà soát SRS
│
├── docker-compose.yml
├── .env.example
└── README.md
```

Cấu trúc có thể được bổ sung khi các module mới được tích hợp.

---

## 5. Phân công cụ thể theo thành viên

### TV1 – Nguyễn Phạm Phú Nam – Auth, Security & Base Infrastructure

**Backend**

- Dựng khung Backend theo Clean Architecture.
- Cấu hình MediatR Pipeline và Validation.
- FR-AUTH-001: Đăng ký tài khoản.
- FR-AUTH-002: Đăng nhập Local JWT.
- FR-AUTH-003: Google Login.
- FR-AUTH-004 → FR-AUTH-007 theo kế hoạch các tuần tiếp theo.
- ASP.NET Core Identity.
- JWT Access Token.
- Refresh Token.
- Role.
- Account Lockout.
- Rate Limiting.
- CORS và các thiết lập Security liên quan đến Auth.

**Frontend**

- `/auth/register`
- `/auth/login`
- `/profile` theo tiến độ module Auth.

**Hạ tầng**

- Docker Compose base.
- PostgreSQL.
- Redis.
- MinIO.

**Đầu ra cần bàn giao**

- Auth API.
- Login / Register UI.
- JWT + Refresh Token.
- Rate Limiting.
- Unit / Integration Test cho Auth.
- Base Infrastructure cho các module khác tích hợp.

---

### TV2 – Dương Văn Minh – Recipe Backend, Data, Search & Cache

**Database**

- Hoàn thiện các Data Model chính liên quan Recipe.
- DbContext hợp nhất cho hệ thống.
- EF Core Migration chính thức của nhóm.
- Seed dữ liệu chung.

**Recipe Module**

- FR-RCP-001 → FR-RCP-007.
- FR-RCP-009.
- FR-RCP-010.
- Tạo / sửa / xóa Recipe.
- Ingredient.
- Recipe Step.
- Draft / Published / Archive.
- Optimistic Concurrency với `RowVersion`.
- Resource-Based Authorization theo tác giả.

**Search**

- FR-SRCH-001 → FR-SRCH-004.
- PostgreSQL Full-Text Search.
- Tìm kiếm không dấu theo yêu cầu SRS.
- Ranking / Filtering theo yêu cầu chức năng.

**Caching**

- Redis.
- Cache-Aside.
- Output Cache.
- Cache Recipe list / detail.

**Đầu ra cần bàn giao**

- Recipe CRUD API.
- Recipe Repository / Query.
- Search module.
- Redis caching.
- Migration và schema dữ liệu dùng chung.
- Unit / Integration Test cho Recipe và Search.

---

### TV3 – Trần Hữu Phan Lâm – Frontend Core & UI/UX

**Frontend Core**

- Next.js App Router.
- Shared Layout.
- Navigation.
- Responsive UI.
- Shared Components.
- REST API Integration.

**Public Screens**

- `/`
- `/recipes`
- `/recipes/[slug]`
- `/categories`
- `/categories/[slug]`
- `/search`

**Author Dashboard**

- `/dashboard/recipes`
- `/dashboard/recipes/new`
- `/dashboard/recipes/[id]/edit`

**UI / Form**

- React Hook Form.
- Zod.
- Multi-step Wizard.
- Responsive Desktop / Tablet / Mobile.

**SEO**

- Metadata.
- Recipe JSON-LD.
- Accessibility.

**Đầu ra cần bàn giao**

- Public UI.
- Recipe Listing / Detail UI.
- Category Public UI.
- Dashboard Recipe.
- API Integration.
- Responsive UI.

---

### TV4 – Mai Quý Phước – Category, File Service, Jobs & Observability

**Category Module**

- FR-CAT-001 → FR-CAT-005.
- Create / Update / Delete Category.
- Slug.
- Category Cache.

**File Service**

- FR-FILE-001.
- FR-FILE-002.
- FR-RCP-008.
- Upload / Delete ảnh.
- MinIO S3.
- Validate dung lượng.
- Validate Magic Bytes.

**Background Jobs**

- FR-JOB-001 → FR-JOB-003.
- Welcome Email.
- Thumbnail / Medium Image.
- Sitemap Generation.

**Observability**

- FR-OBS-001 → FR-OBS-003.
- Serilog.
- OpenTelemetry.
- Health Check.
- `/health`
- `/health/ready`
- `/health/live`

**Frontend**

- `/dashboard/categories`
- Upload Image Component.
- Progress Bar.

**Đầu ra cần bàn giao**

- Category API.
- MinIO File Service.
- Hangfire Jobs.
- Health Check.
- Logging / Tracing.
- Category Admin UI.

---

## 6. Task Matrix

| Thành viên | Vai trò | Nhiệm vụ cốt lõi phải bàn giao | Phạm vi chính |
|---|---|---|---|
| **Nguyễn Phạm Phú Nam** | Auth & Base Infrastructure | Clean Architecture base, Register, Local Login, Google Login, JWT, Refresh Token, Rate Limit, Auth UI, Docker base | FR-AUTH-001 → 007, NFR Security |
| **Dương Văn Minh** | Recipe Backend & Data | Recipe CRUD, Ingredient, Recipe Step, Recipe Status, DbContext/Migration chung, PostgreSQL Search, Redis Cache | FR-RCP-001 → 007, 009, 010; FR-SRCH-001 → 004 |
| **Trần Hữu Phan Lâm** | Frontend Core | Shared UI, Homepage, Recipe Listing/Detail, Category Public Pages, Search Page, Author Dashboard | Public UI, Dashboard, SEO, Usability |
| **Mai Quý Phước** | Services & Admin | Category API, MinIO Upload, Background Jobs, Health Check, Logging, Tracing, Category Admin UI | FR-CAT, FR-FILE, FR-JOB, FR-OBS |

---

## 7. Khu vực ưu tiên phụ trách trong source code

| Thành viên | Khu vực ưu tiên |
|---|---|
| **Phú Nam** | Auth Application, Identity, JWT/Auth services, Auth endpoints, `/auth/*`, Auth UI |
| **Văn Minh** | Recipe Application/Infrastructure, Recipe Repository, Recipe Query/Command, Recipe DB integration, Search và Cache chính thức |
| **Phan Lâm** | Next.js Public Pages, Recipe UI, Category Public UI, Shared UI, Dashboard Recipe |
| **Phước** | Category Backend, File/MinIO, Hangfire, Observability, Admin Category UI |

Các file dùng chung như:

```text
Program.cs
DependencyInjection.cs
DbContext
docker-compose.yml
shared layout
shared configuration
```

cần được kiểm tra kỹ trước khi chỉnh sửa để giảm Git conflict.

---

## 8. Quy trình triển khai khuyến nghị

### Giai đoạn 1 – Khởi tạo Base

- **Phú Nam**: Clean Architecture base và Docker Compose.
- **Văn Minh**: Data Model cơ bản liên quan Recipe.
- **Phan Lâm**: Next.js layout và UI component nền tảng.
- **Phước**: MinIO / Hangfire base theo module được giao.

### Giai đoạn 2 – Phát triển chức năng lõi

- Các thành viên phát triển song song theo module.
- **Phú Nam & Phước**: Hoàn thiện Auth / File Service để hỗ trợ frontend.
- **Văn Minh**: Hoàn thiện Recipe, Search và Caching.
- **Phan Lâm**: Public Page và Dashboard.

### Giai đoạn 3 – Tích hợp, Test và NFR

- Hoàn thiện UI.
- Logging / Health Check / Observability.
- Unit Test / Integration Test / E2E.
- Review Migration và conflict.
- Kiểm tra Security và hiệu năng.

---

## 9. Phân công theo tuần

### Tuần 2

| STT | Thành viên | Công việc được giao | Đầu ra cần có |
|---:|---|---|---|
| 1 | Dương Văn Minh | FR-RCP-1,2,3,4,5,6,7 | Backend Recipe theo các FR được giao |
| 2 | Nguyễn Phạm Phú Nam | FR-AUTH-1,2,3 | Register API, Login API, Google Login |
| 2 | Nguyễn Phạm Phú Nam | `/auth/login`, `/auth/register` | Hai màn hình Auth kết nối Backend |
| 3 | Mai Quý Phước | FR-CAT-1,2,3,4,5 | Category Backend |
| 4 | Trần Hữu Phan Lâm | `/`, `/recipes`, `/recipes/[slug]`, `/categories`, `/categories/[slug]` | Public Frontend Pages |

---

## 10. Theo dõi tiến độ thực tế theo từng thành viên

> Mục này được cập nhật theo tiến độ thật của từng thành viên.  
> Không tự đánh dấu `DONE` cho thành viên khác nếu chưa có xác nhận hoặc bằng chứng từ branch/commit của người phụ trách.

### 10.1. Nguyễn Phạm Phú Nam (MSSV: 2312695) – Auth & Base Infrastructure

#### FR-AUTH-001 – Đăng ký tài khoản

**Trạng thái:** `PARTIAL`

**Đã hoàn thành:**

- API đăng ký tài khoản.
- DTO / Command / Handler / Validator.
- ASP.NET Core Identity.
- Kiểm tra yêu cầu mật khẩu.
- Tạo `ApplicationUser`.
- Gán role `Author`.
- Lưu dữ liệu vào PostgreSQL.
- Sinh Refresh Token.
- Refresh Token lưu dưới dạng SHA-256 hash.
- Unit Test và Integration Test cho luồng đăng ký.

**Còn lại / phụ thuộc:**

- Welcome Email thông qua Hangfire thuộc Background Job của thành viên phụ trách Jobs.

#### FR-AUTH-002 – Đăng nhập Local JWT

**Trạng thái:** `DONE`

**Đã hoàn thành:**

- `POST /api/v1/auth/login`.
- Đăng nhập Email / Password.
- JWT Access Token khoảng 15 phút.
- Refresh Token có thời hạn dài hơn.
- JWT claims gồm `userId`, `email`, `roles`, `jti`.
- Sai thông tin đăng nhập trả HTTP `401`.
- Sai 5 lần liên tiếp thì tài khoản bị khóa 15 phút.
- Tài khoản đang bị khóa trả HTTP `423`.
- Rate Limiting cho Auth: 10 request/phút/IP.
- Khi vượt giới hạn trả HTTP `429` và `Retry-After`.

#### FR-AUTH-003 – Google Login

**Trạng thái tổng:** `PARTIAL`

**Backend:** `DONE`

- Endpoint `POST /api/v1/auth/google`.
- Nhận `idToken`.
- Xác minh Google ID Token bằng `Google.Apis.Auth`.
- Tạo mới hoặc liên kết tài khoản theo email.
- Sinh Access Token và Refresh Token sau khi xác thực thành công.

**Frontend code:** `DONE`

- Tích hợp Google Identity Services.
- Nhận `response.credential`.
- Gửi Google ID Token về Backend.
- Sử dụng `NEXT_PUBLIC_GOOGLE_CLIENT_ID`.
- Không hardcode Client Secret.

**Còn lại / phụ thuộc:**

- Live external verification chưa hoàn tất do nhóm chưa có Google Client ID chính thức.

#### Frontend Auth

| Màn hình | Trạng thái | Nội dung đã hoàn thành |
|---|---|---|
| `/auth/register` | `DONE` | Form đăng ký, React Hook Form + Zod validation, loading/error state, responsive, accessibility và kết nối API |
| `/auth/login` | `DONE` | Email/Password login, xử lý 401/423, Google Identity Services, loading/error state, responsive và accessibility |

#### Base Infrastructure

**Đã hoàn thành:**

- Clean Architecture base.
- PostgreSQL.
- Docker Compose nền tảng.
- Redis / MinIO trong hạ tầng Docker theo cấu hình project.
- MediatR / Validation pipeline nền tảng.
- CORS.
- Rate Limiting.
- Security configuration cơ bản.

---

### 10.2. Dương Văn Minh (MSSV: 2312688) – Recipe Backend, Data, Search & Cache

| Hạng mục | Trạng thái | Đã hoàn thành | Còn lại / Phụ thuộc |
|---|---|---|---|
| FR-RCP-001 → FR-RCP-007 | `CẦN CẬP NHẬT` | Thành viên phụ trách cập nhật theo branch/commit thực tế | Thành viên phụ trách cập nhật |
| Database chung & Migration | `CẦN CẬP NHẬT` | Thành viên phụ trách cập nhật theo branch/commit thực tế | Thành viên phụ trách cập nhật |
| FR-SRCH-001 → FR-SRCH-004 | `CẦN CẬP NHẬT` | Thành viên phụ trách cập nhật theo branch/commit thực tế | Thành viên phụ trách cập nhật |
| Redis Caching | `CẦN CẬP NHẬT` | Thành viên phụ trách cập nhật theo branch/commit thực tế | Thành viên phụ trách cập nhật |

---

### 10.3. Trần Hữu Phan Lâm (MSSV: 2312656) – Frontend Core & UI/UX

| Hạng mục | Trạng thái | Đã hoàn thành | Còn lại / Phụ thuộc |
|---|---|---|---|
| Next.js Core / Shared Layout | `CẦN CẬP NHẬT` | Thành viên phụ trách cập nhật theo branch/commit thực tế | Thành viên phụ trách cập nhật |
| `/` | `CẦN CẬP NHẬT` | Thành viên phụ trách cập nhật | Thành viên phụ trách cập nhật |
| `/recipes` | `CẦN CẬP NHẬT` | Thành viên phụ trách cập nhật | Thành viên phụ trách cập nhật |
| `/recipes/[slug]` | `CẦN CẬP NHẬT` | Thành viên phụ trách cập nhật | Thành viên phụ trách cập nhật |
| `/categories` | `CẦN CẬP NHẬT` | Thành viên phụ trách cập nhật | Thành viên phụ trách cập nhật |
| `/categories/[slug]` | `CẦN CẬP NHẬT` | Thành viên phụ trách cập nhật | Thành viên phụ trách cập nhật |

---

### 10.4. Mai Quý Phước (MSSV: 2312716) – Category, File Service, Jobs & Observability

| Hạng mục | Trạng thái | Đã hoàn thành | Còn lại / Phụ thuộc |
|---|---|---|---|
| FR-CAT-001 → FR-CAT-005 | `CẦN CẬP NHẬT` | Thành viên phụ trách cập nhật theo branch/commit thực tế | Thành viên phụ trách cập nhật |
| FR-FILE-001 / 002 | `CẦN CẬP NHẬT` | Thành viên phụ trách cập nhật | Thành viên phụ trách cập nhật |
| FR-JOB-001 → 003 | `CẦN CẬP NHẬT` | Thành viên phụ trách cập nhật | Thành viên phụ trách cập nhật |
| FR-OBS-001 → 003 | `CẦN CẬP NHẬT` | Thành viên phụ trách cập nhật | Thành viên phụ trách cập nhật |

---

## 11. Lab cá nhân – Nguyễn Phạm Phú Nam

### Lab 3 – Repository Pattern & PostgreSQL Full-Text Search

**Branch**

```text
2312695-NguyenPhamPhuNam-Lab3-Data
```

**Commit**

```text
09622c2 feat(search): add PostgreSQL full-text recipe search
```

**Đã thực hiện**

- `IRecipeSearchRepository`.
- `PostgresRecipeSearchRepository`.
- PostgreSQL Full-Text Search trên `Recipe.Title` và `Recipe.Description`.
- GIN Index `IX_Recipes_Title_Description_Fts`.
- Migration `Lab03AddRecipeFullTextSearchIndex`.
- Integration Test trên PostgreSQL thật.
- Kiểm tra query plan bằng `EXPLAIN ANALYZE`.

> Đây là phần thực hành Lab cá nhân của Phú Nam và không làm thay đổi phân công chính thức của nhóm. Module Search chính thức của project vẫn thuộc thành viên được giao trong Task Matrix.

---

## 12. Database và Migration

Project sử dụng:

- PostgreSQL.
- Entity Framework Core.
- EF Core Migration.

Các model chính hiện có:

```text
ApplicationUser
RefreshToken
Category
Recipe
RecipeIngredient
RecipeStep
```

Database schema được quản lý bằng Migration.

Không sử dụng `EnsureCreated()` để thay thế workflow Migration hiện tại.

Xem danh sách migration:

```powershell
dotnet ef migrations list `
  --project backend/src/CulinaryBlog.Infrastructure `
  --startup-project backend/src/CulinaryBlog.API `
  --context AuthDbContext
```

Apply migration:

```powershell
dotnet ef database update `
  --project backend/src/CulinaryBlog.Infrastructure `
  --startup-project backend/src/CulinaryBlog.API `
  --context AuthDbContext
```

### Lưu ý khi làm việc nhóm

- Không tự ý sửa migration của thành viên khác.
- Migration mới phải được review trước khi merge.
- Không tạo Entity trùng.
- Không reset Docker volume nếu chưa kiểm tra dữ liệu.
- DbContext là file có nguy cơ conflict cao, cần hạn chế chỉnh song song.
- DbContext hiện tại có thể tiếp tục được hợp nhất khi nhóm tích hợp database chính thức.

---

## 13. Yêu cầu môi trường

Mỗi thành viên nên chuẩn bị:

- Git.
- Docker Desktop và Docker Compose.
- .NET SDK 10.
- Node.js và npm phù hợp project.
- Visual Studio / Visual Studio Code / Rider.

Kiểm tra:

```powershell
git --version
docker --version
docker compose version
dotnet --version
node --version
npm --version
```

---

## 14. Cách lấy và chạy dự án

### 14.1. Clone repository

```powershell
git clone https://github.com/Minh2312688/Nhom9_Culinary_Blog.git
cd Nhom9_Culinary_Blog
```

Nếu đã có repository, lấy code theo branch cần làm việc và quy trình Git của nhóm.

Không tự động merge branch của thành viên khác nếu chưa review thay đổi.

### 14.2. Environment

Nếu có `.env.example`:

```powershell
Copy-Item .env.example .env
```

> Không commit `.env`, JWT Secret, Client Secret hoặc mật khẩu database.

### 14.3. Docker

```powershell
docker compose up -d
docker compose ps
```

Theo dõi log:

```powershell
docker compose logs -f
```

Dừng container nhưng giữ volume:

```powershell
docker compose down
```

> Không chạy `docker compose down -v` nếu chưa chủ động muốn xóa dữ liệu development.

### 14.4. Backend

```powershell
dotnet restore backend/CulinaryBlog.sln
dotnet run --project backend/src/CulinaryBlog.API
```

Build Release:

```powershell
dotnet build backend/CulinaryBlog.sln -c Release
```

Một số endpoint Auth hiện có:

```text
POST /api/v1/auth/register
POST /api/v1/auth/login
POST /api/v1/auth/google
```

### 14.5. Frontend

```powershell
cd frontend
npm ci
npm run dev
```

Development server mặc định của Next.js thường là:

```text
http://localhost:3000
```

Port thực tế có thể được thay đổi theo cấu hình môi trường.

---

## 15. Kiểm thử

### Backend

Build:

```powershell
dotnet build backend/CulinaryBlog.sln -c Release
```

Test toàn bộ solution:

```powershell
dotnet test backend/CulinaryBlog.sln -c Release
```

Các nhóm test:

- Application Tests.
- Architecture Tests.
- Integration Tests.

> Không cố định số lượng test trong README chung vì số lượng có thể thay đổi theo branch và tiến độ tích hợp.

### Frontend

Tùy scripts hiện có trong `frontend/package.json`:

```powershell
npm --prefix frontend run lint
npm --prefix frontend run build
```

Các test E2E / UI được thực hiện theo chức năng và tiến độ từng tuần.

---

## 16. Quy ước làm việc với Git

Mỗi thành viên làm việc trên branch riêng theo nhiệm vụ được giao.

Ví dụ:

```text
2312695-NguyenPhamPhuNam-Auth_Infra
2312695-NguyenPhamPhuNam-Lab3-Data
```

Trước khi chỉnh code:

```powershell
git branch --show-current
git status
git log -3 --oneline
```

Trước khi commit:

```powershell
git status --short --untracked-files=all
git diff --check
git diff --stat
```

Không nên sử dụng:

```powershell
git add .
```

khi commit cần kiểm soát chính xác phạm vi file.

Không commit:

```text
.env
secret
node_modules/
.next/
bin/
obj/
TestResults/
test-results/
playwright-report/
log/runtime data
```

Mỗi commit nên tập trung vào một nhóm thay đổi rõ ràng.

Ví dụ:

```text
feat(auth): implement week 2 auth flows and pages
feat(db): add lab 2 migration and sample data
feat(search): add PostgreSQL full-text recipe search
```

---

## 17. Nguyên tắc tích hợp code giữa các thành viên

Để giảm conflict:

1. Mỗi thành viên ưu tiên chỉnh file thuộc module mình phụ trách.
2. Hạn chế refactor file dùng chung nếu chưa cần.
3. Tính năng mới ưu tiên tạo file riêng.
4. Không tự ý merge branch của thành viên khác.
5. Trước khi merge:
   - build project;
   - chạy test;
   - kiểm tra migration;
   - kiểm tra file thay đổi;
   - kiểm tra secret;
   - xử lý conflict có chủ đích.
6. Không tự thay đổi contract hoặc kiến trúc ảnh hưởng nhiều module nếu chưa thống nhất.
7. Commit cá nhân nên nhỏ, rõ ràng để dễ review / cherry-pick / revert.

---

## 18. Quy tắc Database / Migration khi làm việc nhóm

- Không sửa migration của thành viên khác khi chưa thống nhất.
- Migration mới phải được review trước khi merge.
- Không xóa Docker volume khi chưa xác định dữ liệu cần giữ.
- Không tạo Entity trùng.
- Không tạo nhiều Repository có cùng trách nhiệm.
- Khi cần mở rộng module của người khác, trao đổi trước để thống nhất contract.
- Hạn chế chỉnh DbContext song song.

---

## 19. Quy tắc xử lý yêu cầu và mâu thuẫn SRS

Nhóm lưu tài liệu rà soát SRS tại:

```text
docs/srs-audit/
```

Bao gồm:

```text
SRS-AUDIT.md
SRS-CONFLICTS-AND-DECISIONS.md
TEAM-REQUIREMENT-TRACEABILITY.md
SRS-71-PAGE-MASTER-CHECKLIST.md
```

Khi phát hiện hai phần SRS mâu thuẫn:

- Không tự ý chọn một phương án rồi coi là yêu cầu cuối cùng.
- Ghi nhận conflict.
- Chờ nhóm thống nhất.
- Không thay đổi trạng thái conflict khi chưa có quyết định.

Các trạng thái:

```text
OPEN
DECIDED
IMPLEMENTED
SUPERSEDED
```

---

## 20. Quy tắc Security

- Không commit JWT Secret.
- Không commit `.env`.
- Không commit Client Secret.
- Không hardcode password thực.
- Refresh Token lưu hash thay vì raw token.
- Các endpoint Auth có Rate Limiting.
- Không tiết lộ tài khoản tồn tại hay không thông qua lỗi Login.
- CORS chỉ cho phép origin được cấu hình.
- Secrets Development phải tách khỏi source code.

---

## 21. Checklist trước khi Merge

```text
1. Kiểm tra đúng branch.
2. Kiểm tra changed files.
3. Build Backend.
4. Chạy Tests.
5. Kiểm tra Migration.
6. Kiểm tra Secret.
7. Review conflict.
8. Merge khi các bước trên đều đạt.
```

Ưu tiên tạo file mới cho module cá nhân thay vì sửa file dùng chung nếu không thật sự cần.

Các commit nhỏ và độc lập giúp:

- Review dễ hơn.
- Merge dễ hơn.
- Cherry-pick dễ hơn.
- Revert dễ hơn.

---

## 22. Ghi chú

README được cập nhật theo tiến độ thực tế của project.

Phân công chính thức của nhóm được xác định theo:

- Mục 5 – Phân công cụ thể theo thành viên.
- Mục 6 – Task Matrix.
- Mục 9 – Phân công theo tuần.

Các bài Lab cá nhân hoặc thử nghiệm kỹ thuật không tự động thay đổi ownership của module trong project nhóm.
