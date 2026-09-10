# Nhom9_Culinary_Blog
# THÀNH VIÊN VÀ PHÂN CÔNG CÔNG VIỆC NHÓM 9

## 1. Thành viên

| STT | Họ và tên | MSSV | Email | SĐT |
| :---: | :--- | :--- | :--- | :--- |
|1|Dương Văn Minh|2312688|2312688@dlu.edu.vn|0352183984
|2|Nguyễn Phạm Phú Nam|2312695|2312695@dlu.edu.vn|0917707902
|3|Mai Quý Phước|2312716|2312716@dlu.edu.vn|097100756
|4|Trần Hữu Phan Lâm|2312656|2312656@dlu.edu.vn|0562144990

---

## 2. Chi tiết phân công việc cho từng thành viên

### 👤 Thành viên 1: Fullstack – Nền tảng Hệ thống & Module Xác thực (Auth)

**Phạm vi Backend:**
* Dựng khung dự án Clean Architecture (Domain, Application, Infrastructure, Presentation) và cấu hình MediatR Pipeline (Logging, Validation).
* Hiện thực Module Xác thực (FR-AUTH-001 đến FR-AUTH-007): Đăng ký, đăng nhập Local JWT, Google OAuth 2.0, Refresh Token Rotation, Logout, Xem/Sửa Profile.
* Cấu hình Rate Limiting middleware cho các endpoint nhạy cảm.

**Phạm vi Frontend:**
* Dựng khung ứng dụng Next.js App Router, cài đặt Tailwind CSS, cấu hình Auth.js v5 / JWT Client State.
* Xây dựng màn hình Đăng ký (`/auth/register`), Đăng nhập (`/auth/login`), Trang cá nhân (`/profile`).

**Hạ tầng & DevOps:**
* Dựng cấu hình `docker-compose.yml` nền tảng (App, Postgres, Redis, MinIO).

---

### 👤 Thành viên 2: Backend Core Developer – Module Công thức Nấu ăn & Cơ sở Dữ liệu

**Phạm vi Backend:**
* Thiết kế Entity Framework Core Data Models, Migrations và Seeding dữ liệu mẫu.
* Hiện thực lõi Module Công thức (FR-RCP-001 đến FR-RCP-007, FR-RCP-009, FR-RCP-010): Tạo/Sửa/Xóa công thức, quản lý Nguyên liệu, các Bước thực hiện, chuyển trạng thái Draft / Published / Archive.
* Xử lý Optimistic Concurrency qua `RowVersion` và Resource-Based Authorization (chỉ tác giả mới được sửa bài của mình).

**Tìm kiếm & Caching:**
* Hiện thực Module Tìm kiếm (FR-SRCH-001 đến FR-SRCH-004): Tìm kiếm toàn văn bản tiếng Việt không dấu (PostgreSQL Full-Text Search với `tsvector`, `unaccent`).
* Cấu hình chiến lược Caching với Redis (Output Cache / Cache-Aside) cho danh sách và chi tiết công thức.

---

### 👤 Thành viên 3: Frontend Lead Developer – UI/UX & Giao diện Công thức Nấu ăn

**Phạm vi Frontend (Giao diện Người dùng):**
* Thiết kế layout responsive (Mobile/Tablet/Desktop) sử dụng Tailwind CSS.
* Tích hợp TanStack Query (React Query) để kết nối REST API phía Backend.

**Màn hình công khai (Guest/Public):**
* Trang chủ (`/`) & Danh sách công thức có Bộ lọc/Sắp xếp/Phân trang (`/recipes`).
* Trang Chi tiết công thức (`/recipes/[slug]`): Tích hợp chuẩn JSON-LD Schema.org Recipe cho SEO.
* Trang Tìm kiếm (`/search`).

**Màn hình Tác giả (Author Dashboard):**
* Màn hình quản lý danh sách công thức cá nhân (`/dashboard/recipes`).
* Form tạo/chỉnh sửa công thức dạng Wizard nhiều bước sử dụng React Hook Form + Zod (`/dashboard/recipes/new`, `/[id]/edit`).

---

### 👤 Thành viên 4: Fullstack / Services Specialist – Danh mục, Lưu trữ File, Background Jobs & Observability

**Phạm vi Backend:**
* Module Danh mục (FR-CAT-001 đến FR-CAT-005): CRUD danh mục, sinh slug tự động, cache `IMemoryCache`.
* Module Quản lý Tệp tin (FR-FILE-001, FR-FILE-002, FR-RCP-008): Upload/xóa ảnh trên MinIO S3, validate dung lượng (≤5MB) và Magic Bytes.
* Module Background Jobs (FR-JOB-001 đến FR-JOB-003): Cấu hình Hangfire xử lý gửi email chào mừng, tự động sinh thumbnail/medium image, và job tạo `sitemap.xml` tự động.
* Module Quan sát Hệ thống (FR-OBS-001 đến FR-OBS-003): Cấu hình Serilog structured logging, OpenTelemetry tracing và các endpoint Health Checks (`/health`, `/health/ready`, `/health/live`).

**Phạm vi Frontend:**
* Màn hình Quản lý Danh mục dành cho Admin (`/dashboard/categories`).
* Component Upload ảnh (Drag & Drop + Progress bar) phía Client.

---

## 3. Bảng Tổng hợp Phân công Task Matrix

| Thành viên | Vai trò chính | Module phụ trách chính | Yêu cầu FR / NFR đáp ứng |
| :--- | :--- | :--- | :--- |
| **Phú Nam** | Team Lead / Auth & Infra | FR-AUTH, Base Infrastructure, Auth UI | FR-AUTH-001 → 007, NFR-SEC-001 → 003 |
| **Văn Minh** | Backend Core & Data | FR-RCP (Core), FR-SRCH, Caching | FR-RCP-001 → 007, 009, 010, FR-SRCH-001 → 004, NFR-PERF-003/004 |
| **Phan Lâm** | Frontend Lead UI/UX | Frontend Core Pages & Dashboard | Màn hình Public & Dashboard, NFR-USE-001 → 004, NFR-SEO |
| **Phước** | Services & Admin Fullstack | FR-CAT, FR-FILE, FR-JOB, FR-OBS | FR-CAT-001 → 005, FR-FILE-001/002, FR-JOB-001 → 003, FR-OBS-001 → 003 |

---

## 4. Quy trình Triển khai khuyến nghị (Tránh nghẽn tiến độ)

### Giai đoạn 1 (Tuần 1 - Khởi tạo Base)
* **Phú Nam**: Dựng base Clean Architecture & Docker Compose (Postgres, Redis, MinIO).
* **Văn Minh**: Tạo DB Schemas / Entities cơ bản (Recipe, User, Category).
* **Phan Lâm**: Dựng Next.js layout, UI Components nền tảng.
* **Phước**: Tích hợp MinIO Service & Hangfire base.

### Giai đoạn 2 (Tuần 2 & 3 - Phát triển Chức năng Lõi)
* Các thành viên phát triển Backend API song song theo từng Module đã chia.
* **Phú Nam & Phước**: Làm xong Auth & File Service sẽ hỗ trợ Phan Lâm ghép API vào Frontend.
* **Văn Minh**: Tập trung viết FTS PostgreSQL và Caching.

### Giai đoạn 3 (Tuần 4 - Tích hợp, Test & NFRs)
* **Phan Lâm**: Hoàn thiện UI Form Wizard tạo công thức.
* **Phước**: Hoàn thiện Health Checks, Logging, Sitemap.
* Cả nhóm viết Unit Test / Integration Test và kiểm thử luồng end-to-end.