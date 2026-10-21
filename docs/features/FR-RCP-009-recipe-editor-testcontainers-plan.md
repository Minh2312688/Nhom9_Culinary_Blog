# Kế hoạch Triển khai Chi tiết: FR-RCP-009, UI Recipe Editor & PostgreSQL Testcontainers

> **Tài liệu:** Kế hoạch kỹ thuật chi tiết (Technical Implementation Plan)  
> **Người thực hiện:** Dương Văn Minh (TV2 – MSSV: 2312688)  
> **Phạm vi giao việc Tuần 7:**  
> 1. Chốt hợp đồng FR-RCP-009 (nguyên liệu quản lý qua endpoint riêng).  
> 2. Xây dựng UI Editor công thức căn bản: upload / xem trước / chọn ảnh chính, nhập và sửa 6 trường dinh dưỡng.  
> 3. Bổ sung kiểm thử PostgreSQL / Testcontainers cho migration và cập nhật bước nấu đồng thời (`TECH-RISK-002`, `TECH-RISK-003`, `TECH-RISK-012`).  
> **Trạng thái:** Sẵn sàng triển khai (Ready for Implementation) – **Chỉ lập kế hoạch, không thực thi code**.

---

## 1. Tổng quan & Bối cảnh kỹ thuật

Hệ thống Culinary Blog hiện đã hoàn thành khung công thức cốt lõi (FR-RCP-001 đến FR-RCP-007), quản lý các bước nấu ăn (FR-RCP-010: `/api/v1/recipes/{id}/steps`), quản lý ảnh công thức qua MinIO (FR-RCP-008: `/api/v1/recipes/{id}/images`), và tìm kiếm toàn văn FTS trên PostgreSQL (FR-SRCH-001 đến 004).

Tuy nhiên, module Recipe vẫn còn các khoảng trống kỹ thuật trọng yếu được giao cho TV2 trong Tuần 7:
1. **FR-RCP-009 (Quản lý nguyên liệu):** Hiện tại nguyên liệu mới chỉ được nhận gộp trong payload `POST /api/v1/recipes` và `PUT /api/v1/recipes/{id}`. Chưa có các endpoint chuyên biệt để thêm, sửa, xóa từng nguyên liệu độc lập như đặc tả SRS §3.3 trang 35 và §8.6 trang 65–66.
2. **UI Recipe Editor căn bản (`frontend/src/app/dashboard/recipe-editor.tsx`):** Hiện chỉ là form thô sơ với các ô textarea chia dòng bằng ký tự gạch đứng `|`. Editor hoàn toàn thiếu tính năng upload/xem trước/chọn ảnh chính (chưa kết nối với FR-RCP-008) và hoàn toàn thiếu giao diện nhập/sửa 6 chỉ số dinh dưỡng (chưa hiện thực hóa `CONFLICT-008` trên UI).
3. **Kiểm thử PostgreSQL / Testcontainers:** Project `backend/tests/CulinaryBlog.DataMigration.Tests` hiện đang rỗng. Toàn bộ các migration của `ApplicationDbContext` (bao gồm `UnifyIdentityAndBlogSchema`, FTS trigger `unaccent`, `AddActiveRecipeStepOrderIndex`, `AddRecipePublishedAt`) mới chỉ được kiểm tra cục bộ hoặc in-memory, chưa có test suite tự động với PostgreSQL container thật. Đồng thời, nguy cơ xung đột số bước khi cập nhật đồng thời (`TECH-RISK-012` và `TECH-RISK-003`) chưa được kiểm chứng tự động.

---

## 2. Chốt hợp đồng kỹ thuật: FR-RCP-009 (Dedicated Endpoints)

### 2.1. Phân tích phương án

| Tiêu chí | Phương án A: Gộp trong Payload Recipe (`POST`/`PUT /recipes`) | Phương án B: Quản lý qua Endpoint riêng (`/recipes/{id}/ingredients`) |
|---|---|---|
| **Khả năng tương thích SRS** | Chỉ đáp ứng một phần luồng tạo mới nhanh. | Khớp hoàn toàn SRS §3.3 (trang 34–35) và §8.6 (trang 65–66). |
| **Tính nhất quán kiến trúc** | Lệch pha với `RecipeStep` (FR-RCP-010) và `RecipeImage` (FR-RCP-008). | Nhất quán hoàn toàn với mô hình RESTful sub-resource của dự án. |
| **Hiệu năng & Băng thông** | Phải gửi lại toàn bộ mảng nguyên liệu (>= 10 phần tử) kèm `RowVersion` cho mỗi thay đổi nhỏ. | Payload nhỏ gọn, chỉ truyền đúng nguyên liệu cần thao tác. |
| **Trải nghiệm UI Editor** | Cần cơ chế buffer state phức tạp, dễ mất đồng bộ dữ liệu. | Hỗ trợ inline editing, thêm/sửa/xóa tức thì (optimistic UI), drag & drop linh hoạt. |
| **Concurrency & Lock** | Dễ xung đột `RowVersion` của toàn bộ Recipe khi nhiều thao tác diễn ra. | Độc lập, giảm thiểu tối đa tranh chấp trên aggregate root. |

### 2.2. Quyết định chính thức (Contract Decision)

1. **Chuẩn hóa Hướng B (Dedicated Sub-resource Endpoints)** làm hợp đồng chính thức cho quản lý nguyên liệu:
   - `POST /api/v1/recipes/{id:guid}/ingredients`: Thêm một nguyên liệu mới.
   - `PUT /api/v1/recipes/{id:guid}/ingredients/{ingredientId:guid}`: Sửa một nguyên liệu đã tồn tại.
   - `DELETE /api/v1/recipes/{id:guid}/ingredients/{ingredientId:guid}`: Xóa mềm một nguyên liệu.
2. **Quy tắc tương thích ngược (Backward Compatibility)**:
   - Trong `POST /api/v1/recipes`: Tiếp tục cho phép client gửi kèm danh sách ban đầu (`Ingredients`) để tiện khởi tạo bản nháp nhanh chỉ trong 1 request.
   - Trong `PUT /api/v1/recipes/{id}`: Nếu trường `Ingredients` là `null`, backend sẽ **giữ nguyên** danh sách nguyên liệu hiện tại thay vì xóa sạch, tránh ghi đè làm mất dữ liệu nguyên liệu đã được biên tập qua endpoint riêng.
3. **Thống nhất giải quyết các xung đột liên quan**:
   - `CONFLICT-004`: `Quantity` và `Unit` là **nullable** (hỗ trợ ghi chú dạng "vừa đủ", "nêm nếm"). Nếu `Quantity` có giá trị thì bắt buộc `Quantity > 0`. `Unit` tối đa 50 ký tự.
   - `CONFLICT-005`: Sử dụng trường `OrderIndex` (kiểu `int`, mặc định 0) để sắp xếp thứ tự hiển thị, không dùng `StepNumber` hay `SortOrder`.
   - `CONFLICT-011`: Lỗi validation trả về **HTTP 400 Bad Request** với định dạng **Problem Details** (`RFC 7807` / `RFC 9457`).
   - Phân quyền: Yêu cầu Bearer Token hợp lệ. Chỉ Recipe Owner hoặc Admin mới có quyền thao tác (`HTTP 403 Forbidden` nếu không phải owner).

---

## 3. Thiết kế Kỹ thuật Chi tiết: Backend FR-RCP-009

### 3.1. Contracts & DTOs

Vị trí: `backend/src/CulinaryBlog.API/Contracts/Recipes/RecipeIngredientRequests.cs`

```csharp
namespace CulinaryBlog.API.Contracts.Recipes;

/// <summary>Yêu cầu thêm nguyên liệu mới vào công thức.</summary>
public sealed record AddRecipeIngredientRequest(
    string Name,
    decimal? Quantity,
    string? Unit,
    string? Notes,
    int? OrderIndex);

/// <summary>Yêu cầu thay thế/cập nhật toàn bộ thông tin một nguyên liệu.</summary>
public sealed record ReplaceRecipeIngredientRequest(
    string Name,
    decimal? Quantity,
    string? Unit,
    string? Notes,
    int? OrderIndex);
```

DTO phản hồi đã có trong `CulinaryBlog.Application/DTOs/Recipes/RecipeDtos.cs`:
```csharp
public sealed record RecipeIngredientDto(
    Guid Id,
    string Name,
    decimal? Quantity,
    string? Unit,
    string? Notes,
    int OrderIndex);
```

### 3.2. CQRS Commands & Validation

Vị trí: `backend/src/CulinaryBlog.Application/Features/Recipes/Commands/RecipeIngredientCommands.cs`

1. **`AddRecipeIngredientCommand`**:
   - Thuộc tính: `Guid RecipeId, string Name, decimal? Quantity, string? Unit, string? Notes, int? OrderIndex : IRequest<RecipeIngredientDto>`.
   - Validator:
     - `RecipeId`: `NotEmpty()`.
     - `Name`: `NotEmpty().MaximumLength(100).MustNotContainHtmlMarkup()`.
     - `Quantity`: `GreaterThan(0).When(x => x.Quantity.HasValue)`.
     - `Unit`: `MaximumLength(50).MustNotContainHtmlMarkup()`.
     - `Notes`: `MaximumLength(200).MustNotContainHtmlMarkup()`.
     - `OrderIndex`: `GreaterThanOrEqualTo(0).When(x => x.OrderIndex.HasValue)`.
   - Logic Handler:
     - Gọi helper kiểm tra recipe tồn tại và quyền sở hữu (Author-Owner hoặc Admin).
     - Nếu `OrderIndex` không được truyền: tự động gán bằng `(max(OrderIndex của active ingredients) ?? -1) + 1`.
     - Tạo mới thực thể `RecipeIngredient` gắn `RecipeId`.
     - Lưu DbContext, gọi Invalidate Redis detail cache (`recipes:slug:{slug}`) và prefix (`recipes:`).
     - Trả về `RecipeIngredientDto`.

2. **`UpdateRecipeIngredientCommand`**:
   - Thuộc tính: `Guid RecipeId, Guid IngredientId, string Name, decimal? Quantity, string? Unit, string? Notes, int? OrderIndex : IRequest<RecipeIngredientDto>`.
   - Validator: Tương tự như Add, kiểm tra thêm `IngredientId.NotEmpty()`.
   - Logic Handler:
     - Kiểm tra quyền sở hữu Recipe.
     - Truy vấn nguyên liệu theo `IngredientId` và `RecipeId` (chỉ lấy bản ghi `!IsDeleted`), ném `NotFoundException` nếu không thấy.
     - Cập nhật các trường: `Name`, `Quantity`, `Unit`, `Notes`, `OrderIndex` (nếu có giá trị mới).
     - Lưu DbContext, invalidate cache, trả về `RecipeIngredientDto`.

3. **`DeleteRecipeIngredientCommand`**:
   - Thuộc tính: `Guid RecipeId, Guid IngredientId : IRequest`.
   - Logic Handler:
     - Kiểm tra quyền sở hữu Recipe.
     - Truy vấn nguyên liệu, đánh dấu `IsDeleted = true`.
     - Tùy chọn chuẩn hóa: Cập nhật lại `OrderIndex` của các nguyên liệu còn lại để tránh phân mảnh thứ tự hiển thị.
     - Lưu DbContext, invalidate cache.

### 3.3. Minimal API Endpoints

Vị trí: `backend/src/CulinaryBlog.API/Endpoints/RecipeIngredientEndpoints.cs` (hoặc mở rộng trực tiếp trong `RecipeEndpoints.cs` theo mẫu của `RecipeSteps`):

```csharp
group.MapPost("/{id:guid}/ingredients", async (
    Guid id, AddRecipeIngredientRequest request, ISender sender, CancellationToken ct) =>
{
    var result = await sender.Send(new AddRecipeIngredientCommand(
        id, request.Name, request.Quantity, request.Unit, request.Notes, request.OrderIndex), ct);
    return Results.Created($"/api/v1/recipes/{id}/ingredients/{result.Id}", result);
})
.RequireAuthorization().WithName("AddRecipeIngredient")
.Produces<RecipeIngredientDto>(201).ProducesProblem(400).ProducesProblem(401).ProducesProblem(403).ProducesProblem(404);

group.MapPut("/{id:guid}/ingredients/{ingId:guid}", async (
    Guid id, Guid ingId, ReplaceRecipeIngredientRequest request, ISender sender, CancellationToken ct) =>
{
    var result = await sender.Send(new UpdateRecipeIngredientCommand(
        id, ingId, request.Name, request.Quantity, request.Unit, request.Notes, request.OrderIndex), ct);
    return Results.Ok(result);
})
.RequireAuthorization().WithName("UpdateRecipeIngredient")
.Produces<RecipeIngredientDto>(200).ProducesProblem(400).ProducesProblem(401).ProducesProblem(403).ProducesProblem(404);

group.MapDelete("/{id:guid}/ingredients/{ingId:guid}", async (
    Guid id, Guid ingId, ISender sender, CancellationToken ct) =>
{
    await sender.Send(new DeleteRecipeIngredientCommand(id, ingId), ct);
    return Results.NoContent();
})
.RequireAuthorization().WithName("DeleteRecipeIngredient")
.Produces(204).ProducesProblem(401).ProducesProblem(403).ProducesProblem(404);
```

---

## 4. Thiết kế Kỹ thuật Chi tiết: UI Recipe Editor Căn bản

Vị trí:
- `frontend/src/lib/recipes-api.ts`
- `frontend/src/app/dashboard/recipe-editor.tsx`
- `frontend/src/app/dashboard/dashboard.module.css` (hoặc `site-ui.module.css`)

### 4.1. Mở rộng Client API (`recipes-api.ts`)

1. **Bổ sung kiểu dữ liệu dinh dưỡng vào `RecipeInput`**:
   ```typescript
   export interface RecipeNutritionInput {
     calories: number;
     protein: number;
     carbohydrates: number;
     fat: number;
     fiber: number;
     sodium: number;
   }

   export interface RecipeInput {
     title: string;
     description: string | null;
     categoryId: string;
     prepTimeMinutes: number;
     cookTimeMinutes: number;
     servings: number;
     difficulty: string;
     ingredients: RecipeIngredient[];
     steps: RecipeStep[];
     nutrition?: RecipeNutritionInput | null;
   }
   ```

2. **Bổ sung các hàm gọi API ảnh (FR-RCP-008)**:
   ```typescript
   export interface RecipeImageUploadResponse {
     imageId: string;
     originalUrl: string;
     altText: string | null;
     isPrimary: boolean;
   }

   export async function uploadRecipeImage(
     token: string,
     recipeId: string,
     file: File,
     isPrimary = false,
     altText?: string,
   ): Promise<RecipeImageUploadResponse> {
     const formData = new FormData();
     formData.append("file", file);
     if (isPrimary) formData.append("isPrimary", "true");
     if (altText) formData.append("altText", altText);

     return request(`/api/v1/recipes/${recipeId}/images`, {
       method: "POST",
       headers: { Authorization: `Bearer ${token}` },
       body: formData,
     });
   }

   export async function setPrimaryRecipeImage(
     token: string,
     recipeId: string,
     imageId: string,
   ): Promise<void> {
     return request(`/api/v1/recipes/${recipeId}/images/${imageId}/primary`, {
       method: "PATCH",
       headers: authHeaders(token),
     });
   }

   export async function deleteRecipeImage(
     token: string,
     recipeId: string,
     imageId: string,
   ): Promise<void> {
     return request(`/api/v1/recipes/${recipeId}/images/${imageId}`, {
       method: "DELETE",
       headers: authHeaders(token),
     });
   }
   ```

3. **Bổ sung các hàm gọi API nguyên liệu độc lập (FR-RCP-009)**:
   ```typescript
   export async function addRecipeIngredient(
     token: string,
     recipeId: string,
     payload: Omit<RecipeIngredient, "id">,
   ): Promise<RecipeIngredient> {
     return request(`/api/v1/recipes/${recipeId}/ingredients`, {
       method: "POST",
       headers: authHeaders(token, true),
       body: JSON.stringify(payload),
     });
   }

   export async function updateRecipeIngredient(
     token: string,
     recipeId: string,
     ingredientId: string,
     payload: Partial<RecipeIngredient>,
   ): Promise<RecipeIngredient> {
     return request(`/api/v1/recipes/${recipeId}/ingredients/${ingredientId}`, {
       method: "PUT",
       headers: authHeaders(token, true),
       body: JSON.stringify(payload),
     });
   }

   export async function deleteRecipeIngredient(
     token: string,
     recipeId: string,
     ingredientId: string,
   ): Promise<void> {
     return request(`/api/v1/recipes/${recipeId}/ingredients/${ingredientId}`, {
       method: "DELETE",
       headers: authHeaders(token),
     });
   }
   ```

### 4.2. Giao diện Quản lý Ảnh (Upload / Preview / Set Primary / Delete)

1. **State Management trong `RecipeEditor`**:
   - `images: Array<{ id: string; originalUrl: string; altText: string | null; isPrimary: boolean; orderIndex: number }>`
   - `uploadingImage: boolean`
   - `imageError: string | null`
   - `selectedFile: File | null`
   - `previewUrl: string | null`
   - `altTextInput: string`
   - `makePrimaryOnUpload: boolean`

2. **Component UI & Luồng tương tác**:
   - **Upload Dropzone / File Input**:
     - Cho phép chọn file ảnh từ máy (`accept="image/jpeg,image/png,image/webp,image/avif"`).
     - Kiểm tra client-side: kích thước `<= 5MB`. Nếu vượt quá, báo lỗi ngay không gửi request.
     - Cho phép nhập `Alt text` trước khi nhấn "Tải ảnh lên".
     - Checkbox "Đặt làm ảnh đại diện chính ngay khi tải".
     - Trong chế độ Tạo mới (`!editing`), hiển thị hướng dẫn: *"Lưu bản nháp công thức trước khi tải ảnh lên kho lưu trữ MinIO"* (hoặc sau khi tạo thành công chuyển hướng sang màn hình edit với ID vừa sinh).
   - **Gallery Xem trước & Quản lý**:
     - Hiển thị danh sách ảnh dạng Grid responsive (`grid-cols-2 md:grid-cols-4 gap-4`).
     - Mỗi card ảnh gồm:
       - Ảnh thumbnail (dùng thẻ `<img>` với URL `originalUrl`).
       - Huy hiệu `Ảnh chính` (Badge màu cam/turmeric nổi bật) nếu `isPrimary === true`.
       - Nút "Đặt làm ảnh chính" (`Set as Primary`): Disabled khi ảnh đang là chính; khi bấm sẽ gọi `setPrimaryRecipeImage()`, cập nhật lại `isPrimary` trên state local.
       - Nút "Xóa" (`Delete`): Gọi `deleteRecipeImage()`, xác nhận trước khi xóa, cập nhật mảng state sau khi thành công.

### 4.3. Giao diện Nhập và Sửa 6 trường Dinh dưỡng (CONFLICT-008)

1. **State Management**:
   - `includeNutrition: boolean` (Checkbox kích hoạt)
   - `calories: string` (đơn vị: kcal, số nguyên $\ge 0$)
   - `protein: string` (đơn vị: g, số thực $\ge 0$)
   - `carbohydrates: string` (đơn vị: g, số thực $\ge 0$)
   - `fat: string` (đơn vị: g, số thực $\ge 0$)
   - `fiber: string` (đơn vị: g, số thực $\ge 0$)
   - `sodium: string` (đơn vị: mg, số thực $\ge 0$)

2. **Component UI**:
   - Tiêu đề Section: `Giá trị dinh dưỡng (Tính trên mỗi khẩu phần - Per Serving)`.
   - Chú thích rõ ràng theo quyết định `CONFLICT-008`: Các chỉ số được tính trên 1 khẩu phần ăn theo định mức `Servings` ở trên.
   - Bố cục Grid 2 cột trên mobile, 3 hoặc 6 cột trên màn hình lớn:
     - Ô 1: **Năng lượng / Calories** (`kcal`, step="1", min="0")
     - Ô 2: **Chất đạm / Protein** (`g`, step="0.1", min="0")
     - Ô 3: **Carbohydrate** (`g`, step="0.1", min="0")
     - Ô 4: **Chất béo / Fat** (`g`, step="0.1", min="0")
     - Ô 5: **Chất xơ / Fiber** (`g`, step="0.1", min="0")
     - Ô 6: **Natri / Sodium** (`mg`, step="1", min="0")
   - Đọc dữ liệu ban đầu: Trong `load()`, nếu `detail.nutrition` tồn tại thì set các giá trị vào state và bật `includeNutrition = true`.
   - Submit: Nếu `includeNutrition` bật, tạo object `RecipeNutritionInput` chuyển đổi chuỗi sang số (`Number(...)`) và gắn vào `payload.nutrition`. Nếu tắt, gán `payload.nutrition = null`.

### 4.4. Cải tiến Danh sách Nguyên liệu Động (Dynamic Ingredients UI)

Thay thế hoàn toàn ô `textarea` thô sơ bằng danh sách hàng trực quan:
- Mỗi dòng gồm 4 ô input: `Tên nguyên liệu *`, `Số lượng`, `Đơn vị`, `Ghi chú`.
- Nút "Thêm dòng nguyên liệu", nút "Xóa dòng".
- Hỗ trợ nhập liệu linh hoạt (khi edit có thể gọi endpoint riêng hoặc lưu tổng thể).

---

## 5. Thiết kế Kỹ thuật Chi tiết: Kiểm thử PostgreSQL / Testcontainers

Vị trí: `backend/tests/CulinaryBlog.DataMigration.Tests/` (hoặc tích hợp trong `CulinaryBlog.Integration.Tests/`)

### 5.1. Cấu hình Package & Test Fixture

1. **Thêm Package**:
   - `Directory.Packages.props`:
     ```xml
     <PackageVersion Include="Testcontainers.PostgreSql" Version="4.3.0" />
     ```
   - Project file (`CulinaryBlog.DataMigration.Tests.csproj`):
     ```xml
     <ItemGroup>
       <PackageReference Include="Testcontainers.PostgreSql" />
       <PackageReference Include="Npgsql.EntityFrameworkCore.PostgreSQL" />
       <PackageReference Include="Microsoft.EntityFrameworkCore.Design" />
       <PackageReference Include="Microsoft.NET.Test.Sdk" />
       <PackageReference Include="xunit" />
       <PackageReference Include="xunit.runner.visualstudio" />
       <PackageReference Include="FluentAssertions" />
     </ItemGroup>
     ```

2. **PostgresContainerFixture**:
   ```csharp
   public class PostgresContainerFixture : IAsyncLifetime
   {
       public PostgreSqlContainer Container { get; } = new PostgreSqlBuilder()
           .WithImage("postgres:16-alpine")
           .WithDatabase("culinary_blog_test")
           .WithUsername("postgres")
           .WithPassword("P@ssw0rdTest2026")
           .Build();

       public async Task InitializeAsync() => await Container.StartAsync();
       public async Task DisposeAsync() => await Container.DisposeAsync();

       public ApplicationDbContext CreateDbContext()
       {
           var options = new DbContextOptionsBuilder<ApplicationDbContext>()
               .UseNpgsql(Container.GetConnectionString())
               .Options;
           return new ApplicationDbContext(options);
       }
   }
   ```

### 5.2. Test Suite 1: Kiểm thử Migration trên PostgreSQL Thật (`PostgresMigrationTests.cs`)

1. **`ApplyAllMigrations_OnCleanDatabase_Succeeds`**:
   - Khởi động instance PostgreSQL sạch.
   - Gọi `await context.Database.MigrateAsync()`.
   - Kiểm tra `await context.Database.GetPendingMigrationsAsync()` rỗng (0 pending).
   - Kiểm tra bảng `__EFMigrationsHistory` có đầy đủ các migration:
     - `20260920000000_UnifyIdentityAndBlogSchema`
     - `20260925090000_AddActiveRecipeStepOrderIndex`
     - `20260930000000_AddRecipeSearchVectorFtsIndex` (nếu có)
     - `20261014000000_AddRecipePublishedAt` (nếu có)
2. **`Verify_Database_Schema_And_Constraints`**:
   - Kiểm tra bảng `Recipes`, `RecipeSteps`, `RecipeIngredients`, `RecipeImages`, `RecipeNutritions` tồn tại.
   - Kiểm tra PostgreSQL extension `unaccent` đã được kích hoạt (`SELECT 1 FROM pg_extension WHERE extname = 'unaccent'`).
   - Kiểm tra partial unique index `IX_RecipeSteps_RecipeId_StepNumber` với filter `is_deleted = false`.
   - Kiểm tra unique index trên `Recipes(slug)`.
3. **`Rollback_And_Reapply_Migrations_Cycle`**:
   - Kiểm thử hạ cấp (down) migration về một mốc lịch sử cụ thể và áp dụng lại (up), chứng minh tính khả thi của disaster recovery.

### 5.3. Test Suite 2: Kiểm thử Concurrency Cập nhật bước đồng thời (`RecipeStepConcurrencyTests.cs`)

1. **Mô tả Rủi ro Kỹ thuật (`TECH-RISK-012` & `TECH-RISK-003`)**:
   - Khi 2 người dùng (hoặc 2 tab trình duyệt của cùng 1 tác giả) đồng thời thêm bước (`AddRecipeStepCommand`) vào một công thức:
     - Cả 2 cùng đọc `maxStepNumber` từ database (ví dụ hiện có 3 bước, cả 2 đọc ra 3).
     - Cả 2 gán `StepNumber = 4` và gọi `SaveChangesAsync()`.
     - Vì bảng `RecipeSteps` có index unique `(RecipeId, StepNumber)` WHERE `!IsDeleted`, một trong 2 câu lệnh INSERT sẽ bị PostgreSQL chặn và ném ngoại lệ `23505 (unique_violation)`.
2. **Kịch bản kiểm thử 1: Concurrent Add Step Race Condition**:
   - **Mục tiêu**: Giả lập 2 request `AddRecipeStepCommand` bắn đồng thời qua `Task.WhenAll` trên 2 `DbContext` độc lập.
   - **Xác minh**:
     - Không xảy ra tình trạng cả 2 bản ghi cùng mang `StepNumber = 4` (PostgreSQL constraint bảo vệ thành công).
     - Một request thành công (Status 201), một request nhận ngoại lệ Concurrency / Unique Constraint hoặc được cơ chế Retry xử lý an toàn để thành `StepNumber = 5`.
     - Tổng số bước active trong database phải đúng bằng `số bước ban đầu + số request thành công`.
3. **Kịch bản kiểm thử 2: Concurrent Delete & Add Step**:
   - **Mục tiêu**: Một request `DeleteRecipeStepCommand` (đang renumber lại các bước từ 1 đến N) chạy song song với một request `AddRecipeStepCommand`.
   - **Xác minh**: Chuỗi `StepNumber` của các bước còn active luôn tạo thành một dãy số nguyên liên tục `1, 2, ..., N` mà không bị đứt đoạn hay trùng lặp.
4. **Kịch bản kiểm thử 3: Concurrent Step Update & Recipe Aggregate RowVersion**:
   - **Mục tiêu**: Xác minh việc cập nhật bước không làm sai lệch phiên bản `RowVersion` của Recipe cha, hoặc kiểm tra cơ chế khóa optimistic locking khi có sửa đổi song song.

---

## 6. Kế hoạch Xác minh & Tiêu chuẩn Nghiệm thu (Verification & Acceptance Criteria)

### 6.1. Tiêu chuẩn nghiệm thu chi tiết

1. **Về FR-RCP-009**:
   - Đầy đủ 3 endpoint: `POST`, `PUT`, `DELETE` dưới route `/api/v1/recipes/{id}/ingredients`.
   - Validation chuẩn xác: `Name` bắt buộc 1-100 ký tự; `Quantity > 0` nếu có; `Unit` nullable tối đa 50 ký tự; `Notes` nullable tối đa 200 ký tự; `OrderIndex` không âm.
   - Phân quyền nghiêm ngặt: Author-Owner hoặc Admin được phép; user khác trả 403; chưa đăng nhập trả 401; recipe/ingredient không thấy trả 404.
   - Xóa nguyên liệu là xóa mềm (`IsDeleted = true`).
   - Mọi thay đổi nguyên liệu đều tự động xóa cache detail và list của Recipe.
2. **Về UI Recipe Editor**:
   - Upload ảnh hỗ trợ chọn file, kiểm tra định dạng và kích thước <= 5MB trên client.
   - Hiển thị danh sách ảnh xem trước, có huy hiệu rõ ràng cho ảnh đại diện chính.
   - Nút "Đặt làm ảnh chính" hoạt động chính xác với API `PATCH /primary`.
   - Nút xóa ảnh có xác nhận và cập nhật UI ngay lập tức.
   - 6 trường dinh dưỡng hiển thị rõ ràng với đơn vị tương ứng (kcal, g, mg), hỗ trợ nhập số thập phân/nguyên đúng kiểu dữ liệu, bind 2 chiều chuẩn xác khi tạo mới và chỉnh sửa.
3. **Về PostgreSQL / Testcontainers**:
   - Suite test migration chạy thành công trên PostgreSQL 16 container thật.
   - Suite test concurrency chứng minh được tính toàn vẹn của dữ liệu bước nấu khi có xung đột đồng thời.
   - Không còn lỗi cảnh báo nghiêm trọng, không phá vỡ bất kỳ test suite hiện có nào của toàn bộ solution.

### 6.2. Lệnh xác minh dự kiến (Verification Commands)

```powershell
# 1. Kiểm tra build backend
dotnet restore backend/CulinaryBlog.sln
dotnet build backend/CulinaryBlog.sln --no-restore

# 2. Chạy toàn bộ test suite backend (Application, Architecture, Integration)
dotnet test backend/CulinaryBlog.sln --no-restore

# 3. Chạy riêng Testcontainers test suite (khi Docker daemon đang chạy)
dotnet test backend/tests/CulinaryBlog.DataMigration.Tests/CulinaryBlog.DataMigration.Tests.csproj

# 4. Kiểm tra TypeScript & Next.js production build của frontend
cd frontend
npm run lint
npx tsc --noEmit
npm run build
```

---

## 7. Đánh giá Rủi ro Kỹ thuật & Biện pháp Phòng ngừa

| Rủi ro | Mức độ | Nguyên nhân | Biện pháp phòng ngừa |
|---|:---:|---|---|
| **TECH-RISK-012** (Tranh chấp số bước đồng thời) | Cao | 2 request cùng đọc `max(StepNumber)` dẫn đến trùng số bước và vi phạm unique index. | Đón bắt `DbUpdateException` chứa mã lỗi Postgres `23505`, chuyển đổi thành `ConflictException` (HTTP 409) kèm thông báo rõ ràng cho client, hoặc áp dụng retry pattern với max 3 lần. |
| **TECH-RISK-002** (Lỗi extension `unaccent`) | Trung bình | Container test thiếu extension hoặc migration chạy trước khi extension được tạo. | Trong migration script hoặc test fixture, luôn đảm bảo câu lệnh `CREATE EXTENSION IF NOT EXISTS unaccent;` được thực thi trước các lệnh tạo index/trigger FTS. |
| **Môi trường CI thiếu Docker** | Trung bình | Testcontainers yêu cầu Docker daemon đang chạy. | Cấu hình test fixture nhận diện nếu không có Docker daemon thì thông báo rõ ràng hoặc skip có kiểm soát, tránh làm fail toàn bộ pipeline CI local khi chưa bật Docker Desktop. |
| **Xóa nhầm nguyên liệu khi PUT Recipe tổng** | Cao | `UpdateRecipeCommand` cũ nhận mảng nguyên liệu và thay thế toàn bộ. | Chốt hợp đồng: Khi payload `UpdateRecipeCommand` có `Ingredients == null`, backend giữ nguyên danh sách hiện có, không can thiệp vào các nguyên liệu đã tạo qua endpoint riêng. |
| **Lệch kiểu dữ liệu 6 trường dinh dưỡng** | Thấp | Nhập chuỗi rỗng từ ô input HTML thành NaN hoặc 0. | Hàm parse dữ liệu trên frontend xử lý fallback an toàn: chuỗi rỗng hoặc không hợp lệ chuyển thành 0 hoặc báo lỗi validation inline trước khi submit. |
