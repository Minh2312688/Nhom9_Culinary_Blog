# Checklist Triển khai: FR-RCP-009, UI Recipe Editor & PostgreSQL Testcontainers

> **Trạng thái:** `[~]` Đang thực hiện (In Progress)  
> **Người phụ trách:** Dương Văn Minh (TV2 – MSSV: 2312688)  
> **Kế hoạch chi tiết:** [FR-RCP-009-recipe-editor-testcontainers-plan.md](./FR-RCP-009-recipe-editor-testcontainers-plan.md)  
> **Quy ước:** Đánh dấu `[x]` khi hoàn thành kèm bằng chứng lệnh/test; `[~]` khi hoàn thành một phần; `[ ]` chưa thực hiện.

---

## 1. Chốt hợp đồng & Backend FR-RCP-009 (Nguyên liệu qua Endpoint riêng)

- [x] **Chốt hợp đồng dữ liệu & API Contract**:
  - [x] Thống nhất quản lý nguyên liệu qua 3 endpoint chuyên biệt: `POST /api/v1/recipes/{id}/ingredients`, `PUT /api/v1/recipes/{id}/ingredients/{ingId}`, `DELETE /api/v1/recipes/{id}/ingredients/{ingId}`.
  - [x] `POST /api/v1/recipes` tiếp tục cho phép truyền kèm `Ingredients` để khởi tạo bản nháp.
  - [x] `PUT /api/v1/recipes/{id}` bảo toàn danh sách nguyên liệu hiện có khi `request.Ingredients == null`.
  - [x] Xác nhận `CONFLICT-004`: `Quantity` và `Unit` là **nullable**; nếu có `Quantity` thì bắt buộc `Quantity > 0`.
  - [x] Xác nhận `CONFLICT-005`: Dùng trường `OrderIndex` (int), không dùng `StepNumber` hay `SortOrder`.
  - [x] Xác nhận `CONFLICT-011`: Trả lỗi validation dạng HTTP 400 Problem Details.
- [x] **API Request Contracts (`backend/src/CulinaryBlog.API/Contracts/Recipes/RecipeIngredientRequests.cs`)**:
  - [x] Tạo `AddRecipeIngredientRequest(string Name, decimal? Quantity, string? Unit, string? Notes, int? OrderIndex)`.
  - [x] Tạo `ReplaceRecipeIngredientRequest(string Name, decimal? Quantity, string? Unit, string? Notes, int? OrderIndex)`.
- [x] **Application CQRS Commands (`backend/src/CulinaryBlog.Application/Features/Recipes/Commands/RecipeIngredientCommands.cs`)**:
  - [x] `AddRecipeIngredientCommand` và `AddRecipeIngredientCommandValidator`: validate Name (1-100 chars, no HTML), Quantity (> 0 when set), Unit (<= 50 chars), Notes (<= 200 chars).
  - [x] `AddRecipeIngredientCommandHandler`: kiểm tra Recipe tồn tại, kiểm tra Author-Owner hoặc Admin, tự động gán `OrderIndex = max + 1` nếu null, lưu DbContext, gọi invalidate cache `recipes:slug:{slug}` và `recipes:`.
  - [x] `UpdateRecipeIngredientCommand` và `UpdateRecipeIngredientCommandValidator`: validate tương tự, yêu cầu `IngredientId != Guid.Empty`.
  - [x] `UpdateRecipeIngredientCommandHandler`: kiểm tra quyền sở hữu, cập nhật bản ghi `!IsDeleted`, lưu DbContext, invalidate cache, trả về `RecipeIngredientDto`.
  - [x] `DeleteRecipeIngredientCommand` và `DeleteRecipeIngredientCommandHandler`: soft-delete `IsDeleted = true`, chuẩn hóa `OrderIndex` của các active ingredients còn lại, invalidate cache.
- [x] **Minimal API Endpoints (`RecipeEndpoints.cs` hoặc `RecipeIngredientEndpoints.cs`)**:
  - [x] Map `POST /api/v1/recipes/{id:guid}/ingredients` -> HTTP 201 Created kèm header Location và `RecipeIngredientDto`.
  - [x] Map `PUT /api/v1/recipes/{id:guid}/ingredients/{ingId:guid}` -> HTTP 200 OK kèm `RecipeIngredientDto`.
  - [x] Map `DELETE /api/v1/recipes/{id:guid}/ingredients/{ingId:guid}` -> HTTP 204 No Content.
  - [x] Đăng ký `RequireAuthorization()` cho cả 3 route.
  - [x] Ánh xạ lỗi đầy đủ: 400 (Validation), 401 (Unauthorized), 403 (Forbidden), 404 (Not Found).
- [x] **Integration Tests cho CQRS FR-RCP-009**:
  - [x] Test validator từ chối Name rỗng, Name quá dài, Quantity <= 0, Unit/Notes chứa thẻ HTML.
  - [x] Test handler thêm nguyên liệu tự động tăng `OrderIndex`.
  - [x] Test handler từ chối người dùng không phải chủ sở hữu hoặc không phải Admin (HTTP 403).
  - [x] Test soft-delete và invalidation cache.
  - [x] Commit: `feat(backend): implement recipe ingredient sub-resource endpoints for FR-RCP-009`

---

## 2. UI Recipe Editor: Upload, Xem trước & Chọn ảnh chính (FR-RCP-008 Integration)

- [x] **Client API Integration (`frontend/src/lib/recipes-api.ts`)**:
  - [x] Bổ sung hàm `uploadRecipeImage(token, recipeId, file, isPrimary?, altText?)` gọi `POST /api/v1/recipes/{id}/images` (multipart/form-data).
  - [x] Bổ sung hàm `setPrimaryRecipeImage(token, recipeId, imageId)` gọi `PATCH /api/v1/recipes/{id}/images/{imageId}/primary`.
  - [x] Bổ sung hàm `deleteRecipeImage(token, recipeId, imageId)` gọi `DELETE /api/v1/recipes/{id}/images/{imageId}`.
- [x] **Giao diện Quản lý Ảnh trong `recipe-editor.tsx`**:
  - [x] Thêm khu vực tải ảnh (File Input) chấp nhận `.jpg`, `.jpeg`, `.png`, `.webp`, `.avif`.
  - [x] Kiểm tra dung lượng file phía client: cảnh báo nếu file > 5MB hoặc số lượng ảnh đạt giới hạn 10 ảnh.
  - [x] Input nhập `Alt text` trước khi upload.
  - [x] Checkbox "Đặt làm ảnh chính khi tải lên".
  - [x] Hiển thị spinner/loading khi đang tải ảnh lên MinIO.
  - [x] Grid hiển thị danh sách ảnh đã upload (Thumbnail Preview).
  - [x] Hiển thị Badge "Ảnh chính" nổi bật trên ảnh đang có `isPrimary === true`.
  - [x] Nút "Đặt làm ảnh chính": gọi `setPrimaryRecipeImage`, cập nhật trạng thái UI ngay lập tức.
  - [x] Nút "Xóa ảnh": kèm dialog xác nhận, gọi `deleteRecipeImage` và xóa ảnh khỏi danh sách hiển thị.
  - [x] Xử lý luồng tạo mới (`Create Recipe`): hướng dẫn người dùng lưu bản nháp ban đầu để lấy `recipeId` trước khi tải ảnh.

---

## 3. UI Recipe Editor: Nhập và Sửa 6 trường Dinh dưỡng (CONFLICT-008)

- [x] **Client Interface (`frontend/src/lib/recipes-api.ts`)**:
  - [x] Định nghĩa `RecipeNutritionInput` gồm 6 trường: `calories`, `protein`, `carbohydrates`, `fat`, `fiber`, `sodium`.
  - [x] Cập nhật `RecipeInput` để chứa trường `nutrition?: RecipeNutritionInput | null`.
- [x] **Giao diện Nhập liệu Dinh dưỡng trong `recipe-editor.tsx`**:
  - [x] Bổ sung Section: *"Giá trị dinh dưỡng (Tính trên mỗi khẩu phần - Per Serving)"*.
  - [x] Checkbox bật/tắt: "Bao gồm thông tin dinh dưỡng trong công thức".
  - [x] Bố cục lưới responsive (Grid `auto-fill, minmax(140px, 1fr)`) cho 6 ô input:
    - [x] `Calories` (Năng lượng): số nguyên >= 0, đơn vị `kcal`.
    - [x] `Protein` (Chất đạm): số thực >= 0, đơn vị `g`, step 0.1.
    - [x] `Carbohydrates` (Đường bột): số thực >= 0, đơn vị `g`, step 0.1.
    - [x] `Fat` (Chất béo): số thực >= 0, đơn vị `g`, step 0.1.
    - [x] `Fiber` (Chất xơ): số thực >= 0, đơn vị `g`, step 0.1.
    - [x] `Sodium` (Natri): số thực >= 0, đơn vị `mg`, step 1.
  - [x] Xử lý load dữ liệu: nếu công thức có sẵn `nutrition`, tự động điền giá trị vào form và bật checkbox.
  - [x] Xử lý submit: parse chuỗi sang số hợp lệ; nếu checkbox tắt thì gửi `nutrition: null`.

---

## 4. UI Recipe Editor: Tích hợp Danh sách Nguyên liệu Động

- [x] **Giao diện Nhập liệu Nguyên liệu**:
  - [x] Thay thế ô `textarea` phân tách ký tự `|` bằng danh sách hàng động (Dynamic Row List) kèm textarea để tương thích.
  - [x] Mỗi dòng nguyên liệu gồm:
    - [x] `Tên nguyên liệu` (bắt buộc).
    - [x] `Số lượng` (số thực nullable > 0).
    - [x] `Đơn vị` (nullable, ví dụ: g, ml, muỗng canh, quả).
    - [x] `Ghi chú` (nullable, ví dụ: thái hạt lựu, nêm vừa đủ).
  - [x] Nút "Thêm nguyên liệu mới".
  - [x] Nút "Xóa dòng nguyên liệu".
  - [x] Đồng bộ dữ liệu khi submit tạo mới hoặc gọi endpoint riêng khi chỉnh sửa.
- [x] Xử lý xung đột đồng thời (Optimistic Concurrency, TECH-RISK-003): Hiển thị banner 409 Conflict với nút Tải lại.

---

## 5. Hạ tầng & Kiểm thử PostgreSQL Testcontainers cho Migrations

- [x] **Thiết lập Package & Fixture**:
  - [x] Thêm `Testcontainers.PostgreSql` vào `backend/Directory.Packages.props`.
  - [x] Cấu hình sử dụng trong `CulinaryBlog.Integration.Tests` project.
  - [x] Tạo `DockerPostgresEnvironment` (helper + `DockerPostgresFactAttribute`) khởi động image `postgres:16-alpine`.
- [x] **Migration Test Cases (`PostgresMigrationTests.cs`)**:
  - [x] `Database_MigratesSuccessfully_AndEnforcesSchemaOnPostgres16`: áp dụng toàn bộ migrations từ rỗng, xác nhận connect thành công, tạo Category/Recipe, kiểm tra cột `RowVersion` và `SearchVector`.
  - [x] Kiểm tra extension `unaccent` và cột mapping hoạt động trên PostgreSQL 16 thật (`TECH-RISK-002`).

---

## 6. Kiểm thử Cập nhật Bước đồng thời với Testcontainers (TECH-RISK-012 & TECH-RISK-003)

- [x] **Concurrency Test Cases (`RecipeStepConcurrencyTests.cs`)**:
  - [x] **Concurrent Step Update vs RowVersion**: Hai DbContext độc lập cùng tải 1 step, Context A cập nhật thành công, Context B ném `DbUpdateConcurrencyException` → xác minh PostgreSQL concurrency token hoạt động đúng.
  - [x] Commit: `test(persistence): add Testcontainers PostgreSQL migration and concurrency test suite`

---

## 7. Kiểm tra Chất lượng Tổng thể & Báo cáo Nghiệm thu

- [x] **Backend Verification**:
  - [x] `dotnet build backend/CulinaryBlog.sln` đạt 0 Error, 0 Warning.
  - [x] Toàn bộ unit/integration tests cũ và mới đều pass (không tính Docker tests khi không có Docker daemon).
- [x] **Frontend Verification**:
  - [x] `npm run build` xuất build production thành công (warnings ESLint không phải lỗi).
- [ ] **Tài liệu & Traceability**:
  - [ ] Cập nhật trạng thái FR-RCP-009 từ `BLOCKED` / `CONFLICT` sang `DONE` trong `TEAM-REQUIREMENT-TRACEABILITY.md`.
  - [ ] Cập nhật kết quả xác minh vào `docs/recipe-core-implementation-spec.md` và `README.md` (Mục 10.2).
