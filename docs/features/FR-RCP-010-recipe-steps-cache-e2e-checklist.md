# Checklist Triển Khai: Bước Nấu Có Cấu Trúc, Cache Redis, Xóa Ảnh & E2E Testing

> **Trạng thái:** `[~]` Đang thực hiện (In Progress)  
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

---

## 2. Backend & Persistence: Fix Testcontainers, Cache Redis & Luồng Xóa Ảnh MinIO

- [ ] **Sửa lỗi Foreign Key Testcontainers**:
  - [ ] `PostgresMigrationTests.cs`: Tạo `ApplicationUser` trước khi tạo `Recipe` để thỏa mãn ràng buộc `FK_Recipes_AspNetUsers_AuthorId`.
  - [ ] `RecipeStepConcurrencyTests.cs`: Tạo `ApplicationUser` trước khi tạo `Recipe`.
  - [ ] Kiểm tra chạy testcontainer PostgreSQL pass 100%.
- [ ] **Tối ưu hóa Cache Redis (`DistributedRecipeCache.cs`)**:
  - [ ] Chuẩn hóa key đọc/ghi/xóa luôn ở dạng lowercase: `recipes:slug:{slug.ToLowerInvariant()}`.
  - [ ] Áp dụng cơ chế versioning/generation key (`recipes:generation`) cho `RemoveByPrefixAsync` tương tự `DistributedCategoryCache` để vô hiệu hóa cache tìm kiếm/danh sách.
  - [ ] Kiểm tra invalidation khi Create, Update, Delete Recipe, Add/Update/Delete Step, Upload/Delete/SetPrimary Image.
- [ ] **Xóa Ảnh Vật Lý Tránh Rò Rỉ File MinIO (TECH-RISK-004)**:
  - [ ] `RecipeLifecycleHandler.cs`: Eager load `Images` khi xử lý `DeleteRecipeCommand`.
  - [ ] Xóa toàn bộ file ảnh (`OriginalUrl`, `MediumUrl`, `ThumbnailUrl`) trên `IFileStorageService` trước khi xóa bản ghi khỏi database.
  - [ ] Đảm bảo tính idempotent và không làm đứt đoạn tiến trình xóa nếu 1 file storage đã bị xóa trước đó.
- [ ] **Kiểm tra tự động Backend**:
  - [ ] Chạy `dotnet test backend/CulinaryBlog.sln` đạt 100% pass.

---

## 3. Frontend: Form Bước Nấu Có Cấu Trúc & Hiển Thị Dinh Dưỡng

- [ ] **Cải thiện Form Bước Nấu (`recipe-editor.tsx`)**:
  - [ ] Mở rộng interface `StructuredStep` chứa: `id?`, `stepNumber?`, `title`, `description`, `durationMinutes`, `imageUrl`.
  - [ ] Bổ sung các input nhập:
    - [ ] `Tiêu đề bước` (Title, tối đa 200 ký tự).
    - [ ] `Mô tả chi tiết` (Description, bắt buộc).
    - [ ] `Thời gian thực hiện (phút)` (DurationMinutes, số nguyên >= 0).
    - [ ] `Ảnh minh họa (URL)` (ImageUrl, tùy chọn).
  - [ ] Thêm nút di chuyển thứ tự bước: Lên (▲), Xuống (▼), Xóa (×).
  - [ ] Tự động fill đầy đủ các trường của từng bước khi edit công thức.
  - [ ] Gửi dữ liệu có cấu trúc khi gọi API `createRecipe` và `updateRecipe`.
- [ ] **Cập nhật Giao diện Chi tiết (`recipe-detail-client.tsx`)**:
  - [ ] Bổ sung Section `Giá trị dinh dưỡng (Mỗi khẩu phần)` hiển thị 6 chỉ số: Calories, Protein, Carbohydrates, Fat, Fiber, Sodium.
  - [ ] Hiển thị thông tin bước nấu có cấu trúc: Số thứ tự, Tiêu đề, Thời lượng (`⏱ X phút`), Ảnh minh họa và Mô tả chi tiết.
- [ ] **Kiểm tra Frontend Build**:
  - [ ] `npm run build` thành công.

---

## 4. Bộ Kiểm Thử E2E Hoàn Chỉnh với Playwright

- [ ] **Mở rộng Mock API & Tuyến Đường E2E**:
  - [ ] Hỗ trợ mock hoặc intercept cho các API: `POST /api/v1/recipes`, `PUT /api/v1/recipes/{id}`, `GET /api/v1/recipes/{slug}`, upload/set primary/delete ảnh, danh mục.
- [ ] **Kịch bản Kiểm thử E2E (`frontend/e2e/recipe-crud.spec.ts`)**:
  - [ ] Luồng tạo công thức đầy đủ: Ảnh, nguyên liệu, bước có cấu trúc, dinh dưỡng.
  - [ ] Luồng hiển thị chi tiết công thức: Xác minh hiển thị đúng ảnh, nguyên liệu, các bước và bảng dinh dưỡng.
  - [ ] Luồng chỉnh sửa công thức: Sửa nguyên liệu, sửa bước, đổi thứ tự bước (lên/xuống), xóa bước, sửa dinh dưỡng.
  - [ ] Luồng quản lý ảnh: Upload, xem thumbnail preview, đặt ảnh chính, xóa ảnh.
  - [ ] Luồng phân quyền: Chặn guest/người dùng không sở hữu truy cập trái phép.
  - [ ] Luồng dọn dẹp và xóa công thức: Xóa công thức thành công.
- [ ] **Thực thi Playwright E2E**:
  - [ ] Chạy `npx playwright test e2e/recipe-crud.spec.ts` pass 100%.

---

## 5. Báo Cáo Nghiệm Thu & Cập Nhật Tài Liệu

- [ ] Cập nhật `README.md` (Mục 9 và Mục 10).
- [ ] Cập nhật `docs/srs-audit/TEAM-REQUIREMENT-TRACEABILITY.md`.
- [ ] Cập nhật `docs/recipe-core-implementation-spec.md`.
- [ ] Đóng dấu hoàn tất checklist và commit git đầy đủ.
