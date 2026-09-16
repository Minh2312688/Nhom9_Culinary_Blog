# CulinaryBlog Master SRS Audit (Full 71 Pages — All 4 Members)

> **BẢN QUYỀN TÀI LIỆU:**
> ĐÂY LÀ TÀI LIỆU MASTER AUDIT TOÀN BỘ 71 TRANG CỦA SRS v1.0.0 DÀNH CHO CẢ 4 THÀNH VIÊN (TV1, TV2, TV3, TV4).
> TÀI LIỆU NÀY LƯU TẠI THƯ MỤC PRIVATE NOTES VÀ **TUYỆT ĐỐI KHÔNG ĐƯỢC ĐƯA LÊN GIT REPOSITORY**.

---

## 1. Thông tin tài liệu

- **SRS Name:** Culinary Blog – Tài liệu Đặc tả Yêu cầu Phần mềm (Software Requirements Specification)
- **Version:** v1.0.0
- **Date:** 12/2024
- **Status:** Confidential • Phát triển Ứng dụng Web Nâng cao V4
- **Tổng số trang:** 71 trang (Đã audit đầy đủ 71/71 trang từ Page 1 đến Page 71)
- **Phạm vi kiểm toán:** Toàn bộ hệ thống (Frontend Next.js, Backend .NET 10, CSDL PostgreSQL, Dịch vụ ngoài: Redis, MinIO, Hangfire, Seq, Mailhog, Google OAuth)
- **Thành viên nhóm thực hiện:** TV1 (Lead Backend/Auth), TV2 (Recipe/DB/Search), TV3 (Frontend Platform/UI), TV4 (Categories/Storage/Jobs/Obs)
- **Ngày hoàn thành Master Audit:** 16/09/2026

---

## 2. Mục đích Master Audit

1. **Rà soát 100% tài liệu (71/71 trang):** Không bỏ sót bất kỳ trang nào từ trang bìa, lịch sử phiên bản, mục lục, giới thiệu, tổng quan, yêu cầu chức năng, phi chức năng, giao diện, kiến trúc, mô hình dữ liệu, đặc tả API đến toàn bộ các phụ lục tra cứu.
2. **Bảo đảm tính trung thực tuyệt đối:** Ghi nhận chính xác hiện trạng của tài liệu SRS gốc. Không tự ý suy diễn hay âm thầm "sửa" các điểm mâu thuẫn để code cho dễ. Mọi điểm xung đột đều được đánh dấu là `CONFLICT` và đưa vào danh mục chờ xin ý kiến Giảng viên / Nhóm trưởng.
3. **Phân định ranh giới trách nhiệm 4 thành viên:** Xác định rõ ràng phạm vi công việc, phụ thuộc chéo và các điểm nghẽn kỹ thuật cho từng thành viên TV1, TV2, TV3, TV4.
4. **Cung cấp cơ sở dữ liệu tra cứu duy nhất (Single Source of Truth):** Làm kim chỉ nam cho tất cả các giai đoạn triển khai dự án, tránh tranh cãi về hợp đồng API, cấu trúc cơ sở dữ liệu hay quy ước phân trang.

---

## 3. Sai lệch siêu dữ liệu tài liệu (Document Metadata Inconsistencies)

Các điểm sai lệch về mặt số liệu thống kê nội tại trong văn bản SRS (không tạo thành blocker triển khai nghiệp vụ):

1. **FR Count Metadata Inconsistency:**
   - Tại **Chương 1.5** (trang 8), tài liệu tóm tắt hệ thống gồm **27 Functional Requirements**.
   - Tuy nhiên, khi rà soát danh sách định danh cụ thể tại **Chương 3** (trang 17–40), hệ thống chuẩn hóa theo đúng **34 explicit FR IDs** (`FR-AUTH-001..007` [7], `FR-CAT-001..005` [5], `FR-RCP-001..010` [10], `FR-SRCH-001..004` [4], `FR-FILE-001..002` [2], `FR-JOB-001..003` [3], `FR-OBS-001..003` [3]).
   - *Ghi chú thống kê:* Loại bỏ cách thống kê cũ chỉ tính `FR-SRCH-001..002`; chuẩn hóa theo explicit IDs `FR-SRCH-001..004`. Đây là lỗi biên tập siêu dữ liệu tóm tắt của tài liệu gốc, không ảnh hưởng logic triển khai.

2. **NFR Security Count Metadata Inconsistency:**
   - Tại bảng tổng hợp đầu **Chương 4** (trang 40), danh mục `NFR-SEC` được ghi là **6 yêu cầu**.
   - Tuy nhiên, trong phần đặc tả chi tiết tại mục 4.2 (trang 41–42), tài liệu liệt kê đầy đủ **7 explicit IDs** (`NFR-SEC-001` đến `NFR-SEC-007`).
   - *Ghi chú:* Bảng tổng hợp đếm thiếu `NFR-SEC-007` (Secrets Management).

3. **Browser Support Version Matrix Mismatch:**
   - Tại **Chương 2.4.3** (trang 14), tài liệu yêu cầu hỗ trợ Chrome 90+, Firefox 88+, Safari 14+, Edge 90+.
   - Tại **Chương 5.4.2** (trang 49), bảng môi trường production lại quy định Chrome 112+, Firefox 113+, Safari 16+, Edge 112+ (ES2020+). (Đã ghi nhận tại `CONFLICT-022`).

4. **Next.js Version Inconsistency:**
   - Tại **Chương 1.1** (trang 6) và bìa tài liệu ghi nhận `.NET 10 + Next.js App Router` (Next.js 14/15).
   - Tại **Chương 6.1** (trang 50), tài liệu tham chiếu Next.js 15 và React 19 canary.

---

## 4. Design Constraints (Ràng buộc Thiết kế Toàn hệ thống — 10 CONS IDs)

Bảng tổng hợp 10 ràng buộc kiến trúc kỹ thuật bắt buộc toàn bộ nhóm phải tuân thủ:

| ID | Tên ràng buộc | Nội dung ràng buộc theo SRS | Module ảnh hưởng | Owner | Phương pháp kiểm chứng | Conflict liên quan |
| :--- | :--- | :--- | :--- | :--- | :--- | :--- |
| **CONS-001** | Clean Architecture | Hệ thống backend phải tuân thủ nghiêm ngặt mô hình Clean Architecture 4 tầng (Domain, Application, Infrastructure, API). Dependency rule đi từ ngoài vào trong. Domain hoàn toàn không phụ thuộc package ngoài. | Toàn bộ Backend | TV1 | NetArchTest / ArchUnit tests tự động | Không |
| **CONS-002** | CQRS & MediatR | Toàn bộ nghiệp vụ trong Application layer phải tách biệt giữa Command (thay đổi trạng thái) và Query (đọc dữ liệu) thông qua MediatR pipeline. | Application Layer | TV1 | Code review, kiểm tra triển khai `IRequest<T>` | Không |
| **CONS-003** | Tech Stack Core | Backend xây dựng trên nền **.NET 10 Minimal API**; Frontend xây dựng trên nền **Next.js App Router**. | Toàn hệ thống | TV1, TV3 | Build pipeline, Dockerfile inspection | Không |
| **CONS-004** | Stateless JWT | Xác thực API stateless sử dụng **JSON Web Token (JWT)** (Access Token 15 phút, Refresh Token 7 ngày với Token Rotation và Reuse Detection). | Auth & Client State | TV1, TV3 | Security unit tests, Token verification | `CONFLICT-018` |
| **CONS-005** | REST & RFC 7807 | API thiết kế theo phong cách RESTful, URL versioning `/api/v1`. Toàn bộ phản hồi lỗi trả về chuẩn định dạng **RFC 7807 Problem Details**. | Toàn bộ API | TV1, TV2, TV4 | Integration tests kiểm tra error response shape | `CONFLICT-011` |
| **CONS-006** | PostgreSQL 16 & EF Core 10 | Cơ sở dữ liệu quan hệ chính là **PostgreSQL 16**. Sử dụng **EF Core 10 Code First** với Migrations để quản lý schema. | CSDL & Persistence | TV2 | Migration scripts, Integration tests với Testcontainers | Không |
| **CONS-007** | File Upload | Kích thước tệp tải lên tối đa 5 MB. Định dạng chỉ chấp nhận: image/jpeg, image/png, image/webp, image/avif. Kiểm tra MIME type (không chỉ extension). Lưu trữ S3-compatible (MinIO). | Storage Module | TV4 | AWS S3 SDK integration test, MIME validation | `CONFLICT-024` |
| **CONS-008** | FluentValidation Pipeline | Tất cả Command và Query inputs phải được thẩm định tự động qua **FluentValidation** trong MediatR pipeline behavior trước khi handler thực thi. Không validate trong Endpoint handler. | Application Pipeline | TV1 | ValidationBehavior unit tests | `CONFLICT-011` |
| **CONS-009** | Docker Containerization | Ứng dụng PHẢI được đóng gói Docker. Dockerfile multi-stage build (SDK → aspnet runtime). Docker Compose cho local development. (Các services chi tiết được phân bổ ở Chương 6). | DevOps / Infra | TV1, TV4 | `docker compose up` build thành công | Không |
| **CONS-010** | Structured Logging | Structured logging với **Serilog** là bắt buộc. Mọi log entry PHẢI có CorrelationId, RequestPath, UserId (khi đã xác thực). (Log format JSON và tích hợp Seq server chi tiết theo §5.3/§6.4). | Observability | TV1, TV4 | Kiểm tra log output trên console & Seq | Không |

---

## 5. Requirement Inventory (Kiểm kê Yêu cầu Hệ thống)

Hệ thống có tổng cộng **64 yêu cầu** được đánh mã tường minh (34 Functional Requirements + 30 Non-Functional Requirements) cùng 10 Ràng buộc kỹ thuật (CONS).

### 5.1. Functional Requirements (34 explicit FR IDs)

| Requirement ID | Tên | Nhóm | Tóm tắt đúng theo SRS | Primary Owner | Phase | Status | Evidence |
|---|---|---|---|---|---|---|---|
| **FR-AUTH-001** | Đăng ký tài khoản | AUTH | Đăng ký bằng Email, Password, FullName. Password hash PBKDF2. Gửi Welcome Email. | TV1 | Phase 2 | **CONFLICT** | §3.1 p. 17–19 (Tokens vs Info, 512-bit entropy) |
| **FR-AUTH-002** | Đăng nhập tài khoản | AUTH | Đăng nhập bằng Email + Password. Khóa tài khoản 15 phút sau 5 lần thất bại liên tiếp. | TV1 | Phase 2 | **CLEAR** | §3.1 p. 19–20 |
| **FR-AUTH-003** | Đăng nhập Google OAuth | AUTH | Đăng nhập / đăng ký tự động qua tài khoản Google OAuth 2.0. | TV1 | Phase 2 | **CONFLICT** | §3.1 p. 20, §5.3 p. 47, §8.1 p. 61 |
| **FR-AUTH-004** | Làm mới Access Token | AUTH | Cấp Access Token mới từ Refresh Token. Cơ chế Token Rotation & Reuse Detection. | TV1 | Phase 2 | **CONFLICT** | §3.1 p. 21, §4.2 p. 41 (Token Entropy) |
| **FR-AUTH-005** | Đăng xuất | AUTH | Thu hồi Refresh Token hiện tại (Revoke). Xóa token phía client. | TV1 | Phase 2 | **CLEAR** | §3.1 p. 21–22, §8.1 p. 62 |
| **FR-AUTH-006** | Xem thông tin User | AUTH | Trả về thông tin cá nhân của người dùng hiện tại đang đăng nhập (`/auth/me`). | TV1 | Phase 2 | **CLEAR** | §3.1 p. 22, §8.1 p. 62 |
| **FR-AUTH-007** | Cập nhật thông tin User | AUTH | Cập nhật DisplayName/FullName, AvatarUrl, Bio. Email và Role không đổi. | TV1 | Phase 2 | **CONFLICT** | §3.1 p. 23, §7.2 p. 55 (FullName vs DisplayName) |
| **FR-CAT-001** | Xem danh sách Danh mục | CAT | Lấy tất cả danh mục kèm số lượng công thức Published. Sắp xếp theo Name. | TV4 | Phase 3 | **CONFLICT** | §3.2 p. 24 (IMemoryCache vs Redis) |
| **FR-CAT-002** | Xem chi tiết Danh mục | CAT | Lấy thông tin danh mục theo slug kèm danh sách công thức phân trang. | TV4 | Phase 3 | **CONFLICT** | §3.2 p. 24–25, §8.2 p. 63 (Pagination Shape) |
| **FR-CAT-003** | Tạo Danh mục mới | CAT | Admin tạo danh mục mới. Tự động sinh slug duy nhất. Invalidate cache danh mục. | TV4 | Phase 3 | **CONFLICT** | §3.2 p. 25–26 (Cache invalidation mechanism) |
| **FR-CAT-004** | Cập nhật Danh mục | CAT | Admin cập nhật danh mục. Tạo slug mới nếu đổi tên. Invalidate cache. | TV4 | Phase 3 | **CONFLICT** | §3.2 p. 26, §8.2 p. 63 (Fields allowed to update) |
| **FR-CAT-005** | Xóa Danh mục | CAT | Admin xóa danh mục. Chặn nếu còn chứa công thức. | TV4 | Phase 3 | **CONFLICT** | §3.2 p. 26–27, §7.6 p. 58 (Hard vs Soft Delete) |
| **FR-RCP-001** | Xem danh sách Công thức | RCP | Lấy danh sách công thức Published có lọc, phân trang, sắp xếp. | TV2 | Phase 3 | **CONFLICT** | §3.3 p. 27–28 (Sorting, Pagination, Author visibility) |
| **FR-RCP-002** | Xem chi tiết Công thức | RCP | Lấy chi tiết công thức kèm steps, ingredients, nutrition, author theo slug. | TV2 | Phase 3 | **CONFLICT** | §3.3 p. 28–29 (OutputCache 60m vs Redis 5m) |
| **FR-RCP-003** | Tạo Công thức mới | RCP | Author/Admin tạo công thức dạng Draft. Sinh slug duy nhất. | TV2 | Phase 3 | **CONFLICT** | §3.3 p. 29–30 (Ingredient & Nutrition fields) |
| **FR-RCP-004** | Cập nhật Công thức | RCP | Author-Owner/Admin cập nhật công thức. Kiểm tra RowVersion tránh xung đột. | TV2 | Phase 3 | **CONFLICT** | §3.3 p. 30–31 (409 vs 422 Concurrency status) |
| **FR-RCP-005** | Xuất bản / Hủy xuất bản | RCP | Chuyển đổi trạng thái giữa Draft và Published. Invalidate cache liên quan. | TV2 | Phase 3 | **CLEAR** | §3.3 p. 31–32 |
| **FR-RCP-006** | Lưu trữ Công thức | RCP | Chuyển trạng thái sang Archived. Chỉ Author và Admin có thể xem lại. | TV2 | Phase 3 | **CLEAR** | §3.3 p. 32 |
| **FR-RCP-007** | Xóa Công thức | RCP | Author-Owner/Admin xóa công thức nấu ăn. | TV2 | Phase 3 | **CONFLICT** | §3.3 p. 32–33, §7.1 p. 54 (Hard vs Soft Delete) |
| **FR-RCP-008** | Quản lý Ảnh Công thức | RCP | Upload tối đa 10 ảnh / recipe, thiết lập ảnh đại diện chính (IsPrimary), xóa ảnh. | TV2 | Phase 3 | **CONFLICT** | §3.3 p. 33–34 (Endpoint & Property name) |
| **FR-RCP-009** | Quản lý Nguyên liệu | RCP | Thêm, sửa, xóa nguyên liệu của công thức. Sắp xếp thứ tự hiển thị. | TV2 | Phase 3 | **CONFLICT** | §3.3 p. 34–35, §7.4 p. 57 (Quantity/Unit, SortOrder) |
| **FR-RCP-010** | Quản lý Các bước nấu | RCP | Thêm, sửa, xóa các bước thực hiện. Kèm ảnh và thời gian thực hiện. | TV2 | Phase 3 | **CONFLICT** | §3.3 p. 35–36, §8.5 p. 65 (StepNumber, Duration, Title) |
| **FR-SRCH-001** | Tìm kiếm Toàn văn bản | SRCH | Full-Text Search qua PostgreSQL tsvector/tsquery tiếng Việt (unaccent). | TV2 | Phase 4 | **CONFLICT** | §3.4 p. 36–37, §4.1 p. 40 (Cache TTL 5m vs 1m) |
| **FR-SRCH-002** | Lọc Công thức | SRCH | Lọc đa tiêu chí: categoryId, difficulty, maxCookTime, minServings bằng AND logic. | TV2 | Phase 4 | **CLEAR** | §3.4 p. 37–38 |
| **FR-SRCH-003** | Sắp xếp Kết quả | SRCH | Sắp xếp theo createdAt, cookTime, prepTime, title (hỗ trợ ASC/DESC). | TV2 | Phase 4 | **CONFLICT** | §3.4 p. 38, §8.3 p. 63 (Prefix vs Explicit params) |
| **FR-SRCH-004** | Phân trang Kết quả | SRCH | Phân trang danh sách offset-based (`page`, `pageSize` mặc định 12, tối đa 50). | TV2 | Phase 4 | **CONFLICT** | §3.4 p. 38, §8.0 p. 60 (Flat vs Nested meta) |
| **FR-FILE-001** | Upload File lên MinIO | FILE | Upload ảnh binary lên MinIO S3-compatible bucket `culinary-blog`. Max 5MB. | TV4 | Phase 3 | **CLEAR** | §3.5 p. 38 |
| **FR-FILE-002** | Xóa File khỏi MinIO | FILE | Xóa file object trên MinIO theo public URL (idempotent, không throw nếu thiếu). | TV4 | Phase 3 | **CLEAR** | §3.5 p. 38 |
| **FR-JOB-001** | Welcome Email Job | JOB | Hangfire background job gửi email HTML chào mừng sau khi đăng ký thành công. | TV4 | Phase 3 | **CLEAR** | §3.6 p. 38–39 |
| **FR-JOB-002** | Image Thumbnail Job | JOB | Hangfire background job tạo ảnh thumbnail (300x300) và medium (800x600). | TV4 | Phase 3 | **CLEAR** | §3.6 p. 39 |
| **FR-JOB-003** | Sitemap Generation Job | JOB | Hangfire recurring job chạy 02:00 AM UTC tạo sitemap.xml và ping Google Console. | TV4 | Phase 4 | **CLEAR** | §3.6 p. 39 |
| **FR-OBS-001** | Health Check Endpoints | OBS | Cung cấp 3 endpoints: `/health` (tổng hợp), `/health/live`, `/health/ready`. | TV4 | Phase 4 | **CLEAR** | §3.7 p. 40 |
| **FR-OBS-002** | Structured Logging | OBS | Logging có cấu trúc qua Serilog kèm CorrelationId header, thời gian thực thi, userId. | TV1/TV4 | Phase 1/4 | **CLEAR** | §3.7 p. 40 |
| **FR-OBS-003** | Distributed Tracing | OBS | OpenTelemetry instrumentation đo traces HTTP, EF Core và business metrics. | TV4 | Phase 4 | **CLEAR** | §3.7 p. 40 |

---

### 5.2. Non-Functional Requirements (30 detailed NFR IDs)

| Requirement ID | Tên | Nhóm | Nội dung đo lường theo SRS | Primary Owner | Status | Evidence |
|---|---|---|---|---|---|---|
| **NFR-PERF-001** | Response Time API | PERF | p50 ≤ 150ms (GET cache); p95 ≤ 500ms (toàn bộ API); p99 ≤ 1000ms. | TV1/TV2 | **CLEAR** | §4.1 p. 40 |
| **NFR-PERF-002** | Throughput | PERF | Xử lý đồng thời ≥ 100 concurrent users không suy giảm trên 2 vCPU / 4GB RAM. | Toàn đội | **CLEAR** | §4.1 p. 40 |
| **NFR-PERF-003** | Cache Effectiveness | PERF | Redis hit rate ≥ 80%. Category: 30m; Recipe detail: 5m; Search: 1m. | TV2/TV4 | **CONFLICT** | §4.1 p. 40 (Xung đột với FR-RCP-002 và FR-CAT-001) |
| **NFR-PERF-004** | Database Query | PERF | Không N+1 query (bắt buộc Include/ThenInclude, projection). Cảnh báo query > 100ms. | TV2 | **CLEAR** | §4.1 p. 40–41 |
| **NFR-PERF-005** | Core Web Vitals | PERF | Next.js: LCP ≤ 2.5s, CLS ≤ 0.1, INP ≤ 200ms, First Load JS Bundle ≤ 200KB. | TV3 | **CLEAR** | §4.1 p. 41 |
| **NFR-SEC-001** | Password & Hashing | SEC | ASP.NET Core Identity PBKDF2-HMACSHA512 (iteration count ≥ 100.000). | TV1 | **CLEAR** | §4.2 p. 41 |
| **NFR-SEC-002** | JWT Token Security | SEC | Access Token JWT 15m; Refresh Token 128-bit crypto random hash SHA-256 TTL 7d. | TV1 | **CONFLICT** | §4.2 p. 41 (Xung đột 128-bit vs 512-bit ở FR-AUTH-001) |
| **NFR-SEC-003** | Rate Limiting | SEC | Auth endpoints: 10 req/min/IP; General API: 100 req/min/IP; Upload: 5 req/min/IP. | TV1 | **CLEAR** | §4.2 p. 41 |
| **NFR-SEC-004** | Input & File Validation | SEC | FluentValidation mọi input. Chặn SQLi, XSS, magic bytes validation, max 5MB. | TV1/TV4 | **CONFLICT** | §4.2 p. 41, Phụ lục A (422 vs 400 status) |
| **NFR-SEC-005** | HTTPS & CORS | SEC | Bắt buộc TLS 1.2+, HSTS (31536000s). CORS strict origin, cấm wildcard `*`. | TV1 | **CLEAR** | §4.2 p. 41 |
| **NFR-SEC-006** | Resource Ownership | SEC | Phân quyền tầng Application (`RecipeAuthorizationHandler` xác minh Author-Owner). | TV1/TV2 | **CLEAR** | §4.2 p. 42 |
| **NFR-SEC-007** | Secrets Management | SEC | Không commit secrets vào Git (User Secrets ở dev, env vars / Docker secrets ở prod). | Toàn đội | **CLEAR** | §4.2 p. 42 |
| **NFR-USE-001** | Responsive Design | USE | Tối ưu hiển thị Mobile (320-767px), Tablet (768-1199px), Desktop (≥1200px). | TV3 | **CLEAR** | §4.3 p. 42 |
| **NFR-USE-002** | Accessibility (a11y) | USE | Tuân thủ WCAG 2.1 Level AA (semantic HTML5, ARIA, keyboard navigation, contrast ≥ 4.5:1). | TV3 | **CLEAR** | §4.3 p. 42 |
| **NFR-USE-003** | Error Messages | USE | Trả về chuẩn RFC 7807 Problem Details; frontend inline validation trực quan. | TV1/TV3 | **CONFLICT** | §4.3 p. 42 (Xung đột mã lỗi 422 vs 400) |
| **NFR-USE-004** | Loading States | USE | Visual feedback cho mọi async operation (Skeleton loading, Toast notifications). | TV3 | **CLEAR** | §4.3 p. 42 |
| **NFR-REL-001** | Uptime SLA | REL | Uptime ≥ 99.5% (~ 3.65 giờ downtime/năm). Health check định kỳ mỗi 10s. | TV4 | **CLEAR** | §4.4 p. 43 |
| **NFR-REL-002** | Error Resilience | REL | Global Exception Handler; Redis down tự động fallback DB; Hangfire retry 3 lần. | TV1/TV4 | **CLEAR** | §4.4 p. 43 |
| **NFR-REL-003** | Data Durability | REL | PostgreSQL WAL đảm bảo ACID; Backup tự động hàng ngày 03:00 AM; Soft delete. | TV2 | **CONFLICT** | §4.4 p. 43 (Soft delete vs Hard delete) |
| **NFR-MAINT-001**| Code Quality | MAINT | Pass static analysis trước khi merge (.NET SonarAnalyzer, ESLint, không warnings). | Toàn đội | **CLEAR** | §4.5 p. 43 |
| **NFR-MAINT-002**| Test Coverage | MAINT | Unit test coverage ≥ 80% (Domain/Application); Integration test cho luồng chính. | Toàn đội | **CLEAR** | §4.5 p. 43–44 |
| **NFR-MAINT-003**| Documentation | MAINT | Swagger/OpenAPI tự động sinh kèm XML comments; README hướng dẫn cài đặt. | Toàn đội | **CLEAR** | §4.5 p. 44 |
| **NFR-MAINT-004**| Clean Architecture | MAINT | Domain độc lập; Application chỉ phụ thuộc Domain; ArchUnit test kiểm tra phụ thuộc. | TV1 | **CLEAR** | §4.5 p. 44 |
| **NFR-SCALE-001**| Stateless Backend | SCALE | Backend stateless hỗ trợ mở rộng ngang (JWT, Redis distributed cache, RedLock). | TV1/TV4 | **CONFLICT** | §4.6 p. 44 (Redis vs IMemoryCache) |
| **NFR-SCALE-002**| Database Scaling | SCALE | Connection pooling (max 100 conn/instance); Index B-tree và GIN tsvector. | TV2 | **CLEAR** | §4.6 p. 44 |
| **NFR-SCALE-003**| Infra Scaling | SCALE | Kiến trúc container hóa từng service (API, Postgres, Redis, MinIO, Nginx). | Toàn đội | **CLEAR** | §4.6 p. 44 |
| **NFR-SEO-001** | Structured Data | SEO | JSON-LD Schema.org Recipe markup cho trang công thức (đạt Google Rich Results). | TV3 | **CLEAR** | §4.7 p. 44–45 |
| **NFR-SEO-002** | Meta & Open Graph | SEO | Đầy đủ title, meta description (150-160 ký tự), Open Graph images (1200x630px). | TV3 | **CLEAR** | §4.7 p. 45 |
| **NFR-SEO-003** | Sitemap & Robots | SEO | Tự động sinh sitemap.xml hàng ngày và ping Google Search Console qua FR-JOB-003. | TV4 | **CLEAR** | §4.7 p. 45 |
| **NFR-SEO-004** | URL Structure | SEO | URL thân thiện chuẩn SEO (`/recipes/{slug}`, `/categories/{slug}`). | TV2/TV3 | **CLEAR** | §4.7 p. 45 |

---

## 6. Functional Requirements Detailed Audit (34 explicit FRs)

### FR-AUTH-001: Đăng ký Tài khoản Mới (User Registration)
- **ID:** FR-AUTH-001
- **Name:** Đăng ký Tài khoản Mới
- **Actor:** Khách (Guest / Anonymous User)
- **Priority:** Must Have (M)
- **Purpose:** Cho phép người dùng tạo tài khoản mới trong hệ thống Culinary Blog bằng email và mật khẩu.
- **Preconditions:** Khách chưa đăng nhập; email chưa được đăng ký trong hệ thống.
- **Inputs:** `RegisterRequest { email, password, confirmPassword, fullName }`.
- **Outputs:** `AuthResponseDto { accessToken, refreshToken, expiresAt, user: { id, fullName/displayName, email, userName, avatarUrl, roles } }` (hoặc chỉ trả User info).
- **Happy path:** 1. Client POST `/api/v1/auth/register` $\rightarrow$ 2. MediatR dispatch `RegisterCommand` $\rightarrow$ 3. `ValidationBehavior` validate hợp lệ $\rightarrow$ 4. Check email tồn tại $\rightarrow$ 5. Tạo `ApplicationUser` $\rightarrow$ 6. `UserManager.CreateAsync` (PBKDF2 hash) $\rightarrow$ 7. Gán role "Author" $\rightarrow$ 8. Sinh Access Token (15m) $\rightarrow$ 9. Sinh Refresh Token (7d) $\rightarrow$ 10. Lưu `RefreshToken` vào DB $\rightarrow$ 11. Enqueue `FR-JOB-001` (Welcome Email) qua Hangfire $\rightarrow$ 12. Trả về HTTP 201 Created.
- **Alternate/error paths:** A1: Email đã tồn tại $\rightarrow$ HTTP 409 Conflict; A2: Password yếu $\rightarrow$ HTTP 422 Unprocessable; A3: Input validation fail $\rightarrow$ HTTP 422 (hoặc 400); A4: Database lỗi $\rightarrow$ HTTP 500.
- **Validation:** Email chuẩn RFC 5322; Password >= 8 ký tự, gồm chữ hoa, thường, số, ký tự đặc biệt; Password == ConfirmPassword; FullName 2–100 ký tự.
- **Authorization:** Anonymous (không yêu cầu).
- **HTTP method:** POST
- **Endpoint:** `/api/v1/auth/register`
- **Success status:** 201 Created
- **Error statuses:** 400 Bad Request, 409 Conflict, 422 Unprocessable Entity, 500 Internal Server Error
- **Entities affected:** `ApplicationUser`, `RefreshToken`
- **Cache behavior:** Không cache; không tác động cache.
- **External dependency:** ASP.NET Core Identity, Hangfire (`FR-JOB-001`).
- **Owner:** TV1 (Primary), TV3 (Auth UI), TV4 (Hangfire email).
- **Dependencies on other members:** TV2 (ApplicationDbContext integration), TV3 (Register form UI), TV4 (Welcome email job).
- **Related NFR:** `NFR-SEC-001` (PBKDF2), `NFR-SEC-002` (Token security), `NFR-SEC-003` (Rate limiting).
- **Related CONS:** `CONS-004` (JWT stateless), `CONS-008` (FluentValidation).
- **Related API Chapter 8:** §8.1 trang 61.
- **Related Data Model:** §7.7 (ApplicationUser) trang 59, §7.8 (RefreshToken) trang 59.
- **Conflict IDs:** `CONFLICT-009`, `CONFLICT-010`, `CONFLICT-011`, `CONFLICT-018`.
- **Technical Risk IDs:** `TECH-RISK-002`.
- **Status:** **CONFLICT**

### FR-AUTH-002: Đăng nhập Tài khoản (User Login)
- **ID:** FR-AUTH-002
- **Name:** Đăng nhập Tài khoản
- **Actor:** Người dùng đã có tài khoản (Guest/Author/Admin)
- **Priority:** Must Have (M)
- **Purpose:** Xác thực người dùng và cấp bộ token (Access + Refresh) để duy trì phiên làm việc.
- **Preconditions:** Tài khoản tồn tại trong hệ thống; chưa bị vô hiệu hóa (IsActive = true).
- **Inputs:** `LoginRequest { email, password }`.
- **Outputs:** `AuthResponseDto { accessToken, refreshToken, expiresAt, user }`.
- **Happy path:** 1. POST `/api/v1/auth/login` $\rightarrow$ 2. Find user by email $\rightarrow$ 3. Kiểm tra IsActive $\rightarrow$ 4. `CheckPasswordSignInAsync` $\rightarrow$ 5. Reset `AccessFailedCount` $\rightarrow$ 6. Sinh token pair $\rightarrow$ 7. Trả HTTP 200 OK.
- **Alternate/error paths:** A1: Sai mật khẩu $\rightarrow$ Tăng `AccessFailedCount`, nếu đạt 5 lần sai liên tiếp $\rightarrow$ Khóa 15 phút (`LockoutEnd`), trả HTTP 401 hoặc 423; A2: User bị ban $\rightarrow$ HTTP 403.
- **Validation:** Email và Password không được để trống.
- **Authorization:** Anonymous.
- **HTTP method:** POST
- **Endpoint:** `/api/v1/auth/login`
- **Success status:** 200 OK
- **Error statuses:** 400 Bad Request, 401 Unauthorized, 403 Forbidden, 423 Locked
- **Entities affected:** `ApplicationUser`, `RefreshToken`
- **Cache behavior:** Không cache.
- **External dependency:** ASP.NET Core Identity.
- **Owner:** TV1 (Primary), TV3 (Login UI).
- **Dependencies on other members:** TV3 (Login page).
- **Related NFR:** `NFR-SEC-001`, `NFR-SEC-002`, `NFR-SEC-003` (10 req/min/IP).
- **Related CONS:** `CONS-004`.
- **Related API Chapter 8:** §8.1 trang 61.
- **Related Data Model:** §7.7, §7.8.
- **Conflict IDs:** `CONFLICT-011`.
- **Technical Risk IDs:** `TECH-RISK-001`.
- **Status:** **CLEAR**

### FR-AUTH-003: Đăng nhập bằng Google OAuth (Google Social Login)
- **ID:** FR-AUTH-003
- **Name:** Đăng nhập qua Google OAuth
- **Actor:** Khách có tài khoản Google
- **Priority:** Should Have (S)
- **Purpose:** Hỗ trợ đăng nhập nhanh hoặc tự động tạo tài khoản qua Google OAuth 2.0.
- **Preconditions:** Google Client ID & Secret đã cấu hình; tài khoản Google hợp lệ.
- **Inputs:** Google credentials (ExternalLoginInfo / Authorization Code / ID Token).
- **Outputs:** `AuthResponseDto { accessToken, refreshToken, expiresAt, user }`.
- **Happy path:** 1. Xác thực Google credentials $\rightarrow$ 2. Trích xuất email, name, avatar $\rightarrow$ 3. Kiểm tra user trong DB (nếu có thì liên kết, nếu chưa thì tạo mới) $\rightarrow$ 4. Gán role "Author" $\rightarrow$ 5. Cấp token pair $\rightarrow$ 6. Trả 200 OK.
- **Alternate/error paths:** A1: Google token/code không hợp lệ $\rightarrow$ HTTP 401; A2: Email bị ban $\rightarrow$ HTTP 403.
- **Validation:** Token Google không rỗng và pass Google signature verification.
- **Authorization:** Anonymous.
- **HTTP method:** POST
- **Endpoint:** `/api/v1/auth/google`
- **Success status:** 200 OK
- **Error statuses:** 400 Bad Request, 401 Unauthorized, 403 Forbidden
- **Entities affected:** `ApplicationUser`, `RefreshToken`
- **Cache behavior:** Không cache.
- **External dependency:** Google OAuth2 API, ASP.NET Core Identity.
- **Owner:** TV1 (Primary), TV3 (Google login button).
- **Dependencies on other members:** TV3 (Google OAuth client flow).
- **Related NFR:** `NFR-SEC-002`, `NFR-SEC-003`.
- **Related CONS:** `CONS-004`.
- **Related API Chapter 8:** §8.1 trang 61.
- **Related Data Model:** §7.7, §7.8.
- **Conflict IDs:** `CONFLICT-013`.
- **Technical Risk IDs:** `TECH-RISK-014`.
- **Status:** **CONFLICT**

### FR-AUTH-004: Làm mới Access Token (Token Refresh)
- **ID:** FR-AUTH-004
- **Name:** Làm mới Access Token
- **Actor:** Người dùng đã xác thực nhưng Access Token hết hạn
- **Priority:** Must Have (M)
- **Purpose:** Cấp Access Token mới từ Refresh Token mà không bắt người dùng đăng nhập lại.
- **Preconditions:** Client gửi Refresh Token hợp lệ (chưa hết hạn, chưa bị revoke).
- **Inputs:** `RefreshTokenRequest { refreshToken }`.
- **Outputs:** `AuthResponseDto` chứa Access Token mới và Refresh Token mới.
- **Happy path:** 1. POST `/api/v1/auth/refresh-token` $\rightarrow$ 2. Hash SHA-256 raw token $\rightarrow$ 3. Tìm trong DB $\rightarrow$ 4. Đánh dấu token cũ là Revoked $\rightarrow$ 5. Cấp cặp token mới (Rotation) $\rightarrow$ 6. Lưu token mới vào DB $\rightarrow$ 7. Trả 200 OK.
- **Alternate/error paths:** A1: Token hết hạn/không tồn tại $\rightarrow$ HTTP 401; A2: Token đã bị revoke được dùng lại $\rightarrow$ Reuse Detection kích hoạt: thu hồi toàn bộ token family, trả HTTP 401.
- **Validation:** Chuỗi refreshToken không được rỗng.
- **Authorization:** Anonymous (dùng refresh token làm credential).
- **HTTP method:** POST
- **Endpoint:** `/api/v1/auth/refresh-token`
- **Success status:** 200 OK
- **Error statuses:** 400 Bad Request, 401 Unauthorized
- **Entities affected:** `RefreshToken`
- **Cache behavior:** Không cache.
- **External dependency:** DbContext.
- **Owner:** TV1 (Primary), TV3 (Axios/Fetch interceptor).
- **Dependencies on other members:** TV3 (Silent refresh token interceptor).
- **Related NFR:** `NFR-SEC-002`.
- **Related CONS:** `CONS-004`.
- **Related API Chapter 8:** §8.1 trang 61.
- **Related Data Model:** §7.8.
- **Conflict IDs:** `CONFLICT-018`.
- **Technical Risk IDs:** `TECH-RISK-002`.
- **Status:** **CONFLICT**

### FR-AUTH-005: Đăng xuất (User Logout)
- **ID:** FR-AUTH-005
- **Name:** Đăng xuất
- **Actor:** Người dùng đang đăng nhập
- **Priority:** Must Have (M)
- **Purpose:** Hủy bỏ phiên làm việc của người dùng bằng cách thu hồi Refresh Token.
- **Preconditions:** Người dùng đã gửi Refresh Token cần hủy.
- **Inputs:** `LogoutRequest { refreshToken }` hoặc Bearer Token.
- **Outputs:** Thông báo đăng xuất thành công.
- **Happy path:** 1. Tìm RefreshToken theo hash $\rightarrow$ 2. Cập nhật `RevokedAt = DateTime.UtcNow` $\rightarrow$ 3. Trả HTTP 200 OK hoặc 204 No Content.
- **Alternate/error paths:** Token không tồn tại $\rightarrow$ Vẫn trả thành công (idempotent).
- **Validation:** Token không rỗng.
- **Authorization:** Yêu cầu đã xác thực (Bearer Token).
- **HTTP method:** POST
- **Endpoint:** `/api/v1/auth/logout`
- **Success status:** 200 OK / 204 No Content
- **Error statuses:** 401 Unauthorized
- **Entities affected:** `RefreshToken`
- **Cache behavior:** Không cache.
- **External dependency:** DbContext.
- **Owner:** TV1 (Primary), TV3 (Logout button).
- **Dependencies on other members:** TV3 (Xóa client token state).
- **Related NFR:** `NFR-SEC-002`.
- **Related CONS:** `CONS-004`.
- **Related API Chapter 8:** §8.1 trang 62.
- **Related Data Model:** §7.8.
- **Conflict IDs:** Không.
- **Technical Risk IDs:** Không.
- **Status:** **CLEAR**

### FR-AUTH-006: Xem Thông tin Cá nhân (Get Current User Profile)
- **ID:** FR-AUTH-006
- **Name:** Xem Thông tin Cá nhân
- **Actor:** Người dùng đang đăng nhập
- **Priority:** Must Have (M)
- **Purpose:** Trả về thông tin hồ sơ của chính người dùng hiện tại (`/auth/me`).
- **Preconditions:** Access Token hợp lệ trong Authorization header.
- **Inputs:** None (lấy UserId từ token claims).
- **Outputs:** `UserProfileDto { id, email, fullName/displayName, avatarUrl, bio, role, createdAt }`.
- **Happy path:** 1. GET `/api/v1/auth/me` $\rightarrow$ 2. JwtMiddleware xác thực $\rightarrow$ 3. Query ApplicationUser từ DB $\rightarrow$ 4. Map DTO $\rightarrow$ 5. Trả 200 OK.
- **Alternate/error paths:** Token hết hạn hoặc không truyền $\rightarrow$ HTTP 401 Unauthorized.
- **Validation:** Token hợp lệ.
- **Authorization:** Authenticated user.
- **HTTP method:** GET
- **Endpoint:** `/api/v1/auth/me`
- **Success status:** 200 OK
- **Error statuses:** 401 Unauthorized, 404 Not Found
- **Entities affected:** `ApplicationUser`
- **Cache behavior:** Private, không cache public.
- **External dependency:** DbContext, CurrentUserService.
- **Owner:** TV1 (Primary), TV3 (User session loader).
- **Dependencies on other members:** TV3 (Auth context provider).
- **Related NFR:** `NFR-SEC-002`.
- **Related CONS:** `CONS-004`.
- **Related API Chapter 8:** §8.1 trang 62.
- **Related Data Model:** §7.7.
- **Conflict IDs:** `CONFLICT-009`.
- **Technical Risk IDs:** Không.
- **Status:** **CLEAR**

### FR-AUTH-007: Cập nhật Thông tin Cá nhân (Update User Profile)
- **ID:** FR-AUTH-007
- **Name:** Cập nhật Thông tin Cá nhân
- **Actor:** Người dùng đang đăng nhập
- **Priority:** Should Have (S)
- **Purpose:** Cho phép người dùng chỉnh sửa thông tin hiển thị (DisplayName/FullName, Bio, AvatarUrl).
- **Preconditions:** Người dùng đã xác thực.
- **Inputs:** `UpdateProfileRequest { displayName, bio, avatarUrl }`.
- **Outputs:** `UserProfileDto` sau cập nhật.
- **Happy path:** 1. PUT `/api/v1/auth/profile` $\rightarrow$ 2. Validate input $\rightarrow$ 3. Cập nhật User entity $\rightarrow$ 4. Lưu DB $\rightarrow$ 5. Trả 200 OK.
- **Alternate/error paths:** A1: Dữ liệu vi phạm độ dài $\rightarrow$ HTTP 422 (hoặc 400); A2: Token thiếu $\rightarrow$ HTTP 401.
- **Validation:** DisplayName 2–50 ký tự; Bio max 500 ký tự; AvatarUrl định dạng URL hợp lệ. Email và Role KHÔNG được đổi qua endpoint này.
- **Authorization:** Authenticated user.
- **HTTP method:** PUT
- **Endpoint:** `/api/v1/auth/profile`
- **Success status:** 200 OK
- **Error statuses:** 400 Bad Request, 401 Unauthorized, 422 Unprocessable Entity
- **Entities affected:** `ApplicationUser`
- **Cache behavior:** Invalidate user cache nếu có.
- **External dependency:** DbContext.
- **Owner:** TV1 (Primary), TV3 (Profile edit page).
- **Dependencies on other members:** TV3 (Profile form UI).
- **Related NFR:** `NFR-SEC-004`.
- **Related CONS:** `CONS-008`.
- **Related API Chapter 8:** §8.1 trang 62.
- **Related Data Model:** §7.7.
- **Conflict IDs:** `CONFLICT-009`, `CONFLICT-011`.
- **Technical Risk IDs:** Không.
- **Status:** **CONFLICT**

### FR-CAT-001: Xem Danh sách Danh mục (Get Categories)
- **ID:** FR-CAT-001
- **Name:** Xem Danh sách Danh mục
- **Actor:** Tất cả (Guest / Author / Admin)
- **Priority:** Must Have (M)
- **Purpose:** Lấy toàn bộ danh mục công thức kèm số lượng công thức Published (`recipeCount`).
- **Preconditions:** None.
- **Inputs:** None.
- **Outputs:** `CategoryDto[] { id, name, slug, description, imageUrl, recipeCount }`.
- **Happy path:** 1. GET `/api/v1/categories` $\rightarrow$ 2. Kiểm tra cache $\rightarrow$ 3. Nếu miss, query DB kèm count $\rightarrow$ 4. Lưu cache $\rightarrow$ 5. Trả 200 OK.
- **Alternate/error paths:** DB trống $\rightarrow$ Trả mảng rỗng `[]` (200 OK).
- **Validation:** None.
- **Authorization:** Anonymous.
- **HTTP method:** GET
- **Endpoint:** `/api/v1/categories`
- **Success status:** 200 OK
- **Error statuses:** 500 Internal Server Error
- **Entities affected:** `Category`, `Recipe`
- **Cache behavior:** Cache danh sách category với TTL 60m (IMemoryCache) hoặc 30m (Redis).
- **External dependency:** Cache engine (Redis / MemoryCache).
- **Owner:** TV4 (Primary), TV3 (Category list UI).
- **Dependencies on other members:** TV2 (Recipe table foreign key relation).
- **Related NFR:** `NFR-PERF-003`, `NFR-SCALE-001`.
- **Related CONS:** `CONS-005`.
- **Related API Chapter 8:** §8.2 trang 62.
- **Related Data Model:** §7.6.
- **Conflict IDs:** `CONFLICT-020`.
- **Technical Risk IDs:** Không.
- **Status:** **CONFLICT**

### FR-CAT-002: Xem Chi tiết Danh mục và Công thức (Get Category Detail)
- **ID:** FR-CAT-002
- **Name:** Xem Chi tiết Danh mục và Công thức
- **Actor:** Tất cả (Guest / Author / Admin)
- **Priority:** Must Have (M)
- **Purpose:** Xem thông tin một danh mục cụ thể theo slug kèm danh sách công thức phân trang trực thuộc.
- **Preconditions:** Danh mục tồn tại theo slug.
- **Inputs:** Route param `slug`, query params `page`, `pageSize`.
- **Outputs:** `CategoryDetailDto` kèm phân trang recipes.
- **Happy path:** 1. GET `/api/v1/categories/{slug}` $\rightarrow$ 2. Query Category by Slug $\rightarrow$ 3. Query Recipes phân trang where Status=Published $\rightarrow$ 4. Trả 200 OK.
- **Alternate/error paths:** Slug không tồn tại $\rightarrow$ HTTP 404 Not Found.
- **Validation:** Slug hợp lệ; page >= 1, pageSize 1–50.
- **Authorization:** Anonymous.
- **HTTP method:** GET
- **Endpoint:** `/api/v1/categories/{slug}`
- **Success status:** 200 OK
- **Error statuses:** 400 Bad Request, 404 Not Found
- **Entities affected:** `Category`, `Recipe`
- **Cache behavior:** ISR revalidate = 600s phía Next.js.
- **External dependency:** DbContext.
- **Owner:** TV4 (Primary), TV3 (Category detail UI).
- **Dependencies on other members:** TV2 (Recipe queries).
- **Related NFR:** `NFR-PERF-001`, `NFR-SEO-004`.
- **Related CONS:** `CONS-005`.
- **Related API Chapter 8:** §8.2 trang 63.
- **Related Data Model:** §7.6, §7.2.
- **Conflict IDs:** `CONFLICT-012`.
- **Technical Risk IDs:** Không.
- **Status:** **CONFLICT**

### FR-CAT-003: Tạo Danh mục Mới (Create Category)
- **ID:** FR-CAT-003
- **Name:** Tạo Danh mục Mới
- **Actor:** Quản trị viên (Admin)
- **Priority:** Must Have (M)
- **Purpose:** Cho phép Admin tạo danh mục công thức mới, tự động sinh slug duy nhất từ tên.
- **Preconditions:** User đăng nhập với role Admin; tên danh mục chưa tồn tại.
- **Inputs:** `CreateCategoryRequest { name, description, imageUrl?, orderIndex? }`.
- **Outputs:** `CategoryDto` mới tạo.
- **Happy path:** 1. POST `/api/v1/categories` $\rightarrow$ 2. Check Admin role $\rightarrow$ 3. Validate name 2–50 ký tự $\rightarrow$ 4. Slugify sinh slug duy nhất $\rightarrow$ 5. Lưu DB $\rightarrow$ 6. Invalidate cache categories $\rightarrow$ 7. Trả 201 Created.
- **Alternate/error paths:** A1: Không phải Admin $\rightarrow$ HTTP 403; A2: Tên trùng $\rightarrow$ HTTP 409; A3: Validation fail $\rightarrow$ HTTP 422.
- **Validation:** Name 2–50 ký tự, không chứa HTML; Description max 500 ký tự.
- **Authorization:** Role Admin (`RequireAuthorization("Admin")`).
- **HTTP method:** POST
- **Endpoint:** `/api/v1/categories`
- **Success status:** 201 Created
- **Error statuses:** 400 Bad Request, 401 Unauthorized, 403 Forbidden, 409 Conflict, 422 Unprocessable Entity
- **Entities affected:** `Category`
- **Cache behavior:** Invalidate cache danh sách category.
- **External dependency:** DbContext.
- **Owner:** TV4 (Primary), TV1 (Admin Policy).
- **Dependencies on other members:** TV1 (Authorization filter).
- **Related NFR:** `NFR-SEC-006`.
- **Related CONS:** `CONS-008`.
- **Related API Chapter 8:** §8.2 trang 63.
- **Related Data Model:** §7.6.
- **Conflict IDs:** `CONFLICT-011`, `CONFLICT-020`.
- **Technical Risk IDs:** Không.
- **Status:** **CONFLICT**

### FR-CAT-004: Cập nhật Danh mục (Update Category)
- **ID:** FR-CAT-004
- **Name:** Cập nhật Danh mục
- **Actor:** Quản trị viên (Admin)
- **Priority:** Should Have (S)
- **Purpose:** Sửa thông tin danh mục; nếu đổi tên thì tự động sinh slug mới.
- **Preconditions:** User đăng nhập role Admin; Category ID tồn tại.
- **Inputs:** `UpdateCategoryRequest { name, description, imageUrl?, orderIndex? }`.
- **Outputs:** `CategoryDto` sau cập nhật.
- **Happy path:** 1. PUT `/api/v1/categories/{id}` $\rightarrow$ 2. Check Admin $\rightarrow$ 3. Cập nhật fields $\rightarrow$ 4. Invalidate cache $\rightarrow$ 5. Trả 200 OK.
- **Alternate/error paths:** ID không tồn tại $\rightarrow$ HTTP 404; Tên trùng $\rightarrow$ HTTP 409; Không phải Admin $\rightarrow$ HTTP 403.
- **Validation:** Name 2–50 ký tự; Description max 500.
- **Authorization:** Role Admin.
- **HTTP method:** PUT
- **Endpoint:** `/api/v1/categories/{id}`
- **Success status:** 200 OK
- **Error statuses:** 400 Bad Request, 401 Unauthorized, 403 Forbidden, 404 Not Found, 409 Conflict, 422 Unprocessable Entity
- **Entities affected:** `Category`
- **Cache behavior:** Invalidate category cache.
- **External dependency:** DbContext.
- **Owner:** TV4 (Primary), TV1 (Admin Auth).
- **Dependencies on other members:** TV1 (Admin policy).
- **Related NFR:** `NFR-SEC-006`.
- **Related CONS:** `CONS-008`.
- **Related API Chapter 8:** §8.2 trang 63.
- **Related Data Model:** §7.6.
- **Conflict IDs:** `CONFLICT-017`, `CONFLICT-020`.
- **Technical Risk IDs:** Không.
- **Status:** **CONFLICT**

### FR-CAT-005: Xóa Danh mục (Delete Category)
- **ID:** FR-CAT-005
- **Name:** Xóa Danh mục
- **Actor:** Quản trị viên (Admin)
- **Priority:** Should Have (S)
- **Purpose:** Cho phép Admin xóa danh mục khi không còn công thức nào trực thuộc.
- **Preconditions:** Category tồn tại; không chứa công thức nào (RecipeCount == 0).
- **Inputs:** Route param `id`.
- **Outputs:** Message xác nhận hoặc empty.
- **Happy path:** 1. DELETE `/api/v1/categories/{id}` $\rightarrow$ 2. Check Admin $\rightarrow$ 3. Kiểm tra liên kết Recipe (nếu > 0 thì chặn) $\rightarrow$ 4. Xóa Category $\rightarrow$ 5. Invalidate cache $\rightarrow$ 6. Trả 200 OK / 204 No Content.
- **Alternate/error paths:** Còn recipe liên kết $\rightarrow$ HTTP 400 Bad Request ("CATEGORY_HAS_RECIPES"); ID không tồn tại $\rightarrow$ HTTP 404.
- **Validation:** ID hợp lệ.
- **Authorization:** Role Admin.
- **HTTP method:** DELETE
- **Endpoint:** `/api/v1/categories/{id}`
- **Success status:** 200 OK / 204 No Content
- **Error statuses:** 400 Bad Request, 401 Unauthorized, 403 Forbidden, 404 Not Found
- **Entities affected:** `Category`
- **Cache behavior:** Invalidate category cache.
- **External dependency:** DbContext.
- **Owner:** TV4 (Primary), TV2 (Recipe FK check).
- **Dependencies on other members:** TV2 (Recipe relationship).
- **Related NFR:** `NFR-REL-003`.
- **Related CONS:** `CONS-005`.
- **Related API Chapter 8:** §8.2 trang 63.
- **Related Data Model:** §7.6.
- **Conflict IDs:** `CONFLICT-002`.
- **Technical Risk IDs:** Không.
- **Status:** **CONFLICT**

### FR-RCP-001: Xem Danh sách Công thức (Get Recipe List)
- **ID:** FR-RCP-001
- **Name:** Xem Danh sách Công thức
- **Actor:** Tất cả (Guest / Author / Admin)
- **Priority:** Must Have (M)
- **Purpose:** Trả về danh sách công thức có phân trang, lọc đa tiêu chí và sắp xếp.
- **Preconditions:** None.
- **Inputs:** Query params: `page`, `pageSize`, `categoryId`, `difficulty`, `maxCookTime`, `minServings`, `sort`.
- **Outputs:** `PagedResult<RecipeSummaryDto>`.
- **Happy path:** 1. GET `/api/v1/recipes` $\rightarrow$ 2. Filter theo query params $\rightarrow$ 3. Lọc trạng thái (Guest chỉ thấy Published, Author thấy thêm bài của mình) $\rightarrow$ 4. Sắp xếp $\rightarrow$ 5. Skip/Take phân trang $\rightarrow$ 6. Trả 200 OK.
- **Alternate/error paths:** Query params không hợp lệ $\rightarrow$ HTTP 400 Bad Request.
- **Validation:** page >= 1, pageSize 1–50, sort hợp lệ.
- **Authorization:** Anonymous (Author gửi token để xem bài draft của mình).
- **HTTP method:** GET
- **Endpoint:** `/api/v1/recipes`
- **Success status:** 200 OK
- **Error statuses:** 400 Bad Request, 500 Internal Server Error
- **Entities affected:** `Recipe`, `Category`, `ApplicationUser`, `RecipeImage`
- **Cache behavior:** Không cache hoặc cache dynamic query 1m trong Redis.
- **External dependency:** DbContext, Redis.
- **Owner:** TV2 (Primary), TV3 (Recipe list UI).
- **Dependencies on other members:** TV3 (Recipe feed & filter UI).
- **Related NFR:** `NFR-PERF-001`, `NFR-PERF-003`.
- **Related CONS:** `CONS-005`.
- **Related API Chapter 8:** §8.3 trang 63.
- **Related Data Model:** §7.2.
- **Conflict IDs:** `CONFLICT-003`, `CONFLICT-012`, `CONFLICT-021`.
- **Technical Risk IDs:** Không.
- **Status:** **CONFLICT**

### FR-RCP-002: Xem Chi tiết Công thức (Get Recipe Detail)
- **ID:** FR-RCP-002
- **Name:** Xem Chi tiết Công thức Nấu ăn
- **Actor:** Tất cả (Guest / Author / Admin)
- **Priority:** Must Have (M)
- **Purpose:** Trả về toàn bộ thông tin chi tiết của một công thức theo slug (kèm steps, ingredients, nutrition, images, author).
- **Preconditions:** Recipe tồn tại; nếu Draft/Archived thì người yêu cầu phải là Owner hoặc Admin.
- **Inputs:** Route param `slug`.
- **Outputs:** `RecipeDetailDto`.
- **Happy path:** 1. GET `/api/v1/recipes/{slug}` $\rightarrow$ 2. Eager loading include các bảng con $\rightarrow$ 3. Check quyền nếu Draft $\rightarrow$ 4. Map DTO $\rightarrow$ 5. Lưu cache $\rightarrow$ 6. Trả 200 OK.
- **Alternate/error paths:** Slug không tồn tại $\rightarrow$ HTTP 404; Recipe Draft mà không có quyền $\rightarrow$ HTTP 403 Forbidden.
- **Validation:** Slug không rỗng.
- **Authorization:** Anonymous (hoặc Resource-based nếu Draft).
- **HTTP method:** GET
- **Endpoint:** `/api/v1/recipes/{slug}`
- **Success status:** 200 OK
- **Error statuses:** 403 Forbidden, 404 Not Found
- **Entities affected:** `Recipe`, `RecipeStep`, `RecipeIngredient`, `RecipeImage`, `RecipeNutrition`, `Category`, `ApplicationUser`
- **Cache behavior:** Cache với OutputCache (TTL 60m) hoặc Redis cache-aside (TTL 5m).
- **External dependency:** Redis / OutputCache.
- **Owner:** TV2 (Primary), TV3 (Recipe Detail UI).
- **Dependencies on other members:** TV3 (Detail UI, JSON-LD Schema.org).
- **Related NFR:** `NFR-PERF-001`, `NFR-PERF-003`, `NFR-SEO-001`.
- **Related CONS:** `CONS-005`.
- **Related API Chapter 8:** §8.3 trang 63.
- **Related Data Model:** §7.2, §7.2.1, §7.3, §7.4, §7.5.
- **Conflict IDs:** `CONFLICT-005`, `CONFLICT-006`, `CONFLICT-007`, `CONFLICT-008`, `CONFLICT-019`.
- **Technical Risk IDs:** `TECH-RISK-008` (N+1 query).
- **Status:** **CONFLICT**

### FR-RCP-003: Tạo Công thức Nấu ăn Mới (Create Recipe)
- **ID:** FR-RCP-003
- **Name:** Tạo Công thức Nấu ăn Mới
- **Actor:** Tác giả (Author) hoặc Admin
- **Priority:** Must Have (M)
- **Purpose:** Cho phép Author tạo bài viết công thức mới ở trạng thái Draft.
- **Preconditions:** Đã đăng nhập với role Author hoặc Admin.
- **Inputs:** `CreateRecipeRequest { title, description, categoryId, prepTimeMinutes, cookTimeMinutes, servings, difficulty, steps?, ingredients?, nutrition? }`.
- **Outputs:** `RecipeDetailDto` mới tạo.
- **Happy path:** 1. POST `/api/v1/recipes` $\rightarrow$ 2. Validate input $\rightarrow$ 3. Sinh slug duy nhất $\rightarrow$ 4. Gán `AuthorId = currentUserId`, `Status = Draft` $\rightarrow$ 5. Lưu DB $\rightarrow$ 6. Trả 201 Created.
- **Alternate/error paths:** Chưa đăng nhập $\rightarrow$ HTTP 401; Validate fail $\rightarrow$ HTTP 422; Category không tồn tại $\rightarrow$ HTTP 400.
- **Validation:** Title 5–200 ký tự; Servings >= 1; Times >= 0; Difficulty thuộc Easy/Medium/Hard.
- **Authorization:** Role Author / Admin.
- **HTTP method:** POST
- **Endpoint:** `/api/v1/recipes`
- **Success status:** 201 Created
- **Error statuses:** 400 Bad Request, 401 Unauthorized, 403 Forbidden, 422 Unprocessable Entity
- **Entities affected:** `Recipe`, `RecipeStep`, `RecipeIngredient`, `RecipeNutrition`
- **Cache behavior:** Invalidate cache danh sách recipes.
- **External dependency:** DbContext.
- **Owner:** TV2 (Primary), TV1 (Auth Context), TV3 (Recipe Wizard UI).
- **Dependencies on other members:** TV1 (CurrentUserService), TV3 (Multi-step wizard UI).
- **Related NFR:** `NFR-SEC-004`, `NFR-SEC-006`.
- **Related CONS:** `CONS-002`, `CONS-008`.
- **Related API Chapter 8:** §8.3 trang 63.
- **Related Data Model:** §7.2, §7.2.1, §7.3, §7.4.
- **Conflict IDs:** `CONFLICT-004`, `CONFLICT-005`, `CONFLICT-006`, `CONFLICT-007`, `CONFLICT-008`, `CONFLICT-011`.
- **Technical Risk IDs:** `TECH-RISK-003` (Slug collision).
- **Status:** **CONFLICT**

### FR-RCP-004: Cập nhật Công thức (Update Recipe)
- **ID:** FR-RCP-004
- **Name:** Cập nhật Công thức Nấu ăn
- **Actor:** Tác giả sở hữu (Author-Owner) hoặc Admin
- **Priority:** Must Have (M)
- **Purpose:** Cho phép tác giả cập nhật nội dung công thức, chống xung đột ghi đè đồng thời qua RowVersion.
- **Preconditions:** Recipe tồn tại; User là Author-Owner hoặc Admin; truyền `RowVersion` hiện tại.
- **Inputs:** `UpdateRecipeRequest { title, description, categoryId, prepTimeMinutes, cookTimeMinutes, servings, difficulty, rowVersion, steps?, ingredients?, nutrition? }`.
- **Outputs:** `RecipeDetailDto` sau cập nhật.
- **Happy path:** 1. PUT `/api/v1/recipes/{id}` $\rightarrow$ 2. Check quyền sở hữu $\rightarrow$ 3. So sánh RowVersion $\rightarrow$ 4. Cập nhật dữ liệu $\rightarrow$ 5. SaveChanges $\rightarrow$ 6. Invalidate cache $\rightarrow$ 7. Trả 200 OK.
- **Alternate/error paths:** RowVersion mismatch $\rightarrow$ ném ConcurrencyConflictException $\rightarrow$ HTTP 409 (hoặc 422); Không phải owner $\rightarrow$ HTTP 403; ID không tồn tại $\rightarrow$ HTTP 404.
- **Validation:** Giống tạo mới; RowVersion bắt buộc.
- **Authorization:** Author-Owner hoặc Admin.
- **HTTP method:** PUT
- **Endpoint:** `/api/v1/recipes/{id}`
- **Success status:** 200 OK
- **Error statuses:** 400 Bad Request, 401 Unauthorized, 403 Forbidden, 404 Not Found, 409 Conflict, 422 Unprocessable Entity
- **Entities affected:** `Recipe`, `RecipeStep`, `RecipeIngredient`, `RecipeNutrition`
- **Cache behavior:** Invalidate cache chi tiết và danh sách recipe.
- **External dependency:** DbContext.
- **Owner:** TV2 (Primary), TV1 (Resource ownership).
- **Dependencies on other members:** TV1 (RecipeAuthorizationHandler), TV3 (Edit UI).
- **Related NFR:** `NFR-SEC-006`.
- **Related CONS:** `CONS-002`, `CONS-006`.
- **Related API Chapter 8:** §8.3 trang 63.
- **Related Data Model:** §7.2.
- **Conflict IDs:** `CONFLICT-014`.
- **Technical Risk IDs:** Không.
- **Status:** **CONFLICT**

### FR-RCP-005: Xuất bản / Hủy Xuất bản Công thức (Publish / Unpublish Recipe)
- **ID:** FR-RCP-005
- **Name:** Xuất bản / Hủy Xuất bản Công thức
- **Actor:** Tác giả sở hữu (Author-Owner) hoặc Admin
- **Priority:** Must Have (M)
- **Purpose:** Chuyển trạng thái bài viết giữa `Draft` và `Published`.
- **Preconditions:** Recipe tồn tại; User là Author-Owner hoặc Admin; Để Publish phải có ít nhất 1 step và 1 ingredient.
- **Inputs:** None (thao tác qua route action).
- **Outputs:** `{ id, status }`.
- **Happy path:** 1. PATCH `/api/v1/recipes/{id}/publish` $\rightarrow$ 2. Kiểm tra điều kiện đủ step/ingredient $\rightarrow$ 3. Đổi Status = Published $\rightarrow$ 4. Invalidate cache $\rightarrow$ 5. Trả 200 OK.
- **Alternate/error paths:** Thiếu step hoặc ingredient $\rightarrow$ HTTP 400 Bad Request ("RECIPE_NOT_READY_PUBLISH"); Không có quyền $\rightarrow$ HTTP 403.
- **Validation:** Recipe phải thỏa mãn điều kiện toàn vẹn tối thiểu.
- **Authorization:** Author-Owner hoặc Admin.
- **HTTP method:** PATCH
- **Endpoint:** `/api/v1/recipes/{id}/publish` và `/api/v1/recipes/{id}/unpublish`
- **Success status:** 200 OK
- **Error statuses:** 400 Bad Request, 401 Unauthorized, 403 Forbidden, 404 Not Found
- **Entities affected:** `Recipe`
- **Cache behavior:** Invalidate cache recipes.
- **External dependency:** DbContext.
- **Owner:** TV2 (Primary), TV1 (Auth Policy).
- **Dependencies on other members:** TV1 (Authorization).
- **Related NFR:** `NFR-SEC-006`.
- **Related CONS:** `CONS-002`.
- **Related API Chapter 8:** §8.3 trang 64.
- **Related Data Model:** §7.2.
- **Conflict IDs:** Không.
- **Technical Risk IDs:** Không.
- **Status:** **CLEAR**

### FR-RCP-006: Lưu trữ Công thức (Archive Recipe)
- **ID:** FR-RCP-006
- **Name:** Lưu trữ Công thức
- **Actor:** Tác giả sở hữu (Author-Owner) hoặc Admin
- **Priority:** Should Have (S)
- **Purpose:** Chuyển trạng thái công thức sang `Archived` (ẩn khỏi public feed nhưng không xóa khỏi DB).
- **Preconditions:** Recipe tồn tại; User là Owner hoặc Admin.
- **Inputs:** Route param `id`.
- **Outputs:** `{ id, status: "Archived" }`.
- **Happy path:** 1. PATCH `/api/v1/recipes/{id}/archive` $\rightarrow$ 2. Check quyền $\rightarrow$ 3. Đổi Status = Archived $\rightarrow$ 4. Invalidate cache $\rightarrow$ 5. Trả 200 OK.
- **Alternate/error paths:** Không có quyền $\rightarrow$ HTTP 403; ID sai $\rightarrow$ HTTP 404.
- **Validation:** None.
- **Authorization:** Author-Owner hoặc Admin.
- **HTTP method:** PATCH
- **Endpoint:** `/api/v1/recipes/{id}/archive`
- **Success status:** 200 OK
- **Error statuses:** 401 Unauthorized, 403 Forbidden, 404 Not Found
- **Entities affected:** `Recipe`
- **Cache behavior:** Invalidate cache recipes.
- **External dependency:** DbContext.
- **Owner:** TV2 (Primary), TV1 (Auth).
- **Dependencies on other members:** TV1 (Ownership verification).
- **Related NFR:** `NFR-SEC-006`.
- **Related CONS:** `CONS-002`.
- **Related API Chapter 8:** §8.3 trang 64.
- **Related Data Model:** §7.2.
- **Conflict IDs:** Không.
- **Technical Risk IDs:** Không.
- **Status:** **CLEAR**

### FR-RCP-007: Xóa Công thức (Delete Recipe)
- **ID:** FR-RCP-007
- **Name:** Xóa Công thức Nấu ăn
- **Actor:** Tác giả sở hữu (Author-Owner) hoặc Admin
- **Priority:** Must Have (M)
- **Purpose:** Xóa bài viết công thức cùng toàn bộ ảnh minh họa liên kết.
- **Preconditions:** Recipe tồn tại; User là Author-Owner hoặc Admin.
- **Inputs:** Route param `id`.
- **Outputs:** Empty (204 No Content) hoặc confirmation message.
- **Happy path:** 1. DELETE `/api/v1/recipes/{id}` $\rightarrow$ 2. Check quyền $\rightarrow$ 3. Lấy danh sách URL ảnh $\rightarrow$ 4. Xóa Recipe khỏi DB (Hard hoặc Soft delete) $\rightarrow$ 5. Enqueue Hangfire xóa file trên MinIO $\rightarrow$ 6. Invalidate cache $\rightarrow$ 7. Trả 204 No Content.
- **Alternate/error paths:** Không phải owner $\rightarrow$ HTTP 403; ID không tồn tại $\rightarrow$ HTTP 404.
- **Validation:** ID hợp lệ.
- **Authorization:** Author-Owner hoặc Admin.
- **HTTP method:** DELETE
- **Endpoint:** `/api/v1/recipes/{id}`
- **Success status:** 204 No Content
- **Error statuses:** 401 Unauthorized, 403 Forbidden, 404 Not Found
- **Entities affected:** `Recipe`, `RecipeStep`, `RecipeIngredient`, `RecipeImage`, `RecipeNutrition`
- **Cache behavior:** Invalidate cache recipes.
- **External dependency:** Hangfire, MinIO.
- **Owner:** TV2 (Primary), TV4 (MinIO cleanup job).
- **Dependencies on other members:** TV4 (Hangfire file cleanup).
- **Related NFR:** `NFR-REL-003`.
- **Related CONS:** `CONS-006`.
- **Related API Chapter 8:** §8.3 trang 64.
- **Related Data Model:** §7.2.
- **Conflict IDs:** `CONFLICT-001`.
- **Technical Risk IDs:** Không.
- **Status:** **CONFLICT**

### FR-RCP-008: Quản lý Ảnh Công thức (Manage Recipe Images)
- **ID:** FR-RCP-008
- **Name:** Quản lý Ảnh Công thức
- **Actor:** Tác giả sở hữu (Author-Owner) hoặc Admin
- **Priority:** Must Have (M)
- **Purpose:** Tải lên ảnh mới (tối đa 10 ảnh / recipe), đặt ảnh đại diện chính (IsPrimary), và xóa ảnh.
- **Preconditions:** Recipe tồn tại; User có quyền; số lượng ảnh hiện tại < 10.
- **Inputs:** Multipart form data `file`, `altText`, `isPrimary`.
- **Outputs:** `RecipeImageDto { imageId, originalUrl, altText, isPrimary }`.
- **Happy path:** 1. Validate file (MIME, size <= 5MB, magic bytes) $\rightarrow$ 2. Upload MinIO $\rightarrow$ 3. Tạo `RecipeImage` $\rightarrow$ 4. Trigger `FR-JOB-002` (Thumbnail resize) $\rightarrow$ 5. Trả 201 Created.
- **Alternate/error paths:** File > 5MB $\rightarrow$ HTTP 400; MIME không hợp lệ $\rightarrow$ HTTP 400; MinIO offline $\rightarrow$ HTTP 503.
- **Validation:** MIME type (JPEG/PNG/WebP/AVIF), magic bytes, size <= 5MB.
- **Authorization:** Author-Owner hoặc Admin.
- **HTTP method:** POST / PATCH / DELETE
- **Endpoint:** `/api/v1/recipes/{id}/images`, `/api/v1/recipes/{id}/images/{imageId}`
- **Success status:** 201 Created, 200 OK, 204 No Content
- **Error statuses:** 400 Bad Request, 401 Unauthorized, 403 Forbidden, 404 Not Found, 415 Unsupported Media Type, 503 Service Unavailable
- **Entities affected:** `RecipeImage`
- **Cache behavior:** Invalidate recipe cache.
- **External dependency:** MinIO, Hangfire (`FR-JOB-002`).
- **Owner:** TV2 (Primary), TV4 (MinIO SDK & Hangfire Thumbnail Job).
- **Dependencies on other members:** TV4 (IFileStorageService, Thumbnail Job).
- **Related NFR:** `NFR-SEC-004`, `NFR-REL-003`.
- **Related CONS:** `CONS-007`.
- **Related API Chapter 8:** §8.4 trang 64.
- **Related Data Model:** §7.5.
- **Conflict IDs:** `CONFLICT-023`, `CONFLICT-024`.
- **Technical Risk IDs:** `TECH-RISK-011` (Magic bytes stream reading).
- **Status:** **CONFLICT**

### FR-RCP-009: Quản lý Nguyên liệu Công thức (Manage Recipe Ingredients)
- **ID:** FR-RCP-009
- **Name:** Quản lý Nguyên liệu Công thức
- **Actor:** Tác giả sở hữu (Author-Owner) hoặc Admin
- **Priority:** Must Have (M)
- **Purpose:** Thêm, sửa, xóa từng nguyên liệu thành phần của công thức nấu ăn.
- **Preconditions:** Recipe tồn tại; User có quyền.
- **Inputs:** `CreateRecipeIngredientRequest { name, quantity?, unit?, notes?, sortOrder?/orderIndex? }`.
- **Outputs:** `RecipeIngredientDto`.
- **Happy path:** 1. POST `/api/v1/recipes/{id}/ingredients` $\rightarrow$ 2. Validate input $\rightarrow$ 3. Thêm entity $\rightarrow$ 4. SaveChanges $\rightarrow$ 5. Trả 201 Created.
- **Alternate/error paths:** Validation fail $\rightarrow$ HTTP 422 (hoặc 400); Không có quyền $\rightarrow$ HTTP 403.
- **Validation:** Name 1–100 ký tự; Quantity/Unit tuân thủ phương án thống nhất.
- **Authorization:** Author-Owner hoặc Admin.
- **HTTP method:** POST / PUT / DELETE
- **Endpoint:** `/api/v1/recipes/{id}/ingredients`, `/api/v1/recipes/{id}/ingredients/{ingId}`
- **Success status:** 201 Created, 200 OK, 204 No Content
- **Error statuses:** 400 Bad Request, 401 Unauthorized, 403 Forbidden, 404 Not Found, 422 Unprocessable Entity
- **Entities affected:** `RecipeIngredient`
- **Cache behavior:** Invalidate recipe detail cache.
- **External dependency:** DbContext.
- **Owner:** TV2 (Primary), TV3 (Ingredient form UI).
- **Dependencies on other members:** TV3 (Dynamic input list UI).
- **Related NFR:** `NFR-SEC-004`.
- **Related CONS:** `CONS-008`.
- **Related API Chapter 8:** §8.6 trang 65–66.
- **Related Data Model:** §7.4.
- **Conflict IDs:** `CONFLICT-004`, `CONFLICT-005`, `CONFLICT-011`.
- **Technical Risk IDs:** Không.
- **Status:** **CONFLICT**

### FR-RCP-010: Quản lý Các bước Thực hiện (Manage Recipe Steps)
- **ID:** FR-RCP-010
- **Name:** Quản lý Các bước Nấu ăn
- **Actor:** Tác giả sở hữu (Author-Owner) hoặc Admin
- **Priority:** Must Have (M)
- **Purpose:** Thêm, sửa, xóa các bước hướng dẫn nấu ăn kèm ảnh minh họa và thời lượng.
- **Preconditions:** Recipe tồn tại; User có quyền.
- **Inputs:** `CreateRecipeStepRequest { stepNumber?, title?, description, timerMinutes?/durationMinutes?, imageUrl? }`.
- **Outputs:** `RecipeStepDto`.
- **Happy path:** 1. POST `/api/v1/recipes/{id}/steps` $\rightarrow$ 2. Xác định StepNumber $\rightarrow$ 3. Lưu DB $\rightarrow$ 4. Trả 201 Created. Khi xóa bước: tự động renumber các bước còn lại (1, 2, 3...).
- **Alternate/error paths:** Step không tồn tại $\rightarrow$ HTTP 404; Không có quyền $\rightarrow$ HTTP 403.
- **Validation:** Description không được để trống.
- **Authorization:** Author-Owner hoặc Admin.
- **HTTP method:** POST / PUT / DELETE
- **Endpoint:** `/api/v1/recipes/{id}/steps`, `/api/v1/recipes/{id}/steps/{stepId}`
- **Success status:** 201 Created, 200 OK, 204 No Content
- **Error statuses:** 400 Bad Request, 401 Unauthorized, 403 Forbidden, 404 Not Found, 422 Unprocessable Entity
- **Entities affected:** `RecipeStep`
- **Cache behavior:** Invalidate recipe detail cache.
- **External dependency:** DbContext.
- **Owner:** TV2 (Primary), TV3 (Step form UI).
- **Dependencies on other members:** TV3 (Drag-and-drop step reordering UI).
- **Related NFR:** `NFR-SEC-004`.
- **Related CONS:** `CONS-008`.
- **Related API Chapter 8:** §8.5 trang 65.
- **Related Data Model:** §7.3.
- **Conflict IDs:** `CONFLICT-006`, `CONFLICT-007`, `CONFLICT-015`.
- **Technical Risk IDs:** `TECH-RISK-012` (Step renumbering race condition).
- **Status:** **CONFLICT**

### FR-SRCH-001: Tìm kiếm Toàn văn bản (Full-Text Search)
- **ID:** FR-SRCH-001
- **Name:** Tìm kiếm Toàn văn bản Công thức
- **Actor:** Tất cả (Guest / Author / Admin)
- **Priority:** Must Have (M)
- **Purpose:** Tìm kiếm công thức theo từ khóa tiếng Việt không dấu sử dụng PostgreSQL tsvector và unaccent extension.
- **Preconditions:** PostgreSQL đã cài đặt extension `unaccent`.
- **Inputs:** Query string `q` (từ khóa tìm kiếm).
- **Outputs:** `PagedResult<RecipeSummaryDto>` sắp xếp theo độ liên quan (`ts_rank_cd`).
- **Happy path:** 1. Nhận từ khóa `q` $\rightarrow$ 2. Chuẩn hóa unaccent $\rightarrow$ 3. Build `to_tsquery('simple', ...)` với prefix matching `:*` $\rightarrow$ 4. Execute query trên cột `SearchVector` có GIN index $\rightarrow$ 5. Trả kết quả 200 OK.
- **Alternate/error paths:** Từ khóa rỗng $\rightarrow$ Trả danh sách mặc định mới nhất (200 OK).
- **Validation:** Từ khóa <= 100 ký tự.
- **Authorization:** Anonymous.
- **HTTP method:** GET
- **Endpoint:** `/api/v1/recipes?search={q}`
- **Success status:** 200 OK
- **Error statuses:** 400 Bad Request, 500 Internal Server Error
- **Entities affected:** `Recipe`
- **Cache behavior:** Cache query phổ biến 1m (NFR) hoặc 5m / không cache (FR).
- **External dependency:** PostgreSQL FTS engine.
- **Owner:** TV2 (Primary), TV3 (Search UI).
- **Dependencies on other members:** TV3 (Search input bar & debounce).
- **Related NFR:** `NFR-PERF-001`, `NFR-PERF-003`, `NFR-SCALE-002`.
- **Related CONS:** `CONS-006`.
- **Related API Chapter 8:** §8.3 trang 63.
- **Related Data Model:** §7.2 (cột tsvector, GIN index).
- **Conflict IDs:** `CONFLICT-016`.
- **Technical Risk IDs:** `TECH-RISK-004` (Unaccent extension initialization).
- **Status:** **CONFLICT**

### FR-SRCH-002: Lọc Công thức (Filter Recipes)
- **ID:** FR-SRCH-002
- **Name:** Lọc Công thức Đa tiêu chí
- **Actor:** Tất cả (Guest / Author / Admin)
- **Priority:** Must Have (M)
- **Purpose:** Lọc kết quả danh sách công thức theo danh mục, độ khó, thời gian nấu tối đa, số khẩu phần tối thiểu.
- **Preconditions:** None.
- **Inputs:** Query params: `categoryId`, `difficulty`, `maxCookTime`, `minServings`.
- **Outputs:** `PagedResult<RecipeSummaryDto>`.
- **Happy path:** 1. Nhận params $\rightarrow$ 2. Áp dụng dynamic IQueryable where clauses kết hợp bằng AND logic $\rightarrow$ 3. Trả kết quả 200 OK.
- **Alternate/error paths:** Giá trị filter không hợp lệ $\rightarrow$ HTTP 400 Bad Request.
- **Validation:** maxCookTime > 0, minServings > 0, difficulty in (Easy, Medium, Hard).
- **Authorization:** Anonymous.
- **HTTP method:** GET
- **Endpoint:** `/api/v1/recipes?...`
- **Success status:** 200 OK
- **Error statuses:** 400 Bad Request
- **Entities affected:** `Recipe`
- **Cache behavior:** Không cache dynamic filter.
- **External dependency:** DbContext.
- **Owner:** TV2 (Primary), TV3 (Filter sidebar UI).
- **Dependencies on other members:** TV3 (Filter controls).
- **Related NFR:** `NFR-PERF-004`.
- **Related CONS:** `CONS-005`.
- **Related API Chapter 8:** §8.3 trang 63.
- **Related Data Model:** §7.2.
- **Conflict IDs:** Không.
- **Technical Risk IDs:** Không.
- **Status:** **CLEAR**

### FR-SRCH-003: Sắp xếp Kết quả (Sort Recipes)
- **ID:** FR-SRCH-003
- **Name:** Sắp xếp Kết quả Danh sách Công thức
- **Actor:** Tất cả (Guest / Author / Admin)
- **Priority:** Must Have (M)
- **Purpose:** Sắp xếp công thức theo ngày tạo, thời gian nấu, thời gian chuẩn bị hoặc tiêu đề.
- **Preconditions:** None.
- **Inputs:** Query param `sort` (prefix `-`) hoặc `sortBy` & `sortOrder`.
- **Outputs:** `PagedResult<RecipeSummaryDto>`.
- **Happy path:** 1. Parse sort param $\rightarrow$ 2. Áp dụng OrderBy/OrderByDescending $\rightarrow$ 3. Trả 200 OK.
- **Alternate/error paths:** Cột sort không tồn tại $\rightarrow$ Mặc định sort theo `-createdAt` (mới nhất trước).
- **Validation:** Chỉ cho phép sort trên whitelist columns: createdAt, cookTime, prepTime, title.
- **Authorization:** Anonymous.
- **HTTP method:** GET
- **Endpoint:** `/api/v1/recipes?...`
- **Success status:** 200 OK
- **Error statuses:** 400 Bad Request
- **Entities affected:** `Recipe`
- **Cache behavior:** Dynamic.
- **External dependency:** DbContext.
- **Owner:** TV2 (Primary), TV3 (Sort dropdown UI).
- **Dependencies on other members:** TV3 (Sort selector UI).
- **Related NFR:** `NFR-PERF-004`.
- **Related CONS:** `CONS-005`.
- **Related API Chapter 8:** §8.3 trang 63.
- **Related Data Model:** §7.2.
- **Conflict IDs:** `CONFLICT-003`.
- **Technical Risk IDs:** Không.
- **Status:** **CONFLICT**

### FR-SRCH-004: Phân trang Kết quả (Offset-based Pagination)
- **ID:** FR-SRCH-004
- **Name:** Phân trang Danh sách Công thức
- **Actor:** Tất cả (Guest / Author / Admin)
- **Priority:** Must Have (M)
- **Purpose:** Chia nhỏ danh sách kết quả thành các trang với kích thước cố định bằng cơ chế Offset-based (Skip/Take).
- **Preconditions:** None.
- **Inputs:** Query params `page` (default: 1), `pageSize` (default: 12, max: 50).
- **Outputs:** PagedResult chứa danh sách item và metadata phân trang (totalCount, totalPages, hasNextPage, hasPreviousPage).
- **Happy path:** 1. CountAsync tổng số bản ghi $\rightarrow$ 2. Skip((page-1)*pageSize).Take(pageSize) $\rightarrow$ 3. Trả 200 OK.
- **Alternate/error paths:** page < 1 hoặc pageSize > 50 $\rightarrow$ HTTP 400 Bad Request.
- **Validation:** page >= 1, 1 <= pageSize <= 50.
- **Authorization:** Anonymous.
- **HTTP method:** GET
- **Endpoint:** `/api/v1/recipes?...`
- **Success status:** 200 OK
- **Error statuses:** 400 Bad Request
- **Entities affected:** `Recipe`
- **Cache behavior:** Dynamic.
- **External dependency:** DbContext.
- **Owner:** TV2 (Primary), TV3 (Pagination UI component).
- **Dependencies on other members:** TV3 (Paginator component).
- **Related NFR:** `NFR-PERF-001`.
- **Related CONS:** `CONS-005`.
- **Related API Chapter 8:** §8.0 trang 60.
- **Related Data Model:** §7.2.
- **Conflict IDs:** `CONFLICT-012`.
- **Technical Risk IDs:** Không.
- **Status:** **CONFLICT**

### FR-FILE-001: Upload File lên MinIO (File Upload)
- **ID:** FR-FILE-001
- **Name:** Tải tệp tin lên Kho Lưu trữ MinIO
- **Actor:** Người dùng đã xác thực (Author / Admin)
- **Priority:** Must Have (M)
- **Purpose:** Tải file ảnh nhị phân lên bucket `culinary-blog` trên MinIO S3-compatible, sinh URL công khai.
- **Preconditions:** MinIO container đang hoạt động; file thỏa mãn ràng buộc kích thước và định dạng.
- **Inputs:** `IFormFile`, folder đích (`recipes/{id}` hoặc `categories`).
- **Outputs:** Public URL của file đã lưu.
- **Happy path:** 1. Validate magic bytes $\rightarrow$ 2. Sinh unique filename `{Guid.NewGuid()}{ext}` $\rightarrow$ 3. `PutObjectAsync` lên MinIO $\rightarrow$ 4. Trả public URL.
- **Alternate/error paths:** Size > 5MB $\rightarrow$ 400; MIME invalid $\rightarrow$ 400; MinIO fail $\rightarrow$ 503.
- **Validation:** Magic bytes match JPEG/PNG/WebP/AVIF; size <= 5MB.
- **Authorization:** Authenticated user.
- **HTTP method:** Internal Service Call / POST API
- **Endpoint:** Dùng trong `/api/v1/recipes/{id}/images`
- **Success status:** 201 Created
- **Error statuses:** 400 Bad Request, 503 Service Unavailable
- **Entities affected:** MinIO Object Storage, `RecipeImage`
- **Cache behavior:** Public-read policy; CDN / browser cache.
- **External dependency:** MinIO S3-compatible API.
- **Owner:** TV4 (Primary), TV2 (Caller in recipe images).
- **Dependencies on other members:** TV2 (Recipe Images Handler), TV3 (Upload progress UI).
- **Related NFR:** `NFR-SEC-004`, `NFR-REL-003`.
- **Related CONS:** `CONS-007`.
- **Related API Chapter 8:** §8.4 trang 64.
- **Related Data Model:** §7.5.
- **Conflict IDs:** Không.
- **Technical Risk IDs:** `TECH-RISK-006` (Host resolution), `TECH-RISK-011` (Magic bytes), `TECH-RISK-015` (Nginx body size).
- **Status:** **CLEAR**

### FR-FILE-002: Xóa File khỏi MinIO (File Deletion)
- **ID:** FR-FILE-002
- **Name:** Xóa tệp tin khỏi MinIO
- **Actor:** Hệ thống (Hangfire Job) hoặc Admin
- **Priority:** Must Have (M)
- **Purpose:** Xóa an toàn object ảnh trên MinIO khi bài viết hoặc ảnh bị xóa.
- **Preconditions:** None.
- **Inputs:** `fileUrl` cần xóa.
- **Outputs:** Task hoàn thành (idempotent).
- **Happy path:** 1. Trích xuất object name từ URL $\rightarrow$ 2. Gọi `RemoveObjectAsync` $\rightarrow$ 3. Hoàn tất.
- **Alternate/error paths:** File không tồn tại trên MinIO $\rightarrow$ Không throw exception (idempotent); Lỗi kết nối $\rightarrow$ Hangfire retry tối đa 3 lần.
- **Validation:** URL hợp lệ.
- **Authorization:** System / Admin.
- **HTTP method:** Internal Service Call
- **Endpoint:** Background Job Execution
- **Success status:** Success
- **Error statuses:** Retry on error
- **Entities affected:** MinIO Object Storage
- **Cache behavior:** Invalidate CDN nếu có.
- **External dependency:** MinIO.
- **Owner:** TV4 (Primary).
- **Dependencies on other members:** TV2 (Trigger khi delete recipe/image).
- **Related NFR:** `NFR-REL-002`, `NFR-REL-003`.
- **Related CONS:** `CONS-007`.
- **Related API Chapter 8:** §8.4 trang 64.
- **Related Data Model:** §7.5.
- **Conflict IDs:** Không.
- **Technical Risk IDs:** Không.
- **Status:** **CLEAR**

### FR-JOB-001: Welcome Email Job
- **ID:** FR-JOB-001
- **Name:** Gửi Email Chào mừng Thành viên Mới
- **Actor:** Hệ thống (Hangfire Worker)
- **Priority:** Should Have (S)
- **Purpose:** Tác vụ nền gửi email HTML chào mừng đến địa chỉ email vừa đăng ký thành công qua MailKit.
- **Preconditions:** `FR-AUTH-001` thành công.
- **Inputs:** `userId`, `email`, `fullName`.
- **Outputs:** Email được gửi thành công qua SMTP.
- **Happy path:** 1. `BackgroundJob.Enqueue` sau đăng ký $\rightarrow$ 2. Hangfire worker nhận job $\rightarrow$ 3. Render HTML template $\rightarrow$ 4. Kết nối SMTP gửi mail $\rightarrow$ 5. Hoàn tất.
- **Alternate/error paths:** SMTP fail $\rightarrow$ Tự động retry 3 lần với exponential backoff (1m, 5m, 30m). Sau 3 lần fail chuyển sang Failed state.
- **Validation:** Email hợp lệ.
- **Authorization:** System Internal.
- **HTTP method:** Hangfire Background Task
- **Endpoint:** Trigger sau `/api/v1/auth/register`
- **Success status:** Job Succeeded
- **Error statuses:** Job Failed (logged)
- **Entities affected:** None
- **Cache behavior:** Không cache.
- **External dependency:** SMTP Server (Mailhog ở dev, SendGrid ở prod).
- **Owner:** TV4 (Primary), TV1 (Trigger caller).
- **Dependencies on other members:** TV1 (Kích hoạt job sau khi đăng ký).
- **Related NFR:** `NFR-REL-002`.
- **Related CONS:** `CONS-009`.
- **Related API Chapter 8:** None.
- **Related Data Model:** §7.7.
- **Conflict IDs:** Không.
- **Technical Risk IDs:** `TECH-RISK-009`.
- **Status:** **CLEAR**

### FR-JOB-002: Image Resize / Thumbnail Generation Job
- **ID:** FR-JOB-002
- **Name:** Tự động Sinh Ảnh Thu nhỏ (Thumbnail Generation)
- **Actor:** Hệ thống (Hangfire Worker)
- **Priority:** Should Have (S)
- **Purpose:** Tác vụ nền tạo ảnh thumbnail (300x300px) và ảnh medium (800x600px) từ ảnh gốc đã upload, cập nhật URL vào database.
- **Preconditions:** Ảnh gốc đã upload thành công lên MinIO qua `FR-RCP-008`.
- **Inputs:** `imageId` (Guid).
- **Outputs:** 2 ảnh mới trên MinIO và cập nhật cột `MediumUrl`, `ThumbnailUrl` trong bảng `recipe_images`.
- **Happy path:** 1. Đọc stream ảnh gốc từ MinIO $\rightarrow$ 2. Resize thumbnail 300x300 và medium 800x600 $\rightarrow$ 3. Upload cả 2 lên MinIO $\rightarrow$ 4. Cập nhật `RecipeImage` entity $\rightarrow$ 5. SaveChanges.
- **Alternate/error paths:** Retry 3 lần. Nếu fail: ảnh gốc vẫn hiển thị bình thường, chỉ thiếu thumbnail.
- **Validation:** imageId hợp lệ.
- **Authorization:** System Internal.
- **HTTP method:** Hangfire Background Task
- **Endpoint:** Trigger sau upload ảnh recipe
- **Success status:** Job Succeeded
- **Error statuses:** Retry on error
- **Entities affected:** `RecipeImage`, MinIO
- **Cache behavior:** Invalidate recipe detail cache.
- **External dependency:** MinIO, ImageSharp / SkiaSharp.
- **Owner:** TV4 (Primary), TV2 (Caller).
- **Dependencies on other members:** TV2 (Trigger sau upload ảnh).
- **Related NFR:** `NFR-PERF-005` (Tối ưu Core Web Vitals).
- **Related CONS:** `CONS-007`.
- **Related API Chapter 8:** §8.4.
- **Related Data Model:** §7.5.
- **Conflict IDs:** Không.
- **Technical Risk IDs:** Không.
- **Status:** **CLEAR**

### FR-JOB-003: Sitemap Generation Job
- **ID:** FR-JOB-003
- **Name:** Tự động Tạo Sitemap và Thông báo Công cụ Tìm kiếm
- **Actor:** Hệ thống (Hangfire Recurring Scheduler)
- **Priority:** Should Have (S)
- **Purpose:** Tác vụ định kỳ hàng ngày lúc 02:00 AM UTC tạo file `sitemap.xml` chứa URL tất cả công thức Published, categories và trang tĩnh; ping Google Search Console.
- **Preconditions:** Hệ thống có ít nhất 1 bài viết Published.
- **Inputs:** Cron expression `"0 2 * * *"`.
- **Outputs:** File `sitemap.xml` trên MinIO hoặc wwwroot và thông báo HTTP GET đến Google Ping URL.
- **Happy path:** 1. Query toàn bộ Published recipes, categories $\rightarrow$ 2. Tạo sitemap XML chuẩn có `<loc>`, `<lastmod>`, `<changefreq>`, `<priority>` $\rightarrow$ 3. Lưu sitemap $\rightarrow$ 4. Ping `https://www.google.com/ping?sitemap={url}` $\rightarrow$ 5. Log kết quả qua Serilog.
- **Alternate/error paths:** Retry 2 lần nếu thất bại.
- **Validation:** XML format hợp lệ.
- **Authorization:** System Scheduler.
- **HTTP method:** Recurring Cron Job
- **Endpoint:** Cron "0 2 * * *"
- **Success status:** Job Succeeded
- **Error statuses:** Log warning on failure
- **Entities affected:** None
- **Cache behavior:** Static XML cache.
- **External dependency:** Google Search Console Ping API, MinIO.
- **Owner:** TV4 (Primary), TV2 (Published recipe data), TV3 (Robots.txt config).
- **Dependencies on other members:** TV2 (Cung cấp danh sách published slugs).
- **Related NFR:** `NFR-SEO-003`.
- **Related CONS:** `CONS-010`.
- **Related API Chapter 8:** None.
- **Related Data Model:** §7.2, §7.6.
- **Conflict IDs:** Không.
- **Technical Risk IDs:** Không.
- **Status:** **CLEAR**

### FR-OBS-001: Health Check Endpoints
- **ID:** FR-OBS-001
- **Name:** Điểm cuối Kiểm tra Sức khỏe Hệ thống
- **Actor:** Hệ thống giám sát / Load Balancer / Admin
- **Priority:** Must Have (M)
- **Purpose:** Cung cấp 3 endpoints health check: tổng hợp (`/health`), liveness probe (`/health/live`), và readiness probe (`/health/ready`).
- **Preconditions:** None.
- **Inputs:** None.
- **Outputs:** JSON báo cáo trạng thái hoặc text "Healthy".
- **Happy path:** 1. GET `/health/live` $\rightarrow$ Trả 200 OK Healthy; 2. GET `/health/ready` $\rightarrow$ Kiểm tra kết nối DB và Redis $\rightarrow$ Nếu cả hai UP trả 200 OK.
- **Alternate/error paths:** DB hoặc Redis down $\rightarrow$ `/health/ready` trả HTTP 503 Service Unavailable để Nginx/Kubernetes ngừng route traffic.
- **Validation:** None.
- **Authorization:** Anonymous.
- **HTTP method:** GET
- **Endpoint:** `/health`, `/health/live`, `/health/ready`
- **Success status:** 200 OK
- **Error statuses:** 503 Service Unavailable
- **Entities affected:** None
- **Cache behavior:** Không cache (`no-cache`).
- **External dependency:** PostgreSQL, Redis, MinIO health checkers.
- **Owner:** TV4 (Primary), Toàn đội.
- **Dependencies on other members:** Toàn đội (Giám sát service).
- **Related NFR:** `NFR-REL-001`.
- **Related CONS:** `CONS-009`.
- **Related API Chapter 8:** §8.7 trang 65–66.
- **Related Data Model:** None.
- **Conflict IDs:** Không.
- **Technical Risk IDs:** Không.
- **Status:** **CLEAR**

### FR-OBS-002: Structured Logging
- **ID:** FR-OBS-002
- **Name:** Ghi Nhật ký Hệ thống có Cấu trúc (Structured Logging)
- **Actor:** Hệ thống (.NET Pipeline & Middlewares)
- **Priority:** Must Have (M)
- **Purpose:** Mọi HTTP request và MediatR command/query được log kèm CorrelationId, phương thức, thời gian xử lý, userId; cảnh báo khi thực thi > 500ms.
- **Preconditions:** Serilog đã được khởi tạo trong `Program.cs`.
- **Inputs:** `HttpContext`, MediatR Requests.
- **Outputs:** JSON log events xuất ra Console, File rolling hàng ngày, và Seq server.
- **Happy path:** 1. `CorrelationIdMiddleware` đọc hoặc sinh mới `X-Correlation-ID` header $\rightarrow$ 2. Đẩy CorrelationId vào Serilog `LogContext` $\rightarrow$ 3. `LoggingBehavior` log bắt đầu và kết thúc request $\rightarrow$ 4. Cảnh báo Warning nếu elapsed > 500ms.
- **Alternate/error paths:** Unhandled exception $\rightarrow$ `GlobalExceptionHandlerMiddleware` bắt lỗi, log full trace với Level Error, sinh error code RFC 7807 trả về cho client.
- **Validation:** None.
- **Authorization:** Tự động áp dụng cho mọi request.
- **HTTP method:** Middleware / Pipeline Behavior
- **Endpoint:** Toàn bộ API endpoints
- **Success status:** Log Level Info / Debug
- **Error statuses:** Log Level Error / Fatal
- **Entities affected:** None
- **Cache behavior:** None.
- **External dependency:** Serilog, Seq (port 5341).
- **Owner:** TV1 (Middleware base) & TV4 (Seq sink & Observability).
- **Dependencies on other members:** Toàn đội (Sử dụng ILogger trong feature handlers).
- **Related NFR:** `NFR-MAINT-001`, `NFR-REL-002`.
- **Related CONS:** `CONS-010`.
- **Related API Chapter 8:** Phụ lục B.
- **Related Data Model:** None.
- **Conflict IDs:** Không.
- **Technical Risk IDs:** `TECH-RISK-013`.
- **Status:** **CLEAR**

### FR-OBS-003: Distributed Tracing & Custom Metrics
- **ID:** FR-OBS-003
- **Name:** Truy vết Phân tán và Chỉ số Hiệu năng (Distributed Tracing & Metrics)
- **Actor:** Hệ thống (OpenTelemetry Instrumentation)
- **Priority:** Should Have (S)
- **Purpose:** Thu thập dữ liệu distributed tracing (ActivitySource) cho HTTP requests, EF Core database operations và xuất custom business metrics (số lượng recipe tạo/publish, thời gian xử lý).
- **Preconditions:** OpenTelemetry .NET SDK cấu hình OTLP exporter.
- **Inputs:** Incoming HTTP requests, database queries, Hangfire jobs.
- **Outputs:** Traces và Metrics xuất ra Seq / Jaeger / Grafana qua giao thức OTLP.
- **Happy path:** 1. Request đến $\rightarrow$ Sinh TraceId, SpanId $\rightarrow$ 2. Gắn TraceId vào Serilog log correlation $\rightarrow$ 3. EF Core span đo query time $\rightarrow$ 4. Export qua OTLP.
- **Alternate/error paths:** OTLP server offline $\rightarrow$ Tự drop span, không gây treo ứng dụng chính.
- **Validation:** None.
- **Authorization:** Tự động.
- **HTTP method:** OpenTelemetry SDK Instrumentation
- **Endpoint:** Toàn bộ hệ thống
- **Success status:** Traces Exported
- **Error statuses:** None
- **Entities affected:** None
- **Cache behavior:** None.
- **External dependency:** OpenTelemetry OTLP Exporter, Seq / Jaeger.
- **Owner:** TV4 (Primary).
- **Dependencies on other members:** Toàn đội.
- **Related NFR:** `NFR-PERF-001`, `NFR-MAINT-001`.
- **Related CONS:** `CONS-010`.
- **Related API Chapter 8:** None.
- **Related Data Model:** None.
- **Conflict IDs:** Không.
- **Technical Risk IDs:** Không.
- **Status:** **CLEAR**

---

## 7. Non-Functional Requirements Detailed Audit (30 detailed NFRs)

### NFR-PERF-001: Response Time API
- **ID:** NFR-PERF-001
- **Category:** Hiệu năng (Performance)
- **Requirement:** Thời gian phản hồi của hệ thống API phải tuân thủ nghiêm ngặt các ngưỡng đo lường tại steady-state với Redis warm cache.
- **Metric/threshold:** p50 <= 150ms (cho mọi GET endpoints với cached data); p95 <= 500ms (toàn bộ API kể cả write operations); p99 <= 1000ms (không vượt quá 1 giây trong mọi tình huống).
- **Technology:** ASP.NET Core, Redis, OpenTelemetry.
- **Affected modules:** Toàn bộ API endpoints.
- **Owner(s):** TV1, TV2, TV4.
- **Verification method:** Đo lường tự động qua OpenTelemetry metrics + k6 load test script.
- **Related FR:** Tất cả FRs.
- **Conflict:** Không.
- **Risk:** Chậm khi query phức tạp không index.
- **Evidence page:** §4.1 trang 40.

### NFR-PERF-002: System Throughput & Concurrency
- **ID:** NFR-PERF-002
- **Category:** Hiệu năng (Performance)
- **Requirement:** Hệ thống xử lý đồng thời tải người dùng lớn mà không bị suy giảm hiệu năng (no degradation).
- **Metric/threshold:** Xử lý đồng thời >= 100 concurrent users trên cấu hình phần cứng: 2 vCPU, 4GB RAM (single instance); horizontal scaling tăng tuyến tính khi bổ sung instance.
- **Technology:** Kestrel web server, Npgsql connection pooling, Redis.
- **Affected modules:** Toàn bộ hệ thống.
- **Owner(s):** Toàn đội (TV1/TV2/TV3/TV4).
- **Verification method:** K6 test pipeline: smoke test $\rightarrow$ load test $\rightarrow$ stress test.
- **Related FR:** Toàn bộ FRs.
- **Conflict:** Không.
- **Risk:** Cạn kiệt connection pool nếu không release DbContext đúng cách.
- **Evidence page:** §4.1 trang 40.

### NFR-PERF-003: Cache Effectiveness
- **ID:** NFR-PERF-003
- **Category:** Hiệu năng (Performance)
- **Requirement:** Tỷ lệ trúng bộ nhớ đệm (Cache Hit Rate) đạt mức cao; thời gian sống (TTL) và chiến lược invalidation được cấu hình chuẩn xác.
- **Metric/threshold:** Redis Cache Hit Rate >= 80% trong điều kiện bình thường. Category list: TTL = 30m; Recipe detail: TTL = 5m (cache-aside); Search results: TTL = 1m (60s). Cache invalidation: Event-driven (xóa ngay khi Create/Update/Delete).
- **Technology:** Redis distributed cache, StackExchange.Redis.
- **Affected modules:** Category, Recipe, Search.
- **Owner(s):** TV2 (Recipe Cache), TV4 (Category Cache).
- **Verification method:** Redis CLI `INFO stats` (keyspace_hits / keyspace_misses) và Grafana dashboard.
- **Related FR:** `FR-CAT-001`, `FR-RCP-001`, `FR-RCP-002`, `FR-SRCH-001`.
- **Conflict:** `CONFLICT-016` (Search TTL 1m vs 5m), `CONFLICT-019` (Recipe Detail OutputCache 60m vs Redis 5m), `CONFLICT-020` (Category IMemoryCache 1h vs Redis 30m).
- **Risk:** Stale data nếu quên invalidate khi update.
- **Evidence page:** §4.1 trang 40.

### NFR-PERF-004: Database Query Optimization
- **ID:** NFR-PERF-004
- **Category:** Hiệu năng (Performance)
- **Requirement:** Mọi truy vấn CSDL PostgreSQL phải được tối ưu, triệt tiêu vấn đề N+1 query và có index phù hợp.
- **Metric/threshold:** 0 query N+1 (bắt buộc dùng `.Include()`, `.ThenInclude()` hoặc Projection); mọi cột WHERE/ORDER BY có B-tree index; cảnh báo slow query log khi thời gian thực thi > 100ms.
- **Technology:** EF Core, PostgreSQL, Serilog.
- **Affected modules:** Data Access Layer (TV2 lead).
- **Owner(s):** TV2 (Primary).
- **Verification method:** Chạy `EXPLAIN ANALYZE` và review query logs trước khi merge code.
- **Related FR:** `FR-RCP-001`, `FR-RCP-002`, `FR-SRCH-001..004`.
- **Conflict:** Không.
- **Risk:** `TECH-RISK-008` (Eager loading quá nhiều collection gây Cartesian explosion).
- **Evidence page:** §4.1 trang 40–41.

### NFR-PERF-005: Frontend Performance (Core Web Vitals)
- **ID:** NFR-PERF-005
- **Category:** Hiệu năng (Performance)
- **Requirement:** Giao diện Next.js đạt chuẩn Google Core Web Vitals để đảm bảo trải nghiệm người dùng và thứ hạng SEO.
- **Metric/threshold:** LCP <= 2.5s; CLS <= 0.1; INP <= 200ms; First Load JS Bundle <= 200KB (gzipped).
- **Technology:** Next.js App Router, Image Optimization (`next/image`), Code Splitting, ISR.
- **Affected modules:** Frontend (TV3).
- **Owner(s):** TV3 (Primary).
- **Verification method:** Lighthouse CI audit tự động trong quy trình phát triển.
- **Related FR:** Toàn bộ giao diện người dùng.
- **Conflict:** Không.
- **Risk:** Ảnh không tối ưu làm giảm điểm LCP.
- **Evidence page:** §4.1 trang 41.

### NFR-SEC-001: Password Hashing & Complexity
- **ID:** NFR-SEC-001
- **Category:** Bảo mật (Security)
- **Requirement:** Mật khẩu người dùng được băm an toàn, không bao giờ lưu trữ dạng plaintext.
- **Metric/threshold:** ASP.NET Core Identity PBKDF2-HMACSHA512 với iteration count >= 100,000. Yêu cầu mật khẩu: tối thiểu 8 ký tự, ít nhất 1 chữ hoa, 1 chữ thường, 1 số, 1 ký tự đặc biệt.
- **Technology:** ASP.NET Core Identity.
- **Affected modules:** Auth Module.
- **Owner(s):** TV1 (Primary).
- **Verification method:** Unit tests kiểm tra PasswordValidator và mã băm trong DB.
- **Related FR:** `FR-AUTH-001`, `FR-AUTH-002`.
- **Conflict:** Không.
- **Risk:** Cấu hình iteration quá thấp nếu không chỉ định rõ.
- **Evidence page:** §4.2 trang 41.

### NFR-SEC-002: JWT Token Security & Rotation
- **ID:** NFR-SEC-002
- **Category:** Bảo mật (Security)
- **Requirement:** Cơ chế bảo vệ và xoay vòng token xác thực theo chuẩn bảo mật cao nhất.
- **Metric/threshold:** Access Token: JWT ký HS256, TTL = 15 phút (claims: userId, email, roles, jti). Refresh Token: 128-bit cryptographically secure random bytes, hash SHA-256 trước khi lưu DB, TTL = 7 ngày. Refresh Token Rotation (thu hồi ngay sau khi dùng). Reuse Detection (thu hồi cả family nếu token cũ bị tái sử dụng).
- **Technology:** Microsoft.AspNetCore.Authentication.JwtBearer, System.Security.Cryptography.
- **Affected modules:** Auth Module.
- **Owner(s):** TV1 (Primary).
- **Verification method:** Automated security integration tests cho luồng token rotation và reuse attack.
- **Related FR:** `FR-AUTH-001`, `FR-AUTH-004`.
- **Conflict:** `CONFLICT-018` (128-bit crypto random ở đây vs 512-bit ở FR-AUTH-001).
- **Risk:** `TECH-RISK-002` (So sánh raw token thay vì SHA-256 hash).
- **Evidence page:** §4.2 trang 41.

### NFR-SEC-003: Rate Limiting
- **ID:** NFR-SEC-003
- **Category:** Bảo mật (Security)
- **Requirement:** Giới hạn tần suất gọi API theo địa chỉ IP để chống tấn công Brute-force và DoS.
- **Metric/threshold:** Auth endpoints (`/auth/*`): 10 request/phút/IP; API chung: 100 request/phút/IP; Upload endpoints: 5 request/phút/IP. Trả HTTP 429 kèm header `Retry-After`.
- **Technology:** ASP.NET Core RateLimiting middleware (Fixed Window, Sliding Window).
- **Affected modules:** Presentation Layer (API).
- **Owner(s):** TV1 (Primary).
- **Verification method:** Postman / k6 bắn liên tục request kiểm tra mã 429 và Retry-After header.
- **Related FR:** `FR-AUTH-001..007`, `FR-RCP-008`.
- **Conflict:** Không.
- **Risk:** Khóa nhầm IP người dùng sau proxy nếu không lấy đúng client IP qua `X-Forwarded-For`.
- **Evidence page:** §4.2 trang 41.

### NFR-SEC-004: Input Validation & File Upload Security
- **ID:** NFR-SEC-004
- **Category:** Bảo mật (Security)
- **Requirement:** Mọi dữ liệu đầu vào phải được kiểm tra tính hợp lệ trước khi đi vào tầng nghiệp vụ; bảo vệ tệp tin tải lên.
- **Metric/threshold:** FluentValidation 100% inputs; parameterized queries chống SQLi; HTML sanitization + Content-Security-Policy chống XSS; Magic bytes verification (đọc 4 bytes đầu) cho file upload; Dung lượng tối đa 5MB; Đặt tên file bằng GUID chống Path Traversal.
- **Technology:** FluentValidation, EF Core, FileStream magic bytes reader.
- **Affected modules:** Application & Presentation.
- **Owner(s):** TV1 (ValidationBehavior), TV4 (File upload).
- **Verification method:** Unit tests validation rules; upload file giả mạo định dạng để kiểm tra từ chối.
- **Related FR:** Toàn bộ API có payload; `FR-FILE-001`, `FR-RCP-008`.
- **Conflict:** `CONFLICT-011` (Mã phản hồi validation 422 vs 400).
- **Risk:** `TECH-RISK-011` (Quên reset stream position sau khi đọc magic bytes).
- **Evidence page:** §4.2 trang 41.

### NFR-SEC-005: HTTPS, HSTS & Strict CORS
- **ID:** NFR-SEC-005
- **Category:** Bảo mật (Security)
- **Requirement:** Toàn bộ lưu lượng mạng phải được mã hóa qua TLS và cấu hình CORS chặt chẽ.
- **Metric/threshold:** TLS 1.2+ bắt buộc; HSTS header (`max-age=31536000`); CORS chỉ cho phép origin cấu hình sẵn (`http://localhost:3000` ở dev, domain production), cấm wildcard `*`.
- **Technology:** Nginx SSL termination, ASP.NET Core CORS middleware.
- **Affected modules:** Infrastructure & Presentation.
- **Owner(s):** TV1 (CORS policy), TV4 (Nginx config).
- **Verification method:** Kiểm tra HTTP headers qua curl hoặc securityheaders.com.
- **Related FR:** Toàn bộ endpoints.
- **Conflict:** Không.
- **Risk:** Lỗi CORS trên frontend nếu cấu hình port dev không khớp.
- **Evidence page:** §4.2 trang 41.

### NFR-SEC-006: Authorization & Resource Ownership
- **ID:** NFR-SEC-006
- **Category:** Bảo mật (Security)
- **Requirement:** Kiểm tra quyền sở hữu tài nguyên tại Application Layer, không phụ thuộc hoàn toàn vào UI.
- **Metric/threshold:** Author chỉ được sửa/xóa công thức do chính mình tạo (`AuthorId == currentUserId`); Admin có quyền quản trị toàn bộ; Ghi audit log mọi thao tác ghi (write) kèm userId và timestamp.
- **Technology:** ASP.NET Core AuthorizationHandler (`RecipeAuthorizationHandler`), Serilog.
- **Affected modules:** Application Layer.
- **Owner(s):** TV1, TV2.
- **Verification method:** Unit tests và Integration tests với tài khoản Author khác để xác nhận bị chặn HTTP 403 Forbidden.
- **Related FR:** `FR-RCP-004`, `FR-RCP-005`, `FR-RCP-006`, `FR-RCP-007`, `FR-RCP-008..010`.
- **Conflict:** Không.
- **Risk:** Bỏ sót kiểm tra quyền ở các sub-resource (steps, ingredients).
- **Evidence page:** §4.2 trang 42.

### NFR-SEC-007: Secrets Management
- **ID:** NFR-SEC-007
- **Category:** Bảo mật (Security)
- **Requirement:** Không bao giờ lưu trữ hoặc commit mật khẩu, khóa bí mật vào Git repository.
- **Metric/threshold:** Development sử dụng .NET User Secrets (`dotnet user-secrets`); Production sử dụng Environment Variables / Docker Compose secrets; Quét secrets tự động trước commit.
- **Technology:** .NET Configuration, .gitignore, TruffleHog / GitLeaks.
- **Affected modules:** Toàn bộ solution.
- **Owner(s):** Toàn đội.
- **Verification method:** Kiểm tra file `.env` và `appsettings.Development.json` không chứa secrets production.
- **Related FR:** Toàn bộ cấu hình tích hợp.
- **Conflict:** Không.
- **Risk:** Sơ ý commit file `.env` lên git.
- **Evidence page:** §4.2 trang 42.

### NFR-USE-001: Responsive Design
- **ID:** NFR-USE-001
- **Category:** Khả năng Sử dụng (Usability)
- **Requirement:** Giao diện hiển thị tối ưu và mượt mà trên tất cả các kích thước màn hình.
- **Metric/threshold:** Mobile: 320px – 767px (single column, touch-friendly); Tablet: 768px – 1199px (2-column grid); Desktop: >= 1200px (full layout). Không bị tràn ngang màn hình.
- **Technology:** Tailwind CSS utility-first.
- **Affected modules:** Frontend (TV3).
- **Owner(s):** TV3 (Primary).
- **Verification method:** Chrome DevTools Responsive Mode + kiểm thử trên thiết bị thật (iOS/Android).
- **Related FR:** Toàn bộ màn hình UI.
- **Conflict:** Không.
- **Risk:** Vỡ layout bảng nguyên liệu trên màn hình mobile nhỏ.
- **Evidence page:** §4.3 trang 42.

### NFR-USE-002: Web Accessibility (a11y)
- **ID:** NFR-USE-002
- **Category:** Khả năng Sử dụng (Usability)
- **Requirement:** Giao diện tuân thủ tiêu chuẩn tiếp cận người khuyết tật.
- **Metric/threshold:** Đạt chuẩn WCAG 2.1 Level AA; Tỷ lệ tương phản màu >= 4.5:1 (văn bản) và >= 3:1 (UI components); Sử dụng Semantic HTML5 (`<article>`, `<nav>`, `<main>`); Hỗ trợ điều hướng hoàn toàn bằng bàn phím (Tab, Enter, Escape).
- **Technology:** Semantic HTML5, ARIA attributes.
- **Affected modules:** Frontend (TV3).
- **Owner(s):** TV3 (Primary).
- **Verification method:** Kiểm tra bằng axe DevTools và screen readers (NVDA / VoiceOver).
- **Related FR:** Toàn bộ giao diện người dùng.
- **Conflict:** Không.
- **Risk:** Quên gắn `altText` cho ảnh đại diện hoặc icon button thiếu `aria-label`.
- **Evidence page:** §4.3 trang 42.

### NFR-USE-003: Actionable Error Messages & RFC 7807
- **ID:** NFR-USE-003
- **Category:** Khả năng Sử dụng (Usability)
- **Requirement:** Thông báo lỗi phía API và giao diện phải rõ ràng, định lượng và có tính hướng dẫn hành động (actionable).
- **Metric/threshold:** API trả về chuẩn RFC 7807 Problem Details (type, title, status, detail, errors{}); Frontend hiển thị lỗi inline ngay cạnh trường nhập liệu; Không lộ stack trace ở môi trường production.
- **Technology:** FluentValidation, RFC 7807 Middleware, React Hook Form.
- **Affected modules:** API & Frontend.
- **Owner(s):** TV1 (API Error Handling), TV3 (Form Error UI).
- **Verification method:** Test các trường hợp gửi sai dữ liệu và xác nhận cấu trúc JSON lỗi trả về.
- **Related FR:** Toàn bộ endpoints write.
- **Conflict:** `CONFLICT-011` (Mã HTTP 422 vs 400).
- **Risk:** Message lỗi hardcode không nhất quán ngôn ngữ.
- **Evidence page:** §4.3 trang 42.

### NFR-USE-004: Loading States & Visual Feedback
- **ID:** NFR-USE-004
- **Category:** Khả năng Sử dụng (Usability)
- **Requirement:** Mọi thao tác bất đồng bộ phải có phản hồi thị giác trực quan, không để người dùng thấy màn hình trắng hoặc nút bấm đơ.
- **Metric/threshold:** Loading skeleton khi fetch dữ liệu; Optimistic UI updates cho các thao tác nhanh; Toast notifications xác nhận thành công/thất bại; Thanh tiến trình (progress bar %) khi upload ảnh.
- **Technology:** React Skeleton, Toast Notification Library (Sonner / React Hot Toast).
- **Affected modules:** Frontend (TV3).
- **Owner(s):** TV3 (Primary).
- **Verification method:** Manual testing với throttling kết nối mạng Slow 3G trên DevTools.
- **Related FR:** Toàn bộ UI tương tác.
- **Conflict:** Không.
- **Risk:** Quên trạng thái disable button khi đang submit gây duplicate requests.
- **Evidence page:** §4.3 trang 42.

### NFR-REL-001: Uptime SLA
- **ID:** NFR-REL-001
- **Category:** Độ tin cậy (Reliability)
- **Requirement:** Hệ thống đảm bảo tính sẵn sàng hoạt động liên tục trong năm.
- **Metric/threshold:** Uptime >= 99.5% (tương đương tối đa 3.65 giờ gián đoạn/năm); Thông báo bảo trì trước 48 giờ; Health check probe định kỳ mỗi 10 giây.
- **Technology:** Docker, Nginx, Health Check Middleware.
- **Affected modules:** Infrastructure & Hosting.
- **Owner(s):** TV4 (Primary).
- **Verification method:** Giám sát liên tục qua UptimeRobot / Better Uptime.
- **Related FR:** `FR-OBS-001`.
- **Conflict:** Không.
- **Risk:** Restart container đồng loạt gây downtime.
- **Evidence page:** §4.4 trang 43.

### NFR-REL-002: Error Handling & Resilience
- **ID:** NFR-REL-002
- **Category:** Độ tin cậy (Reliability)
- **Requirement:** Xử lý lỗi linh hoạt (gracefully), không sập toàn bộ ứng dụng khi một dịch vụ phụ trợ gặp sự cố.
- **Metric/threshold:** Global Exception Handler bắt toàn bộ unhandled exceptions; Database connection pool tự reconnect với timeout 30s; Khi Redis offline $\rightarrow$ tự động fallback sang database trực tiếp (không throw exception); Hangfire retry 3 lần với exponential backoff.
- **Technology:** ASP.NET Core Middleware, Polly, Hangfire.
- **Affected modules:** Toàn bộ backend.
- **Owner(s):** TV1 (Exception Middleware), TV4 (Redis & Hangfire).
- **Verification method:** Tắt thử nghiệm Redis container trong khi API đang chạy và xác minh API vẫn đọc được DB.
- **Related FR:** `FR-JOB-001..003`, `FR-CAT-001`, `FR-RCP-002`.
- **Conflict:** Không.
- **Risk:** `TECH-RISK-005` (Quên bắt exception Redis connection làm crash pipeline).
- **Evidence page:** §4.4 trang 43.

### NFR-REL-003: Data Durability & Backup
- **ID:** NFR-REL-003
- **Category:** Độ tin cậy (Reliability)
- **Requirement:** Dữ liệu người dùng và bài viết không bị thất thoát khi có sự cố phần cứng hoặc khởi động lại dịch vụ.
- **Metric/threshold:** PostgreSQL Write-Ahead Logging (WAL) đảm bảo tính ACID; Backup CSDL tự động hàng ngày lúc 03:00 AM (`pg_dump`), lưu trữ tối thiểu 30 ngày; MinIO volume persistent; Soft delete công thức cho phép khôi phục.
- **Technology:** PostgreSQL WAL, Cron backup script, Docker persistent volumes.
- **Affected modules:** Database & Storage.
- **Owner(s):** TV2 (Database), TV4 (MinIO volume).
- **Verification method:** Khôi phục thử nghiệm bản backup CSDL trên môi trường staging.
- **Related FR:** `FR-RCP-007`, `FR-FILE-001..002`.
- **Conflict:** `CONFLICT-001` (Soft delete ở đây vs Hard delete ở FR-RCP-007).
- **Risk:** Mất dữ liệu nếu quên cấu hình Docker Volume trên host disk.
- **Evidence page:** §4.4 trang 43.

### NFR-MAINT-001: Code Quality & Static Analysis
- **ID:** NFR-MAINT-001
- **Category:** Khả năng Bảo trì (Maintainability)
- **Requirement:** Mã nguồn phải tuân thủ chuẩn quy ước lập trình và vượt qua kiểm tra tĩnh trước khi tích hợp.
- **Metric/threshold:** 0 compiler warnings trong build CI; .NET pass SonarAnalyzer, StyleCop, EditorConfig; TypeScript/React pass ESLint (Airbnb ruleset) và Prettier.
- **Technology:** Roslyn Analyzers, ESLint, Prettier, Directory.Build.props.
- **Affected modules:** Toàn bộ solution.
- **Owner(s):** Toàn đội (TV1 lead).
- **Verification method:** Chạy `dotnet build --warnaserror` và `npm run lint` trong pipeline CI.
- **Related FR:** Toàn bộ mã nguồn.
- **Conflict:** Không.
- **Risk:** Bỏ qua warning gây tích tụ nợ kỹ thuật (technical debt).
- **Evidence page:** §4.5 trang 43.

### NFR-MAINT-002: Test Coverage
- **ID:** NFR-MAINT-002
- **Category:** Khả năng Bảo trì (Maintainability)
- **Requirement:** Hệ thống phải có độ bao phủ kiểm thử tự động toàn diện để bảo đảm an toàn khi tái cấu trúc.
- **Metric/threshold:** Test coverage >= 80% đối với tầng Domain và Application; Đầy đủ Integration tests cho các luồng nghiệp vụ chính (Auth, Recipe CRUD, Search).
- **Technology:** xUnit, FluentAssertions, Moq, Testcontainers.
- **Affected modules:** Domain & Application.
- **Owner(s):** Toàn đội.
- **Verification method:** Chạy `dotnet test --collect:"XPlat Code Coverage"` và xuất báo cáo Coverlet.
- **Related FR:** Toàn bộ FRs.
- **Conflict:** Không.
- **Risk:** Viết test hình thức chỉ để tăng coverage mà không assert logic nghiệp vụ.
- **Evidence page:** §4.5 trang 43–44.

### NFR-MAINT-003: API Documentation
- **ID:** NFR-MAINT-003
- **Category:** Khả năng Bảo trì (Maintainability)
- **Requirement:** Tài liệu API phải được sinh tự động từ mã nguồn kèm giải thích chi tiết cho từng tham số và mã phản hồi.
- **Metric/threshold:** OpenAPI / Swagger spec sinh tự động từ code kèm XML comments; Đầy đủ schemas, status codes, examples; Giao diện Scalar / Swagger UI truy cập được tại `/scalar`.
- **Technology:** Microsoft.AspNetCore.OpenApi, Scalar.AspNetCore.
- **Affected modules:** Presentation Layer.
- **Owner(s):** TV1 (Primary).
- **Verification method:** Truy cập `/scalar` trên trình duyệt và kiểm tra tính đầy đủ của endpoints.
- **Related FR:** Toàn bộ API endpoints.
- **Conflict:** Không.
- **Risk:** Quên viết XML comments trên các DTO mới tạo.
- **Evidence page:** §4.5 trang 44.

### NFR-MAINT-004: Clean Architecture Adherence
- **ID:** NFR-MAINT-004
- **Category:** Khả năng Bảo trì (Maintainability)
- **Requirement:** Duy trì nghiêm ngặt các ranh giới kiến trúc và chiều phụ thuộc một chiều trong Clean Architecture.
- **Metric/threshold:** Domain độc lập 100% (không tham chiếu thư viện ngoài); Application chỉ phụ thuộc Domain; Infrastructure thực thi interfaces của Application; Không reference ngược; Kiểm tra tự động bằng ArchUnit test.
- **Technology:** Clean Architecture, ArchUnit.NET.
- **Affected modules:** Solution Structure.
- **Owner(s):** TV1 (Primary).
- **Verification method:** Thực thi `CulinaryBlog.ArchitectureTests` project trong bộ unit test.
- **Related FR:** Toàn bộ kiến trúc backend.
- **Conflict:** Không.
- **Risk:** Sơ ý reference Infrastructure vào Application để dùng tiện ích CSDL.
- **Evidence page:** §4.5 trang 44.

### NFR-SCALE-001: Stateless Backend
- **ID:** NFR-SCALE-001
- **Category:** Khả năng Mở rộng (Scalability)
- **Requirement:** Tầng backend API được thiết kế hoàn toàn không lưu trạng thái (stateless) để có thể mở rộng ngang tức thì.
- **Metric/threshold:** Xác thực JWT stateless (không dùng session memory trên server); Bộ nhớ đệm chia sẻ bắt buộc dùng Redis (cấm dùng `IMemoryCache` in-process cho dữ liệu dùng chung); Khóa phân tán RedLock cho các job singleton; Hangfire worker chạy đa tiến trình với PostgreSQL shared queue.
- **Technology:** JWT, Redis, RedLock.net, Hangfire.
- **Affected modules:** Toàn bộ backend.
- **Owner(s):** TV1 (Auth & Architecture), TV4 (Redis & Hangfire).
- **Verification method:** Chạy 2 instances API song song đằng sau Nginx load balancer và xác minh request định tuyến ngẫu nhiên vẫn hoạt động hoàn hảo.
- **Related FR:** `FR-AUTH-001..007`, `FR-CAT-001`, `FR-JOB-003`.
- **Conflict:** `CONFLICT-020` (Cấm IMemoryCache ở đây vs FR-CAT-001 dùng IMemoryCache).
- **Risk:** Lưu dữ liệu session trong bộ nhớ ram của một instance đơn lẻ.
- **Evidence page:** §4.6 trang 44.

### NFR-SCALE-002: Database Scaling & Indexing
- **ID:** NFR-SCALE-002
- **Category:** Khả năng Mở rộng (Scalability)
- **Requirement:** Cơ sở dữ liệu PostgreSQL được tối ưu hóa để hỗ trợ dữ liệu lớn và lượng kết nối đồng thời cao.
- **Metric/threshold:** Connection pooling tối đa 100 connections/instance; Chiến lược index đầy đủ: B-tree cho equality/range, GIN cho full-text search (tsvector); Khả năng tách read replica và table partitioning khi dữ liệu > 1 triệu dòng.
- **Technology:** PostgreSQL 16, Npgsql.
- **Affected modules:** Database Layer.
- **Owner(s):** TV2 (Primary).
- **Verification method:** Kiểm tra `pg_stat_activity` và query explain trên database có seed 50,000 records.
- **Related FR:** `FR-RCP-001`, `FR-SRCH-001`.
- **Conflict:** Không.
- **Risk:** Quên index khóa ngoại làm chậm các phép JOIN bảng con.
- **Evidence page:** §4.6 trang 44.

### NFR-SCALE-003: Infrastructure Scaling
- **ID:** NFR-SCALE-003
- **Category:** Khả năng Mở rộng (Scalability)
- **Requirement:** Hạ tầng hệ thống container hóa sẵn sàng cho việc mở rộng trên môi trường đám mây hoặc cụm máy chủ.
- **Metric/threshold:** Mỗi service là một Docker container riêng biệt (API, Postgres, Redis, MinIO, Nginx, Seq, Mailhog); Nginx đóng vai trò load balancer upstream pool; MinIO hỗ trợ Distributed Mode; CDN Cloudflare phục vụ static assets frontend.
- **Technology:** Docker Compose, Nginx Upstream, Cloudflare CDN.
- **Affected modules:** DevOps / Hạ tầng.
- **Owner(s):** Toàn đội (TV1/TV4 lead).
- **Verification method:** Test scale container bằng lệnh `docker compose up --scale api=2`.
- **Related FR:** Toàn bộ hệ thống.
- **Conflict:** Không.
- **Risk:** Cấu hình port bị xung đột khi scale service trong Docker.
- **Evidence page:** §4.6 trang 44.

### NFR-SEO-001: Structured Data (Recipe Schema.org)
- **ID:** NFR-SEO-001
- **Category:** Tối ưu Tìm kiếm (SEO)
- **Requirement:** Mọi trang chi tiết công thức nấu ăn phải có siêu dữ liệu có cấu trúc JSON-LD chuẩn Schema.org.
- **Metric/threshold:** Khai báo `@type: "Recipe"` với đầy đủ các thuộc tính: name, description, image, author, datePublished, prepTime, cookTime, totalTime, recipeYield, recipeIngredient[], recipeInstructions[], nutrition; Đạt 100% Google Rich Results Test (hiển thị star rating, time, ingredients trên Google Search).
- **Technology:** Next.js JSON-LD script, Schema.org.
- **Affected modules:** Frontend Recipe Detail (TV3).
- **Owner(s):** TV3 (Primary).
- **Verification method:** Dùng công cụ Google Rich Results Test kiểm tra URL trang công thức.
- **Related FR:** `FR-RCP-002`.
- **Conflict:** Không.
- **Risk:** Thiếu trường bắt buộc làm mất Rich Snippet trên Google.
- **Evidence page:** §4.7 trang 44–45.

### NFR-SEO-002: Meta Tags & Social Open Graph
- **ID:** NFR-SEO-002
- **Category:** Tối ưu Tìm kiếm (SEO)
- **Requirement:** Đầy đủ các thẻ meta chuẩn và Open Graph để tối ưu hóa hiển thị khi chia sẻ trên mạng xã hội.
- **Metric/threshold:** Thẻ `<title>` dạng `{Recipe Name} | Culinary Blog` (<= 60 ký tự); `<meta name="description">` (150–160 ký tự); Open Graph đầy đủ: `og:title`, `og:description`, `og:image` (1200x630px), `og:url`, `og:type`; Thẻ Canonical URL chuẩn; Robots: `index, follow` (Published) và `noindex` (Draft/Archived).
- **Technology:** Next.js Metadata API (`generateMetadata`).
- **Affected modules:** Frontend (TV3).
- **Owner(s):** TV3 (Primary).
- **Verification method:** Facebook Sharing Debugger và Twitter Card Validator.
- **Related FR:** `FR-RCP-002`, `FR-CAT-002`.
- **Conflict:** Không.
- **Risk:** Slug chứa ký tự đặc biệt làm hỏng thẻ Canonical URL.
- **Evidence page:** §4.7 trang 45.

### NFR-SEO-003: Automated Sitemap & Robots.txt
- **ID:** NFR-SEO-003
- **Category:** Tối ưu Tìm kiếm (SEO)
- **Requirement:** Tự động tạo và cập nhật sơ đồ trang web sitemap.xml và khai báo robots.txt chuẩn xác.
- **Metric/threshold:** Sitemap.xml sinh tự động hàng ngày bởi `FR-JOB-003` (Hangfire cron "0 2 * * *"); Bao gồm 100% Published recipes, categories và static pages; Tự động ping Google Search Console sau khi cập nhật; File robots.txt cho phép crawlers và trỏ link Sitemap URL.
- **Technology:** Hangfire Recurring Job, XML Serializer, Google Ping API.
- **Affected modules:** Background Jobs & Next.js static.
- **Owner(s):** TV4 (Sitemap Job), TV3 (Robots.txt route).
- **Verification method:** Kiểm tra cấu trúc hợp lệ của `sitemap.xml` và log ping Google thành công.
- **Related FR:** `FR-JOB-003`.
- **Conflict:** Không.
- **Risk:** Chứa nhầm các trang riêng tư (dashboard, profile) trong sitemap.
- **Evidence page:** §4.7 trang 45.

### NFR-SEO-004: SEO-Friendly URL Structure
- **ID:** NFR-SEO-004
- **Category:** Tối ưu Tìm kiếm (SEO)
- **Requirement:** Cấu trúc đường dẫn URL thân thiện với người dùng và các bộ máy tìm kiếm.
- **Metric/threshold:** Đường dẫn dạng slug: `/recipes/{slug}`, `/categories/{slug}` (chữ thường, không dấu tiếng Việt, phân tách bằng dấu gạch ngang); Tự động 301 Redirect nếu slug của bài viết dạng draft thay đổi trước đó; Không dùng query string cho nội dung bài viết chính.
- **Technology:** Slugify Helper, Next.js dynamic routing.
- **Affected modules:** Backend (TV2) & Frontend (TV3).
- **Owner(s):** TV2 (Slug generation), TV3 (Routing).
- **Verification method:** Kiểm tra routing và status code 301 khi truy cập slug cũ.
- **Related FR:** `FR-RCP-001..003`, `FR-CAT-001..003`.
- **Conflict:** Không.
- **Risk:** Slug tiếng Việt bị lỗi font nếu không qua bộ lọc unaccent chuẩn.
- **Evidence page:** §4.7 trang 45.

---

## 8. Data Model Audit (Chương 7 — Toàn bộ Entities & Bảng CSDL)

Hệ thống sử dụng **PostgreSQL 16** với **EF Core 10 Code First**. Về mặt thiết kế mô hình dữ liệu, cần phân biệt rõ ràng giữa **9 Kiểu Thực thể (Model Types)** và **7 explicit persistence tables được mô tả trong Chapter 7**:

### 8.0. Phân loại Cấu trúc Thực thể và Bảng CSDL
- **9 Kiểu Thực thể trong Mô hình (Model Types):**
  1. `BaseEntity` (Abstract Base Class chứa Id, CreatedAt, UpdatedAt, IsDeleted, RowVersion)
  2. `Recipe` (Aggregate Root / Central Entity)
  3. `RecipeNutrition` (Owned Entity đại diện cho thông tin dinh dưỡng per-serving)
  4. `RecipeStep` (Domain Entity các bước thực hiện)
  5. `RecipeIngredient` (Domain Entity nguyên liệu nấu ăn)
  6. `RecipeImage` (Domain Entity hình ảnh bài viết)
  7. `Category` (Domain Entity danh mục món ăn)
  8. `ApplicationUser` (Identity Entity kế thừa `IdentityUser<string>`)
  9. `RefreshToken` (Auth Entity quản lý token gia hạn)
- **7 explicit persistence tables được mô tả trong Chapter 7:**
  1. `Recipes` (chứa các cột Recipe + 6 cột tiền tố `Nutrition_` của RecipeNutrition)
  2. `RecipeSteps`
  3. `RecipeIngredients`
  4. `RecipeImages`
  5. `Categories`
  6. `AspNetUsers`
  7. `RefreshTokens`
  - *Ghi chú kiến trúc:*
    - `BaseEntity` là abstract class không có table riêng theo quy ước kế thừa.
    - `RecipeNutrition` là Owned Entity, không có bảng riêng mà nhúng thành 6 cột tiền tố `Nutrition_` trong bảng `Recipes`.
    - Tài liệu không khẳng định toàn bộ PostgreSQL schema chỉ có 7 physical tables, vì SRS không kiểm kê chi tiết toàn bộ các bảng hạ tầng phụ trợ của ASP.NET Core Identity (ví dụ: `AspNetRoles`, `AspNetUserRoles`, `AspNetUserClaims`, `AspNetUserLogins`, `AspNetUserTokens`, `AspNetRoleClaims`).
### 8.1. Bảng thuộc tính chi tiết cấp trường (Field-Level Audit Table)

| Entity | Field | Type | Required | Nullable | Unique | Default | Relation / Constraint | Evidence |
| :--- | :--- | :--- | :--- | :--- | :--- | :--- | :--- | :--- |
| **BaseEntity** | `Id` | `uuid` (Guid) | Có | Không | Có (PK) | `gen_random_uuid()` | Khóa chính UUID v4 chống đoán ID tuần tự | §7.1 p.54 |
| BaseEntity | `CreatedAt` | `timestamptz` | Có | Không | Không | `NOW()` | Thời điểm tạo; EF Core AuditInterceptor gán tự động | §7.1 p.54 |
| BaseEntity | `UpdatedAt` | `timestamptz` | Không | Có | Không | NULL | Thời điểm cập nhật cuối; AuditInterceptor cập nhật | §7.1 p.54 |
| BaseEntity | `IsDeleted` | `boolean` | Có | Không | Không | `false` | Cờ soft delete; EF Core Global Query Filter | §7.1 p.54 |
| BaseEntity | `RowVersion` | `bytea` | Có | Không | Không | — | Concurrency token; Optimistic concurrency [Timestamp] | §7.1 p.54 |
| **Recipe** | `Id` | `uuid` | Có | Không | Có (PK) | Kế thừa | PK kế thừa từ BaseEntity | §7.2 p.54 |
| Recipe | `Title` | `varchar(200)` | Có | Không | Không | — | Tiêu đề; index IDX_Recipe_Title (GIN trigram optional) | §7.2 p.54 |
| Recipe | `Slug` | `varchar(220)` | Có | Không | Có | — | URL-friendly slug; IDX_Recipe_Slug (UNIQUE B-tree) | §7.2 p.54 |
| Recipe | `Description` | `text` | Có | Không | Không | — | Mô tả tóm tắt (max 2000 ký tự); card preview & SEO | §7.2 p.55 |
| Recipe | `Instructions` | `text` | Có | Không | Không | — | Hướng dẫn tổng quan markdown (legacy field) | §7.2 p.55 |
| Recipe | `PrepTime` | `integer` | Có | Không | Không | — | Thời gian chuẩn bị (phút); CHECK > 0 | §7.2 p.55 |
| Recipe | `CookTime` | `integer` | Có | Không | Không | — | Thời gian nấu (phút); CHECK >= 0 (0 cho no-cook) | §7.2 p.55 |
| Recipe | `Servings` | `integer` | Có | Không | Không | — | Số khẩu phần ăn; CHECK > 0 | §7.2 p.55 |
| Recipe | `Difficulty` | `smallint` (enum) | Có | Không | Không | `1` (Easy) | 1=Easy, 2=Medium, 3=Hard, 4=Expert; IDX_Recipe_Difficulty | §7.2 p.55 |
| Recipe | `Status` | `smallint` (enum) | Có | Không | Không | `0` (Draft) | 0=Draft, 1=Published, 2=Archived; IDX_Recipe_Status | §7.2 p.55 |
| Recipe | `CategoryId` | `uuid` | Có | Không | Không | — | FK → Categories.Id; ON DELETE RESTRICT; IDX_Recipe_CategoryId | §7.2 p.55 |
| Recipe | `AuthorId` | `varchar(450)` | Có | Không | Không | — | FK → AspNetUsers.Id; ON DELETE CASCADE; IDX_Recipe_AuthorId | §7.2 p.55 |
| Recipe | `SearchVector` | `tsvector` | Không | Có | Không | NULL | Full-text search vector; GIN index; DB Trigger unaccent | §7.2 p.56 |
| Recipe | `PublishedAt` | `timestamptz` | Không | Có | Không | NULL | Thời điểm chuyển sang Published; IDX_Recipe_PublishedAt | §7.2 p.56 |
| Recipe | `CreatedAt` | `timestamptz` | Có | Không | Không | `NOW()` | Kế thừa BaseEntity | §7.2 p.56 |
| Recipe | `UpdatedAt` | `timestamptz` | Không | Có | Không | NULL | Kế thừa BaseEntity | §7.2 p.56 |
| Recipe | `IsDeleted` | `boolean` | Có | Không | Không | `false` | Kế thừa BaseEntity; Partial index IDX_Recipe_IsDeleted | §7.2 p.56 |
| Recipe | `RowVersion` | `bytea` | Có | Không | Không | — | Kế thừa BaseEntity; Optimistic concurrency token | §7.2 p.56 |
| **RecipeNutrition** | `Nutrition_Calories` | `decimal(8,2)` | Không | Có | Không | NULL | Năng lượng (kcal / serving); Owned Entity trong Recipes | §7.2.1 p.56 |
| RecipeNutrition | `Nutrition_Protein` | `decimal(8,2)` | Không | Có | Không | NULL | Đạm (gram / serving); Owned Entity trong Recipes | §7.2.1 p.56 |
| RecipeNutrition | `Nutrition_Carbohydrates` | `decimal(8,2)` | Không | Có | Không | NULL | Tinh bột (gram / serving); Owned Entity trong Recipes | §7.2.1 p.56 |
| RecipeNutrition | `Nutrition_Fat` | `decimal(8,2)` | Không | Có | Không | NULL | Chất béo (gram / serving); Owned Entity trong Recipes | §7.2.1 p.56 |
| RecipeNutrition | `Nutrition_Fiber` | `decimal(8,2)` | Không | Có | Không | NULL | Chất xơ (gram / serving); Owned Entity trong Recipes | §7.2.1 p.56 |
| RecipeNutrition | `Nutrition_Sodium` | `decimal(8,2)` | Không | Có | Không | NULL | Natri (mg / serving); Owned Entity trong Recipes | §7.2.1 p.56 |
| **RecipeStep** | `Id` | `uuid` | Có | Không | Có (PK) | Kế thừa | PK kế thừa BaseEntity | §7.3 p.57 |
| RecipeStep | `RecipeId` | `uuid` | Có | Không | Không | — | FK → Recipes.Id; ON DELETE CASCADE | §7.3 p.57 |
| RecipeStep | `StepNumber` | `integer` | Có | Không | Composite | — | Thứ tự bước (1, 2, 3..); UNIQUE(RecipeId, StepNumber); CHECK > 0 | §7.3 p.57 |
| RecipeStep | `Title` | `varchar(200)` | Có | Không | Không | — | Tên bước ngắn gọn | §7.3 p.57 |
| RecipeStep | `Description` | `text` | Có | Không | Không | — | Mô tả chi tiết cách thực hiện bước | §7.3 p.57 |
| RecipeStep | `TimerMinutes` | `integer` | Không | Có | Không | NULL | Thời gian chờ bước (phút); CHECK >= 0; Nullable | §7.3 p.57 |
| RecipeStep | `ImageUrl` | `varchar(500)` | Không | Có | Không | NULL | URL ảnh minh họa bước tải lên MinIO; Nullable | §7.3 p.57 |
| RecipeStep | `CreatedAt` | `timestamptz` | Có | Không | Không | `NOW()` | Kế thừa BaseEntity | §7.3 p.57 |
| RecipeStep | `UpdatedAt` | `timestamptz` | Không | Có | Không | NULL | Kế thừa BaseEntity | §7.3 p.57 |
| RecipeStep | `IsDeleted` | `boolean` | Có | Không | Không | `false` | Kế thừa BaseEntity | §7.3 p.57 |
| RecipeStep | `RowVersion` | `bytea` | Có | Không | Không | — | Kế thừa BaseEntity | §7.3 p.57 |
| **RecipeIngredient** | `Id` | `uuid` | Có | Không | Có (PK) | Kế thừa | PK kế thừa BaseEntity | §7.4 p.57 |
| RecipeIngredient | `RecipeId` | `uuid` | Có | Không | Không | — | FK → Recipes.Id; ON DELETE CASCADE | §7.4 p.57 |
| RecipeIngredient | `Name` | `varchar(200)` | Có | Không | Không | — | Tên nguyên liệu | §7.4 p.57 |
| RecipeIngredient | `Quantity` | `decimal(10,3)` | Không | Có | Không | NULL | Số lượng nguyên liệu; Nullable cho nêm nếm vừa đủ | §7.4 p.57 |
| RecipeIngredient | `Unit` | `varchar(50)` | Không | Có | Không | NULL | Đơn vị đo lường (gram, ml, thìa...); Nullable | §7.4 p.57 |
| RecipeIngredient | `Notes` | `varchar(500)` | Không | Có | Không | NULL | Ghi chú sơ chế (thái hạt lựu...); Nullable | §7.4 p.57 |
| RecipeIngredient | `OrderIndex` | `integer` | Có | Không | Không | `0` | Thứ tự hiển thị trong danh sách | §7.4 p.57 |
| RecipeIngredient | `CreatedAt` | `timestamptz` | Có | Không | Không | `NOW()` | Kế thừa BaseEntity | §7.4 p.57 |
| RecipeIngredient | `UpdatedAt` | `timestamptz` | Không | Có | Không | NULL | Kế thừa BaseEntity | §7.4 p.57 |
| RecipeIngredient | `IsDeleted` | `boolean` | Có | Không | Không | `false` | Kế thừa BaseEntity | §7.4 p.57 |
| RecipeIngredient | `RowVersion` | `bytea` | Có | Không | Không | — | Kế thừa BaseEntity | §7.4 p.57 |
| **RecipeImage** | `Id` | `uuid` | Có | Không | Có (PK) | Kế thừa | PK kế thừa BaseEntity | §7.5 p.58 |
| RecipeImage | `RecipeId` | `uuid` | Có | Không | Không | — | FK → Recipes.Id; ON DELETE CASCADE | §7.5 p.58 |
| RecipeImage | `OriginalUrl` | `varchar(500)` | Có | Không | Không | — | URL ảnh gốc tải lên MinIO; Not null | §7.5 p.58 |
| RecipeImage | `MediumUrl` | `varchar(500)` | Không | Có | Không | NULL | Ảnh 800x600 do FR-JOB-002 resize; Nullable trước khi resize | §7.5 p.58 |
| RecipeImage | `ThumbnailUrl` | `varchar(500)` | Không | Có | Không | NULL | Ảnh 300x300 do FR-JOB-002 resize; Nullable | §7.5 p.58 |
| RecipeImage | `AltText` | `varchar(200)` | Không | Có | Không | NULL | Alt text hỗ trợ Accessibility (A11y); Nullable | §7.5 p.58 |
| RecipeImage | `IsPrimary` | `boolean` | Có | Không | Không | `false` | Ảnh đại diện chính; mỗi recipe tối đa 1 ảnh true | §7.5 p.58 |
| RecipeImage | `OrderIndex` | `integer` | Có | Không | Không | `0` | Thứ tự sắp xếp trong gallery | §7.5 p.58 |
| RecipeImage | `CreatedAt` | `timestamptz` | Có | Không | Không | `NOW()` | Kế thừa BaseEntity | §7.5 p.58 |
| RecipeImage | `UpdatedAt` | `timestamptz` | Không | Có | Không | NULL | Kế thừa BaseEntity | §7.5 p.58 |
| RecipeImage | `IsDeleted` | `boolean` | Có | Không | Không | `false` | Kế thừa BaseEntity | §7.5 p.58 |
| RecipeImage | `RowVersion` | `bytea` | Có | Không | Không | — | Kế thừa BaseEntity | §7.5 p.58 |
| **Category** | `Id` | `uuid` | Có | Không | Có (PK) | Kế thừa | PK kế thừa BaseEntity | §7.6 p.58 |
| Category | `Name` | `varchar(100)` | Có | Không | Có | — | Tên danh mục; UNIQUE; Not null | §7.6 p.58 |
| Category | `Slug` | `varchar(120)` | Có | Không | Có | — | URL slug duy nhất; UNIQUE B-tree; Not null | §7.6 p.58 |
| Category | `Description` | `text` | Không | Có | Không | NULL | Mô tả chi tiết danh mục; Nullable | §7.6 p.58 |
| Category | `ImageUrl` | `varchar(500)` | Không | Có | Không | NULL | Ảnh đại diện danh mục; Nullable | §7.6 p.58 |
| Category | `OrderIndex` | `integer` | Có | Không | Không | `0` | Thứ tự hiển thị menu navigation | §7.6 p.58 |
| Category | `CreatedAt` | `timestamptz` | Có | Không | Không | `NOW()` | Kế thừa BaseEntity | §7.6 p.58 |
| Category | `UpdatedAt` | `timestamptz` | Không | Có | Không | NULL | Kế thừa BaseEntity | §7.6 p.58 |
| Category | `IsDeleted` | `boolean` | Có | Không | Không | `false` | Kế thừa BaseEntity; Soft delete | §7.6 p.58 |
| Category | `RowVersion` | `bytea` | Có | Không | Không | — | Kế thừa BaseEntity | §7.6 p.58 |
| **ApplicationUser** | `Id` | `varchar(450)` | Có | Không | Có (PK) | — | PK kế thừa IdentityUser | §7.7 p.58 |
| ApplicationUser | `UserName` | `varchar(256)` | Có | Không | Có | — | Identity default | §7.7 p.59 |
| ApplicationUser | `Email` | `varchar(256)` | Có | Không | Có | — | Identity default | §7.7 p.59 |
| ApplicationUser | `PasswordHash` | `text` | Không | Có | Không | NULL | Password hash (NULL nếu user thuần Google OAuth) | §7.7 p.59 |
| ApplicationUser | `DisplayName` | `varchar(100)` | Có | Không | Không | — | Tên hiển thị công khai (bắt buộc) | §7.7 p.59 |
| ApplicationUser | `AvatarUrl` | `varchar(500)` | Không | Có | Không | NULL | URL ảnh avatar người dùng | §7.7 p.59 |
| ApplicationUser | `Bio` | `text` | Không | Có | Không | NULL | Tiểu sử ngắn tác giả | §7.7 p.59 |
| ApplicationUser | `IsActive` | `boolean` | Có | Không | Không | `true` | Trạng thái kích hoạt tài khoản | §7.7 p.59 |
| ApplicationUser | `CreatedAt` | `timestamptz` | Có | Không | Không | `NOW()` | Thời điểm tạo tài khoản | §7.7 p.59 |
| **RefreshToken** | `Id` | `uuid` | Có | Không | Có (PK) | — | Khóa chính UUID v4 | §7.8 p.59 |
| RefreshToken | `UserId` | `varchar(450)` | Có | Không | Không | — | FK → AspNetUsers.Id; ON DELETE CASCADE | §7.8 p.59 |
| RefreshToken | `TokenHash` | `varchar(64)` | Có | Không | Có | — | SHA-256 hash của token thô; UNIQUE; IDX_RefreshToken_Hash | §7.8 p.59 |
| RefreshToken | `ExpiresAt` | `timestamptz` | Có | Không | Không | — | Thời hạn token (7 ngày kể từ lúc tạo) | §7.8 p.59 |
| RefreshToken | `RevokedAt` | `timestamptz` | Không | Có | Không | NULL | Thời điểm thu hồi (NULL = còn hiệu lực) | §7.8 p.59 |
| RefreshToken | `ReplacedByTokenHash` | `varchar(64)` | Không | Có | Không | NULL | Hash của token kế thừa (để truy vết Token Family) | §7.8 p.59 |
| RefreshToken | `CreatedAt` | `timestamptz` | Có | Không | Không | `NOW()` | Thời điểm cấp phát | §7.8 p.59 |
| RefreshToken | `CreatedByIp` | `varchar(45)` | Không | Có | Không | NULL | Địa chỉ IP yêu cầu cấp token để audit bảo mật | §7.8 p.60 |

### 8.2. Phân tích chi tiết từng Entity

1. **BaseEntity (Abstract):**
   - **PK:** `Id uuid DEFAULT gen_random_uuid()`.
   - **Auditing:** `CreatedAt timestamptz NOT NULL DEFAULT NOW()`, `UpdatedAt timestamptz NULL`. Cập nhật tự động qua EF Core `AuditInterceptor`.
   - **Soft Delete:** `IsDeleted boolean NOT NULL DEFAULT false`. Tất cả truy vấn tự động áp dụng EF Core Global Query Filter `.Where(x => !x.IsDeleted)`.
   - **Concurrency Control:** `RowVersion bytea NOT NULL` đánh dấu `[Timestamp]` phục vụ Optimistic Concurrency Control (OCC). Khi xảy ra conflict, EF Core ném `DbUpdateConcurrencyException`.

2. **Recipe (Aggregate Root):**
   - **Khóa chính:** `Id uuid` (PK).
   - **Quan hệ:**
     - `CategoryId uuid NOT NULL`: FK đến `Categories.Id`, hành vi `ON DELETE RESTRICT` (ngăn xóa Category nếu có Recipe).
     - `AuthorId varchar(450) NOT NULL`: FK đến `AspNetUsers.Id`, hành vi `ON DELETE CASCADE`.
     - Owned Entity `RecipeNutrition`: Lưu trực tiếp 6 cột dinh dưỡng trên bảng `Recipes`.
     - 1-to-many với `RecipeStep`, `RecipeIngredient`, `RecipeImage`.
   - **Indexes:**
     - `IDX_Recipe_Slug`: UNIQUE B-tree trên `Slug`.
     - `IDX_Recipe_CategoryId`: B-tree trên `CategoryId`.
     - `IDX_Recipe_AuthorId`: B-tree trên `AuthorId`.
     - `IDX_Recipe_Status`: Index trên `Status`.
     - `IDX_Recipe_Difficulty`: Index trên `Difficulty`.
     - `IDX_Recipe_PublishedAt`: Index trên `PublishedAt`.
     - `IDX_Recipe_IsDeleted`: Partial index trên `IsDeleted` phục vụ soft delete.
     - `IDX_Recipe_Search`: GIN index trên `SearchVector` (`tsvector`) hỗ trợ full-text search tiếng Việt với `unaccent`.

3. **RecipeNutrition (Owned Entity):**
   - Không có bảng riêng; nhúng trực tiếp vào bảng `Recipes`.
   - 6 trường định lượng dinh dưỡng **per-serving**: `Nutrition_Calories` (kcal), `Nutrition_Protein` (g), `Nutrition_Carbohydrates` (g), `Nutrition_Fat` (g), `Nutrition_Fiber` (g), `Nutrition_Sodium` (mg).
   - Tất cả đều là `decimal(8,2)?` nullable.

4. **RecipeStep:**
   - **PK:** `Id uuid`.
   - **FK:** `RecipeId uuid NOT NULL`, FK đến `Recipes.Id` với `ON DELETE CASCADE`.
   - **Constraint:** `StepNumber integer NOT NULL CHECK (StepNumber > 0)`, Composite UNIQUE `(RecipeId, StepNumber)` đảm bảo không trùng số thứ tự bước trong một bài viết.
   - **Timer:** `TimerMinutes integer NULL CHECK (TimerMinutes >= 0)`.

5. **RecipeIngredient:**
   - **PK:** `Id uuid`.
   - **FK:** `RecipeId uuid NOT NULL`, FK đến `Recipes.Id` với `ON DELETE CASCADE`.
   - **Fields:** `Quantity decimal(10,3) NULL`, `Unit varchar(50) NULL`, `Notes varchar(500) NULL`, `OrderIndex integer NOT NULL DEFAULT 0`.

6. **RecipeImage:**
   - **PK:** `Id uuid`.
   - **FK:** `RecipeId uuid NOT NULL`, FK đến `Recipes.Id` với `ON DELETE CASCADE`.
   - **Fields:** `OriginalUrl varchar(500) NOT NULL`, `MediumUrl varchar(500) NULL`, `ThumbnailUrl varchar(500) NULL`, `AltText varchar(200) NULL`.
   - **Constraints:** `IsPrimary boolean NOT NULL DEFAULT false` (chỉ tối đa 1 ảnh primary cho mỗi recipe), `OrderIndex integer NOT NULL DEFAULT 0`.

7. **Category:**
   - **PK:** `Id uuid`.
   - **Constraints:** `Name varchar(100) NOT NULL UNIQUE`, `Slug varchar(120) NOT NULL UNIQUE`.
   - **Index:** `IDX_Category_Slug` (UNIQUE B-tree).
   - **Delete Behavior:** Soft delete (`IsDeleted = true`). Nếu có recipe liên kết, DB chặn xóa qua FK RESTRICT và API trả lỗi `CATEGORY_DELETE_HAS_RECIPES` (HTTP 409).

8. **ApplicationUser:**
   - Kế thừa `IdentityUser<string>`, bảng `AspNetUsers`.
   - Custom columns: `DisplayName varchar(100) NOT NULL`, `AvatarUrl varchar(500) NULL`, `Bio text NULL`, `IsActive boolean NOT NULL DEFAULT true`, `CreatedAt timestamptz NOT NULL DEFAULT NOW()`.

9. **RefreshToken:**
   - **PK:** `Id uuid`.
   - **FK:** `UserId varchar(450) NOT NULL`, FK đến `AspNetUsers.Id` với `ON DELETE CASCADE`.
   - **Fields:** `TokenHash varchar(64) NOT NULL UNIQUE` (SHA-256 hash của token ngẫu nhiên, không lưu raw token), `ExpiresAt timestamptz NOT NULL`, `RevokedAt timestamptz NULL`, `ReplacedByTokenHash varchar(64) NULL`, `CreatedByIp varchar(45) NULL`.
   - **Index:** `IDX_RefreshToken_Hash` trên `TokenHash`.

---

## 9. REST API Master Contract (Chương 8 — 33 Endpoints)

Base URL: `/api/v1`. Toàn bộ response chuẩn hóa JSON theo envelope `{ data, meta }` cho trường hợp thành công và RFC 7807 Problem Details cho trường hợp lỗi.

### 9.1. Bảng Master Contract toàn bộ Endpoints (LITERAL THEO CHAPTER 8)

| # | Module | Method | Route | Request Body / Params (Chapter 8) | Response Data Shape (Chapter 8) | Auth Required | Success | Errors (RFC 7807) | Related FR | Conflict Link |
| :- | :--- | :--- | :--- | :--- | :--- | :--- | :--- | :--- | :--- | :--- |
| 1 | Auth | `POST` | `/auth/register` | `{ email, password, displayName }` | `201: { userId, email, displayName }` | Không | 201 Created | 400: validation errors, 409: email đã tồn tại | `FR-AUTH-001` | `CONFLICT-010` (Chương 3 trả tokens vs Chương 8 chỉ trả user info) |
| 2 | Auth | `POST` | `/auth/login` | `{ email, password }` | `200: { accessToken, refreshToken, expiresIn }` | Không | 200 OK | 401: sai credentials, 429: quá giới hạn rate limit | `FR-AUTH-002` | Không |
| 3 | Auth | `POST` | `/auth/google` | `{ idToken }` (ID Token từ Google Sign-In JS SDK) | `200: { accessToken, refreshToken, expiresIn }` | Không | 200 OK | 400: invalid token | `FR-AUTH-003` | `CONFLICT-013` (ExternalLoginInfo vs PKCE vs idToken) |
| 4 | Auth | `POST` | `/auth/refresh` | `{ refreshToken }` | `200: { accessToken, refreshToken, expiresIn }` | Không (dùng refreshToken) | 200 OK | 401: token hết hạn / bị revoke | `FR-AUTH-004` | `CONFLICT-018` |
| 5 | Auth | `POST` | `/auth/logout` | `{ refreshToken }` | (Empty body) | Bearer JWT | 204 No Content | 401: Unauthorized | `FR-AUTH-005` | Không |
| 6 | Auth | `GET` | `/auth/me` | (None) | `200: { id, email, displayName, avatarUrl, bio, roles }` | Bearer JWT | 200 OK | 401: Unauthorized | `FR-AUTH-006` | Không |
| 7 | Auth | `PATCH` | `/auth/me` | `{ displayName?, avatarUrl?, bio? }` | `200: { id, email, displayName, avatarUrl, bio }` | Bearer JWT | 200 OK | 400: validation, 401: Unauthorized | `FR-AUTH-007` | `CONFLICT-009` (FullName/UserName vs DisplayName/Bio) |
| 8 | Category | `GET` | `/categories` | (None) | `200: [{ id, name, slug, description, imageUrl, recipeCount }]` | Không | 200 OK | — | `FR-CAT-001` | `CONFLICT-020` |
| 9 | Category | `GET` | `/categories/{slug}` | Query: `?page=1&pageSize=10&sortBy=...` | `200: { category, recipes: PagedResult }` | Không | 200 OK | 404: Category not found | `FR-CAT-002` | `CONFLICT-012` |
| 10 | Category | `POST` | `/categories` | `{ name, description?, imageUrl? }` | `201: { id, name, slug, description }` | Bearer + Admin | 201 Created | 400: validation, 403: Forbidden, 409: name đã tồn tại | `FR-CAT-003` | Không |
| 11 | Category | `PUT` | `/categories/{id}` | `{ name, description?, imageUrl?, orderIndex? }` | `200: category updated` | Bearer + Admin | 200 OK | 400, 403, 404 | `FR-CAT-004` | `CONFLICT-017` |
| 12 | Category | `DELETE` | `/categories/{id}` | (None) | (Empty body) | Bearer + Admin | 204 No Content | 403: Forbidden, 404: Not found, 409: Có recipes thuộc category này | `FR-CAT-005` | `CONFLICT-002` |
| 13 | Recipe | `GET` | `/recipes` | `?page&pageSize&sortBy&sortOrder&categoryId&difficulty&minPrepTime&maxPrepTime` | `200: PagedResult<RecipeSummaryDto>` | Không (Author xem thêm Draft) | 200 OK | 400: validation | `FR-RCP-001` | `CONFLICT-003`, `CONFLICT-012`, `CONFLICT-021` |
| 14 | Recipe | `GET` | `/recipes/{slug}` | (None) | `200: RecipeDetailDto` (kèm steps, ingredients, images, nutrition) | Không (Draft: Author/Admin) | 200 OK | 404: Not found, 403: Draft | `FR-RCP-002` | `CONFLICT-008`, `CONFLICT-019` |
| 15 | Recipe | `GET` | `/recipes/search` | `?q={keyword}&page&pageSize&categoryId&difficulty` | `200: PagedResult<RecipeSummaryDto>` | Không | 200 OK | 400: query < 2 ký tự | `FR-SRCH-001..004` | `CONFLICT-003`, `CONFLICT-012`, `CONFLICT-016` |
| 16 | Recipe | `POST` | `/recipes` | `{ title, description, categoryId, prepTime, cookTime, servings, difficulty, instructions, nutrition? }` | `201: RecipeDetailDto` | Bearer (Author/Admin) | 201 Created | 400, 401, 403, 404 | `FR-RCP-003` | `CONFLICT-008` (4 vs 6 chỉ số dinh dưỡng) |
| 17 | Recipe | `PUT` | `/recipes/{id}` | `{ title?, description?, categoryId?, prepTime?, cookTime?, servings?, difficulty?, instructions?, nutrition? }` | `200: RecipeDetailDto` | Bearer (Owner/Admin) | 200 OK | 400, 401, 403, 404, 409 (Concurrency) | `FR-RCP-004` | `CONFLICT-008`, `CONFLICT-014` |
| 18 | Recipe | `PATCH` | `/recipes/{id}/publish` | (None) | `200: { id, status: "Published", publishedAt }` | Bearer (Owner/Admin) | 200 OK | 400: validation, 401, 403, 404 | `FR-RCP-005` | Không |
| 19 | Recipe | `PATCH` | `/recipes/{id}/unpublish` | (None) | `200: { id, status: "Draft" }` | Bearer (Owner/Admin) | 200 OK | 401, 403, 404 | `FR-RCP-005` | Không |
| 20 | Recipe | `PATCH` | `/recipes/{id}/archive` | (None) | `200: { id, status: "Archived" }` | Bearer (Owner/Admin) | 200 OK | 401, 403, 404 | `FR-RCP-006` | Không |
| 21 | Recipe | `DELETE` | `/recipes/{id}` | (None) | (Empty body) | Bearer (Owner/Admin) | 204 No Content | 401, 403, 404 | `FR-RCP-007` | `CONFLICT-001` (Soft vs Hard delete) |
| 22 | Images | `POST` | `/recipes/{id}/images` | `multipart/form-data: file (image), altText?, isPrimary?` | `201: { imageId, originalUrl, altText, isPrimary }` | Bearer (Owner/Admin) | 201 Created | 400: MIME invalid / size > 5MB, 401, 403, 404 | `FR-RCP-008`, `FR-FILE-001` | `CONFLICT-024` (Payload shape & property name) |
| 23 | Images | `PATCH` | `/recipes/{id}/images/{imageId}` | `{ altText?, isPrimary?, orderIndex? }` | `200: image updated` | Bearer (Owner/Admin) | 200 OK | 400, 401, 403, 404 | `FR-RCP-008` | `CONFLICT-023` (Set-primary contract) |
| 24 | Images | `DELETE` | `/recipes/{id}/images/{imageId}` | (None) | (Empty body) | Bearer (Owner/Admin) | 204 No Content | 400: không cho xóa ảnh primary duy nhất, 401, 403, 404 | `FR-RCP-008` | Không |
| 25 | Steps | `POST` | `/recipes/{id}/steps` | `{ stepNumber, title, description, timerMinutes?, imageUrl? }` | `201: RecipeStepDto` | Bearer (Owner/Admin) | 201 Created | 400, 401, 403, 404 | `FR-RCP-010` | `CONFLICT-006`, `CONFLICT-007`, `CONFLICT-015` |
| 26 | Steps | `PUT` | `/recipes/{id}/steps/{stepId}` | `{ stepNumber?, title?, description?, timerMinutes?, imageUrl? }` | `200: RecipeStepDto` | Bearer (Owner/Admin) | 200 OK | 400, 401, 403, 404 | `FR-RCP-010` | `CONFLICT-006`, `CONFLICT-007`, `CONFLICT-015` |
| 27 | Steps | `DELETE` | `/recipes/{id}/steps/{stepId}` | (None) | (Empty body) | Bearer (Owner/Admin) | 204 No Content | 401, 403, 404 | `FR-RCP-010` | Không |
| 28 | Ingredients | `POST` | `/recipes/{id}/ingredients` | `{ name, quantity?, unit?, notes?, orderIndex? }` | `201: RecipeIngredientDto` | Bearer (Owner/Admin) | 201 Created | 400, 401, 403, 404 | `FR-RCP-009` | `CONFLICT-004`, `CONFLICT-005` |
| 29 | Ingredients | `PUT` | `/recipes/{id}/ingredients/{ingId}` | `{ name?, quantity?, unit?, notes?, orderIndex? }` | `200: RecipeIngredientDto` | Bearer (Owner/Admin) | 200 OK | 400, 401, 403, 404 | `FR-RCP-009` | `CONFLICT-004`, `CONFLICT-005` |
| 30 | Ingredients | `DELETE` | `/recipes/{id}/ingredients/{ingId}` | (None) | (Empty body) | Bearer (Owner/Admin) | 204 No Content | 401, 403, 404 | `FR-RCP-009` | Không |
| 31 | Health | `GET` | `/health` | (None) | `200: { "status":"Healthy", "entries":{...} } | 503: Unhealthy` | Không | 200 OK | 503 Unhealthy | `FR-OBS-001` | Không |
| 32 | Health | `GET` | `/health/live` | (None) | `200: Healthy` | Không | 200 OK | (Crash) | `FR-OBS-001` | Không |
| 33 | Health | `GET` | `/health/ready` | (None) | `200: Healthy | 503: Unhealthy` | Không | 200 OK | 503 Unhealthy | `FR-OBS-001` | Không |

---

## 10. Frontend / UI Requirements Audit (Chương 5.1 & Các yêu cầu UI liên quan)

Hệ thống frontend được xây dựng trên nền tảng **Next.js App Router (React 19)** theo mô hình kết hợp Single Page Application (SPA), Server-Side Rendering (SSR), và Incremental Static Regeneration (ISR).

### 10.1. Danh mục 14 Routes / Màn hình giao diện

| Route / Màn hình | Mục đích & Nội dung | Loại Rendering | Yêu cầu Authentication | Quyền hạn (Role) | SEO / Metadata | Owner |
| :--- | :--- | :--- | :--- | :--- | :--- | :--- |
| `/` | Trang chủ: Banner, danh sách công thức nổi bật, categories | ISR (`revalidate=3600`) | Không | Public | Meta tags, Open Graph, Twitter Cards | TV3 |
| `/recipes` | Danh sách công thức: Tìm kiếm, lọc theo category/difficulty/thời gian, phân trang | SSR (Dynamic) | Không | Public | Meta tags động theo query | TV3 |
| `/recipes/[slug]` | Chi tiết công thức: Nguyên liệu, các bước nấu có timer, bảng dinh dưỡng, tác giả | ISR (`revalidate=300`) | Không (Public); Author/Admin xem được Draft | Public | JSON-LD Schema.org/Recipe, Open Graph đầy đủ | TV3 |
| `/categories` | Danh mục tổng hợp tất cả thể loại món ăn | ISR (`revalidate=3600`) | Không | Public | Meta tags tĩnh | TV3 |
| `/categories/[slug]` | Danh sách công thức thuộc danh mục cụ thể | ISR (`revalidate=600`) | Không | Public | Canonical URL, Meta description động | TV3 |
| `/auth/login` | Form đăng nhập email/password, nút Google OAuth 2.0 | CSR | Không (Redirect sang dashboard nếu đã đăng nhập) | Guest | Noindex, Nofollow | TV1 |
| `/auth/register` | Form đăng ký tài khoản mới | CSR | Không (Redirect nếu đã login) | Guest | Noindex, Nofollow | TV1 |
| `/dashboard` | Trang tổng quan quản trị cá nhân: Thống kê số bài viết, trạng thái | CSR | Bắt buộc | Author, Admin | Noindex, Private | TV3 |
| `/dashboard/recipes` | Bảng quản lý danh sách công thức cá nhân (Draft, Published, Archived) | CSR | Bắt buộc | Author, Admin | Noindex, Private | TV3 |
| `/dashboard/recipes/new`| Wizard đa bước tạo công thức mới (Thông tin chung, nguyên liệu, các bước, upload ảnh) | CSR | Bắt buộc | Author, Admin | Noindex, Private | TV3 |
| `/dashboard/recipes/[id]/edit` | Chỉnh sửa công thức đã có, quản lý gallery ảnh | CSR | Bắt buộc | Owner, Admin | Noindex, Private | TV3 |
| `/dashboard/categories` | Quản lý danh mục (Thêm, sửa, xóa, sắp xếp orderIndex) | CSR | Bắt buộc | Admin | Noindex, Private | TV3 |
| `/profile` | Xem và cập nhật thông tin cá nhân (DisplayName, AvatarUrl, Bio) | CSR | Bắt buộc | All authenticated users | Noindex, Private | TV3 |
| `/search` | Trang kết quả tìm kiếm Full-Text Search công thức nấu ăn | SSR | Không | Public | Meta description động | TV3 |

### 10.2. Các yêu cầu kỹ thuật Frontend & UX Chi tiết
1. **Responsive & Mobile-First:**
   - Hỗ trợ hoàn hảo 3 breakpoints: Mobile (< 768px), Tablet (768px - 1024px), Desktop (> 1024px).
   - Tương thích touch events trên thiết bị di động (bộ đếm timer, cuộn ngang ảnh gallery).
2. **Khả năng tiếp cận (Accessibility - WCAG 2.1 AA):**
   - Màu sắc đạt độ tương phản tối thiểu 4.5:1 đối với văn bản thông thường.
   - Hỗ trợ điều hướng hoàn toàn bằng bàn phím (Keyboard navigation: `Tab`, `Enter`, `Escape`).
   - Mọi ảnh đều có thuộc tính `alt` (lấy từ `AltText` của `RecipeImage`).
   - Kiểm thử tự động không có vi phạm qua công cụ `axe-core`.
3. **Quản lý State & Data Fetching:**
   - Server State: Sử dụng `TanStack Query` (React Query) cho caching, automatic background refetching, và optimistic updates.
   - Client Auth State: Quản lý qua Context hoặc Zustand; đồng bộ trạng thái access token trong bộ nhớ (memory) và lưu trữ refresh token bảo mật.
4. **UX Xử lý lỗi & Trạng thái tải:**
   - Hiển thị Skeleton Loader thay vì spinner che khuất màn hình khi tải trang hoặc nạp danh sách.
   - Bắt lỗi API chuẩn RFC 7807, parse field `errors` để hiển thị validation message ngay dưới từng ô input form.
   - Thông báo lỗi hệ thống hoặc ngoại lệ qua Toast Notification tự động biến mất sau 4 giây.
5. **Tối ưu SEO & Web Vitals:**
   - Đạt điểm Lighthouse Performance >= 90, SEO >= 95, Accessibility >= 90.
   - Các chỉ số Core Web Vitals: LCP < 2.5s, CLS < 0.1, INP < 200ms.
   - Tự động nén và tối ưu hóa hình ảnh hiển thị qua component `next/image` với định dạng WebP/AVIF.

---

## 11. Infra / DevOps Architecture Audit (Chương 6.4 & Môi trường Triển khai)

### 11.1. Kiến trúc Docker Compose Toàn diện (8 Services)

Hệ thống được đóng gói container đồng nhất qua `docker-compose.yml`, phục vụ phát triển local và triển khai production:

| Service Name | Docker Image | Host Port : Container Port | Volumes Mount | Dependencies | Vai trò & Cấu hình môi trường | Môi trường áp dụng |
| :--- | :--- | :--- | :--- | :--- | :--- | :--- |
| `nginx` | `nginx:alpine` | `80:80`, `443:443` | `./nginx/nginx.conf`, `./ssl/` | `api`, `frontend` | Reverse proxy, SSL termination, định tuyến `/api/v1` đến backend và `/` đến frontend | Production & Staging |
| `api` | `culinaryblog-api` (Dockerfile) | `5000:8080` | None | `postgres`, `redis`, `minio` | .NET 10 Minimal API Backend; kết nối CSDL, Cache, Storage qua env vars | Dev, Test, Prod |
| `frontend` | `culinaryblog-web` (Dockerfile) | `3000:3000` | None | `api` | Next.js 15/16 App Router Node.js runtime | Dev, Test, Prod |
| `postgres` | `postgres:16-alpine` | `5432:5432` | `pgdata:/var/lib/postgresql/data` | None | Hệ quản trị CSDL quan hệ chính; tự kích hoạt extension `unaccent`, `pg_trgm` | Dev, Test, Prod |
| `redis` | `redis:7-alpine` | `6379:6379` | `redisdata:/data` | None | In-memory cache & Distributed Rate Limiting; cờ `--appendonly yes` | Dev, Test, Prod |
| `minio` | `minio/minio:latest` | `9000:9000`, `9001:9001` | `miniodata:/data` | None | S3-compatible Object Storage lưu trữ ảnh; Console UI tại port 9001 | Dev, Test, Prod |
| `seq` | `datalust/seq:latest` | `5341:80` | `seqdata:/data` | None | Trung tâm lưu trữ và phân tích Structured Logs (Serilog OTLP ingest) | **Dev & Staging Only** |
| `mailhog` | `mailhog/mailhog` | `8025:8025`, `1025:1025`| None | None | SMTP Server ảo hứng email thử nghiệm (Web UI port 8025, SMTP port 1025) | **Dev Only** |

### 11.2. Phân biệt Ranh giới Phát triển: Giai đoạn Hiện tại vs Kiến trúc Đích
- **Giai đoạn 1 (Phase 1):** Tập trung vào Base Backend Foundation (TV1) gồm .NET 10 API, PostgreSQL, Docker Compose cơ sở (`postgres`, `redis`, `mailhog`), MediatR Pipeline (Logging, Validation). Chưa yêu cầu cài đặt toàn bộ Nginx SSL, MinIO cluster production hay Grafana/Tempo.
- **Kiến trúc Đích (Final SRS Architecture):** Toàn bộ 8 services hoạt động trơn tru, Nginx reverse proxy bảo vệ toàn bộ traffic, cấu hình HTTPS TLS 1.2+, Seq quản lý centralized logs, OpenTelemetry xuất trace metrics sang APM server.

---

## 12. Third-Party Services Audit (Chương 5.3 & Các tích hợp bên ngoài)

Hệ thống Culinary Blog tích hợp với 9 dịch vụ và thành phần mở rộng bên ngoài:

### 1. Google OAuth 2.0
- **Mục đích:** Xác thực đăng nhập/đăng ký người dùng nhanh chóng bằng tài khoản Google (`FR-AUTH-003`).
- **Giao thức / SDK:** OAuth 2.0 Authorization Code + PKCE hoặc xác thực qua Google ID Token (Google.Apis.Auth .NET SDK).
- **Cấu hình / Secrets:** `Authentication__Google__ClientId`, `Authentication__Google__ClientSecret`.
- **Hành vi khi lỗi (Failure):** Trả về HTTP 400 Problem Details (`AUTH_GOOGLE_TOKEN_INVALID`).
- **Fallback:** Người dùng chuyển sang đăng nhập bằng email và mật khẩu truyền thống.
- **Owner:** TV1.
- **Tham chiếu:** `FR-AUTH-003`, `NFR-SEC-004`, `CONFLICT-004`, `CONFLICT-013`.
- **Technical Risk:** `TECH-RISK-001`.

### 2. MinIO (S3-Compatible Object Storage)
- **Mục đích:** Lưu trữ file ảnh công thức gốc và ảnh sau resize (`FR-FILE-001`, `FR-FILE-002`, `FR-RCP-008`).
- **Giao thức / SDK:** AWS SDK for .NET (`AWSSDK.S3`) với endpoint override trỏ tới MinIO service.
- **Cấu hình / Secrets:** `MinIO__Endpoint`, `MinIO__AccessKey`, `MinIO__SecretKey`, `MinIO__BucketName` (mặc định: `culinary-blog`).
- **Hành vi khi lỗi:** Trả về HTTP 500 hoặc 503 nếu MinIO unreachable. Ghi log cảnh báo và rollback transaction metadata ảnh.
- **Fallback:** Không có fallback lưu local file system trên production để đảm bảo tính stateless của container.
- **Owner:** TV4.
- **Tham chiếu:** `FR-FILE-001..002`, `FR-RCP-008`, `NFR-REL-002`, `CONS-007`.
- **Technical Risk:** `TECH-RISK-004`.

### 3. Redis 7
- **Mục đích:** Cache phản hồi danh mục/công thức (`FR-CAT-001`, `FR-RCP-001..002`) và thực thi Sliding Window Distributed Rate Limiter.
- **Giao thức / SDK:** `StackExchange.Redis`, Microsoft Extensions Caching StackExchangeRedis.
- **Cấu hình / Secrets:** `ConnectionStrings__Redis` (vd: `localhost:6379`).
- **Hành vi khi lỗi:** Khi Redis down, CachingBehavior bắt exception, ghi log Warning và fallback truy vấn trực tiếp xuống PostgreSQL (Cache Bypass) giúp hệ thống duy trì hoạt động.
- **Owner:** TV2 (Recipe Cache), TV1 (Rate Limiting).
- **Tham chiếu:** `FR-CAT-001`, `FR-RCP-001..002`, `NFR-PERF-003`, `NFR-PERF-004`, `CONFLICT-005`.
- **Technical Risk:** `TECH-RISK-005`.

### 4. SMTP Server & Mailhog
- **Mục đích:** Gửi email chào mừng kích hoạt tài khoản bất đồng bộ (`FR-JOB-001`).
- **Giao thức / SDK:** `MailKit` / `MimeKit` qua interface `IEmailSender`.
- **Cấu hình / Secrets:** `Smtp__Host`, `Smtp__Port`, `Smtp__Username`, `Smtp__Password`. Local: trỏ vào `mailhog:1025`.
- **Hành vi khi lỗi:** Hangfire tự động retry theo cơ chế Exponential Backoff (tối đa 5 lần). Nếu thất bại hoàn toàn chuyển vào Dead Letter Queue để Admin kiểm tra.
- **Owner:** TV4 (Hangfire), TV1 (Trigger event khi register).
- **Tham chiếu:** `FR-JOB-001`, `NFR-REL-001`.
- **Technical Risk:** `TECH-RISK-006`.

### 5. Hangfire Background Job Processing
- **Mục đích:** Quản lý hàng đợi và thực thi tác vụ nền: Gửi email (`FR-JOB-001`), resize ảnh đa kích thước (`FR-JOB-002`), sinh và ping sitemap (`FR-JOB-003`).
- **Giao thức / SDK:** `Hangfire.Core`, `Hangfire.AspNetCore`, `Hangfire.PostgreSql`.
- **Cấu hình / Secrets:** Dùng chung connection string CSDL PostgreSQL, schema riêng `hangfire`. Dashboard tại `/hangfire` bảo vệ bởi Admin Authorization Policy.
- **Hành vi khi lỗi:** Tự động retry job lỗi, lưu trạng thái lỗi chi tiết vào bảng lịch sử.
- **Owner:** TV4.
- **Tham chiếu:** `FR-JOB-001..003`, `NFR-REL-001`, `NFR-SCALE-003`.
- **Technical Risk:** `TECH-RISK-007`.

### 6. Serilog + Seq Centralized Logging
- **Mục đích:** Ghi log có cấu trúc (Structured Logging) định dạng JSON kèm Correlation ID và đẩy log tập trung (`FR-OBS-001`).
- **Giao thức / SDK:** `Serilog.AspNetCore`, `Serilog.Sinks.Console`, `Serilog.Sinks.Seq`.
- **Cấu hình / Secrets:** `Seq__ServerUrl = http://seq:5341`.
- **Hành vi khi lỗi:** Console sink vẫn hoạt động độc lập ngay cả khi Seq service offline.
- **Owner:** TV4 (Primary), TV1 (Base middleware).
- **Tham chiếu:** `FR-OBS-001`, `NFR-MAINT-001`, `CONS-010`.
- **Technical Risk:** Không đáng kể.

### 7. OpenTelemetry .NET SDK
- **Mục đích:** Distributed Tracing và Performance Metrics qua chuẩn OTLP (`FR-OBS-003`).
- **Giao thức / SDK:** `OpenTelemetry.Exporter.OpenTelemetryProtocol`, instrumentation cho ASP.NET Core, HttpClient, EF Core.
- **Cấu hình / Secrets:** `OTEL_EXPORTER_OTLP_ENDPOINT`.
- **Hành vi khi lỗi:** Tự động drop span traces nếu server thu nhận offline, không ảnh hưởng hiệu năng ứng dụng.
- **Owner:** TV4.
- **Tham chiếu:** `FR-OBS-003`, `NFR-PERF-001`, `NFR-MAINT-001`.
- **Technical Risk:** Không.

### 8. Google Search Console Sitemap Ping
- **Mục đích:** Tự động thông báo cho Googlebot lập chỉ mục khi sitemap XML được cập nhật định kỳ (`FR-JOB-003`).
- **Giao thức / SDK:** HTTP GET request đến `https://www.google.com/ping?sitemap={sitemapUrl}` qua `HttpClient`.
- **Cấu hình / Secrets:** Không cần API key hoặc credential bảo mật.
- **Hành vi khi lỗi:** Ghi log cảnh báo nếu Google trả mã lỗi, job vẫn hoàn thành việc ghi file sitemap.
- **Owner:** TV4.
- **Tham chiếu:** `FR-JOB-003`, `NFR-SEO-003`.
- **Technical Risk:** Google có thể ngừng hỗ trợ endpoint sitemap ping không chứng thực (cần theo dõi chính sách Google).

### 9. PostgreSQL Extensions (`unaccent`, `pg_trgm`)
- **Mục đích:** Hỗ trợ tìm kiếm Full-Text Search không dấu tiếng Việt và khớp mờ trigram (`FR-SRCH-001`, `FR-SRCH-002`).
- **Giao thức / SDK:** SQL Native Extension kích hoạt qua EF Core migration script (`CREATE EXTENSION IF NOT EXISTS unaccent;`).
- **Cấu hình / Secrets:** Cần quyền Superuser trong database khi chạy migration khởi tạo extension.
- **Hành vi khi lỗi:** Nếu thiếu extension, tìm kiếm tiếng Việt có dấu sẽ không match được từ khóa không dấu.
- **Owner:** TV2.
- **Tham chiếu:** `FR-SRCH-001..002`, `NFR-PERF-002`.
- **Technical Risk:** `TECH-RISK-002`.

---

## 13. Conflicts Master Register (Danh mục 25 Mâu thuẫn Kiến trúc)

Tất cả 24 mâu thuẫn dưới đây đều được trích xuất từ việc đối chiếu chéo giữa các chương của SRS v1.0.0. Toàn bộ đang ở trạng thái **OPEN** (chờ xác nhận từ Giảng viên / Nhóm trưởng).

| Conflict ID | Tên mâu thuẫn | Bằng chứng A (Evidence A) | Bằng chứng B (Evidence B) | Ảnh hưởng chính | Trạng thái |
| :--- | :--- | :--- | :--- | :--- | :--- |
| **CONFLICT-001** | Recipe Delete Strategy | §3.3 FR-RCP-010 p.35: Dùng `DbSet.Remove()` hard delete | §7.1 p.54, §7.2 p.56, §8.3 p.64: Soft delete (`IsDeleted=true`) | TV2 (DB/API) | **OPEN** |
| **CONFLICT-002** | Category Delete Strategy | §3.2 FR-CAT-005 p.27: Xóa danh mục khỏi CSDL | §7.1 p.54, §7.6 p.58, §8.2 p.63: Soft delete | TV4 (Category) | **OPEN** |
| **CONFLICT-003** | Sorting Convention | §3.3 FR-RCP-001 p.28: `sort=title`, `sort=-createdAt` | §8 p.61, §8.3 p.63: `sortBy=createdAt&sortOrder=desc` | TV2, TV3 | **OPEN** |
| **CONFLICT-004** | RecipeIngredient Quantity / Unit | §3.3 FR-RCP-009 p.35: Quantity & Unit bắt buộc | §7.4 p.57, §8.6 p.65: Quantity & Unit là Nullable | TV2, TV3 | **OPEN** |
| **CONFLICT-005** | Ingredient Ordering Name | §3.3 FR-RCP-009 p.35: Sắp xếp theo `StepNumber` | §7.4 p.57, §8.6 p.65: Sắp xếp theo trường `OrderIndex` | TV2 | **OPEN** |
| **CONFLICT-006** | RecipeStep Duration | §3.3 FR-RCP-009 p.34: Lưu trường `DurationMinutes` | §7.3 p.57, §8.5 p.65: Lưu trường `TimerMinutes` | TV2, TV3 | **OPEN** |
| **CONFLICT-007** | RecipeStep Title | §3.3 FR-RCP-009 p.34: Bước chỉ gồm StepNumber & Description | §7.3 p.57, §8.5 p.65: Cột `Title varchar(200) NOT NULL` bắt buộc | TV2, TV3 | **OPEN** |
| **CONFLICT-008** | RecipeNutrition Fields | §3.3 FR-RCP-003 p.29: 4 trường (Calories, Protein, Carbs, Fat) | §7.2.1 p.56: 6 trường (+ Fiber, Sodium per-serving) | TV2, TV3 | **OPEN** |
| **CONFLICT-009** | Auth User Name Fields | §3.1 Auth / Account p.17: Sử dụng `FullName` và `UserName` | §3.1 User Profile p.23, §7.7 p.59, §8.1 p.62: Dùng `DisplayName` và `Bio` | TV1, TV3 | **OPEN** |
| **CONFLICT-010** | Register Response | §3.1 FR-AUTH-001 p.19: Chỉ trả về `{ userId, email }` | §8.1 p.61: Trả về kèm `token` và `refreshToken` | TV1, TV3 | **OPEN** |
| **CONFLICT-011** | Validation HTTP Status | §4.2 NFR-SEC-006 p.41: Lỗi validation trả HTTP 422 | §8 p.61, Phụ lục A p.67: Lỗi validation trả HTTP 400 | TV1, Toàn đội | **OPEN** |
| **CONFLICT-012** | Pagination Response Shape | §3.3 FR-RCP-001 p.28: `{ items, totalCount, pageNumber }` | §8 p.61: Chuẩn hóa envelope `{ data: [], meta: { page, total } }` | TV1, TV2, TV3 | **OPEN** |
| **CONFLICT-013** | Google OAuth Contract | §3.1 FR-AUTH-003 p.21: Gửi `{ code, redirectUri }` đổi token | §8.1 p.61: Gửi trực tiếp `{ idToken }` lên backend | TV1, TV3 | **OPEN** |
| **CONFLICT-014** | Concurrency Conflict HTTP Status | §3.3 FR-RCP-004 p.31 & §8.3 p.63: Lỗi concurrency trả HTTP 409 Conflict | Phụ lục A p.67 & Phụ lục B p.68: Lỗi CONCURRENCY_CONFLICT trả HTTP 422 Unprocessable Entity | TV2 | **OPEN** |
| **CONFLICT-015** | RecipeStep StepNumber Generation | §3.3 FR-RCP-009 p.34: Server tự động tính `StepNumber` | §8.5 p.65: Client gửi `stepNumber` trong Request Body | TV2, TV3 | **OPEN** |
| **CONFLICT-016** | Search Query Cache TTL | §3.4 FR-SRCH-001 p.36: Cache kết quả tìm kiếm 5 phút | §4.1 NFR-PERF-003 p.39: Không cache search query trên Redis | TV2 | **OPEN** |
| **CONFLICT-017** | Category Update Allowed Fields | §3.2 FR-CAT-004 p.26: Chỉ cho phép cập nhật `name`, `description` | §8.2 p.63: Cho phép cập nhật thêm `imageUrl` và `orderIndex` | TV4 | **OPEN** |
| **CONFLICT-018** | Refresh Token Entropy | §3.1 FR-AUTH-004 p.22: Tạo token ngẫu nhiên 32 bytes (256-bit) | §4.2 NFR-SEC-002 p.40: Tạo token ngẫu nhiên 64 bytes (512-bit) | TV1 | **OPEN** |
| **CONFLICT-019** | Recipe Detail Cache TTL / Tech | §3.3 FR-RCP-002 p.29: Cache In-Memory 10 phút | §4.1 NFR-PERF-003 p.39: Cache Redis phân tán 30 phút | TV2 | **OPEN** |
| **CONFLICT-020** | Category Cache Technology / TTL | §3.2 FR-CAT-001 p.24: In-Memory IMemoryCache 60 phút | §4.1 NFR-PERF-003 p.39: Redis phân tán (Distributed Cache) 24h | TV4 | **OPEN** |
| **CONFLICT-021** | Recipe List Author Visibility | §3.3 FR-RCP-001 p.28 (Mô tả): Thấy Draft & Archived của mình | §3.3 FR-RCP-001 p.28 (Step 4): Chỉ thấy Published và Draft của mình | TV2, TV3 | **OPEN** |
| **CONFLICT-022** | Browser Version Support Matrix | §2.4.3 p.14: Hỗ trợ Chrome 90+, Firefox 88+, Safari 14+ | §5.4.2 p.49: Yêu cầu tối thiểu Chrome 112+, Firefox 113+, Safari 16+ | TV3 | **OPEN** |
| **CONFLICT-023** | Recipe Image Set-Primary Endpoint Contract | §3.3 FR-RCP-008 p.34: Route `PATCH /api/v1/recipes/{id}/images/{imageId}/primary` | §8.4 p.64: Route `PATCH /recipes/{id}/images/{imageId}` body `{ altText?, isPrimary?, orderIndex? }` | TV2, TV4 | **OPEN** |
| **CONFLICT-024** | Recipe Image Upload Response Shape / URL Property Naming | §3.3 FR-RCP-008 p.34: Upload ảnh trả HTTP 201 với `{ url, isPrimary }` | §8.4 p.64: Upload trả HTTP 201 với `{ imageId, originalUrl, altText, isPrimary }` & §7.5 p.58 cột DB là `OriginalUrl` | TV2, TV4 | **OPEN** |
| **CONFLICT-025** | BaseEntity Inheritance vs ApplicationUser Identity Inheritance | §6.4 p.52, §7.1 p.54: Quy định tất cả entities kế thừa BaseEntity (có Id, CreatedAt, UpdatedAt, IsDeleted, RowVersion) | §7.7 p.58–59: ApplicationUser kế thừa IdentityUser<string>, không mô tả kế thừa BaseEntity (không có RowVersion, IsDeleted) | TV1, TV2 | **OPEN** |

---

## 14. Technical Risks Register (Toàn đội — 15 Technical Risks)

| Risk ID | Module | Mô tả rủi ro kỹ thuật | Bằng chứng SRS | Nguyên nhân có thể thất bại khi triển khai | Owner | Phụ thuộc | Phương pháp kiểm chứng | Trạng thái |
| :--- | :--- | :--- | :--- | :--- | :--- | :--- | :--- | :--- |
| **TECH-RISK-001** | Auth | Lệch luồng Google OAuth giữa SPA Next.js và .NET Backend | §3.1 p.21 vs §8.1 p.61 | Frontend Next.js dùng SDK Google Identity sinh ra ID Token trong khi backend kỳ vọng Authorization Code hoặc ngược lại | TV1 | TV3 Frontend | Test luồng đăng nhập Google trên staging environment | OPEN |
| **TECH-RISK-002** | Search / DB | Lỗi Extension PostgreSQL `unaccent` và GIN Trigger | §7.2 p.56, §3.4 p.36 | Database container khởi tạo thiếu extension unaccent khiến câu lệnh Trigger FTS ném exception hoặc tìm kiếm tiếng Việt có dấu bị sai lệch | TV2 | TV1 Docker base | Chạy migration test trên Testcontainers PostgreSQL | OPEN |
| **TECH-RISK-003** | Recipe / DB | Concurrency conflict trên Aggregate Recipe đa bảng | §4.4 p.43, §7.1 p.54 | Thao tác cập nhật công thức cùng lúc với thêm Step/Ingredient gây xung đột phiên bản `RowVersion` (DbUpdateConcurrencyException) | TV2 | TV1 Interceptor | Unit & Integration tests giả lập 2 request đồng thời | OPEN |
| **TECH-RISK-004** | File Storage | Rò rỉ file ảnh rác trên MinIO khi tạo bài viết thất bại | §3.5 p.37, §7.5 p.58 | Người dùng tải ảnh lên MinIO thành công nhưng sau đó hủy form lưu Recipe, khiến file trên MinIO bị mồ côi (orphaned files) | TV4 | TV2 Recipe | Background Job quét định kỳ xóa ảnh không gắn với RecipeId | OPEN |
| **TECH-RISK-005** | Caching | Xung đột Cache Invalidation storm khi cập nhật bài viết | §4.1 p.39, §6.2 p.52 | Khi sửa Recipe, nếu không xóa đúng cache keys của category và danh sách trang chủ, người dùng xem dữ liệu cũ | TV2 | TV4 Redis | Integration test kiểm tra cache evict sau update command | OPEN |
| **TECH-RISK-006** | Background | Khóa bảng và phân mảnh CSDL do Hangfire polling | §3.6 p.38, §5.3 p.48 | Hangfire dùng chung schema và connection pool với ứng dụng chính, tần suất poll job cao gây quá tải connection pool PostgreSQL | TV4 | TV2 DbContext | Cấu hình Schema riêng biệt (`hangfire`) và giới hạn Worker Pool | OPEN |
| **TECH-RISK-007** | Background | Tràn bộ nhớ RAM khi resize ảnh độ phân giải lớn | §3.6 p.38, §4.5 p.44 | Xử lý ảnh 5MB bằng SixLabors.ImageSharp đồng thời trên nhiều luồng Hangfire làm server OOM (Out Of Memory) | TV4 | MinIO | Giới hạn số worker xử lý ảnh tối đa 2 tác vụ song song | OPEN |
| **TECH-RISK-008** | Auth | Race condition khi Refresh Token Rotation trong nhiều tab | §3.1 p.22, §4.2 p.40 | Người dùng mở nhiều tab cùng lúc, tab 1 refresh thành công làm revoke token cũ khiến tab 2 refresh bị kích hoạt cơ chế Reuse Detection (bị logout oan) | TV1 | TV3 Frontend | Thêm Grace Period (30 giây) cho refresh token cũ trong Token Family | OPEN |
| **TECH-RISK-009** | Frontend | Stale data trên Next.js ISR do trễ revalidate | §5.1 p.46, §4.7 p.45 | Trang `/recipes/[slug]` có `revalidate=300` (5 phút), tác giả sửa công thức nhưng độc giả vẫn thấy nội dung cũ trong 5 phút | TV3 | TV2 API | Kích hoạt On-Demand Revalidation (`revalidatePath` / `revalidateTag`) | OPEN |
| **TECH-RISK-010** | DB / Perf | Suy giảm hiệu năng truy vấn do Global Query Filter `IsDeleted` | §7.1 p.54, §4.1 p.39 | EF Core tự động append `.Where(x => !x.IsDeleted)` vào mọi câu query, thiếu partial index sẽ gây Table Scan trên bảng lớn | TV2 | TV1 BaseEntity | Tạo Partial Index `CREATE INDEX ON recipes (is_deleted) WHERE NOT is_deleted` | OPEN |
| **TECH-RISK-011** | Infra / RateLimit | Lệch đồng hồ thời gian giữa các container làm sai Rate Limiter | §4.2 p.41, §5.2 p.47 | Sliding Window Rate Limiter dựa trên Redis timestamp, nếu đồng hồ container API và Redis lệch nhau sẽ chặn request không chính xác | TV1 | Docker Infra | Cấu hình đồng bộ NTP trên máy chủ host | OPEN |
| **TECH-RISK-012** | Frontend | Xung đột xác thực giữa NextAuth/Auth.js và Custom JWT Backend | §5.1 p.46, §8.1 p.61 | NextAuth mặc định quản lý session qua cookie riêng, khó đồng bộ Bearer Token trong memory với background refresh của backend | TV3 | TV1 Auth | Sử dụng Custom Auth Provider hoặc Zustand Store thay vì phụ thuộc sâu vào NextAuth session | OPEN |
| **TECH-RISK-013** | SEO / Job | Google ngừng hỗ trợ HTTP GET sitemap ping | §3.6 p.38, §5.3 p.48 | Endpoint `google.com/ping?sitemap=` bị Google thông báo deprecate, Hangfire job gửi ping liên tục nhận mã lỗi HTTP 404/410 | TV4 | Google API | Xử lý graceful degradation trong job, ghi log info thay vì ném exception | OPEN |
| **TECH-RISK-014** | Category / DB | Race condition khi xóa danh mục có Recipe mới tạo | §3.2 p.27, §7.2 p.55 | Admin xóa danh mục đồng thời với Author đang bấm lưu Recipe thuộc danh mục đó, dẫn đến ngoại lệ FK RESTRICT không được xử lý đẹp | TV4 | TV2 Recipe | Bọc trong Transaction với IsolationLevel thích hợp | OPEN |
| **TECH-RISK-015** | Observability | OTLP Exporter làm nghẽn pipeline khi Seq/Collector down | §4.5 p.44, §5.3 p.48 | OpenTelemetry SDK gửi traces qua mạng, nếu collector bị treo và timeout không được set ngắn, toàn bộ HTTP request bị chậm | TV4 | Toàn đội | Cấu hình timeout OTLP exporter <= 1 giây và bật chế độ non-blocking buffer | OPEN |

---

## 15. Questions to Confirm with Lecturer (25 Câu hỏi Tham vấn Giảng viên)

Bộ câu hỏi này được lập ra tương ứng trực tiếp với 24 mâu thuẫn kiến trúc (CONFLICT-001 đến CONFLICT-024) nhằm xin ý kiến phê duyệt chính thức từ Giảng viên hướng dẫn:

- **Q01 (liên quan CONFLICT-001):** Thưa Thầy/Cô, nghiệp vụ xóa công thức nấu ăn (FR-RCP-010) nhóm nên cài đặt theo cơ chế **Soft Delete** (`IsDeleted = true` kết hợp EF Core Global Query Filter như trong Data Model §7.1) hay **Hard Delete** xóa vật lý khỏi CSDL?
- **Q02 (liên quan CONFLICT-002):** Nghiệp vụ xóa danh mục (FR-CAT-005) nhóm nên áp dụng Soft Delete thống nhất với toàn bộ hệ thống hay xóa hẳn bản ghi nếu danh mục chưa có bài viết?
- **Q03 (liên quan CONFLICT-003):** Chuẩn tham số sắp xếp trên API GET `/recipes`, nhóm thống nhất dùng `sortBy` & `sortOrder` (theo chuẩn Chapter 8) hay dùng cú pháp tiền tố `sort=-createdAt`?
- **Q04 (liên quan CONFLICT-004):** Trường số lượng (`Quantity`) và đơn vị (`Unit`) của nguyên liệu (RecipeIngredient) là bắt buộc nhập hay cho phép để trống (Nullable) đối với các nguyên liệu gia vị nêm nếm "vừa đủ"?
- **Q05 (liên quan CONFLICT-005):** Thứ tự nguyên liệu trong bài viết được quản lý qua trường `OrderIndex` (int) hay theo quy ước nào khác?
- **Q06 (liên quan CONFLICT-006):** Trường thời gian thực hiện của từng bước nấu ăn được đặt tên thống nhất là `TimerMinutes` (theo Data Model & API Spec) hay `DurationMinutes`?
- **Q07 (liên quan CONFLICT-007):** Mỗi bước nấu ăn (RecipeStep) có bắt buộc phải có tiêu đề tóm tắt ngắn (`Title`, varchar 200) hay chỉ cần nội dung mô tả chi tiết (`Description`)?
- **Q08 (liên quan CONFLICT-008):** Thông tin dinh dưỡng của công thức (RecipeNutrition) lưu trữ đầy đủ 6 chỉ số (Calories, Protein, Carbohydrates, Fat, Fiber, Sodium per-serving) theo Data Model §7.2.1 hay chỉ lưu 4 chỉ số cơ bản?
- **Q09 (liên quan CONFLICT-009):** Khi đăng ký tài khoản và quản lý thông tin tác giả, hệ thống thống nhất sử dụng `DisplayName` và `Bio` (theo Data Model §7.7 & API §8.1) hay dùng `FullName` và `UserName` (theo Chapter 3 §3.1)?
- **Q10 (liên quan CONFLICT-010):** Sau khi đăng ký thành công (POST `/auth/register`), API trả về cặp JWT tokens để người dùng tự động đăng nhập luôn hay chỉ trả thông tin tài khoản vừa tạo?
- **Q11 (liên quan CONFLICT-011):** Khi dữ liệu đầu vào không hợp lệ qua FluentValidation, mã lỗi HTTP trả về thống nhất là **400 Bad Request** (RFC 7807) hay **422 Unprocessable Entity**?
- **Q12 (liên quan CONFLICT-012):** Cấu trúc dữ liệu phân trang chuẩn của toàn bộ hệ thống thống nhất là envelope dạng `{ data: [...], meta: { page, pageSize, total, totalPages } }` có đúng không?
- **Q13 (liên quan CONFLICT-013):** Luồng đăng nhập Google OAuth 2.0 ở frontend sẽ gửi thẳng `idToken` lên backend xác thực (theo API Spec §8.1) hay gửi `authorizationCode` kèm `redirectUri`?
- **Q14 (liên quan CONFLICT-014):** Khi phát hiện xung đột đồng thời (Optimistic Concurrency Conflict qua RowVersion), mã HTTP trả về chuẩn là **409 Conflict** (theo FR-RCP-004 trang 31 & API §8.3 trang 63) hay **422 Unprocessable Entity** (theo Phụ lục A trang 67 & Bảng mã lỗi Phụ lục B trang 68 CONCURRENCY_CONFLICT)?
- **Q15 (liên quan CONFLICT-015):** Thứ tự bước nấu (`StepNumber`) sẽ do client truyền lên tường minh hay do backend tự động tăng dần khi tạo mới?
- **Q16 (liên quan CONFLICT-016):** Kết quả tìm kiếm Full-Text Search có lưu cache 5 phút trên Redis không, hay truy vấn trực tiếp xuống PostgreSQL có GIN index?
- **Q17 (liên quan CONFLICT-017):** Khi cập nhật danh mục qua PUT `/categories/{id}`, Admin có được phép cập nhật thêm ảnh (`imageUrl`) và thứ tự hiển thị (`orderIndex`) không?
- **Q18 (liên quan CONFLICT-018):** Độ dài ngẫu nhiên của chuỗi Refresh Token bảo mật là 32 bytes (256-bit) hay 64 bytes (512-bit)?
- **Q19 (liên quan CONFLICT-019):** Cache chi tiết bài viết (Recipe Detail) sẽ lưu trên In-Memory Cache (10 phút) hay trên Redis phân tán (30 phút)?
- **Q20 (liên quan CONFLICT-020):** Danh sách thể loại (Categories) sẽ được cache bằng memory cục bộ hay lưu trên Redis phân tán (24 giờ)?
- **Q21 (liên quan CONFLICT-021):** Tác giả (Author) khi xem danh sách công thức của chính mình có nhìn thấy các bài viết đã bị đưa vào lưu trữ (`Archived`) hay chỉ thấy `Draft` và `Published`?
- **Q22 (liên quan CONFLICT-022):** Tiêu chuẩn phiên bản trình duyệt tối thiểu hỗ trợ là Chrome 90+ / Safari 14+ (§2.4.3) hay Chrome 112+ / Safari 16+ (§5.4.2)?
- **Q23 (liên quan CONFLICT-023):** Thao tác đặt ảnh đại diện chính của bài viết sử dụng endpoint riêng biệt `PATCH /api/v1/recipes/{id}/images/{imageId}/primary` (theo FR-RCP-008 trang 34) hay dùng endpoint cập nhật metadata chung `PATCH /recipes/{id}/images/{imageId}` với body `{ isPrimary: true }` (theo Chapter 8 §8.4 trang 64)?
- **Q24 (liên quan CONFLICT-024):** Payload phản hồi khi upload ảnh thành công (POST `/recipes/{id}/images`) trả về `{ url, isPrimary }` (theo FR-RCP-008 trang 34) hay trả về payload đầy đủ `{ imageId, originalUrl, altText, isPrimary }` (theo Chapter 8 §8.4 trang 64 & Data Model §7.5)?
- **Q25 (liên quan CONFLICT-025):** Theo §6.4 trang 52 và §7.1 trang 54, quy tắc chung là mọi entity đều kế thừa BaseEntity (có Id, CreatedAt, UpdatedAt, IsDeleted, RowVersion). Tuy nhiên §7.7 trang 58–59 đặc tả ApplicationUser kế thừa IdentityUser<string> mà không mô tả các trường của BaseEntity. ApplicationUser có thuộc quy tắc BaseEntity hay là ngoại lệ Identity riêng?

---

## 16. Master Summary & Project Governance Metrics

### 16.1. Bảng số liệu tổng hợp kiểm toán toàn dự án

| Chỉ số định lượng | Giá trị thực tế | Nguồn kiểm chứng trong SRS v1.0.0 | Ghi chú kiểm toán |
| :--- | :--- | :--- | :--- |
| **Tổng số trang đã kiểm toán (Pages Audited)** | **71 / 71 trang** | Toàn bộ tài liệu PDF từ Trang 1 đến Trang 71 | Đã đọc, phân tích và lập checklist 100% trang |
| **Số trang còn thiếu (Pages Missing)** | **0 trang** | Bảng đối chiếu SRS-71-PAGE-MASTER-CHECKLIST.md | Không bỏ sót bất kỳ trang nào |
| **Tổng số Functional Requirements (Explicit IDs)** | **34 FR IDs** | Chương 3 (AUTH: 7, CAT: 5, RCP: 10, SRCH: 4, FILE: 2, JOB: 3, OBS: 3) | Loại bỏ cách đếm cũ; metadata §1.5 ghi nhầm "27 FR" |
| **Tổng số Non-Functional Requirements (Detailed IDs)** | **30 NFR IDs** | Chương 4 (PERF: 5, SEC: 7, USE: 4, REL: 3, MAINT: 4, SCALE: 3, SEO: 4) | Chuẩn hóa toàn bộ NFR chi tiết |
| **Tổng số Design Constraints** | **10 CONS IDs** | Chương 2.5 (`CONS-001` đến `CONS-010`) | Ràng buộc kiến trúc bắt buộc toàn bộ nhóm tuân thủ |
| **Kiểu Thực thể trong Mô hình (Model Types)** | **9 Kiểu** | Chương 7 (BaseEntity, Recipe, RecipeNutrition, RecipeStep, RecipeIngredient, RecipeImage, Category, ApplicationUser, RefreshToken) | 1 abstract base class + 1 aggregate root + 1 owned entity + 6 entities |
| **Bảng lưu trữ bền vững (Persistence Tables)** | **7 explicit persistence tables được mô tả trong Chapter 7** | Chương 7 (Recipes, RecipeSteps, RecipeIngredients, RecipeImages, Categories, AspNetUsers, RefreshTokens) | RecipeNutrition là owned columns trong Recipes; BaseEntity không có table riêng; không khẳng định toàn bộ PostgreSQL schema chỉ có 7 tables do còn infrastructure tables của Identity |
| **Tổng số API Endpoints** | **33 Endpoints** | Chương 8 (Auth: 7, Category: 5, Recipe: 9, Images: 3, Steps: 3, Ingredients: 3, Health: 3) | 33 endpoints định nghĩa tường minh |
| **Tổng số Mâu thuẫn Kiến trúc (Conflicts)** | **25 Conflicts** | `CONFLICT-001` đến `CONFLICT-025` | 100% ở trạng thái OPEN, có bằng chứng đối chiếu 2 phía |
| **Mâu thuẫn mới phát hiện qua Master Audit** | **4 Conflicts mới** | `CONFLICT-022`, `CONFLICT-023`, `CONFLICT-024`, `CONFLICT-025` | Browser version, Set primary route, Image upload shape, BaseEntity vs Identity |
| **Tổng số Rủi ro Kỹ thuật (Technical Risks)** | **15 Risks** | `TECH-RISK-001` đến `TECH-RISK-015` | Bao phủ rủi ro của cả 4 thành viên |
| **Số câu hỏi tham vấn Giảng viên** | **25 Câu hỏi** | `Q01` đến `Q25` | Khớp 1-1 với 25 Conflicts |
| **Nhóm Mâu thuẫn Giao tiếp Đa thành viên** | **4 CROSS-TEAM BLOCKER GROUPS covering 6 conflict IDs: 001, 002, 012, 019, 020, 025** | Toàn hệ thống | Quản trị độc lập theo ma trận phụ thuộc chéo |

### 16.2. Thống kê Blockers theo từng thành viên nhóm

- **TV1 (Auth, Base Architecture, Docker Base):**
  - **5 Module Blockers Phase 2:** `CONFLICT-009`, `CONFLICT-010`, `CONFLICT-011`, `CONFLICT-013`, `CONFLICT-018`.
  - **Phụ thuộc Cross-Team Blocker:** `CONFLICT-025` (phối hợp TV2 về mô hình kế thừa BaseEntity vs IdentityUser, thuộc Group 4).
  - *(Lưu ý: `CONFLICT-004` thuộc về TV2 Recipe, không thuộc TV1).*
  - *Điểm nghẽn chính:* Định dạng payload Google OAuth 2.0, quy ước response đăng ký trả token hay user info, mã lỗi validation 400 vs 422, và entropy refresh token.
- **TV2 (Recipe Core, Database, Search, Recipe Cache):**
  - **13 Module Blockers:** `CONFLICT-003`, `CONFLICT-004`, `CONFLICT-005`, `CONFLICT-006`, `CONFLICT-007`, `CONFLICT-008`, `CONFLICT-011`, `CONFLICT-014`, `CONFLICT-015`, `CONFLICT-016`, `CONFLICT-021`, `CONFLICT-023`, `CONFLICT-024`.
  - **Phụ thuộc các nhóm Cross-Team Blockers:** `CONFLICT-001` (Recipe delete semantics, Group 1), `CONFLICT-012` (Recipe pagination, Group 2), `CONFLICT-019` (Recipe Cache owner, Group 3), `CONFLICT-025` (DbContext & Migrations với TV1, Group 4).
  - *Điểm nghẽn chính:* Soft delete vs Hard delete Recipe, tham số phân trang & sắp xếp, nullable Quantity/Unit nguyên liệu, số lượng chỉ số dinh dưỡng 4 hay 6 per-serving, mã lỗi concurrency 409 vs 422.
- **TV3 (Next.js Frontend, Layout, Recipe UI, Dashboard):**
  - **14 Module Blockers / UI Impacts:** `CONFLICT-003`, `CONFLICT-004`, `CONFLICT-006`, `CONFLICT-007`, `CONFLICT-008`, `CONFLICT-009`, `CONFLICT-010`, `CONFLICT-011`, `CONFLICT-013`, `CONFLICT-017`, `CONFLICT-021`, `CONFLICT-022`, `CONFLICT-023`, `CONFLICT-024`.
  - **Phụ thuộc các nhóm Cross-Team Blockers:** `CONFLICT-001` / `CONFLICT-002` (Delete UX filtering, Group 1), `CONFLICT-012` (Pagination envelope data shape, Group 2).
  - *Điểm nghẽn chính:* Envelope phân trang `{ data, meta }`, các trường thông tin tác giả trong form đăng ký, luồng đăng ký tự động login, hiển thị bài Archived của tác giả, và browser support matrix.
- **TV4 (Categories, MinIO File Upload, Hangfire, Observability):**
  - **4 Module Blockers:** `CONFLICT-011`, `CONFLICT-017`, `CONFLICT-023`, `CONFLICT-024`.
  - **Phụ thuộc các nhóm Cross-Team Blockers:** `CONFLICT-002` (Category delete semantics, Group 1), `CONFLICT-012` (Category pagination, Group 2), `CONFLICT-020` (Category Cache owner, Group 3).
  - *Điểm nghẽn chính:* Soft delete Category, các trường cho phép sửa danh mục, route đặt ảnh primary (`{imageId}/primary` vs metadata PATCH), và payload upload ảnh trả về `{ url, isPrimary }` vs `{ imageId, originalUrl, altText, isPrimary }`.
- **Cross-Team Blockers (Điểm nghẽn giao tiếp toàn đội):**
  - **4 CROSS-TEAM BLOCKER GROUPS covering 6 conflict IDs: 001, 002, 012, 019, 020, 025:**
    1. **Group 1: Delete Semantics (`CONFLICT-001`, `CONFLICT-002`):** Cơ chế xóa dữ liệu (Soft Delete `IsDeleted=true` vs Hard Delete `DbSet.Remove()`) giữa TV2/TV4 (CSDL) và TV3 (Frontend UI filtering).
    2. **Group 2: Pagination Data Shape (`CONFLICT-012`):** Chuẩn phân trang và envelope API `{ data, meta }` giữa Backend (TV1, TV2, TV4) và Client Fetching (TV3).
    3. **Group 3: Caching Architecture & Invalidation (`CONFLICT-019`, `CONFLICT-020`):** Chiến lược đồng bộ bộ nhớ đệm và vô hiệu hóa cache (In-Memory vs Redis phân tán) giữa Backend Commands (TV2: Recipe Cache owner, TV4: Category Cache owner) và Caching Queries.
    4. **Group 4: BaseEntity vs Identity Inheritance (`CONFLICT-025`):** Mô hình kế thừa thực thể `BaseEntity` vs `ApplicationUser` (`IdentityUser<string>`) giữa TV1 (Auth Entity) và TV2 (DbContext / Migrations).

---
