# CulinaryBlog Team Requirement Traceability Matrix

> **BẢN QUYỀN TÀI LIỆU:**
> ĐÂY LÀ TÀI LIỆU MA TRẬN TRUY VẾT YÊU CẦU TOÀN DIỆN CHO CẢ 4 THÀNH VIÊN (TV1, TV2, TV3, TV4).
> TÀI LIỆU NẰM TRONG PRIVATE NOTES, **TUYỆT ĐỐI KHÔNG ĐƯỢC ĐƯA LÊN GIT REPOSITORY**.

---

## 1. Phân công trách nhiệm tổng thể 4 thành viên

- **TV1 (Backend Core & Architecture Lead / Auth):**
  - Base Architecture: Clean Architecture 4 layers, Dependency Injection, Configuration.
  - MediatR Behaviors: `LoggingBehavior`, `ValidationBehavior`, Exception Handling Middleware.
  - Authentication & Authorization: `FR-AUTH-001..007`, ASP.NET Core Identity, JWT, Google OAuth.
  - Security & Rate Limiting: `NFR-SEC-001..005`, Rate limiting middleware.
  - Docker Base & Infrastructure Foundation: Multi-stage Dockerfile, docker-compose base.
  - Auth UI State: Next.js client authentication context, Auth.js v5 client integration.

- **TV2 (Recipe Core, Search & Database Lead):**
  - Database & Migrations: EF Core DbContext lead, Fluent API mappings, Migrations management.
  - Recipe Core: `FR-RCP-001..007`, Recipe lifecycle (Draft, Published, Archived), Soft delete vs Hard delete.
  - Recipe Nested Data: `FR-RCP-008..010`, Recipe Steps, Ingredients, Images, Nutrition (per serving).
  - Search & Querying: `FR-SRCH-001..004`, PostgreSQL Full-Text Search (tsvector/unaccent), Filter, Sort, Offset Pagination.
  - Recipe Caching: Output Cache / Redis cache-aside cho Recipe detail.

- **TV3 (Frontend Platform & UI/UX Lead):**
  - Next.js Foundation: Next.js App Router setup, layout, Tailwind CSS design system, responsive breakpoints.
  - Public UI: Home page (`/`), Recipe list (`/recipes`), Recipe detail (`/recipes/[slug]`), Category list (`/categories`).
  - Dashboard & Author UI: Author dashboard (`/dashboard`), Recipe CRUD multi-step wizard (`/dashboard/recipes/new`, edit).
  - UX Attributes: Form validation UI (React Hook Form), Skeleton loading, Toast notifications, Accessibility (WCAG 2.1 AA).
  - SEO & Web Vitals: Core Web Vitals (LCP, CLS, INP), JSON-LD Schema.org Recipe, Open Graph metadata.

- **TV4 (Category, Storage, Background Jobs & Observability):**
  - Category Management: `FR-CAT-001..005`, Category CRUD, slug generation, category caching.
  - Object Storage: `FR-FILE-001..002`, MinIO integration, IFileStorageService, file upload validation (magic bytes, 5MB).
  - Background Jobs: `FR-JOB-001..003`, Hangfire setup, PostgreSQL job queue, Welcome Email, Image thumbnail, Sitemap generation.
  - Observability & Reliability: `FR-OBS-001..003`, Health check endpoints, Serilog structured logging sinks, OpenTelemetry tracing & metrics.

---

## 2. Master Requirement Traceability Matrix

> **Cập nhật 2026-09-23:** Đại diện nhóm đã xác nhận phương án cho toàn bộ CONFLICT-001..025; xem [decision log](SRS-CONFLICTS-AND-DECISIONS.md). `BLOCKED` trong ma trận này hiện chỉ dành cho rủi ro kỹ thuật/phụ thuộc chưa xử lý. Các yêu cầu từng bị chặn riêng bởi conflict chuyển về `IN_PROGRESS`; việc chốt thiết kế không đồng nghĩa đã hoàn thành code.
 (34 FRs + 30 NFRs + 10 CONS)

Quy ước trạng thái:
- `VERIFIED`: Đã hoàn thành và kiểm chứng tự động qua test suite hiện tại.
- `DONE`: Đã triển khai xong code nhưng chưa có test kiểm chứng đầy đủ.
- `IN_PROGRESS`: Đang trong quá trình triển khai theo kế hoạch phase.
- `NOT_STARTED`: Chưa bắt đầu triển khai.
- `BLOCKED`: Bị chặn bởi các mâu thuẫn đặc tả (`CONFLICT`) chưa thống nhất.

| Requirement ID | TV1 | TV2 | TV3 | TV4 | Primary Owner | Dependency | Conflict / Risk | Status |
|---|---|---|---|---|---|---|---|---|
| **FR-AUTH-001** | **X** | | X | X | **TV1** | ASP.NET Identity, MailKit (TV4), Auth UI (TV3) | CONFLICT-010, CONFLICT-018, CONFLICT-011 | **IN_PROGRESS** |
| **FR-AUTH-002** | **X** | | X | | **TV1** | ASP.NET Identity, Login UI (TV3) | TECH-RISK-001 | **NOT_STARTED** |
| **FR-AUTH-003** | **X** | | X | | **TV1** | Google Cloud Console, Next.js Client (TV3) | CONFLICT-013 | **IN_PROGRESS** |
| **FR-AUTH-004** | **X** | | X | | **TV1** | RefreshToken Entity (TV2 DbContext) | CONFLICT-018, TECH-RISK-002 | **BLOCKED** |
| **FR-AUTH-005** | **X** | | X | | **TV1** | CurrentUserService, DbContext (TV2) | None | **NOT_STARTED** |
| **FR-AUTH-006** | **X** | | X | | **TV1** | JWT Middleware, User Profile UI (TV3) | CONFLICT-009 | **IN_PROGRESS** |
| **FR-AUTH-007** | **X** | | X | | **TV1** | ApplicationUser, Profile UI (TV3) | CONFLICT-009, CONFLICT-011 | **IN_PROGRESS** |
| **FR-CAT-001** | | X | X | **X** | **TV4** | DbContext (TV2), Category UI (TV3) | CONFLICT-020 | **IN_PROGRESS** |
| **FR-CAT-002** | | X | X | **X** | **TV4** | Recipe Queries (TV2), Category Detail UI (TV3) | CONFLICT-012 | **IN_PROGRESS** |
| **FR-CAT-003** | | X | X | **X** | **TV4** | Admin Authorization (TV1), DbContext (TV2) | CONFLICT-020 | **IN_PROGRESS** |
| **FR-CAT-004** | | X | X | **X** | **TV4** | Admin Authorization (TV1), DbContext (TV2) | CONFLICT-017 | **IN_PROGRESS** |
| **FR-CAT-005** | | X | X | **X** | **TV4** | Recipe Foreign Key check (TV2) | CONFLICT-002 | **IN_PROGRESS** |
| **FR-RCP-001** | | **X** | X | | **TV2** | Search/Filter Logic, Recipe Card UI (TV3) | CONFLICT-003, CONFLICT-012, CONFLICT-021 | **IN_PROGRESS** |
| **FR-RCP-002** | | **X** | X | | **TV2** | Eager Loading, Redis Cache, Detail UI (TV3) | CONFLICT-005..008, CONFLICT-019 | **IN_PROGRESS** |
| **FR-RCP-003** | | **X** | X | | **TV2** | Author Authorization (TV1), Wizard UI (TV3) | CONFLICT-004..008, TECH-RISK-003 | **BLOCKED** |
| **FR-RCP-004** | | **X** | X | | **TV2** | RowVersion handling, Edit UI (TV3) | CONFLICT-014 | **IN_PROGRESS** |
| **FR-RCP-005** | | **X** | X | | **TV2** | Author-Owner policy (TV1), Dashboard UI (TV3) | None | **NOT_STARTED** |
| **FR-RCP-006** | | **X** | X | | **TV2** | Author-Owner policy (TV1), Dashboard UI (TV3) | None | **NOT_STARTED** |
| **FR-RCP-007** | | **X** | X | X | **TV2** | Hangfire delete image (TV4), UI (TV3) | CONFLICT-001 | **IN_PROGRESS** |
| **FR-RCP-008** | | **X** | X | X | **TV2** | MinIO upload service (TV4), Hangfire thumbnail (TV4) | CONFLICT-023, CONFLICT-024, TECH-RISK-011 | **BLOCKED** |
| **FR-RCP-009** | | **X** | X | | **TV2** | Ingredient form UI (TV3) | CONFLICT-004, CONFLICT-005 | **IN_PROGRESS** |
| **FR-RCP-010** | | **X** | X | | **TV2** | Step form UI (TV3) | CONFLICT-006, CONFLICT-007, CONFLICT-015, TECH-RISK-012 | **BLOCKED** |
| **FR-SRCH-001** | | **X** | X | | **TV2** | PostgreSQL tsvector/unaccent, Search UI (TV3) | CONFLICT-016, TECH-RISK-004 | **BLOCKED** |
| **FR-SRCH-002** | | **X** | X | | **TV2** | Filter panel UI (TV3) | None | **NOT_STARTED** |
| **FR-SRCH-003** | | **X** | X | | **TV2** | Sort dropdown UI (TV3) | CONFLICT-003 | **IN_PROGRESS** |
| **FR-SRCH-004** | | **X** | X | | **TV2** | Pagination component (TV3) | CONFLICT-012 | **IN_PROGRESS** |
| **FR-FILE-001** | | X | X | **X** | **TV4** | MinIO S3 SDK, Image upload form (TV3) | TECH-RISK-006, TECH-RISK-015 | **NOT_STARTED** |
| **FR-FILE-002** | | X | | **X** | **TV4** | MinIO S3 SDK, Hangfire Delete Job (TV4) | None | **NOT_STARTED** |
| **FR-JOB-001** | X | | | **X** | **TV4** | Hangfire Queue, MailKit SMTP, Auth Trigger (TV1) | TECH-RISK-009 | **NOT_STARTED** |
| **FR-JOB-002** | | X | | **X** | **TV4** | ImageSharp/SkiaSharp, MinIO, Recipe Upload (TV2) | None | **NOT_STARTED** |
| **FR-JOB-003** | | X | X | **X** | **TV4** | Published Recipes (TV2), Google Console Ping | None | **NOT_STARTED** |
| **FR-OBS-001** | X | X | | **X** | **TV4** | Postgres Health, Redis Health, MinIO Health | None | **NOT_STARTED** |
| **FR-OBS-002** | **X** | | | X | **TV1 / TV4** | Serilog Sinks, CorrelationIdMiddleware | TECH-RISK-013 | **DONE** (TV1 Middleware base) |
| **FR-OBS-003** | | | | **X** | **TV4** | OpenTelemetry .NET SDK, Seq / Jaeger | None | **NOT_STARTED** |
| **NFR-PERF-001** | X | X | X | | **Toàn đội** | Redis, EF Core Optimization, k6 load test | None | **NOT_STARTED** |
| **NFR-PERF-002** | X | X | X | X | **Toàn đội** | Docker instance specs (2 vCPU, 4GB RAM) | None | **NOT_STARTED** |
| **NFR-PERF-003** | | X | | X | **TV2 / TV4** | Redis distributed cache configuration | CONFLICT-016, CONFLICT-019, CONFLICT-020 | **IN_PROGRESS** |
| **NFR-PERF-004** | | **X** | | | **TV2** | LINQ Projection, Include, B-tree indexes | TECH-RISK-008 | **NOT_STARTED** |
| **NFR-PERF-005** | | | **X** | | **TV3** | Next.js ISR, Image optimization, Lighthouse CI | TECH-RISK-010 | **NOT_STARTED** |
| **NFR-SEC-001** | **X** | | | | **TV1** | ASP.NET Core Identity PBKDF2 configuration | None | **NOT_STARTED** |
| **NFR-SEC-002** | **X** | | | | **TV1** | JWT Provider, RefreshToken TokenHash SHA-256 | CONFLICT-018, TECH-RISK-002 | **BLOCKED** |
| **NFR-SEC-003** | **X** | | | | **TV1** | ASP.NET Core RateLimiting middleware | None | **NOT_STARTED** |
| **NFR-SEC-004** | **X** | X | X | X | **TV1 / TV4** | FluentValidation, Magic bytes reader | CONFLICT-011, TECH-RISK-011 | **BLOCKED** |
| **NFR-SEC-005** | **X** | | X | X | **TV1 / TV4** | Nginx SSL termination, CORS appsettings | None | **NOT_STARTED** |
| **NFR-SEC-006** | **X** | X | | | **TV1** | RecipeAuthorizationHandler (Resource ownership) | None | **NOT_STARTED** |
| **NFR-SEC-007** | **X** | X | X | X | **Toàn đội** | User secrets (dev), Environment variables (prod) | None | **NOT_STARTED** |
| **NFR-USE-001** | | | **X** | | **TV3** | Tailwind CSS responsive utility classes | None | **NOT_STARTED** |
| **NFR-USE-002** | | | **X** | | **TV3** | Semantic HTML5, ARIA, Keyboard navigation | None | **NOT_STARTED** |
| **NFR-USE-003** | X | | **X** | | **TV1 / TV3** | RFC 7807 Problem Details, React Hook Form UI | CONFLICT-011 | **IN_PROGRESS** |
| **NFR-USE-004** | | | **X** | | **TV3** | Skeleton components, Toast notifications | None | **NOT_STARTED** |
| **NFR-REL-001** | | | | **X** | **TV4** | Uptime SLA >= 99.5%, Health check readiness | None | **NOT_STARTED** |
| **NFR-REL-002** | **X** | X | | X | **TV1 / TV4** | Global Exception Handler, Redis fallback | TECH-RISK-005 | **DONE** (TV1 Middleware base) |
| **NFR-REL-003** | | **X** | | X | **TV2 / TV4** | PostgreSQL WAL, automated backup, Soft delete | CONFLICT-001 | **IN_PROGRESS** |
| **NFR-MAINT-001**| **X** | X | X | X | **Toàn đội** | SonarAnalyzer (.NET), ESLint (Next.js) | None | **NOT_STARTED** |
| **NFR-MAINT-002**| **X** | X | X | X | **Toàn đội** | Unit & Integration Test Coverage >= 80% | None | **DONE** (Phase 1 Baseline tests) |
| **NFR-MAINT-003**| **X** | X | | | **TV1** | Swagger / OpenAPI XML comments generation | None | **DONE** (Scalar/OpenAPI setup) |
| **NFR-MAINT-004**| **X** | | | | **TV1** | Clean Architecture, ArchUnit test project | None | **VERIFIED** (Solution structure) |
| **NFR-SCALE-001**| **X** | | | X | **TV1 / TV4** | Stateless Backend, JWT, Redis Distributed Cache | CONFLICT-020 | **IN_PROGRESS** |
| **NFR-SCALE-002**| | **X** | | | **TV2** | Npgsql Connection Pooling, B-tree/GIN indexes | None | **NOT_STARTED** |
| **NFR-SCALE-003**| X | X | X | **X** | **Toàn đội** | Docker Multi-container setup, Nginx reverse proxy | None | **DONE** (Docker Compose base) |
| **NFR-SEO-001** | | | **X** | | **TV3** | JSON-LD Schema.org Recipe structured data | None | **NOT_STARTED** |
| **NFR-SEO-002** | | | **X** | | **TV3** | Open Graph, Twitter Cards, dynamic meta tags | None | **NOT_STARTED** |
| **NFR-SEO-003** | | | X | **X** | **TV4** | Sitemap.xml generation (FR-JOB-003), robots.txt | None | **NOT_STARTED** |
| **NFR-SEO-004** | | X | **X** | | **TV2 / TV3** | Slug-based URLs (/recipes/{slug}), 301 redirects | TECH-RISK-003 | **NOT_STARTED** |
| **CONS-001** | **X** | | | | **TV1** | Clean Architecture 4 layers, Domain independent | None | **VERIFIED** |
| **CONS-002** | **X** | | | | **TV1** | CQRS + MediatR pipeline behaviors | None | **VERIFIED** |
| **CONS-003** | **X** | | **X** | | **TV1 / TV3** | .NET 10 Minimal APIs, Next.js App Router | None | **VERIFIED** (Backend base) |
| **CONS-004** | **X** | | | | **TV1** | JWT stateless (15m/7d), PBKDF2 Identity | CONFLICT-018 | **BLOCKED** |
| **CONS-005** | **X** | | | | **TV1** | RESTful design, RFC 7807, URL path versioning | CONFLICT-011 | **IN_PROGRESS** |
| **CONS-006** | | **X** | | | **TV2** | PostgreSQL only, EF Core Code-First migrations | None | **NOT_STARTED** |
| **CONS-007** | | | | **X** | **TV4** | File upload max 5MB, MIME/magic bytes check | TECH-RISK-011, TECH-RISK-015 | **NOT_STARTED** |
| **CONS-008** | **X** | | | | **TV1** | FluentValidation via MediatR ValidationBehavior | None | **VERIFIED** |
| **CONS-009** | **X** | | | | **TV1** | Docker multi-stage build, Docker Compose | None | **VERIFIED** |
| **CONS-010** | **X** | | | | **TV1** | Serilog structured logging (CorrelationId, UserId) | TECH-RISK-013 | **VERIFIED** |

---

## 3. Bảng phân công chi tiết theo từng thành viên

### 3.1. TV1 Traceability (Backend Core & Architecture / Auth)
- **Phạm vi (Scope):** Nền tảng kiến trúc Backend Clean Architecture, CQRS Pipeline, Quản lý định danh (Identity & Auth), Bảo mật API và Giới hạn tốc độ (Rate Limiting).
- **FR Owned:** `FR-AUTH-001` .. `FR-AUTH-007` (7 Functional Requirements).
- **NFR Owned:** `NFR-SEC-001`, `NFR-SEC-002`, `NFR-SEC-003`, `NFR-SEC-004`, `NFR-SEC-005`, `NFR-SEC-006`, `NFR-SEC-007`, `NFR-MAINT-003`, `NFR-MAINT-004`, `NFR-SCALE-001`.
- **CONS Affected:** `CONS-001`, `CONS-002`, `CONS-003`, `CONS-004`, `CONS-005`, `CONS-008`, `CONS-009`, `CONS-010`.
- **Entities Managed:** `ApplicationUser`, `RefreshToken`, `BaseEntity`.
- **Endpoints:** `/api/v1/auth/register`, `/api/v1/auth/login`, `/api/v1/auth/google`, `/api/v1/auth/refresh`, `/api/v1/auth/logout`, `/api/v1/auth/me`, `PATCH /api/v1/auth/me`.
- **Dependencies:** TV2 (ApplicationDbContext integration), TV3 (Auth UI screens), TV4 (Hangfire Welcome Email trigger).
- **Conflicts liên quan:** `CONFLICT-009`, `CONFLICT-010`, `CONFLICT-011`, `CONFLICT-013`, `CONFLICT-018`, `CONFLICT-025` (Cross-team với TV2).
- **Technical Risks:** `TECH-RISK-001` (Lockout flow), `TECH-RISK-002` (TokenHash lookup), `TECH-RISK-013` (CorrelationId forwarding), `TECH-RISK-014` (Google OAuth claims).
- **Phase 2 follow-up work (conflict decisions settled):**
  1. `CONFLICT-009`: Tên người dùng (`FullName` + `UserName` vs `DisplayName` + `Bio`).
  2. `CONFLICT-010`: Response đăng ký (Kèm Tokens vs Chỉ User info).
  3. `CONFLICT-011`: Validation HTTP Status (`422` vs `400`).
  4. `CONFLICT-013`: Google OAuth Flow (ExternalLoginInfo / Code+PKCE vs Client idToken).
  5. `CONFLICT-018`: Refresh Token entropy (512-bit vs 128-bit SHA-256).
- **Deliverables Phase 1 (Đã hoàn thành):** Solution architecture 4 layers, MediatR Pipeline, LoggingBehavior, ValidationBehavior, Dockerfile multi-stage, Unit tests.
- **Test Expectations:** Unit tests cho Auth handlers, Token generation tests, Integration tests cho đăng nhập/đăng ký/refresh token, Rate limiting tests.

### 3.2. TV2 Traceability (Recipe Core, Search & Database)
- **Phạm vi (Scope):** Nghiệp vụ cốt lõi công thức nấu ăn (Recipe CRUD, Steps, Ingredients, Images, Nutrition), Quản trị CSDL (DbContext, Migrations, Indexing), Tìm kiếm và lọc phân trang.
- **FR Owned:** `FR-RCP-001` .. `FR-RCP-010` (10 FRs) và `FR-SRCH-001` .. `FR-SRCH-004` (4 FRs).
- **NFR Owned:** `NFR-PERF-001`, `NFR-PERF-003`, `NFR-PERF-004`, `NFR-REL-003`, `NFR-SCALE-002`, `NFR-SEO-004`.
- **CONS Affected:** `CONS-006` (Postgres / EF Core), `CONS-002` (CQRS), `CONS-008` (FluentValidation).
- **Entities Managed:** `Recipe`, `RecipeNutrition` (Owned 1-1), `RecipeStep`, `RecipeIngredient`, `RecipeImage`.
- **Endpoints:** `/api/v1/recipes` (GET, POST), `/api/v1/recipes/{slug}` (GET), `/api/v1/recipes/{id}` (PUT, DELETE), `/api/v1/recipes/{id}/publish` (PATCH), `/api/v1/recipes/{id}/unpublish` (PATCH), `/api/v1/recipes/{id}/archive` (PATCH), `/api/v1/recipes/{id}/images` (POST, PATCH, DELETE), `/api/v1/recipes/{id}/steps` (POST, PUT, DELETE), `/api/v1/recipes/{id}/ingredients` (POST, PUT, DELETE).
- **Dependencies:** TV1 (AuthorId từ CurrentUserService, Auth policies), TV4 (MinIO Image Storage, Hangfire Image Cleanup Job).
- **Conflicts liên quan:** `CONFLICT-001`, `CONFLICT-003..008`, `CONFLICT-012`, `CONFLICT-014..016`, `CONFLICT-019`, `CONFLICT-021`, `CONFLICT-023`, `CONFLICT-024`, `CONFLICT-025`.
- **Technical Risks:** `TECH-RISK-003` (Slug collision), `TECH-RISK-004` (Postgres unaccent extension), `TECH-RISK-008` (N+1 queries prevention), `TECH-RISK-012` (Step renumbering race).
- **Implementation follow-up (conflict decisions settled):**
  1. `CONFLICT-001`: Chiến lược xóa Recipe (Hard delete vs Soft delete `IsDeleted`).
  2. `CONFLICT-008`: Số lượng chỉ số dinh dưỡng (4 fields vs 6 fields; Calculation scope: PER SERVING).
  3. `CONFLICT-014`: Concurrency RowVersion HTTP status (`409` vs `422`).
  4. `CONFLICT-019`: Cache Recipe detail (Output Cache 60m vs Redis 5m).
  5. `CONFLICT-021`: Quyền xem bài viết của Author (Có thấy Archived của mình hay không).
- **Test Expectations:** Unit tests cho Recipe handlers, EF Core in-memory/sqlite tests cho CRUD, Integration tests cho FTS PostgreSQL, Concurrency conflict tests.

### 3.3. TV3 Traceability (Frontend Platform & UI/UX)
- **Phạm vi (Scope):** Toàn bộ giao diện người dùng trên nền Next.js App Router (13 màn hình/routes), State management, Form validation UX, Tối ưu SEO và Core Web Vitals.
- **FR Supported:** Toàn bộ giao diện tương ứng 34 FRs (Auth screens, Category pages, Recipe browsing/detail/wizard, Search UI, Author dashboard).
- **NFR Owned:** `NFR-PERF-005` (Core Web Vitals), `NFR-USE-001` (Responsive), `NFR-USE-002` (Accessibility a11y), `NFR-USE-003` (Error UX), `NFR-USE-004` (Loading states), `NFR-SEO-001` (JSON-LD), `NFR-SEO-002` (Meta / Open Graph).
- **CONS Affected:** `CONS-003` (Next.js App Router, không Pages Router).
- **Routes Managed:** `/`, `/recipes`, `/recipes/[slug]`, `/categories`, `/categories/[slug]`, `/auth/login`, `/auth/register`, `/dashboard`, `/dashboard/recipes`, `/dashboard/recipes/new`, `/dashboard/recipes/[id]/edit`, `/profile`, `/search`.
- **Dependencies:** TV1 (API Contracts Auth & Tokens), TV2 (API Contracts Recipes & Search), TV4 (API Contracts Categories & Image URLs).
- **Conflicts liên quan:** `CONFLICT-003` (Sorting query string), `CONFLICT-009` (Display name), `CONFLICT-010` (Register redirect vs auto-login), `CONFLICT-012` (Pagination data shape), `CONFLICT-013` (Google OAuth login flow), `CONFLICT-022` (Browser version matrix).
- **Technical Risks:** `TECH-RISK-010` (Next.js standalone build & runtime environment variables).
- **Implementation follow-up (conflict decisions settled):**
  1. `CONFLICT-012`: Cấu trúc phân trang (Flat shape `{ items, totalCount }` vs Nested shape `{ data, meta }`).
  2. `CONFLICT-010`: Luồng sau khi đăng ký (Chuyển sang login hay lưu token vào session).
  3. `CONFLICT-022`: Ngưỡng hỗ trợ trình duyệt (Chrome 90+ hay Chrome 112+).
- **Test Expectations:** Component unit tests (Jest/React Testing Library), E2E smoke tests (Playwright), Lighthouse CI audit (LCP <= 2.5s, CLS <= 0.1, INP <= 200ms).

### 3.4. TV4 Traceability (Category, Storage, Jobs & Observability)
- **Phạm vi (Scope):** Phân hệ Quản lý Danh mục (Category), Dịch vụ lưu trữ tệp tin đối tượng (MinIO), Hệ thống tác vụ nền (Hangfire), và Nền tảng giám sát hệ thống (Observability).
- **FR Owned:** `FR-CAT-001..005` (5 FRs), `FR-FILE-001..002` (2 FRs), `FR-JOB-001..003` (3 FRs), `FR-OBS-001..003` (3 FRs).
- **NFR Owned:** `NFR-REL-001` (Uptime SLA 99.5%), `NFR-REL-002` (Error resilience & retry), `NFR-SEO-003` (Sitemap & Robots).
- **CONS Affected:** `CONS-007` (File upload max 5MB, MIME/magic bytes), `CONS-009` (Docker containers: MinIO, Redis, Seq, Mailhog), `CONS-010` (Serilog).
- **Entities Managed:** `Category`.
- **Endpoints:** `/api/v1/categories` (GET, POST), `/api/v1/categories/{slug}` (GET), `/api/v1/categories/{id}` (PUT, DELETE), `/health`, `/health/live`, `/health/ready`.
- **Dependencies:** TV1 (Admin authorization, Welcome Email trigger), TV2 (Recipe foreign key constraints, Recipe images upload/cleanup).
- **Conflicts liên quan:** `CONFLICT-002` (Category Hard vs Soft delete), `CONFLICT-017` (Category update fields), `CONFLICT-020` (Category cache IMemoryCache vs Redis).
- **Technical Risks:** `TECH-RISK-006` (MinIO host resolution), `TECH-RISK-007` (Hangfire Dashboard security filter), `TECH-RISK-009` (Hangfire complex object serialization), `TECH-RISK-011` (Magic bytes stream validation), `TECH-RISK-015` (Nginx client_max_body_size).
- **Implementation follow-up (conflict decisions settled):**
  1. `CONFLICT-002`: Xóa danh mục dùng Hard Delete hay Soft Delete.
  2. `CONFLICT-020`: Công nghệ cache danh mục (`IMemoryCache` 1h vs `Redis` 30m).
  3. `CONFLICT-017`: Các trường cho phép cập nhật khi sửa danh mục (chỉ Name+Desc hay cả ImageUrl+OrderIndex).
- **Test Expectations:** Unit tests cho Category handlers và File validation, Integration tests với MinIO test container, Hangfire job execution verification tests.

---

## 4. Ma trận phụ thuộc chéo giữa các thành viên (Cross-Member Dependencies Matrix)

> **QUY TẮC QUẢN TRỊ ĐA THÀNH VIÊN:**
> Quản lý tập trung **4 CROSS-TEAM BLOCKER GROUPS covering 6 conflict IDs: 001, 002, 012, 019, 020, 025**:
> - **Group 1: Delete Semantics (`CONFLICT-001`, `CONFLICT-002`):** TV2 (Recipe) + TV4 (Category) phối hợp TV3 (Frontend UI filtering).
> - **Group 2: Pagination Data Shape (`CONFLICT-012`):** TV2 (Recipe) + TV4 (Category) + TV1 (Audit) phối hợp TV3 (Client Envelope fetching).
> - **Group 3: Caching Architecture & Invalidation (`CONFLICT-019`, `CONFLICT-020`):** TV2 (Recipe Cache owner) + TV4 (Category Cache owner).
> - **Group 4: BaseEntity vs Identity Inheritance (`CONFLICT-025`):** TV1 (Auth Entity) + TV2 (DbContext & Migrations).

| Thành viên cung cấp | Dịch vụ / Artifact bàn giao | Thành viên tiếp nhận | Bản chất phụ thuộc | Rủi ro & Điểm nghẽn cần giải quyết |
| :--- | :--- | :--- | :--- | :--- |
| **TV2 (DB Lead)** | `ApplicationDbContext` & Model Configurations | **TV1 (Auth)** | TV1 cần cấu hình IdentityUser, RefreshToken và DbSet vào DbContext chung; phụ thuộc `CONFLICT-025` (Group 4) | Hai bên cần thống nhất việc kế thừa BaseEntity cho ApplicationUser trước khi sinh migration schema chung. |
| **TV1 (Auth)** | Token Pair & Auth API Contracts | **TV3 (Frontend)** | TV3 cần API đăng nhập, đăng ký và refresh token để quản lý auth state | Bị chặn bởi `CONFLICT-010` và `CONFLICT-013`. Cần thống nhất trước khi code client auth. |
| **TV4 (Category)**| Category DTOs & Queries | **TV2 (Recipe)** | TV2 cần hiển thị thông tin danh mục trong Recipe Detail và lọc theo CategoryId | Bị chặn bởi `CONFLICT-020` (TV4 Category Cache owner; cần chốt cơ chế cache Redis để TV2 đọc nhất quán). |
| **TV2 (Recipe)** | Recipe Detail & Search API Contracts | **TV3 (Frontend)** | TV3 render trang chi tiết công thức, thanh tìm kiếm và bộ lọc đa tiêu chí | Bị chặn bởi `CONFLICT-003` (Sorting), `CONFLICT-012` (Group 2 Pagination shape) và `CONFLICT-021` (Recipe Author visibility). |
| **TV4 (Storage)** | MinIO Upload Presigned / Upload API | **TV2 (Recipe) & TV3 (UI)** | Upload ảnh món ăn và avatar người dùng | Bị chặn bởi `CONFLICT-023` (Route đặt primary) và `CONFLICT-024` (Response shape upload: `{ url, isPrimary }` vs full metadata). |
| **TV2 (Recipe)** | Recipe Detail Cache | **TV4 (Category)** | Đồng bộ cache khi danh mục bị đổi tên/xóa | Bị chặn bởi `CONFLICT-019` (TV2 Recipe Cache owner) và `CONFLICT-020` (TV4 Category Cache owner). |

## 5. Kế hoạch kiểm thử truy vết toàn hệ thống (Test Traceability Matrix)

| Yêu cầu kiểm thử | Loại kiểm thử (Test Type) | Thành viên phụ trách | Tiêu chuẩn chấp nhận (Acceptance Criteria) |
|---|---|---|---|
| **Clean Architecture Rules** | Architecture Test (ArchUnit.NET) | **TV1** | Domain không phụ thuộc tầng nào; Application chỉ phụ thuộc Domain; không reference ngược. Pass 100%. |
| **Pipeline Behaviors** | Unit Test (xUnit / Moq / FluentAssertions) | **TV1** | LoggingBehavior log đúng time; ValidationBehavior throw ValidationException khi validator fail. |
| **Auth Commands & Handlers** | Unit Test | **TV1** | Hash đúng mật khẩu, sinh JWT claims đúng, token rotation revoke token cũ, reuse detection hoạt động. |
| **Auth API Endpoints** | Integration Test (WebApplicationFactory) | **TV1** | Đăng ký thành công trả 201; đăng nhập sai 5 lần bị khóa 15 phút; refresh token thành công trả 200. |
| **Recipe CRUD & State Machine** | Unit Test | **TV2** | Chuyển trạng thái Draft -> Published -> Archived đúng business rules; không publish nếu thiếu step/ingredient. |
| **Recipe Concurrency Control** | Integration Test (PostgreSQL Testcontainer) | **TV2** | Hai request cập nhật cùng recipe với RowVersion cũ: 1 thành công, 1 nhận HTTP 409 Conflict. |
| **Full-Text Search Accuracy** | Integration Test (PostgreSQL) | **TV2** | Tìm kiếm không dấu tiếng Việt (unaccent) trả đúng kết quả; rank theo ts_rank_cd chính xác. |
| **Category CRUD & Slug Generation** | Unit & Integration Test | **TV4** | Tên có dấu sinh slug không dấu chuẩn; trùng tên tự động thêm suffix `-1`, `-2`; xóa khi còn recipe trả lỗi 400. |
| **File Storage & Magic Bytes** | Unit & Integration Test (MinIO Testcontainer) | **TV4** | Đổi đuôi file .exe thành .jpg bị chặn bởi magic bytes check; upload ảnh hợp lệ lưu đúng bucket, max 5MB. |
| **Hangfire Background Execution** | Integration Test | **TV4** | Welcome email enqueue thành công; retry 3 lần khi SMTP timeout; job không thất bại ngầm. |
| **Health Checks Endpoints** | Integration Test | **TV4** | `/health/live` luôn trả 200; `/health/ready` trả 503 khi DB hoặc Redis tắt; trả 200 khi cả hai hoạt động. |
| **Frontend Responsive & Layout** | Visual & Component Test | **TV3** | Hiển thị đúng trên 320px (mobile), 768px (tablet), 1200px (desktop); không vỡ layout. |
| **Accessibility Compliance** | Automated a11y Test (axe-core / Lighthouse) | **TV3** | WCAG 2.1 Level AA: Color contrast >= 4.5:1; đầy đủ ARIA labels; bàn phím điều hướng được 100%. |
| **Core Web Vitals Performance** | Lighthouse CI / PageSpeed | **TV3** | LCP <= 2.5s, CLS <= 0.1, INP <= 200ms, First Load JS Bundle <= 200KB (gzipped). |
| **Structured Data SEO Markup** | Google Rich Results Test | **TV3** | Schema.org Recipe JSON-LD pass 100% không warning/error; sinh rich snippet trên công cụ test. |
| **System Load & Throughput** | Load Test (k6) | **Toàn đội** | >= 100 concurrent users trên 2 vCPU / 4GB RAM; p50 <= 150ms, p95 <= 500ms; error rate < 1%. |
