# TV1 - Giai đoạn 1: Ranh giới tích hợp & Phân định trách nhiệm

## TV1 sở hữu trong giai đoạn này (Phase 1)
- Khung backend Clean Architecture chuẩn: Domain, Application, Infrastructure, API/Presentation.
- MediatR Pipeline: LoggingBehavior -> ValidationBehavior -> Handler.
- Base endpoint kỹ thuật kiểm tra pipeline: `GET /` và `GET /api/v1/system/base-status`.
- Docker Compose nền tảng: API, PostgreSQL 16, Redis 7, MinIO.
- Quy ước dependency, cấu hình môi trường mẫu (.env.example), Dockerfile multi-stage.

## Chưa làm trong giai đoạn 1 (Thuộc Phase 2 hoặc Thành viên khác)
- FR-AUTH-001..007.
- ASP.NET Core Identity / JWT / Google OAuth.
- Refresh Token persistence/rotation.
- Rate limiting Auth.
- Next.js auth pages & client auth state.
- EF Core entities/migrations của Recipe/Category.
- MinIO file upload service/Hangfire/Observability.

## Quy tắc phân quyền & phòng tránh merge conflict
- **Thành viên 1 (TV1):** Chỉ dựng base nền tảng ở Phase 1. Thiết kế `ApplicationUser` và `RefreshToken` ở Phase 2, phối hợp với TV2 để đưa vào DbContext chung.
- **Thành viên 2 (TV2):** Sở hữu Recipe Core / Recipe business (domain, repository, PostgreSQL FTS, Redis recipe cache) và chủ trì DbContext tổng / EF Core migrations. TV1 không tạo Recipe entity.
- **Thành viên 3 (TV3):** Sở hữu Next.js base layout và UI chung. TV1 chỉ thêm Auth UI ở Phase 2 trên nền base của TV3.
- **Thành viên 4 (TV4):** Sở hữu Category Module (CRUD, slug, category cache) cùng MinIO file service implementation, Hangfire và Observability. TV1 chỉ giữ service/container nền tảng ở Docker Compose.
