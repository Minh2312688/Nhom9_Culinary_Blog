# Checklist Triển Khai: Bước Nấu Có Cấu Trúc, Cache Redis, Xóa Ảnh & E2E Testing

> **Trạng thái:** `[x]` Đã hoàn thành (Completed)  
> **Người phụ trách:** Dương Văn Minh (TV2 – MSSV: 2312688)  
> **Kế hoạch chi tiết:** [2026-10-28-recipe-crud-enhancement-e2e-plan.md](../superpowers/plans/2026-10-28-recipe-crud-enhancement-e2e-plan.md)  
> **Đặc tả kỹ thuật:** [FR-RCP-010-recipe-steps-cache-e2e-spec.md](./FR-RCP-010-recipe-steps-cache-e2e-spec.md)  
> **Quy ước:** Đánh dấu `[x]` khi hoàn thành kèm bằng chứng lệnh/test; `[~]` khi hoàn thành một phần; `[ ]` chưa thực hiện.

---

## 1. Kế Hoạch, Đặc Tả & Ma Trận Yêu Cầu

- [x] **Phân tích hiện trạng mã nguồn & tài liệu SRS Audit**:
  - [x] Khảo sát `RecipeStep` trong Domain (`StepNumber`, `Title`, `Description`, `DurationMinutes`, `ImageUrl`).
  - [x] Khảo sát hiện trạng `recipe-editor.tsx` thiếu trường cấu trúc cho bước nấu.
  - [x] Khảo sát cơ chế Redis cache trong `DistributedRecipeCache.cs` và luồng xóa ảnh MinIO.
- [x] **Biên soạn tài liệu**:
  - [x] Tạo file đặc tả kỹ thuật: `docs/features/FR-RCP-010-recipe-steps-cache-e2e-spec.md`.
  - [x] Tạo kế hoạch chi tiết: `docs/superpowers/plans/2026-10-28-recipe-crud-enhancement-e2e-plan.md`.
  - [x] Tạo checklist theo dõi: `docs/features/FR-RCP-010-recipe-steps-cache-e2e-checklist.md`.
  - [x] Commit: `70b4bac4` (`docs: create plan, spec, and checklist for recipe steps, cache, and e2e`).

---

## 2. Backend & Persistence: Fix Testcontainers, Cache Redis & Luồng Xóa Ảnh MinIO

- [x] **Sửa lỗi Foreign Key Testcontainers**:
  - [x] `PostgresMigrationTests.cs`: Tạo `ApplicationUser` trước khi tạo `Recipe` để thỏa mãn ràng buộc `FK_Recipes_AspNetUsers_AuthorId`.
  - [x] `RecipeStepConcurrencyTests.cs`: Tạo `ApplicationUser` trước khi tạo `Recipe`.
  - [x] Kiểm tra chạy testcontainer PostgreSQL: 138/138 integration tests pass 100% (0 failed, 9 skipped).
- [x] **Tối ưu hóa Cache Redis (`DistributedRecipeCache.cs`)**:
  - [x] Chuẩn hóa key đọc/ghi/xóa luôn ở dạng lowercase: `recipes:slug:{slug.ToLowerInvariant()}`.
  - [x] Áp dụng cơ chế generation key pattern (`recipes:generation`) cho `RemoveByPrefixAsync` tương tự `DistributedCategoryCache` để vô hiệu hóa cache tìm kiếm/danh sách.
  - [x] Đảm bảo invalidation nhất quán khi Create, Update, Delete Recipe, Add/Update/Delete Step, Upload/Delete/SetPrimary Image.
- [x] **Xóa Ảnh Vật Lý Tránh Rò Rỉ File MinIO (TECH-RISK-004)**:
  - [x] `RecipeLifecycleHandler.cs`: Eager load `Images` khi xử lý `DeleteRecipeCommand`.
  - [x] Xóa toàn bộ file ảnh (`OriginalUrl`, `MediumUrl`, `ThumbnailUrl`) trên `IFileStorageService` trước khi xóa bản ghi khỏi database.
  - [x] Đảm bảo tính idempotent và không làm đứt đoạn tiến trình xóa nếu 1 file storage đã bị xóa trước đó.
- [x] **Kiểm tra tự động Backend**:
  - [x] `CulinaryBlog.Application.Tests`: 725 passed, 0 failed.
  - [x] `CulinaryBlog.Integration.Tests`: 138 passed, 0 failed.
  - [x] Commit: `eaf17043` (`feat(backend): optimize recipe redis cache invalidation, minio cleanup on delete, and fix testcontainers FK`).

---

## 3. Frontend: Form Bước Nấu Có Cấu Trúc & Hiển Thị Dinh Dưỡng

- [x] **Cải thiện Form Bước Nấu (`recipe-editor.tsx`)**:
  - [x] Mở rộng interface `StructuredStep` chứa: `id?`, `stepNumber?`, `title`, `description`, `durationMinutes`, `imageUrl`.
  - [x] Bổ sung các input nhập:
    - [x] `Tiêu đề bước` (Title, tối đa 200 ký tự).
    - [x] `Mô tả chi tiết` (Description, bắt buộc).
    - [x] `Thời gian thực hiện (phút)` (DurationMinutes, số nguyên >= 0).
    - [x] `Ảnh minh họa (URL)` (ImageUrl, tùy chọn).
  - [x] Thêm nút di chuyển thứ tự bước: Lên (↑), Xuống (↓), Xóa (×).
  - [x] Tự động fill đầy đủ các trường của từng bước khi edit công thức.
  - [x] Gửi dữ liệu có cấu trúc khi gọi API `createRecipe` và `updateRecipe`.
- [x] **Cập nhật Giao diện Chi tiết (`recipe-detail-client.tsx`)**:
  - [x] Bổ sung Section `Giá trị dinh dưỡng (Mỗi khẩu phần)` hiển thị 6 chỉ số: Calories, Protein, Carbohydrates, Fat, Fiber, Sodium.
  - [x] Hiển thị thông tin bước nấu có cấu trúc: Số thứ tự, Tiêu đề, Thời lượng (`⏱ X phút`), Ảnh minh họa và Mô tả chi tiết.
- [x] **Kiểm tra Frontend Build**:
  - [x] `npx tsc --noEmit`: 0 errors.
  - [x] `npm run build`: Static & dynamic pages compiled successfully in 42s.
  - [x] Commit: `e7c7bdd2` (`feat(frontend): enhance recipe structured steps editor, reordering, and nutrition display`).

---

## 4. Bộ Kiểm Thử E2E Hoàn Chỉnh với Playwright

- [x] **Mở rộng Mock API & Tuyến Đường E2E (`mock-recipe-api.mjs`)**:
  - [x] Hỗ trợ in-memory CRUD cho recipes, categories pagination (`/api/v1/categories`), upload/primary/delete image.
- [x] **Kịch bản Kiểm thử E2E (`frontend/e2e/recipe-crud-flow.spec.ts`)**:
  - [x] 1. Quyền truy cập: Chưa xác thực -> Redirect về `/auth/login`.
  - [x] 2. Quyền truy cập: Đã xác thực -> Hiển thị form tạo công thức.
  - [x] 3. Luồng tạo công thức đầy đủ: Ảnh, nguyên liệu, bước có cấu trúc, dinh dưỡng (6 chỉ số).
  - [x] 4. Sắp xếp thứ tự bước nấu: Đổi thứ tự lên/xuống (Move Up/Down), xóa bước, lưu đúng thứ tự.
  - [x] 5. Luồng hiển thị chi tiết công thức: Xác minh hiển thị đúng ảnh, nguyên liệu, các bước có thời gian/ảnh và bảng dinh dưỡng.
  - [x] 6. Quản lý ảnh công thức: Upload ảnh, đặt làm ảnh chính (`⭐`), xóa ảnh (`🗑`).
  - [x] 7. Phân quyền cập nhật: Người dùng không sở hữu công thức bị từ chối 403 Forbidden.
- [x] **Kết quả kiểm thử Playwright**:
  - [x] Chạy `npx playwright test e2e/recipe-crud-flow.spec.ts`: **7/7 passed 100% (35.2s)**.
  - [x] Regression tests (`author-profile.spec.ts`): **2/2 passed 100%**.
  - [x] Commit: `68a7ee7f` (`test(e2e): add comprehensive recipe crud, structured steps, nutrition and image flow tests`).

---

## 5. Báo Cáo, Ma Trận Truy Xuất (Traceability) & Nghiệm Thu

- [x] Cập nhật bảng phân công và ma trận truy xuất `docs/srs-audit/TEAM-REQUIREMENT-TRACEABILITY.md` (FR-RCP-010, FR-RCP-007, FR-RCP-008, TECH-RISK-004 chuyển trạng thái `DONE`).
- [x] Cập nhật `README.md` phần tiến độ Tuần 8 của TV2 Dương Văn Minh.
- [x] Cập nhật file spec và checklist hoàn thành.
