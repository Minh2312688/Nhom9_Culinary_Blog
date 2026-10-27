# Kế hoạch Triển khai Chi tiết: FR-RCP-009, UI Recipe Editor & PostgreSQL Testcontainers

> **Trạng thái:** Sẵn sàng triển khai (Ready for Implementation) – **Chỉ lập kế hoạch, không thực thi code**.  
> **Thành viên phụ trách:** Dương Văn Minh (TV2 – Recipe Backend, Data, Search & Cache)  
> **Mục tiêu Tuần 7:**  
> 1. Chốt hợp đồng FR-RCP-009 (nguyên liệu quản lý qua endpoint riêng biệt).  
> 2. Xây dựng UI Editor công thức căn bản: upload/xem trước/chọn ảnh chính, nhập và sửa 6 trường dinh dưỡng.  
> 3. Bổ sung kiểm thử PostgreSQL/Testcontainers cho migration và cập nhật bước đồng thời (`TECH-RISK-002`, `TECH-RISK-003`, `TECH-RISK-012`).  
> **Tài liệu tham chiếu:**  
> - Kế hoạch đặc tả chi tiết: `docs/features/FR-RCP-009-recipe-editor-testcontainers-plan.md`  
> - Checklist nghiệm thu: `docs/features/FR-RCP-009-recipe-editor-testcontainers-checklist.md`  

---

## 1. Mục tiêu & Quyết định Hợp đồng

### 1.1. Chốt hợp đồng FR-RCP-009: Quản lý nguyên liệu qua endpoint riêng biệt
- **Phương án lựa chọn:** Quản lý nguyên liệu độc lập qua 3 RESTful sub-resource endpoints:
  - `POST /api/v1/recipes/{id:guid}/ingredients` (Thêm nguyên liệu mới -> trả `201 Created` + `RecipeIngredientDto`)
  - `PUT /api/v1/recipes/{id:guid}/ingredients/{ingId:guid}` (Sửa nguyên liệu -> trả `200 OK` + `RecipeIngredientDto`)
  - `DELETE /api/v1/recipes/{id:guid}/ingredients/{ingId:guid}` (Xóa mềm nguyên liệu -> trả `204 No Content`)
- **Tương thích ngược:**
  - `POST /api/v1/recipes`: Vẫn hỗ trợ truyền mảng `Ingredients` ban đầu để khởi tạo nhanh bản nháp.
  - `PUT /api/v1/recipes/{id}`: Giữ nguyên danh sách nguyên liệu hiện có nếu `Ingredients == null`, không xóa mất dữ liệu đã quản lý qua endpoint riêng.
- **Ràng buộc dữ liệu & Quyền hạn:**
  - `CONFLICT-004`: `Quantity` và `Unit` là **nullable**; nếu `Quantity` có giá trị thì `Quantity > 0`. `Unit` tối đa 50 ký tự.
  - `CONFLICT-005`: Dùng trường `OrderIndex` (int), tự động tăng `max + 1` nếu không truyền.
  - `CONFLICT-011`: Validation trả HTTP 400 Problem Details.
  - Phân quyền: Yêu cầu Bearer Token của Author-Owner hoặc Admin (người khác nhận HTTP 403 Forbidden).
  - Cache: Invalidate Redis detail cache `recipes:slug:{slug}` và list cache `recipes:`.

### 1.2. UI Recipe Editor Căn bản
- **Tích hợp Upload, Xem trước và Chọn ảnh chính (FR-RCP-008)**:
  - Kết nối các API: `POST /api/v1/recipes/{id}/images` (upload multipart), `PATCH /api/v1/recipes/{id}/images/{imageId}/primary`, `DELETE /api/v1/recipes/{id}/images/{imageId}`.
  - Dropzone chọn file, kiểm tra client-side: định dạng JPEG/PNG/WebP/AVIF, kích thước <= 5MB, giới hạn tối đa 10 ảnh.
  - Thư viện xem trước (Thumbnail Gallery) với huy hiệu `Ảnh chính` trực quan.
  - Tác vụ: Đặt làm ảnh chính tức thì, Xóa ảnh kèm xác nhận.
- **Nhập và Sửa 6 trường Dinh dưỡng (CONFLICT-008)**:
  - Hiện thực hóa 6 chỉ số dinh dưỡng **PER SERVING**:
    1. `Calories` (kcal, int >= 0)
    2. `Protein` (g, decimal >= 0)
    3. `Carbohydrates` (g, decimal >= 0)
    4. `Fat` (g, decimal >= 0)
    5. `Fiber` (g, decimal >= 0)
    6. `Sodium` (mg, decimal >= 0)
  - Bố cục responsive dạng Grid, tự động nạp dữ liệu khi edit và bind vào `payload.nutrition` khi lưu công thức.
- **Danh sách Nguyên liệu Động (Dynamic Ingredients UI)**:
  - Thay thế `textarea` phân tách `|` bằng bảng/hàng nhập liệu động (Tên, Số lượng, Đơn vị, Ghi chú).

### 1.3. Kiểm thử PostgreSQL / Testcontainers cho Migration và Concurrency
- **Hạ tầng Testcontainers**:
  - Tích hợp package `Testcontainers.PostgreSql` vào `Directory.Packages.props` và `CulinaryBlog.DataMigration.Tests`.
  - Khởi tạo container PostgreSQL 16 cô lập, sạch sẽ cho mỗi chu kỳ kiểm thử.
- **Kiểm thử Migration**:
  - Áp dụng toàn bộ migrations từ rỗng: `context.Database.MigrateAsync()`.
  - Kiểm tra tính tương thích của PostgreSQL extension `unaccent`, trigger FTS, partial unique index trên `RecipeSteps` (`(RecipeId, StepNumber)` WHERE `!IsDeleted`), unique index trên `Recipes(slug)`.
  - Kiểm thử khả năng rollback an toàn.
- **Kiểm thử Cập nhật Bước đồng thời (`TECH-RISK-012` & `TECH-RISK-003`)**:
  - Kịch bản 1: Hai request `AddRecipeStepCommand` bắn đồng thời qua `Task.WhenAll` trên 2 DbContext riêng biệt. Xác minh PostgreSQL constraint chặn trùng `StepNumber`, dữ liệu toàn vẹn, trả 409 hoặc retry an toàn.
  - Kịch bản 2: Một request `DeleteRecipeStepCommand` chạy song song với `AddRecipeStepCommand`. Xác minh chuỗi `StepNumber` của các active step luôn liên tục $1, 2, \dots, N$.
  - Kịch bản 3: Sửa step đồng thời với cập nhật `RowVersion` trên Recipe cha.

---

## 2. Danh sách các file dự kiến thao tác khi triển khai

### Backend:
- `backend/Directory.Packages.props` (Thêm `Testcontainers.PostgreSql`)
- `backend/src/CulinaryBlog.API/Contracts/Recipes/RecipeIngredientRequests.cs` (Mới)
- `backend/src/CulinaryBlog.Application/Features/Recipes/Commands/RecipeIngredientCommands.cs` (Mới)
- `backend/src/CulinaryBlog.API/Endpoints/RecipeIngredientEndpoints.cs` (hoặc mở rộng `RecipeEndpoints.cs`)
- `backend/tests/CulinaryBlog.DataMigration.Tests/CulinaryBlog.DataMigration.Tests.csproj` (Cấu hình project)
- `backend/tests/CulinaryBlog.DataMigration.Tests/PostgresMigrationTests.cs` (Mới)
- `backend/tests/CulinaryBlog.DataMigration.Tests/RecipeStepConcurrencyTests.cs` (Mới)

### Frontend:
- `frontend/src/lib/recipes-api.ts` (Mở rộng API client: nutrition, upload/primary/delete image, ingredient endpoints)
- `frontend/src/app/dashboard/recipe-editor.tsx` (Nâng cấp UI Editor: Image Gallery, 6 Nutrition fields, Dynamic Ingredients)

---

## 3. Tiêu chuẩn Nghiệm thu (Acceptance Criteria)

1. Backend cung cấp đầy đủ 3 endpoint nguyên liệu, tuân thủ `CONFLICT-004`, `CONFLICT-005`, `CONFLICT-011`, bảo toàn danh sách khi update recipe cha.
2. UI Editor cho phép upload ảnh lên MinIO, hiển thị danh sách xem trước, chọn ảnh đại diện chính, xóa ảnh an toàn.
3. UI Editor cung cấp 6 trường dinh dưỡng theo khẩu phần, hỗ trợ nhập số và lưu trữ chính xác.
4. Test suite Testcontainers chạy trên PostgreSQL 16 thật, chứng minh toàn bộ migration áp dụng sạch sẽ và cơ chế concurrency của RecipeSteps không bao giờ làm hỏng dữ liệu hay trùng số bước.
5. `dotnet build` và `npm run build` thành công, không phát sinh lỗi cảnh báo mới.
