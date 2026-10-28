# Kế Hoạch Triển Khai: Bước Nấu Có Cấu Trúc, Cache Redis, Xóa Ảnh & E2E Testing

> **Phụ trách:** Dương Văn Minh (TV2 – MSSV: 2312688)  
> **Thời gian:** Tuần 8 (28/10/2026)  
> **Nhánh thực hiện:** `2312688_DuongVanMinh_feature/recipe-crud-enhancement-e2e`  
> **Tài liệu đặc tả:** [FR-RCP-010-recipe-steps-cache-e2e-spec.md](../features/FR-RCP-010-recipe-steps-cache-e2e-spec.md)  
> **Checklist:** [FR-RCP-010-recipe-steps-cache-e2e-checklist.md](../features/FR-RCP-010-recipe-steps-cache-e2e-checklist.md)

---

## 1. Phân Chia Giai Đoạn & Công Việc

### Giai đoạn 1: Lập Kế hoạch, Đặc tả Kỹ thuật & Cập nhật Ma trận Traceability
- [x] Phân tích hiện trạng dự án, mã nguồn backend, frontend và các file tài liệu SRS audit.
- [x] Soạn thảo file đặc tả kỹ thuật: `docs/features/FR-RCP-010-recipe-steps-cache-e2e-spec.md`.
- [x] Soạn thảo kế hoạch triển khai: `docs/superpowers/plans/2026-10-28-recipe-crud-enhancement-e2e-plan.md`.
- [x] Soạn thảo checklist theo dõi: `docs/features/FR-RCP-010-recipe-steps-cache-e2e-checklist.md`.
- [x] Commit: `docs: create plan, spec, and checklist for recipe steps, cache, and e2e`

### Giai đoạn 2: Backend & Persistence: Fix Testcontainers, Cache Redis & Luồng Xóa Ảnh MinIO
- [ ] Sửa lỗi Foreign Key `FK_Recipes_AspNetUsers_AuthorId` trong `PostgresMigrationTests.cs` và `RecipeStepConcurrencyTests.cs` bằng cách tạo bản ghi `ApplicationUser` trước khi chèn `Recipe`.
- [ ] Cải thiện `DistributedRecipeCache.cs`:
  - Chuẩn hóa key lưu/xóa chữ thường: `recipes:slug:{slug.ToLowerInvariant()}`.
  - Tích hợp cache generation key (`recipes:generation`) cho `RemoveByPrefixAsync` tương tự `DistributedCategoryCache` để vô hiệu hóa cache danh sách và tìm kiếm nhất quán.
- [ ] Cải thiện luồng xóa Recipe trong `RecipeLifecycleHandler.cs`:
  - Eager load danh sách `Images` của Recipe khi xử lý `DeleteRecipeCommand`.
  - Thực hiện xóa các file ảnh vật lý trên MinIO thông qua `IFileStorageService.DeleteAsync` (`OriginalUrl`, `MediumUrl`, `ThumbnailUrl`) trước khi xóa entity khỏi database để ngăn chặn file mồ côi (`TECH-RISK-004`).
- [ ] Chạy kiểm thử tự động `dotnet test` xác nhận 100% test pass.
- [ ] Commit: `feat(backend): optimize recipe redis cache invalidation, minio cleanup on delete, and fix testcontainers foreign key`

### Giai đoạn 3: Frontend Recipe Steps & Nutrition Display Enhancement
- [ ] Nâng cấp interface `StructuredStep` trong `recipe-editor.tsx` để hỗ trợ đầy đủ các trường: `title`, `description`, `durationMinutes`, `imageUrl`.
- [ ] Cập nhật giao diện nhập liệu bước nấu trong `recipe-editor.tsx`:
  - Hỗ trợ thêm bước, nhập tiêu đề, mô tả, thời gian nấu, ảnh minh họa.
  - Nút di chuyển thứ tự bước (lên / xuống), nút xóa bước.
  - Tự động load đầy đủ các thuộc tính của bước khi mở form chỉnh sửa công thức hiện có.
- [ ] Cập nhật `RecipeDetailClient` (`frontend/src/app/recipes/[slug]/recipe-detail-client.tsx`):
  - Hiển thị đầy đủ thông tin bước nấu có cấu trúc (tiêu đề, thời lượng thực hiện, ảnh minh họa, nội dung chi tiết).
  - Bổ sung section hiển thị giá trị dinh dưỡng (Calories, Protein, Carbohydrates, Fat, Fiber, Sodium) trên mỗi khẩu phần.
- [ ] Kiểm tra build frontend `npm run build` không phát sinh lỗi.
- [ ] Commit: `feat(frontend): enhance structured recipe steps editor and display nutrition on detail page`

### Giai đoạn 4: Bộ Kiểm thử E2E Hoàn Chỉnh với Playwright
- [ ] Cập nhật `frontend/e2e/support/mock-recipe-api.mjs` hoặc route mocks hỗ trợ đầy đủ các endpoint tạo/sửa công thức, bước có cấu trúc, dinh dưỡng, ảnh và phân quyền.
- [ ] Xây dựng file test E2E `frontend/e2e/recipe-crud.spec.ts`:
  - Test luồng tạo công thức mới với đầy đủ ảnh, nguyên liệu, bước nấu có cấu trúc và thông tin dinh dưỡng.
  - Test hiển thị trang chi tiết công thức xác nhận ảnh, nguyên liệu, các bước và dinh dưỡng hiển thị chính xác.
  - Test chỉnh sửa công thức: sửa đổi nguyên liệu, sửa bước nấu, đổi thứ tự bước, xóa bước, cập nhật dinh dưỡng.
  - Test quản lý ảnh: upload, đặt ảnh đại diện chính, xóa ảnh.
  - Test phân quyền: chặn người dùng chưa đăng nhập hoặc không phải tác giả/admin.
- [ ] Chạy và xác minh toàn bộ test E2E với Playwright.
- [ ] Commit: `test(e2e): add comprehensive recipe crud e2e tests covering steps, images, nutrition, ordering, and permissions`

### Giai đoạn 5: Cập Nhật Tài Liệu Nghiệm Thu & Hoàn Tất
- [ ] Cập nhật `README.md` (Mục 9 và Mục 10) ghi nhận tiến độ thực tế và kết quả kiểm thử.
- [ ] Cập nhật `docs/srs-audit/TEAM-REQUIREMENT-TRACEABILITY.md` nâng trạng thái `FR-RCP-010`, `FR-RCP-007`, `FR-RCP-008` sang `DONE`.
- [ ] Cập nhật `docs/features/FR-RCP-010-recipe-steps-cache-e2e-checklist.md` đánh dấu hoàn thành toàn bộ hạng mục kèm bằng chứng.
- [ ] Commit: `docs: update traceability, readme, and implementation checklist for completed week 8 tasks`
