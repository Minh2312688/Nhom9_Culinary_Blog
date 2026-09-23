# Kế hoạch triển khai Recipe Core và dữ liệu báo cáo

## Trạng thái thực hiện

Quy ước: `[x]` đã triển khai và kiểm tra build; `[~]` triển khai một phần; `[ ]` còn thiếu.

- [x] Domain entities: Recipe, Category, RecipeIngredient, RecipeStep, RecipeNutrition, RecipeImage.
- [x] EF Core DbContext, Fluent configurations, soft delete và RowVersion.
- [x] CQRS/API cho FR-RCP-001 đến FR-RCP-007.
- [x] Redis distributed cache cho chi tiết recipe, TTL 5 phút.
- [x] Tạo migration `InitialRecipeSchema` trong Infrastructure.
- [x] Tạo `ApplicationDbContextFactory` để scaffold migration không cần chạy API.
- [x] Tạo `RandomDataSeeder.SeedAsync` bất đồng bộ, idempotent và chỉ chạy khi `Seed:Enabled=true`.
- [x] Seed tối thiểu 20 categories và 100 recipes.
- [x] Mỗi recipe báo cáo có ít nhất 10 nguyên liệu và 5 bước chế biến.
- [x] Docker Compose chạy migration trước rồi chạy seeder cho môi trường báo cáo.
- [x] Robot Framework kiểm tra số lượng category/recipe và children của recipe.
- [x] Redis unavailable fallback: API tiếp tục phục vụ bằng database/memory cache.
- [ ] Integration test bằng Testcontainers PostgreSQL/Redis.
- [ ] Hoàn thiện JWT/Identity production và các endpoint quản lý nested data riêng lẻ.

## Quyết định dữ liệu báo cáo

- Seeder không dùng `EnsureCreated`; startup gọi `Database.MigrateAsync()` trước.
- Dataset dùng prefix `report-category-` và `report-recipe-` để không đụng dữ liệu nghiệp vụ thật.
- Random generator dùng seed cố định `20260921`, vì vậy dữ liệu có tính tái lập cho báo cáo.
- Seeder truy vấn dữ liệu đã có trước khi thêm, có thể chạy lại mà không nhân bản.
- Mục tiêu được hiểu là ngưỡng tối thiểu: database có thể có nhiều hơn 20 category hoặc 100 recipe.
- Recipe report ở trạng thái Published để Robot có thể kiểm tra bằng anonymous API.

## Migration và startup

Migration nằm tại:

`backend/src/CulinaryBlog.Infrastructure/Persistence/Migrations/20260921083055_InitialRecipeSchema.cs`

Migration tạo các bảng ApplicationUsers, Categories, Recipes, RecipeIngredients, RecipeSteps, RecipeImages và RecipeNutritions cùng khóa ngoại, index slug và index quan hệ.

`Program.cs` chỉ gọi migration/seeder khi cấu hình:

```json
{
  "Seed": { "Enabled": true }
}
```

Docker Compose đặt `Seed__Enabled=true` cho API báo cáo. Local Development mặc định không seed nếu không bật cờ.

## RandomDataSeeder

File triển khai:

`backend/src/CulinaryBlog.Infrastructure/Persistence/RandomDataSeeder.cs`

Luồng chạy:

1. Tìm hoặc tạo author cố định `report-author-...`.
2. Tạo đủ 20 category có slug `report-category-01` đến `report-category-20`.
3. Tạo đủ 100 recipe có slug `report-recipe-001` đến `report-recipe-100`.
4. Gắn recipe luân phiên vào 20 category.
5. Tạo 10 ingredients và 5 steps cho mỗi recipe.
6. Tạo nutrition ngẫu nhiên và lưu bằng `SaveChangesAsync`.
7. Ghi log số lượng cuối cùng để dùng trong báo cáo.

Seeder dùng `Random(20260921)` để kết quả ngẫu nhiên có thể tái lập. Nếu recipe đã tồn tại nhưng thiếu children, seeder bổ sung đến đúng ngưỡng tối thiểu.

## Robot Framework

Files:

- `robot/requirements.txt`
- `robot/recipe_report.robot`

Robot kiểm tra:

- GET categories có ít nhất 20 items.
- GET recipes có ít nhất 100 items.
- GET detail từng recipe Published và xác nhận tối thiểu 10 ingredients/5 steps.

Chạy báo cáo:

```powershell
python -m pip install -r robot/requirements.txt
robot -d robot/results robot/recipe_report.robot
```

API và PostgreSQL phải đang chạy, đồng thời `Seed:Enabled=true` phải được bật ở môi trường báo cáo.

## Verification commands

```powershell
dotnet restore backend/CulinaryBlog.sln
dotnet build backend/CulinaryBlog.sln --no-restore
dotnet test backend/CulinaryBlog.sln --no-restore
```

Kết quả cần đạt:

- Build thành công.
- Test suite hiện tại không thất bại.
- Robot báo cáo không có test fail.
- Log startup xác nhận category count >= 20 và recipe count >= 100.
# Kế hoạch Triển khai Chi tiết Module Công thức Nấu ăn (Recipe Core)

Kế hoạch này tập trung vào việc hiện thực hóa các chức năng từ **FR-RCP-001** đến **FR-RCP-007** do Thành viên 2 (TV2) phụ trách. Dựa trên việc rà soát kỹ lưỡng các tài liệu Master Audit và Conflict Log, kế hoạch đã giải quyết triệt để các mâu thuẫn (OPEN Conflicts) để đảm bảo quá trình code diễn ra thông suốt, không xung đột.

## Trạng thái thực hiện

Quy ước: `[x]` đã có code và đã kiểm tra build/test; `[~]` đã có một phần nhưng còn giới hạn; `[ ]` chưa triển khai.

- [x] Bước 1 - Domain entities Recipe, Step, Ingredient, Image, Nutrition và RecipeStatus.
- [x] Bước 2 - DbContext, DbSet, Fluent configuration, global soft-delete filter, quan hệ FK và RowVersion.
- [x] FR-RCP-001 - Danh sách, lọc, explicit sorting và flat pagination.
- [x] FR-RCP-002 - Chi tiết theo slug, eager loading, kiểm tra quyền và cache-aside TTL 5 phút.
- [x] FR-RCP-003 - Tạo recipe Draft, tạo slug duy nhất, nested ingredients/steps/nutrition.
- [x] FR-RCP-004 - Cập nhật recipe theo owner/admin và xử lý RowVersion thành 409.
- [x] FR-RCP-005 - Publish với điều kiện tối thiểu một ingredient và một step.
- [x] FR-RCP-006 - Archive recipe.
- [x] FR-RCP-007 - Delete qua cơ chế soft-delete của DbContext và invalidation detail cache.
- [x] Validation - Validator cho query, create và update; test tự động cho các cạnh chính.
- [x] API - Đã map GET/POST/PUT/PATCH publish/archive/DELETE dưới `/api/v1/recipes`.
- [~] Redis - Đã dùng Redis distributed cache khi có `Redis:ConnectionString`; fallback memory cho local không có Redis.
- [~] Cache list invalidation - danh sách hiện không cache; `RemoveByPrefixAsync` là no-op do `IDistributedCache` không có API scan key portable.
- [x] Đã loại bỏ toàn bộ triển khai dữ liệu giả/demo và suite tạo dữ liệu kiểm thử ngẫu nhiên.
- [ ] Hangfire cleanup ảnh sau delete.
- [ ] Integration tests với Testcontainers PostgreSQL/Redis và manual verification qua Swagger/Scalar.
- [ ] Migrations/seeding recipe và hoàn thiện authentication JWT/resource authorization của toàn hệ thống.

Chi tiết kỹ thuật và giới hạn hiện tại được ghi tại [docs/recipe-core-implementation-spec.md](docs/recipe-core-implementation-spec.md).

## Quyết định conflict chính thức

Ngày 2026-09-23, đại diện nhóm xác nhận áp dụng phương án đề xuất cho toàn bộ 25 conflict. Decision log tại [docs/srs-audit/SRS-CONFLICTS-AND-DECISIONS.md](docs/srs-audit/SRS-CONFLICTS-AND-DECISIONS.md) là nguồn chuẩn về phương án đã chọn. `DECIDED` nghĩa là đã thống nhất thiết kế, không đồng nghĩa đã triển khai code. CONFLICT-004, 005 và 011 đã được triển khai cùng FR-RCP-009; các conflict còn lại cần được thực hiện theo module.

## Đề xuất Triển khai Từng bước

---

### Bước 1: Domain Layer (Thiết kế Entities)

Định nghĩa các entities cốt lõi kế thừa từ `BaseEntity` (đảm bảo có `Id`, `CreatedAt`, `UpdatedAt`, `IsDeleted`, `RowVersion`).

#### [x] `src/Domain/Entities/Recipe.cs`
- **Properties:** Title, Slug, Description, CategoryId, AuthorId, Status (Enum: Draft, Published, Archived), PrepTimeMinutes, CookTimeMinutes, Servings, Difficulty.
- **Navigations:** `ICollection<RecipeStep>`, `ICollection<RecipeIngredient>`, `ICollection<RecipeImage>`, `RecipeNutrition`.
- **Behavior:** Khởi tạo danh sách trống trong constructor. Thêm phương thức để tự động sinh/cập nhật Slug từ Title.

#### [x] `src/Domain/Entities/RecipeNutrition.cs`, `RecipeStep.cs`, `RecipeIngredient.cs`
- `RecipeNutrition`: Lưu 6 chỉ số (Calories, Protein, Carbohydrates, Fat, Fiber, Sodium).
- `RecipeStep`: StepNumber, Title, Description, DurationMinutes, ImageUrl.
- `RecipeIngredient`: Name, Quantity (nullable), Unit (nullable), Notes, OrderIndex.

---

### Bước 2: Infrastructure Layer (EF Core & Database)

Thiết lập Entity Framework Core cấu hình qua Fluent API, bao gồm indexing và query filters.

#### [x] `src/Infrastructure/Data/ApplicationDbContext.cs`
- Thêm `DbSet<Recipe>`, `DbSet<RecipeStep>`, `DbSet<RecipeIngredient>`, v.v.
- Ghi đè `SaveChangesAsync` để tự động cập nhật `UpdatedAt` và quản lý concurrency (RowVersion).

#### [x] `src/Infrastructure/Data/Configurations/RecipeConfiguration.cs`
- Đặt **Global Query Filter**: `builder.HasQueryFilter(r => !r.IsDeleted);`.
- Định nghĩa PostgreSQL Index: `builder.HasIndex(x => x.Slug).IsUnique();`. 
- Cấu hình **RowVersion**: `builder.Property(x => x.RowVersion).IsRowVersion();`.
- Khai báo quan hệ One-to-Many với DeleteBehavior phù hợp (Restrict với Category, Cascade với các entities con nếu dùng soft delete ở cha).

---

### Bước 3: Application Layer (CQRS & MediatR Pipeline)

Sử dụng MediatR để phân tách các Use Case. Tất cả input phải đi qua FluentValidation.

#### Xem Danh sách & Tìm kiếm (FR-RCP-001)
- **Query:** `GetRecipesQuery { Page, PageSize, CategoryId, Difficulty, MaxCookTime, MinServings, SortBy, SortOrder }`.
- **Handler:** 
  - Lấy `UserId` từ `ICurrentUserService`.
  - Filter logic: Trạng thái `Published` HOẶC (`Draft`/`Archived` VÀ `AuthorId == currentUserId`).
  - Cấu trúc trả về: `PagedResult<RecipeSummaryDto>`.

#### Xem Chi tiết (FR-RCP-002)
- **Query:** `GetRecipeBySlugQuery { Slug }`.
- **Handler:** 
  - Tích hợp Redis: Lấy từ Cache (`recipes:slug:{slug}`). Nếu miss -> EF Core Include() -> Lưu Redis TTL 5 phút.
  - Phân quyền động: Nếu trạng thái là Draft/Archived, kiểm tra `currentUserId == AuthorId` hoặc role `Admin`. Nếu không, ném `ForbiddenAccessException`.

#### Tạo & Cập nhật Công thức (FR-RCP-003, FR-RCP-004)
- **Command:** `CreateRecipeCommand` và `UpdateRecipeCommand`.
- **Validation:** FluentValidation kiểm tra `Title` (5-200 chars), `Servings > 0`.
- **Logic:** Update sẽ nhận `RowVersion` từ client. Cập nhật các fields. Nếu `DbUpdateConcurrencyException` xảy ra, throw custom `ConcurrencyException` để Pipeline trả về **409 Conflict**.
- **Side effect:** Gọi Redis Service để Invalidate cache list và detail (`recipes:*`).

#### Các thao tác Đổi trạng thái (FR-RCP-005, FR-RCP-006, FR-RCP-007)
- **Commands:** `PublishRecipeCommand`, `ArchiveRecipeCommand`, `DeleteRecipeCommand`.
- **Logic Publish:** Kiểm tra có ít nhất 1 step và 1 ingredient trước khi chuyển sang `Published`.
- **Logic Delete:** Đặt `IsDeleted = true`. Trigger Hangfire Job (`FR-JOB-002` cleanup) để xóa ảnh từ MinIO.

---

### Bước 4: Presentation / API Layer (Minimal APIs hoặc Controllers)

#### [x] `src/API/Endpoints/RecipeEndpoints.cs`
- **GET** `/api/v1/recipes` & `/api/v1/recipes/{slug}`: Mở cho Anonymous.
- **POST** `/api/v1/recipes`: Cần auth (`RequireAuthorization`).
- **PUT** `/api/v1/recipes/{id}`: Route update yêu cầu RowVersion trong body. Phân quyền dùng `IAuthorizationService` (Author-Owner policy).
- **PATCH** `/api/v1/recipes/{id}/publish`, `/archive`: Thao tác chuyển đổi trạng thái.
- **DELETE** `/api/v1/recipes/{id}`: Trả về `204 No Content`.

## Verification Plan

### Automated Tests (Unit & Integration)
- **CQRS Tests:** Sử dụng xUnit và Moq để test các Handler. Kiểm tra Exception được ném ra đúng khi RowVersion sai, hoặc unauthorized.
- **Validation Tests:** Chạy FluentValidation unit tests kiểm tra các cạnh của Input (ví dụ Title rỗng, MaxCookTime < 0).
- **Integration Tests:** Sử dụng WebApplicationFactory kết hợp Testcontainers (PostgreSQL, Redis).
  - Verify gọi API GET danh sách nhận đúng schema `PagedResult`.
  - Verify Update API với `RowVersion` cũ trả đúng `HTTP 409 Conflict`.

### Manual Verification
- Sử dụng Swagger UI để test flow tạo -> update -> publish -> xóa mềm.
- Đăng nhập với 2 user khác nhau để test phân quyền (User A không thể update/xem bài draft của User B).
- Kiểm tra dữ liệu trong PGAdmin để đảm bảo `IsDeleted` hoạt động đúng thay vì mất record.
- Dùng Redis CLI (`monitor`) để verify cache hit/miss và invalidation.

## Cập nhật FR-RCP-009 - 2026-09-23

- Backend đã có command/handler và validator cho thêm, sửa, xóa ingredient; API POST/PUT/DELETE được bảo vệ authorization và kiểm tra chủ sở hữu/Admin.
- `Quantity` và `Unit` nullable; nếu có Quantity thì phải lớn hơn 0. Thứ tự dùng `OrderIndex`. PUT thay thế toàn bộ các trường ingredient; trường nullable gửi null sẽ được xóa.
- Lỗi validation trả HTTP 400 Problem Details; resource sai route trả 404; thao tác trái quyền trả 403; xóa mềm và cache detail được xử lý.
- 13 test FR-RCP-009 chạy qua; toàn solution có 72 test pass trong lần verification gần nhất. Integration dùng EF Core InMemory; PostgreSQL/Testcontainers verification còn lại. UI form còn phụ thuộc TV3.

## Quyết định conflict chính thức

- Ngày 2026-09-23, đại diện nhóm xác nhận áp dụng phương án đề xuất cho toàn bộ 25 mục trong [SRS conflict decision log](docs/srs-audit/SRS-CONFLICTS-AND-DECISIONS.md).
- `DECIDED` nghĩa là đã chốt thiết kế; không được hiểu là đã triển khai code. CONFLICT-004, 005, 011 được đánh dấu `IMPLEMENTED` theo phần FR-RCP-009.
- Các conflict còn lại là quyết định đầu vào cho kế hoạch triển khai theo module; cập nhật checklist và spec từng module khi hoàn tất code và verification.
