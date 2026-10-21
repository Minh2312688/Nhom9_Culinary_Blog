# Checklist Triển khai: FR-RCP-009, UI Recipe Editor & PostgreSQL Testcontainers

> **Trạng thái:** `[ ]` Chưa thực hiện (Ready for Execution)  
> **Người phụ trách:** Dương Văn Minh (TV2 – MSSV: 2312688)  
> **Kế hoạch chi tiết:** [FR-RCP-009-recipe-editor-testcontainers-plan.md](./FR-RCP-009-recipe-editor-testcontainers-plan.md)  
> **Quy ước:** Đánh dấu `[x]` khi hoàn thành kèm bằng chứng lệnh/test; `[~]` khi hoàn thành một phần; `[ ]` chưa thực hiện.

---

## 1. Chốt hợp đồng & Backend FR-RCP-009 (Nguyên liệu qua Endpoint riêng)

- [ ] **Chốt hợp đồng dữ liệu & API Contract**:
  - [ ] Thống nhất quản lý nguyên liệu qua 3 endpoint chuyên biệt: `POST /api/v1/recipes/{id}/ingredients`, `PUT /api/v1/recipes/{id}/ingredients/{ingId}`, `DELETE /api/v1/recipes/{id}/ingredients/{ingId}`.
  - [ ] `POST /api/v1/recipes` tiếp tục cho phép truyền kèm `Ingredients` để khởi tạo bản nháp.
  - [ ] `PUT /api/v1/recipes/{id}` bảo toàn danh sách nguyên liệu hiện có khi `request.Ingredients == null`.
  - [ ] Xác nhận `CONFLICT-004`: `Quantity` và `Unit` là **nullable**; nếu có `Quantity` thì bắt buộc `Quantity > 0`.
  - [ ] Xác nhận `CONFLICT-005`: Dùng trường `OrderIndex` (int), không dùng `StepNumber` hay `SortOrder`.
  - [ ] Xác nhận `CONFLICT-011`: Trả lỗi validation dạng HTTP 400 Problem Details.
- [ ] **API Request Contracts (`backend/src/CulinaryBlog.API/Contracts/Recipes/RecipeIngredientRequests.cs`)**:
  - [ ] Tạo `AddRecipeIngredientRequest(string Name, decimal? Quantity, string? Unit, string? Notes, int? OrderIndex)`.
  - [ ] Tạo `ReplaceRecipeIngredientRequest(string Name, decimal? Quantity, string? Unit, string? Notes, int? OrderIndex)`.
- [ ] **Application CQRS Commands (`backend/src/CulinaryBlog.Application/Features/Recipes/Commands/RecipeIngredientCommands.cs`)**:
  - [ ] `AddRecipeIngredientCommand` và `AddRecipeIngredientCommandValidator`: validate Name (1-100 chars, no HTML), Quantity (> 0 when set), Unit (<= 50 chars), Notes (<= 200 chars).
  - [ ] `AddRecipeIngredientCommandHandler`: kiểm tra Recipe tồn tại, kiểm tra Author-Owner hoặc Admin, tự động gán `OrderIndex = max + 1` nếu null, lưu DbContext, gọi invalidate cache `recipes:slug:{slug}` và `recipes:`.
  - [ ] `UpdateRecipeIngredientCommand` và `UpdateRecipeIngredientCommandValidator`: validate tương tự, yêu cầu `IngredientId != Guid.Empty`.
  - [ ] `UpdateRecipeIngredientCommandHandler`: kiểm tra quyền sở hữu, cập nhật bản ghi `!IsDeleted`, lưu DbContext, invalidate cache, trả về `RecipeIngredientDto`.
  - [ ] `DeleteRecipeIngredientCommand` và `DeleteRecipeIngredientCommandHandler`: soft-delete `IsDeleted = true`, chuẩn hóa `OrderIndex` của các active ingredients còn lại, invalidate cache.
- [ ] **Minimal API Endpoints (`RecipeEndpoints.cs` hoặc `RecipeIngredientEndpoints.cs`)**:
  - [ ] Map `POST /api/v1/recipes/{id:guid}/ingredients` -> HTTP 201 Created kèm header Location và `RecipeIngredientDto`.
  - [ ] Map `PUT /api/v1/recipes/{id:guid}/ingredients/{ingId:guid}` -> HTTP 200 OK kèm `RecipeIngredientDto`.
  - [ ] Map `DELETE /api/v1/recipes/{id:guid}/ingredients/{ingId:guid}` -> HTTP 204 No Content.
  - [ ] Đăng ký `RequireAuthorization()` cho cả 3 route.
  - [ ] Ánh xạ lỗi đầy đủ: 400 (Validation), 401 (Unauthorized), 403 (Forbidden), 404 (Not Found).
- [ ] **Unit Tests cho CQRS FR-RCP-009**:
  - [ ] Test validator từ chối Name rỗng, Name quá dài, Quantity <= 0, Unit/Notes chứa thẻ HTML.
  - [ ] Test handler thêm nguyên liệu tự động tăng `OrderIndex`.
  - [ ] Test handler từ chối người dùng không phải chủ sở hữu hoặc không phải Admin (HTTP 403).
  - [ ] Test soft-delete và invalidation cache.

---

## 2. UI Recipe Editor: Upload, Xem trước & Chọn ảnh chính (FR-RCP-008 Integration)

- [ ] **Client API Integration (`frontend/src/lib/recipes-api.ts`)**:
  - [ ] Bổ sung hàm `uploadRecipeImage(token, recipeId, file, isPrimary?, altText?)` gọi `POST /api/v1/recipes/{id}/images` (multipart/form-data).
  - [ ] Bổ sung hàm `setPrimaryRecipeImage(token, recipeId, imageId)` gọi `PATCH /api/v1/recipes/{id}/images/{imageId}/primary`.
  - [ ] Bổ sung hàm `deleteRecipeImage(token, recipeId, imageId)` gọi `DELETE /api/v1/recipes/{id}/images/{imageId}`.
- [ ] **Giao diện Quản lý Ảnh trong `recipe-editor.tsx`**:
  - [ ] Thêm khu vực tải ảnh (Dropzone / File Input) chấp nhận `.jpg`, `.jpeg`, `.png`, `.webp`, `.avif`.
  - [ ] Kiểm tra dung lượng file phía client: cảnh báo nếu file > 5MB hoặc số lượng ảnh đạt giới hạn 10 ảnh.
  - [ ] Input nhập `Alt text` trước hoặc sau khi upload.
  - [ ] Checkbox "Đặt làm ảnh chính khi tải lên".
  - [ ] Hiển thị spinner/loading khi đang tải ảnh lên MinIO.
  - [ ] Grid hiển thị danh sách ảnh đã upload (Thumbnail Preview).
  - [ ] Hiển thị Badge "Ảnh chính" nổi bật trên ảnh đang có `isPrimary === true`.
  - [ ] Nút "Đặt làm ảnh chính": gọi `setPrimaryRecipeImage`, cập nhật trạng thái UI ngay lập tức.
  - [ ] Nút "Xóa ảnh": kèm dialog/hộp thoại xác nhận, gọi `deleteRecipeImage` và xóa ảnh khỏi danh sách hiển thị.
  - [ ] Xử lý luồng tạo mới (`Create Recipe`): hướng dẫn người dùng lưu bản nháp ban đầu để lấy `recipeId` trước khi tải ảnh.

---

## 3. UI Recipe Editor: Nhập và Sửa 6 trường Dinh dưỡng (CONFLICT-008)

- [ ] **Client Interface (`frontend/src/lib/recipes-api.ts`)**:
  - [ ] Định nghĩa `RecipeNutritionInput` gồm 6 trường: `calories`, `protein`, `carbohydrates`, `fat`, `fiber`, `sodium`.
  - [ ] Cập nhật `RecipeInput` để chứa trường `nutrition?: RecipeNutritionInput | null`.
- [ ] **Giao diện Nhập liệu Dinh dưỡng trong `recipe-editor.tsx`**:
  - [ ] Bổ sung Section: *"Giá trị dinh dưỡng (Tính trên mỗi khẩu phần - Per Serving)"*.
  - [ ] Checkbox bật/tắt: "Bao gồm thông tin dinh dưỡng trong công thức".
  - [ ] Bố cục lưới responsive (Grid 2 cột mobile, 3 hoặc 6 cột desktop) cho 6 ô input:
    - [ ] `Calories` (Năng lượng): số nguyên >= 0, đơn vị `kcal`.
    - [ ] `Protein` (Chất đạm): số thực >= 0, đơn vị `g`, step 0.1.
    - [ ] `Carbohydrates` (Đường bột): số thực >= 0, đơn vị `g`, step 0.1.
    - [ ] `Fat` (Chất béo): số thực >= 0, đơn vị `g`, step 0.1.
    - [ ] `Fiber` (Chất xơ): số thực >= 0, đơn vị `g`, step 0.1.
    - [ ] `Sodium` (Natri): số thực >= 0, đơn vị `mg`, step 1 hoặc 0.1.
  - [ ] Xử lý load dữ liệu: nếu công thức có sẵn `nutrition`, tự động điền giá trị vào form và bật checkbox.
  - [ ] Xử lý submit: parse chuỗi sang số hợp lệ; nếu checkbox tắt thì gửi `nutrition: null`.

---

## 4. UI Recipe Editor: Tích hợp Danh sách Nguyên liệu Động

- [ ] **Giao diện Nhập liệu Nguyên liệu**:
  - [ ] Thay thế ô `textarea` phân tách ký tự `|` bằng danh sách hàng động (Dynamic Row List).
  - [ ] Mỗi dòng nguyên liệu gồm:
    - [ ] `Tên nguyên liệu` (bắt buộc).
    - [ ] `Số lượng` (số thực nullable > 0).
    - [ ] `Đơn vị` (nullable, ví dụ: g, ml, muỗng canh, quả).
    - [ ] `Ghi chú` (nullable, ví dụ: thái hạt lựu, nêm vừa đủ).
  - [ ] Nút "Thêm nguyên liệu mới".
  - [ ] Nút "Xóa dòng nguyên liệu".
  - [ ] Đồng bộ dữ liệu khi submit tạo mới hoặc gọi endpoint riêng khi chỉnh sửa.

---

## 5. Hạ tầng & Kiểm thử PostgreSQL Testcontainers cho Migrations

- [ ] **Thiết lập Package & Fixture**:
  - [ ] Thêm `Testcontainers.PostgreSql` vào `backend/Directory.Packages.props`.
  - [ ] Cấu hình project `backend/tests/CulinaryBlog.DataMigration.Tests/CulinaryBlog.DataMigration.Tests.csproj` (hoặc trong Integration Tests).
  - [ ] Tạo `PostgresContainerFixture` khởi động image `postgres:16-alpine`.
- [ ] **Migration Test Cases (`PostgresMigrationTests.cs`)**:
  - [ ] `ApplyAllMigrations_OnCleanDatabase_Succeeds`: áp dụng toàn bộ migrations từ rỗng, xác nhận 0 pending migrations.
  - [ ] `Verify_Tables_Exist_In_Database`: xác nhận các bảng `Recipes`, `RecipeSteps`, `RecipeIngredients`, `RecipeImages`, `RecipeNutritions`, `Categories`, `AspNetUsers` được tạo đầy đủ.
  - [ ] `Verify_Postgres_Unaccent_Extension`: kiểm tra extension `unaccent` hoạt động trong PostgreSQL thật (`TECH-RISK-002`).
  - [ ] `Verify_RecipeStep_Active_Unique_Index`: kiểm tra constraint `(RecipeId, StepNumber)` WHERE `is_deleted = false`.
  - [ ] `Verify_Recipe_Slug_Unique_Index`: kiểm tra index unique trên `Recipes(slug)`.
  - [ ] `Verify_Migration_Rollback_Capability`: kiểm thử rollback (down) và áp dụng lại (up) an toàn.

---

## 6. Kiểm thử Cập nhật Bước đồng thời với Testcontainers (TECH-RISK-012 & TECH-RISK-003)

- [ ] **Concurrency Test Cases (`RecipeStepConcurrencyTests.cs`)**:
  - [ ] **Concurrent Add Step Race**:
    - [ ] Giả lập 2 task `AddRecipeStepCommand` bắn đồng thời qua `Task.WhenAll` trên 2 DbContext riêng biệt cùng một Recipe.
    - [ ] Xác minh: PostgreSQL unique constraint chặn không cho phép trùng `StepNumber`.
    - [ ] Xác minh: Không có bản ghi step nào bị mồ côi hoặc hỏng dữ liệu; hệ thống trả về mã lỗi 409 Conflict hoặc cơ chế retry tự gán `StepNumber = max + 1` thành công.
  - [ ] **Concurrent Delete & Add Step**:
    - [ ] Giả lập 1 task `DeleteRecipeStepCommand` (đang renumber lại các bước) chạy song song với 1 task `AddRecipeStepCommand`.
    - [ ] Xác minh: Sau khi cả 2 hoàn tất, chuỗi `StepNumber` của các active steps luôn tạo thành dãy liên tục $1, 2, \dots, N$ không đứt gãy.
  - [ ] **Concurrent Step Update vs Recipe RowVersion (`TECH-RISK-003`)**:
    - [ ] Kiểm tra hành vi của `RowVersion` trên Recipe khi các child step bị thay đổi đồng thời với thông tin Recipe cha.

---

## 7. Kiểm tra Chất lượng Tổng thể & Báo cáo Nghiệm thu

- [ ] **Backend Verification**:
  - [ ] `dotnet build backend/CulinaryBlog.sln` đạt 0 Error, 0 Warning.
  - [ ] Toàn bộ unit/integration tests cũ và mới đều pass:
    `dotnet test backend/CulinaryBlog.sln --no-restore`
  - [ ] Testcontainers tests chạy thành công trên máy có Docker daemon.
- [ ] **Frontend Verification**:
  - [ ] `npm run lint` trong thư mục `frontend` không có lỗi.
  - [ ] `npx tsc --noEmit` không có lỗi kiểu TypeScript.
  - [ ] `npm run build` xuất build production thành công.
- [ ] **Tài liệu & Traceability**:
  - [ ] Cập nhật trạng thái FR-RCP-009 từ `BLOCKED` / `CONFLICT` sang `DONE` trong `TEAM-REQUIREMENT-TRACEABILITY.md` sau khi code hoàn tất.
  - [ ] Cập nhật kết quả xác minh vào `docs/recipe-core-implementation-spec.md` và `README.md` (Mục 10.2).
