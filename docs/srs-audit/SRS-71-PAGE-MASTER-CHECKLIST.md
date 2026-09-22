# CulinaryBlog SRS 71-Page Master Checklist

> **BẢN QUYỀN TÀI LIỆU:**
> ĐÂY LÀ BẢNG KIỂM TRA TOÀN DIỆN 71/71 TRANG CỦA SRS v1.0.0 DÀNH CHO CẢ NHÓM (TV1, TV2, TV3, TV4).
> TÀI LIỆU NẰM TRONG PRIVATE NOTES, **TUYỆT ĐỐI KHÔNG ĐƯỢC ĐƯA LÊN GIT REPOSITORY**.

---

## 1. Bảng kiểm tra chi tiết từng trang (Page 1 → Page 71)

| Page | Section | Nội dung chính | Requirements / Rules | Conflict / Risk | Owner | Audited |
|---|---|---|---|---|---|---|
| **Page 01** | Bìa tài liệu | Giáo trình Phát triển Ứng dụng Web Nâng cao V4 (.NET 10 + Next.js App Router). Đặc tả Yêu cầu Phần mềm (SRS) v1.0.0 Culinary Blog. | Tiêu chuẩn đồ án môn học, định danh dự án | None | Toàn đội | **YES** |
| **Page 02** | Lịch sử thay đổi tài liệu | Bảng phiên bản: v0.1 (Draft), v0.5 (Internal Review), v1.0.0 (Official Release 12/2024). | Quy trình quản lý phiên bản tài liệu | None | Toàn đội | **YES** |
| **Page 03** | Mục lục (Phần I) | Mục lục chi tiết: Lịch sử, Chương 1 (Giới thiệu), Chương 2 (Tổng quan), Chương 3 (FR-AUTH-001..007, FR-CAT-001..003). | Cấu trúc phân cấp tài liệu phần đầu | None | Toàn đội | **YES** |
| **Page 04** | Mục lục (Phần II) | Mục lục chi tiết: FR-CAT-004..005, FR-RCP-001..010, FR-SRCH-001..004, FR-FILE, FR-JOB, FR-OBS, Chương 4 (NFR), Chương 5, Chương 6. | Cấu trúc các module nghiệp vụ và kiến trúc | None | Toàn đội | **YES** |
| **Page 05** | Mục lục (Phần III) | Mục lục chi tiết: Chương 7 (Data Model 7.1..7.8), Chương 8 (API 8.1..8.8), Phụ lục A, B, C. | Cấu trúc tầng dữ liệu, API và phụ lục tra cứu | None | Toàn đội | **YES** |
| **Page 06** | Chương 1: 1.1 & 1.2 | 1.1 Mục đích tài liệu; 1.2 Phạm vi dự án (Xây dựng Web blog ẩm thực chia sẻ công thức, tương tác cộng đồng tác giả). | Mục tiêu hệ thống, đối tượng phục vụ chính | None | Toàn đội | **YES** |
| **Page 07** | Chương 1: 1.2, 1.3 & 1.4 | 1.2 Phạm vi chi tiết; 1.3 Ngoài phạm vi (Out of scope: E-commerce, thanh toán online, native mobile app, đặt bàn, chat realtime); 1.4 Thuật ngữ (Phần 1: API, JWT, CORS, FTS, SPA, SSR, ISR). | Ranh giới phạm vi hệ thống | None | Toàn đội | **YES** |
| **Page 08** | Chương 1: 1.4 & 1.5 | 1.4 Thuật ngữ (Phần 2: LCP, CLS, INP, OWASP, ACID, CQRS, MediatR); 1.5 Cấu trúc tài liệu (Tóm tắt các chương). | Quy ước thuật ngữ kỹ thuật | INCONSISTENCY: Chương 1.5 ghi 27 FR vs 34 explicit FR ở Chương 3 | Toàn đội | **YES** |
| **Page 09** | Chương 1: 1.6 | 1.6 Tài liệu tham khảo và Tiêu chuẩn (Mục 1-10: RFC 7807, RFC 7519 JWT, OWASP Top 10, WCAG 2.1 AA, Clean Architecture, Next.js, .NET 10, PostgreSQL). | Tiêu chuẩn kỹ thuật quốc tế và framework | None | Toàn đội | **YES** |
| **Page 10** | Chương 1: 1.6 | 1.6 Tiếp tục tài liệu tham khảo (Mục 11-20: FluentValidation, Serilog, Hangfire, MinIO S3 API, OpenTelemetry, Redis, Docker docs). | Thư viện và công cụ mã nguồn mở | None | Toàn đội | **YES** |
| **Page 11** | Chương 2: 2.1 | 2.1 Bối cảnh sản phẩm; 2.1.1 Vị trí hệ sinh thái (Web responsive, SEO platform); 2.1.2 Mục tiêu nghiệp vụ (Tìm kiếm < 5s, >= 100 concurrent users, uptime 99.5%). | Chỉ số mục tiêu kinh doanh và trải nghiệm | None | Toàn đội | **YES** |
| **Page 12** | Chương 2: 2.2 | 2.2 Các hệ thống bên ngoài và Giao tiếp (PostgreSQL 16, Redis 7, MinIO S3, Google OAuth 2.0, SMTP MailKit, Hangfire, Serilog/Seq, Nginx). | Danh mục tích hợp dịch vụ phụ trợ | None | Toàn đội | **YES** |
| **Page 13** | Chương 2: 2.3 & 2.4.1 | 2.3 Người dùng và Vai trò (Guest, Author, Admin; 3 tầng phân quyền: Role, Resource, Policy); 2.4.1 Môi trường server (Ubuntu 22.04 LTS, .NET 10, Node 20/22, Postgres 16 unaccent/pg_trgm, Redis 7, MinIO). | Ma trận phân quyền tác nhân và cấu hình production | None | Toàn đội | **YES** |
| **Page 14** | Chương 2: 2.4.2, 2.4.3 & 2.5 | 2.4.2 Môi trường phát triển (.NET 10 SDK, Node 20+, Docker Desktop); 2.4.3 Yêu cầu trình duyệt client (Chrome 90+, Firefox 88+, Edge 90+, Safari 14+); 2.5 Ràng buộc thiết kế (CONS-001..CONS-005). | CONS-001 (Clean Arch), CONS-002 (CQRS), CONS-003 (.NET 10/Next.js App), CONS-004 (JWT), CONS-005 (REST/RFC7807) | CONFLICT-022 (Browser Chrome 90+ vs 112+ ở p.49) | Toàn đội | **YES** |
| **Page 15** | Chương 2: 2.5 & 2.6.1 | 2.5 Ràng buộc thiết kế tiếp tục (CONS-006..CONS-010); 2.6.1 Giả định (Internet, Docker local, seed Bogus 50 recipe mẫu). | CONS-006 (Postgres EF Core), CONS-007 (Upload 5MB/magic bytes), CONS-008 (FluentValidation), CONS-009 (Docker multi-stage), CONS-010 (Serilog) | None | Toàn đội | **YES** |
| **Page 16** | Chương 2: 2.6.2 | 2.6.2 Bảng mức độ ảnh hưởng phụ thuộc (Postgres, Redis, MinIO, Next.js, Google OAuth, Identity, Hangfire, Serilog); Kế hoạch dự phòng khi phụ thuộc lỗi. | Chiến lược phục hồi và chịu lỗi phụ thuộc ngoài | TECH-RISK-005 (Redis fallback database) | Toàn đội | **YES** |
| **Page 17** | Chương 3: Giới thiệu & 3.1 | Giới thiệu Chương 3 (Quy ước MoSCoW M/S/C/W); 3.1 Module Xác thực (FR-AUTH-001 Đăng ký tài khoản: Mục đích, Actor Guest, MoSCoW M, Bắt đầu Luồng chính bước 1-6). | FR-AUTH-001 (Đăng ký tài khoản) | None | TV1 | **YES** |
| **Page 18** | Chương 3: 3.1 FR-AUTH-001 | 3.1 FR-AUTH-001 Tiếp tục: Luồng chính bước 7-12 (gán role Author, sinh access token 15m, refresh token 512-bit, lưu DB, enqueue WelcomeEmailJob, trả token pair); Luồng thay thế A1-A4 (409, 422, 500). | FR-AUTH-001 luồng chính & ngoại lệ | CONFLICT-010 (Token pair vs User info), CONFLICT-018 (512-bit vs 128-bit entropy) | TV1 / TV4 | **YES** |
| **Page 19** | Chương 3: 3.1 FR-AUTH-002 | 3.1 FR-AUTH-002 Đăng nhập tài khoản: Mục đích, Actor, Điều kiện tiên quyết, Luồng chính bước 1-7 (kiểm tra password, trả token pair); Luồng thay thế A1 (Sai pass: tăng failed count, khóa 15m sau 5 lần), A2 (Chưa active), A3 (Bị ban). | FR-AUTH-002 (Đăng nhập, Lockout 15 phút) | TECH-RISK-001 (Identity Lockout AccessFailedCount) | TV1 | **YES** |
| **Page 20** | Chương 3: 3.1 FR-AUTH-003 & 004 | 3.1 FR-AUTH-003 Đăng nhập Google OAuth (nhận ExternalLoginInfo, auto link/create, trả token pair); 3.1 FR-AUTH-004 Làm mới Access Token (Bắt đầu luồng token refresh). | FR-AUTH-003 (Google OAuth), FR-AUTH-004 (Refresh Token) | CONFLICT-013 (Google OAuth ExternalLoginInfo vs Code+PKCE vs idToken) | TV1 / TV3 | **YES** |
| **Page 21** | Chương 3: 3.1 FR-AUTH-004 & 005 | 3.1 FR-AUTH-004 Tiếp tục: Luồng chính bước 3-8 (Token Rotation, Reuse Detection thu hồi toàn bộ token family); 3.1 FR-AUTH-005 Đăng xuất (Thu hồi token trong DB). | FR-AUTH-004 (Rotation & Reuse Detection), FR-AUTH-005 (Đăng xuất) | TECH-RISK-002 (SHA-256 TokenHash lookup) | TV1 | **YES** |
| **Page 22** | Chương 3: 3.1 FR-AUTH-005, 006 & 007 | 3.1 FR-AUTH-005 Kết thúc; 3.1 FR-AUTH-006 Xem thông tin cá nhân /auth/me (trả về UserProfileDto); 3.1 FR-AUTH-007 Cập nhật thông tin cá nhân (Bắt đầu). | FR-AUTH-006 (Xem Profile), FR-AUTH-007 (Sửa Profile) | CONFLICT-009 (FullName/UserName vs DisplayName/Bio) | TV1 | **YES** |
| **Page 23** | Chương 3: 3.1 & 3.2 | 3.1 FR-AUTH-007 Kết thúc (cấm đổi Email/Role, validate DisplayName 2-50, Bio max 500); 3.2 Module Quản lý Danh mục (Giới thiệu FR-CAT; Bắt đầu FR-CAT-001). | FR-AUTH-007 hoàn tất, khởi đầu FR-CAT | CONFLICT-011 (HTTP 422 vs 400 validation status) | TV1 / TV4 | **YES** |
| **Page 24** | Chương 3: 3.2 FR-CAT-001 & 002 | 3.2 FR-CAT-001 Xem danh sách Danh mục (Cache IMemoryCache 'categories:all' TTL 60m, query DB kèm recipeCount); 3.2 FR-CAT-002 Xem chi tiết Danh mục (Bắt đầu). | FR-CAT-001 (Danh sách danh mục), FR-CAT-002 (Chi tiết danh mục) | CONFLICT-020 (IMemoryCache 1h vs Redis 30m) | TV4 | **YES** |
| **Page 25** | Chương 3: 3.2 FR-CAT-002 & 003 | 3.2 FR-CAT-002 Tiếp tục (Query recipes theo categoryId, phân trang); 3.2 FR-CAT-003 Tạo Danh mục Mới [Admin] (Validate Name 2-50, sinh slug duy nhất, xóa cache IMemoryCache). | FR-CAT-002 (Phân trang), FR-CAT-003 (Tạo danh mục Admin) | CONFLICT-012 (Flat vs Nested pagination), CONFLICT-020 | TV4 | **YES** |
| **Page 26** | Chương 3: 3.2 FR-CAT-003, 004 & 005 | 3.2 FR-CAT-003 Luồng thay thế (403, 409, 422); 3.2 FR-CAT-004 Cập nhật Danh mục [Admin] (Chỉ cho phép sửa Name và Description); 3.2 FR-CAT-005 Xóa Danh mục [Admin] (Mô tả: Hard delete bản ghi). | FR-CAT-004 (Sửa danh mục), FR-CAT-005 (Xóa danh mục) | CONFLICT-017 (Allowed update fields), CONFLICT-002 (Hard vs Soft Delete) | TV4 | **YES** |
| **Page 27** | Chương 3: 3.2 & 3.3 | 3.2 FR-CAT-005 Tiếp tục (Chặn xóa nếu còn Recipe liên kết -> 400); 3.3 Module Quản lý Công thức Nấu ăn (Giới thiệu FR-RCP; Bắt đầu FR-RCP-001). | FR-CAT-005 hoàn tất, khởi đầu FR-RCP | None | TV4 / TV2 | **YES** |
| **Page 28** | Chương 3: 3.3 FR-RCP-001 & 002 | 3.3 FR-RCP-001 Xem danh sách Công thức (Mô tả Author thấy Draft/Archived của mình; Luồng chính Happy Path bước 4 chỉ lọc Published hoặc Draft của Author); 3.3 FR-RCP-002 Xem Chi tiết Công thức (Output Cache policy 'RecipeDetail' TTL 60m). | FR-RCP-001 (Danh sách recipe), FR-RCP-002 (Chi tiết recipe) | CONFLICT-021 (Author Visibility), CONFLICT-019 (Output Cache 60m vs Redis 5m), CONFLICT-003, CONFLICT-012 | TV2 | **YES** |
| **Page 29** | Chương 3: 3.3 FR-RCP-002 & 003 | 3.3 FR-RCP-002 Tiếp tục (Eager loading: Steps, Ingredients, Images, Category, Author, Nutrition; check quyền Draft/Archived); 3.3 FR-RCP-003 Tạo Công thức Mới [Author/Admin] (Bắt đầu: tạo Draft, sinh slug duy nhất). | FR-RCP-002 chi tiết, FR-RCP-003 (Tạo recipe) | TECH-RISK-003 (Slug collision resolution) | TV2 | **YES** |
| **Page 30** | Chương 3: 3.3 FR-RCP-003 & 004 | 3.3 FR-RCP-003 Tiếp tục (Thêm nested Steps, Ingredients, Nutrition 4 trường; validate Quantity > 0, Unit required); 3.3 FR-RCP-004 Cập nhật Công thức [Author-Owner/Admin] (Bắt đầu). | FR-RCP-003 nested data, FR-RCP-004 (Cập nhật recipe) | CONFLICT-004 (Quantity/Unit validation), CONFLICT-008 (4 vs 6 nutrition fields) | TV2 | **YES** |
| **Page 31** | Chương 3: 3.3 FR-RCP-004 & 005 | 3.3 FR-RCP-004 Tiếp tục (Cập nhật thông tin, steps, ingredients; kiểm tra RowVersion concurrency: nếu mismatch -> ném ConcurrencyConflictException -> 409 Conflict); 3.3 FR-RCP-005 Xuất bản / Hủy xuất bản (Bắt đầu). | FR-RCP-004 (RowVersion Concurrency), FR-RCP-005 (Publish/Unpublish) | CONFLICT-014 (RowVersion HTTP 409 vs 422) | TV2 | **YES** |
| **Page 32** | Chương 3: 3.3 FR-RCP-005, 006 & 007 | 3.3 FR-RCP-005 Hoàn tất (Điều kiện publish: >= 1 step, >= 1 ingredient); 3.3 FR-RCP-006 Lưu trữ Công thức (Archive); 3.3 FR-RCP-007 Xóa Công thức [Author-Owner/Admin] (Mô tả: Hard delete và cascade). | FR-RCP-005 (Publish), FR-RCP-006 (Archive), FR-RCP-007 (Xóa recipe) | CONFLICT-001 (Hard delete vs Soft delete) | TV2 | **YES** |
| **Page 33** | Chương 3: 3.3 FR-RCP-007 & 008 | 3.3 FR-RCP-007 Tiếp tục (Cascade DB, enqueue Hangfire xóa ảnh MinIO, trả 204); 3.3 FR-RCP-008 Quản lý Ảnh Công thức (Mô tả: upload max 10 ảnh / recipe, set primary, delete; Bắt đầu). | FR-RCP-007 hoàn tất, FR-RCP-008 (Quản lý ảnh recipe) | None | TV2 / TV4 | **YES** |
| **Page 34** | Chương 3: 3.3 FR-RCP-008 & 009 | 3.3 FR-RCP-008 Tiếp tục (Upload trả 201 { url, isPrimary }; Step 9: PATCH /recipes/{id}/images/{imageId}/primary; Step 12: Delete image); 3.3 FR-RCP-009 Quản lý Nguyên liệu (Bắt đầu). | FR-RCP-008 (Upload, Primary, Delete ảnh), FR-RCP-009 (Nguyên liệu) | CONFLICT-023 (Route /images/{imageId}/primary), CONFLICT-024 (Upload response { url, isPrimary }), TECH-RISK-011 | TV2 / TV4 | **YES** |
| **Page 35** | Chương 3: 3.3 FR-RCP-009 & 010 | 3.3 FR-RCP-009 Tiếp tục (CRUD nguyên liệu riêng lẻ: POST { name, quantity, unit, notes?, sortOrder? }, PUT, DELETE; A3 trả 422); 3.3 FR-RCP-010 Quản lý Các bước nấu (Mô tả: CRUD steps kèm StepNumber, DurationMinutes, ImageUrl, Title?). | FR-RCP-009 (CRUD nguyên liệu), FR-RCP-010 (CRUD bước nấu) | CONFLICT-005 (SortOrder vs OrderIndex), CONFLICT-006 (Duration vs Timer), CONFLICT-007 (Title) | TV2 | **YES** |
| **Page 36** | Chương 3: 3.3 FR-RCP-010 & 3.4 | 3.3 FR-RCP-010 Tiếp tục (Step 1-4: POST step server tự tăng StepNumber = Max + 1; DELETE tự động renumber các bước còn lại 1, 2, 3...); 3.4 Module Tìm kiếm và Phân trang (Giới thiệu FR-SRCH; Bắt đầu FR-SRCH-001 FTS tiếng Việt). | FR-RCP-010 hoàn tất, khởi đầu FR-SRCH | CONFLICT-015 (StepNumber server auto vs client specified), TECH-RISK-012 (Step renumbering race) | TV2 | **YES** |
| **Page 37** | Chương 3: 3.4 FR-SRCH-001 & 002 | 3.4 FR-SRCH-001 Tiếp tục (PostgreSQL tsvector trên title và description, unaccent, tsquery prefix matching ':*', ts_rank_cd; đề xuất cache 5m hoặc không cache); 3.4 FR-SRCH-002 Lọc Công thức (categoryId, difficulty, maxCookTime, minServings kết hợp AND). | FR-SRCH-001 (FTS tiếng Việt), FR-SRCH-002 (Lọc công thức) | CONFLICT-016 (Search cache TTL 5m vs 1m), TECH-RISK-004 (Unaccent extension) | TV2 | **YES** |
| **Page 38** | Chương 3: 3.4, 3.5 & 3.6 | 3.4 FR-SRCH-003 Sắp xếp (sort=createdAt, sort=-createdAt, sort=title); 3.4 FR-SRCH-004 Phân trang (Offset-based page, pageSize, totalCount); 3.5 Module Quản lý Tệp tin (FR-FILE-001 Upload MinIO 5MB; FR-FILE-002 Xóa file MinIO idempotent); 3.6 Module Background Jobs (Giới thiệu Hangfire). | FR-SRCH-003, FR-SRCH-004, FR-FILE-001, FR-FILE-002 | CONFLICT-003 (Sorting prefix vs params), CONFLICT-012 (Pagination shape) | TV2 / TV4 | **YES** |
| **Page 39** | Chương 3: 3.6 & 3.7 | 3.6 Bảng Jobs tiếp tục: FR-JOB-001 Welcome Email (Fire-and-forget, retry 3 lần); FR-JOB-002 Image Thumbnail Job (Tạo 300x300 và 800x600); FR-JOB-003 Sitemap Generation Job (Recurring 02:00 AM UTC, ping Google); 3.7 Module Quan sát (FR-OBS-001 Health Checks: /health, /health/live, /health/ready). | FR-JOB-001, FR-JOB-002, FR-JOB-003, FR-OBS-001 | TECH-RISK-007 (Hangfire Dashboard Auth), TECH-RISK-009 (Job serialization) | TV4 | **YES** |
| **Page 40** | Chương 3: 3.7 & Chương 4 | 3.7 FR-OBS-002 Structured Logging (Serilog, CorrelationIdMiddleware, cảnh báo > 500ms); FR-OBS-003 Distributed Tracing (OpenTelemetry .NET SDK, OTLP); Chương 4: Yêu cầu Phi Chức năng (Bảng summary NFR-PERF 5, NFR-SEC 6 [inconsistency], NFR-USE 4, NFR-REL 3, NFR-MAINT 4, NFR-SCALE 3, NFR-SEO 4); 4.1 Hiệu năng (NFR-PERF-001 Response time p50<=150ms, p95<=500ms; NFR-PERF-002 Throughput >= 100 concurrent users; NFR-PERF-003 Cache effectiveness: Cat 30m, Recipe 5m, Search 1m; NFR-PERF-004 DB query không N+1). | FR-OBS-002, FR-OBS-003, NFR-PERF-001..004 | INCONSISTENCY: NFR-SEC summary count 6 vs detailed 7; CONFLICT-016, CONFLICT-019, CONFLICT-020 | Toàn đội | **YES** |
| **Page 41** | Chương 4: 4.1 & 4.2 | 4.1 NFR-PERF-004 Tiếp tục (Slow query > 100ms, EXPLAIN ANALYZE); NFR-PERF-005 Core Web Vitals (LCP <= 2.5s, CLS <= 0.1, INP <= 200ms, Bundle <= 200KB); 4.2 Bảo mật (NFR-SEC-001 PBKDF2 iteration >= 100,000; NFR-SEC-002 JWT 15m, Refresh token 128-bit random SHA-256 TTL 7d, rotation, reuse detection; NFR-SEC-003 Rate limit Auth 10, General 100, Upload 5; NFR-SEC-004 FluentValidation, magic bytes; NFR-SEC-005 HTTPS TLS 1.2+, strict CORS). | NFR-PERF-005, NFR-SEC-001..005 | CONFLICT-018 (Refresh token 128-bit vs 512-bit entropy) | TV1 / TV3 / TV4 | **YES** |
| **Page 42** | Chương 4: 4.2 & 4.3 | 4.2 NFR-SEC-006 Phân quyền Application (RecipeAuthorizationHandler, audit trail); NFR-SEC-007 Quản lý Secrets (User secrets dev, env vars prod); 4.3 Khả năng Sử dụng (NFR-USE-001 Responsive 3 breakpoints; NFR-USE-002 Accessibility WCAG 2.1 AA; NFR-USE-003 Error messages RFC 7807; NFR-USE-004 Loading states skeleton, toast). | NFR-SEC-006, NFR-SEC-007, NFR-USE-001..004 | CONFLICT-011 (RFC 7807 status 422 vs 400) | TV1 / TV3 | **YES** |
| **Page 43** | Chương 4: 4.4 & 4.5 | 4.4 Độ tin cậy (NFR-REL-001 Uptime SLA >= 99.5%, health check 10s; NFR-REL-002 Xử lý lỗi: Global Exception Handler, Redis down fallback DB, Hangfire retry 3; NFR-REL-003 Lưu trữ bền bỉ: Postgres WAL, backup tự động 03:00 AM 30 ngày, MinIO persistent volume, Soft delete Recipe); 4.5 Khả năng Bảo trì (NFR-MAINT-001 SonarAnalyzer, ESLint; NFR-MAINT-002 Test coverage >= 80% Domain/Application). | NFR-REL-001..003, NFR-MAINT-001..002 | CONFLICT-001 (Soft delete vs Hard delete) | TV2 / TV4 | **YES** |
| **Page 44** | Chương 4: 4.5, 4.6 & 4.7 | 4.5 NFR-MAINT-003 Swagger XML comments; NFR-MAINT-004 Clean Architecture (Domain độc lập, ArchUnit tests); 4.6 Khả năng Mở rộng (NFR-SCALE-001 Stateless backend: JWT, Redis distributed cache, cấm IMemoryCache, RedLock; NFR-SCALE-002 DB scaling: pool max 100, B-tree/GIN; NFR-SCALE-003 Infra scaling: containers, Nginx upstream, MinIO distributed, CDN); 4.7 Tối ưu SEO (NFR-SEO-001 JSON-LD Schema.org Recipe; NFR-SEO-002 Meta tags & Open Graph). | NFR-MAINT-003..004, NFR-SCALE-001..003, NFR-SEO-001..002 | CONFLICT-020 (Redis vs IMemoryCache) | Toàn đội | **YES** |
| **Page 45** | Chương 4: 4.7 | 4.7 NFR-SEO-003 Sitemap & Robots (tự động qua FR-JOB-003, robots.txt, ping Google Search Console); NFR-SEO-004 Cấu trúc URL (/recipes/{slug}, /categories/{slug}, 301 redirect khi đổi slug). | NFR-SEO-003, NFR-SEO-004 | None | TV3 / TV4 | **YES** |
| **Page 46** | Chương 5: 5.1 | 5.1 Giao diện Người dùng UI (Next.js App Router SPA + SSR/ISR; Bắt đầu bảng 13 màn hình: /, /recipes, /recipes/[slug], /categories, /categories/[slug], /auth/login, /auth/register, /dashboard, /dashboard/recipes, /dashboard/recipes/new). | Đặc tả 10 màn hình đầu tiên của Web Frontend | None | TV3 / TV1 | **YES** |
| **Page 47** | Chương 5: 5.1, 5.2 & 5.3 | 5.1 Bảng màn hình tiếp tục: /dashboard/recipes/[id]/edit, /profile, /search; 5.2 Giao diện REST API (HTTP/1.1 & HTTP/2 HTTPS, Base URLs, Bearer header, Response format { data, meta }, RFC 7807, CORS, Rate limit headers); 5.3 Dịch vụ Bên thứ ba (Bắt đầu: Google OAuth 2.0 Authorization Code + PKCE, callback URI). | Đặc tả màn hình UI còn lại, REST API standards, Google OAuth | CONFLICT-012 (Response shape data/meta vs flat), CONFLICT-013 (OAuth flow) | TV1 / TV3 | **YES** |
| **Page 48** | Chương 5: 5.3 & 5.4 | 5.3 Dịch vụ bên thứ ba tiếp tục (MinIO port 9000, Hangfire schema hangfire, Serilog/Seq port 5341, OpenTelemetry OTLP, MailKit SMTP Mailhog port 1025/8025, Google Search Console ping); 5.4 Giao diện Phần cứng (Bảng thông số server CPU 2 vCPU, RAM 4GB/8GB, Disk 20GB/50GB). | Đặc tả tích hợp MinIO, Hangfire, Seq, Mailhog, cấu hình server | TECH-RISK-006 (MinIO host resolution), TECH-RISK-015 (Nginx 5MB upload limit) | TV4 / Toàn đội | **YES** |
| **Page 49** | Chương 5: 5.4.2 & Chương 6: 6.1 | 5.4.2 Yêu cầu trình duyệt client (Chrome 112+, Firefox 113+, Safari 16+, Edge 112+ ES2020+); Chương 6: Kiến trúc Hệ thống (6.1 Tổng quan kiến trúc phân tầng: Next.js frontend, Nginx reverse proxy, .NET backend, PostgreSQL, Redis, MinIO). | Yêu cầu trình duyệt production, Kiến trúc tổng thể hệ thống | CONFLICT-022 (Browser matrix Chrome 112+ vs Chrome 90+ ở p.14) | Toàn đội | **YES** |
| **Page 50** | Chương 6: 6.2 | 6.2 Kiến trúc Backend Clean Architecture (Sơ đồ 4 tầng: Domain lõi, Application use cases/MediatR, Infrastructure data/external, Presentation Minimal APIs; Nguyên tắc Dependency Rule). | Quy tắc phụ thuộc một chiều trong Clean Architecture | None | TV1 | **YES** |
| **Page 51** | Chương 6: 6.2 & 6.3 | 6.2 Chi tiết cấu trúc 4 tầng (Domain: Entities, Value Objects, Domain Events; Application: Features CQRS, DTOs, Behaviors; Infrastructure: EF Core, Repositories, Redis, MinIO; Presentation: Endpoints, Middlewares); 6.3 CQRS + MediatR Pipeline (Luồng Request -> Pipeline Behaviors -> Handler -> Response). | Cấu trúc thư mục source code và CQRS pipeline | None | TV1 | **YES** |
| **Page 52** | Chương 6: 6.3, 6.4 & 6.5 | 6.3 Bảng Pipeline Behaviors (LoggingBehavior, ValidationBehavior); 6.4 Mô hình Quan hệ Thực thể (ERD tóm tắt 7 quan hệ chính); 6.5 Triển khai Docker Compose (Kiến trúc container hóa 8 services). | Pipeline behaviors, Sơ đồ ERD, Tổng quan Docker | None | Toàn đội | **YES** |
| **Page 53** | Chương 6: 6.5 | 6.5 Bảng chi tiết Docker Compose 8 Services (nginx, api 5000:8080, frontend 3000:3000, postgres 5432, redis 6379, minio 9000/9001, seq 5341:80, mailhog 8025/1025; ports, volumes, dependencies). | Thông số kỹ thuật deployment Docker Compose | None | Toàn đội | **YES** |
| **Page 54** | Chương 7: 7.1 & 7.2 | 7.1 BaseEntity (Abstract class: Id uuid PK, CreatedAt timestamptz, UpdatedAt timestamptz, IsDeleted, RowVersion; quy định tất cả entities kế thừa BaseEntity); 7.2 Recipe (Bắt đầu bảng Recipe: Id, Title, Slug, Description, PrepTime, CookTime, Servings, Difficulty). | Entity BaseEntity và Recipe (phần 1) | CONFLICT-025 (Quy tắc kế thừa BaseEntity vs ApplicationUser Identity) | TV2 / TV1 | **YES** |
| **Page 55** | Chương 7: 7.2 | 7.2 Recipe Tiếp tục (Cột Status Draft/Published/Archived, CategoryId FK, AuthorId FK, RowVersion xmin byte[] concurrency token, IsDeleted boolean default false Global Query Filter; Bảng 5 indexes của Recipes). | Entity Recipe (phần 2) và Indexing strategy | CONFLICT-001 (IsDeleted vs Hard delete), CONFLICT-014 (RowVersion 409 vs 422) | TV2 | **YES** |
| **Page 56** | Chương 7: 7.2.1 & 7.3 | 7.2.1 RecipeNutrition (Owned Entity 1-1: RecipeId PK/FK, Calories kcal/serving, Protein g/serving, Carbohydrates g/serving, Fat g/serving, Fiber g/serving, Sodium mg/serving; Xác nhận PER SERVING); 7.3 RecipeStep (Bắt đầu bảng cột). | Entity RecipeNutrition và RecipeStep (phần 1) | CONFLICT-008 (4 vs 6 nutrition fields; Scope: PER SERVING) | TV2 | **YES** |
| **Page 57** | Chương 7: 7.3 & 7.4 | 7.3 RecipeStep Tiếp tục (Id uuid, RecipeId FK cascade, StepNumber int unique composite, Title varchar 200, Description text, TimerMinutes int NULL, ImageUrl varchar 500); 7.4 RecipeIngredient (Id uuid, RecipeId FK cascade, Name varchar 200, Quantity decimal 10,3 NULL, Unit varchar 50 NULL, Notes varchar 500 NULL, OrderIndex int default 0). | Entity RecipeStep (phần 2) và RecipeIngredient | CONFLICT-004 (Quantity/Unit), CONFLICT-005 (SortOrder vs OrderIndex), CONFLICT-006 (Duration vs Timer), CONFLICT-007 (Title) | TV2 | **YES** |
| **Page 58** | Chương 7: 7.5 & 7.6 & 7.7 | 7.5 RecipeImage (OriginalUrl, MediumUrl, ThumbnailUrl, IsPrimary, OrderIndex); 7.6 Category (Name, Slug, Description, ImageUrl, OrderIndex); 7.7 ApplicationUser (Kế thừa IdentityUser<string>, AspNetUsers). | Entity RecipeImage, Category, ApplicationUser | CONFLICT-024 (DB OriginalUrl vs FR { url, isPrimary }), CONFLICT-025 (IdentityUser vs BaseEntity) | TV2 / TV4 / TV1 | **YES** |
| **Page 59** | Chương 7: 7.7 & 7.8 | 7.7 ApplicationUser (IdentityUser custom columns: DisplayName varchar 100, AvatarUrl varchar 500, Bio text, IsActive bool default true, CreatedAt; Identity columns: Id varchar 450, Email, PasswordHash, LockoutEnabled, AccessFailedCount); 7.8 RefreshToken (Bắt đầu bảng cột: Id uuid, UserId FK cascade, TokenHash varchar 64 unique IDX_RefreshToken_Hash, ExpiresAt, RevokedAt, ReplacedByTokenHash, CreatedAt). | Entity ApplicationUser và RefreshToken (phần 1) | CONFLICT-009 (FullName/UserName vs DisplayName/Bio) | TV1 | **YES** |
| **Page 60** | Chương 7: 7.8 & Chương 8: 8.0 | 7.8 RefreshToken Tiếp tục (CreatedByIp varchar 45 NULL); Chương 8: Đặc tả REST API (8.0 Quy ước chung: Base URL /api/v1, ApiResponse<T>, PagedResult<T>, ProblemDetails RFC 7807; Bảng cấu trúc phản hồi). | Entity RefreshToken hoàn tất, Quy ước API toàn hệ thống | CONFLICT-011 (422 vs 400 status), CONFLICT-012 (Flat vs Nested pagination) | Toàn đội | **YES** |
| **Page 61** | Chương 8: 8.1 | 8.1 Authentication Endpoints (POST /api/v1/auth/register, POST /api/v1/auth/login, POST /api/v1/auth/google nhận { idToken }, POST /api/v1/auth/refresh-token). | API Endpoints nhóm Auth (phần 1) | CONFLICT-010 (Register response), CONFLICT-013 (Google OAuth { idToken }) | TV1 | **YES** |
| **Page 62** | Chương 8: 8.1 & 8.2 | 8.1 Authentication Endpoints tiếp tục (POST /api/v1/auth/logout, GET /api/v1/auth/me, PUT /api/v1/auth/profile); 8.2 Category Endpoints (GET /api/v1/categories, GET /api/v1/categories/{slug}, POST /api/v1/categories). | API Endpoints Auth (phần 2) và Category (phần 1) | CONFLICT-009, CONFLICT-020 | TV1 / TV4 | **YES** |
| **Page 63** | Chương 8: 8.2 & 8.3 | 8.2 Category Endpoints tiếp tục (PUT /api/v1/categories/{id} cho phép sửa Name, Desc, ImageUrl, OrderIndex; DELETE /api/v1/categories/{id}); 8.3 Recipe Endpoints (GET /api/v1/recipes với params search, category, sort, page; GET /api/v1/recipes/{slug}; POST /api/v1/recipes; PUT /api/v1/recipes/{id} với RowVersion). | API Endpoints Category (phần 2) và Recipe (phần 1) | CONFLICT-017 (Category update fields), CONFLICT-003, CONFLICT-012, CONFLICT-014 | TV4 / TV2 | **YES** |
| **Page 64** | Chương 8: 8.3 & 8.4 | 8.3 Recipe Endpoints tiếp tục (PATCH /publish, PATCH /unpublish, PATCH /archive, DELETE /recipes/{id}); 8.4 Recipe Images Endpoints (POST upload trả { imageId, originalUrl, altText, isPrimary }, PATCH /images/{imageId}, DELETE /images/{imageId}). | API Endpoints Recipe (phần 2) và Recipe Images | CONFLICT-001 (Delete), CONFLICT-023 (PATCH {imageId} body vs /primary), CONFLICT-024 (Upload response shape) | TV2 / TV4 | **YES** |
| **Page 65** | Chương 8: 8.5, 8.6 & 8.7 | 8.5 Recipe Steps Endpoints (POST /recipes/{id}/steps nhận stepNumber, PUT /recipes/{id}/steps/{stepId}, DELETE /recipes/{id}/steps/{stepId}); 8.6 Recipe Ingredients Endpoints (POST /recipes/{id}/ingredients nhận { name, quantity?, unit?, notes?, orderIndex? }, PUT, DELETE); 8.7 Health Check Endpoints (GET /health). | API Endpoints Recipe Steps, Ingredients và Health Check tổng hợp | CONFLICT-015 (StepNumber client specified vs server auto) | TV2 / TV4 | **YES** |
| **Page 66** | Chương 8: 8.7 & Phụ lục A | 8.7 Health Check Endpoints tiếp tục (GET /health/live, GET /health/ready); Phụ lục A: HTTP Status Codes (Bảng mã HTTP sử dụng: 200, 201, 204, 400, 401, 403, 404, 409, 415, 422, 429, 500, 503). | Health check probes và Bảng mã trạng thái HTTP chuẩn | None | Toàn đội | **YES** |
| **Page 67** | Phụ lục A & B | Phụ lục A tiếp tục; Phụ lục B: Application Error Codes (Cấu trúc mã lỗi hệ thống theo domain; Bắt đầu bảng mã lỗi: AUTH_INVALID_CREDENTIALS 401, AUTH_USER_LOCKED 423/403, AUTH_EMAIL_EXISTS 409, AUTH_TOKEN_EXPIRED 401, AUTH_TOKEN_REVOKED 401). | Từ điển mã lỗi tầng ứng dụng nhóm Auth | None | TV1 | **YES** |
| **Page 68** | Phụ lục B | Phụ lục B tiếp tục: CATEGORY_NOT_FOUND 404, CATEGORY_HAS_RECIPES 400, CATEGORY_NAME_EXISTS 409, RECIPE_NOT_FOUND 404, RECIPE_FORBIDDEN 403, CONCURRENCY_CONFLICT 422, RECIPE_ALREADY_PUBLISHED 400, RECIPE_NOT_READY_PUBLISH 400. | Từ điển mã lỗi nhóm Category và Recipe | CONFLICT-014 (CONCURRENCY_CONFLICT ánh xạ 422 vs 409 ở Ch.3/Ch.8) | TV2 / TV4 | **YES** |
| **Page 69** | Phụ lục B & C | Phụ lục B tiếp tục: FILE_SIZE_EXCEEDED 400, FILE_MIME_INVALID 400, FILE_MAGIC_BYTES_INVALID 400, STORAGE_UNAVAILABLE 503, RATE_LIMIT_EXCEEDED 429, VALIDATION_ERROR 422, INTERNAL_SERVER_ERROR 500; Phụ lục C: Thuật ngữ (Access Token, Application Layer, ArchUnit.NET, ASP.NET Identity, Bogus, Cache-Aside). | Từ điển mã lỗi nhóm File/Storage/System và Từ điển thuật ngữ (phần 1) | None | Toàn đội | **YES** |
| **Page 70** | Phụ lục C | Phụ lục C tiếp tục bảng thuật ngữ: Clean Architecture, Correlation ID, CQRS, Distributed Tracing, Domain Layer, Eager Loading, EF Core, FluentValidation, Full-Text Search (FTS), Global Exception Handler, Hangfire, Health Checks, ISR, Infrastructure Layer. | Từ điển thuật ngữ kỹ thuật và kiến trúc (phần 2) | None | Toàn đội | **YES** |
| **Page 71** | Phụ lục C | Phụ lục C tiếp tục và kết thúc bảng thuật ngữ: MediatR, MinIO, N+1 Query Problem, Nginx, OpenTelemetry, Output Cache, PBKDF2, PostgreSQL, Problem Details (RFC 7807), Rate Limiting, Redis, Refresh Token, Refresh Token Rotation, Reuse Detection, RowVersion (xmin), Semantic HTML5, Seq, Serilog, Soft Delete, tsvector/tsquery, Unaccent, WCAG 2.1 AA. | Từ điển thuật ngữ kỹ thuật và kiến trúc (phần 3 - kết thúc tài liệu) | None | Toàn đội | **YES** |

---

## 2. Xác nhận độ bao phủ toàn bộ 71 trang (Page Coverage Check)

PAGE 01 YES
PAGE 02 YES
PAGE 03 YES
PAGE 04 YES
PAGE 05 YES
PAGE 06 YES
PAGE 07 YES
PAGE 08 YES
PAGE 09 YES
PAGE 10 YES
PAGE 11 YES
PAGE 12 YES
PAGE 13 YES
PAGE 14 YES
PAGE 15 YES
PAGE 16 YES
PAGE 17 YES
PAGE 18 YES
PAGE 19 YES
PAGE 20 YES
PAGE 21 YES
PAGE 22 YES
PAGE 23 YES
PAGE 24 YES
PAGE 25 YES
PAGE 26 YES
PAGE 27 YES
PAGE 28 YES
PAGE 29 YES
PAGE 30 YES
PAGE 31 YES
PAGE 32 YES
PAGE 33 YES
PAGE 34 YES
PAGE 35 YES
PAGE 36 YES
PAGE 37 YES
PAGE 38 YES
PAGE 39 YES
PAGE 40 YES
PAGE 41 YES
PAGE 42 YES
PAGE 43 YES
PAGE 44 YES
PAGE 45 YES
PAGE 46 YES
PAGE 47 YES
PAGE 48 YES
PAGE 49 YES
PAGE 50 YES
PAGE 51 YES
PAGE 52 YES
PAGE 53 YES
PAGE 54 YES
PAGE 55 YES
PAGE 56 YES
PAGE 57 YES
PAGE 58 YES
PAGE 59 YES
PAGE 60 YES
PAGE 61 YES
PAGE 62 YES
PAGE 63 YES
PAGE 64 YES
PAGE 65 YES
PAGE 66 YES
PAGE 67 YES
PAGE 68 YES
PAGE 69 YES
PAGE 70 YES
PAGE 71 YES

---

## 3. Thống kê kiểm toán trang (Page Audit Metrics)

- **Total pages in SRS:** 71
- **Pages audited:** 71
- **Pages missing:** 0
- **Audit completion rate:** 100.0%
- **Audit verification date:** 16/09/2026
- **Audited by:** Antigravity AI Pair Programmer & TV1 Lead
