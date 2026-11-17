# Kế hoạch triển khai Recipe Core và dữ liệu báo cáo

> **Cập nhật trạng thái hiện tại: 17/11/2026.** Đây là kế hoạch và hồ sơ triển
> khai được tích lũy qua nhiều giai đoạn, không phải danh sách việc cần bắt đầu
> lại. Trạng thái dưới đây đã được đối chiếu với mã hiện tại; phần ghi nhận ngày
> cũ bên dưới được giữ làm lịch sử. Đặc tả trạng thái kỹ thuật chi tiết: [docs/recipe-core-implementation-spec.md](docs/recipe-core-implementation-spec.md).

## Trạng thái thực hiện

Quy ước ở snapshot 17/11/2026: `[x]` đã có trong implementation hiện tại;
`[~]` có giới hạn hoặc phụ thuộc môi trường; `[ ]` chưa triển khai. Kết quả
build/test có ngày ghi nhận riêng, không hàm ý đã chạy lại trong lần cập nhật này.

- [x] Domain entities: Recipe, Category, RecipeIngredient, RecipeStep, RecipeNutrition, RecipeImage.
- [x] Một `ApplicationDbContext` quản lý Identity, refresh token, category, recipe, search và các bảng liên quan; không còn `AuthDbContext`.
- [x] CQRS/API cho FR-RCP-001 đến FR-RCP-007.
- [x] Đăng ký Redis distributed cache cho recipe/category; dùng memory cache khi không cấu hình Redis.
- [x] Hợp nhất migration Identity và Recipe/Search vào một migration stream của `ApplicationDbContext`.
- [x] Design-time factory đọc connection string từ `ConnectionStrings__Postgres`, không chứa mật khẩu mặc định.
- [~] `RandomDataSeeder.SeedAsync` còn trong source để tạo dataset báo cáo nhưng không có call site trong startup; Compose mặc định gọi `Lab02DataSeeder` với sample JSON.
- [x] Sample seeder tạo danh mục và 20 công thức từ `tools/sample-data/20_cong_thuc_mon_an.json`; không đồng nghĩa với dataset báo cáo 100 recipe.
- [x] Trong Development, startup tự migrate `ApplicationDbContext`; Compose bật `Seed__Enabled` để thêm dữ liệu mẫu.
- [~] `robot/recipe_report.robot` vẫn tồn tại nhưng yêu cầu dataset báo cáo 100 recipe; seed mặc định không tạo dataset đó, nên suite không phải kiểm chứng mặc định cho Compose.
- [x] Redis lỗi: cache bỏ qua lỗi và luồng tiếp tục truy vấn database; khi không cấu hình Redis, dùng distributed memory cache.
- [~] Integration tests dùng Testcontainers PostgreSQL đã có cho migration rỗng và nâng cấp dữ liệu legacy (2/2 `PostgresMigrationTests` đạt ngày 2026-11-17), sample seeding và concurrency; chưa có bằng chứng về Testcontainers Redis cho recipe.
- [x] Có endpoint quản lý ingredient, step và image riêng; trạng thái auth production cần được xác nhận theo cấu hình triển khai, không kết luận từ API local.
- [x] Recipe deletion xóa đối tượng ảnh từ storage trong luồng lệnh hiện tại; chưa có job Hangfire riêng để dọn ảnh khi xóa recipe.

## Quyết định dữ liệu báo cáo

- `Lab02DataSeeder` là seed Development được `Program.cs` gọi khi `Seed:Enabled=true`; nó nạp sample JSON, tạo danh mục cần thiết và tải ảnh mẫu lên MinIO.
- `RandomDataSeeder` là helper độc lập cho dataset báo cáo, dùng prefix `report-category-` / `report-recipe-` và seed cố định `20260921`; startup không gọi helper này.
- `robot/recipe_report.robot` giả định đã có ít nhất 100 recipe báo cáo. Không chạy suite này trên database chỉ dùng seed JSON mặc định nếu chưa tạo dataset báo cáo bằng một quy trình riêng.

## Migration và startup

Migration duy nhất được quản lý bởi `ApplicationDbContext` tại `backend/src/CulinaryBlog.Infrastructure/Persistence/Migrations`. Migration hợp nhất tạo Identity tables, liên kết Recipe với `AspNetUsers` và chuẩn hóa model cũ. Trong Development, API gọi `Database.MigrateAsync()` trước khi seed; Compose đặt `Seed__Enabled=true`.

Migration hợp nhất không tự chuyển database từng được tạo bằng `AuthDbContext`; tạo database mới để giữ database cũ nguyên trạng. Hướng dẫn chạy ứng dụng: `docs/run-project.md`; hướng dẫn DB, tài khoản và seed: `docs/postgresql-setup.md`.

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

Quy ước lịch sử của danh sách dưới đây: `[x]` đã có code và từng được kiểm tra;
`[~]` có một phần hoặc còn giới hạn; `[ ]` còn thiếu tại thời điểm ghi kế hoạch.
Đây không phải kết quả chạy lại build/test trong lần cập nhật tài liệu.

- [x] Bước 1 - Domain entities Recipe, Step, Ingredient, Image, Nutrition và RecipeStatus.
- [x] Bước 2 - DbContext, DbSet, Fluent configuration, global soft-delete filter, quan hệ FK và RowVersion.
- [x] FR-RCP-001 - Danh sách, lọc, explicit sorting và flat pagination.
- [x] FR-RCP-002 - Chi tiết theo slug, eager loading, kiểm tra quyền và cache-aside TTL 5 phút.
- [x] FR-RCP-003 - Tạo recipe Draft, tạo slug duy nhất, nested ingredients/steps/nutrition.
- [x] FR-RCP-004 - Cập nhật recipe chỉ bởi owner và xử lý RowVersion thành 409.
- [x] FR-RCP-005 - Publish với điều kiện tối thiểu một ingredient và một step.
- [x] FR-RCP-006 - Archive recipe.
- [x] FR-RCP-007 - Delete qua cơ chế soft-delete của DbContext và invalidation detail cache.
- [x] Validation - Validator cho query, create và update; test tự động cho các cạnh chính.
- [x] API - Đã map GET/POST/PUT/PATCH publish/archive/DELETE dưới `/api/v1/recipes`.
- [x] Redis distributed cache khi có `Redis:ConnectionString`; distributed memory cache khi không cấu hình Redis.
- [x] Cache danh sách và search có key theo filter/visibility; các thao tác Recipe yêu cầu invalidation qua cache abstraction.
- [~] Sample data vẫn được seed trong Development; `RandomDataSeeder` và Robot report suite còn trong repository nhưng không được dùng tự động.
- [x] Có lệnh xóa ảnh đồng bộ trong luồng xóa; chưa có Hangfire job riêng cho dọn ảnh khi xóa recipe.
- [~] Có integration tests với Testcontainers PostgreSQL; chưa có bằng chứng về Testcontainers Redis hoặc xác minh production database trong tài liệu này.
- [x] Migration hợp nhất và sample seeding đã có. Không xem nội dung lịch sử này là xác nhận rằng mọi cấu hình auth production đã được triển khai.

Chi tiết kỹ thuật và giới hạn hiện tại được ghi tại [docs/recipe-core-implementation-spec.md](docs/recipe-core-implementation-spec.md).

## Quyết định đã ghi nhận trong giai đoạn triển khai

> [!IMPORTANT]
> **Quyết định giải quyết xung đột (Conflict Resolutions)**
> Đây là ghi nhận lịch sử các quyết định được chọn; mục này không còn là yêu cầu review đang chờ:
> 1. **Delete Strategy (CONFLICT-001):** Sử dụng **Soft Delete** (`IsDeleted = true` + EF Core Global Query Filter). An toàn cho dữ liệu người dùng.
> 2. **Sorting (CONFLICT-003):** Sử dụng **Explicit params** (`sortBy=title&sortOrder=asc/desc`).
> 3. **Pagination (CONFLICT-012):** Sử dụng **Flat shape** `PagedResult<T> { items, totalCount, page, pageSize, totalPages, hasNextPage, hasPreviousPage }`.
> 4. **Concurrency (CONFLICT-014):** Trả về **HTTP 409 Conflict** khi sai `RowVersion`.
> 5. **Caching (CONFLICT-019):** Sử dụng **Redis distributed cache** (TTL 5m, Cache-Aside) thay vì Output Cache để dễ scale-out.
> 6. **Model Naming (CONFLICT-005, 006, 007, 008):** Dùng `OrderIndex`, `DurationMinutes`, có trường `Title` cho Step, và 6 chỉ số dinh dưỡng tính theo *PER SERVING*.

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

> Đây là mô tả thiết kế ban đầu; một số chi tiết đã được thay đổi. Quyền xem
> hiện tại được ghi ở đầu tài liệu và trong `docs/recipe-core-implementation-spec.md`.

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
  - Đề xuất ban đầu cho Draft/Archived là owner hoặc Admin; hành vi hiện tại đã thay đổi: chỉ owner được xem recipe chưa Published, Admin bị từ chối.

#### Tạo & Cập nhật Công thức (FR-RCP-003, FR-RCP-004)
- **Command:** `CreateRecipeCommand` và `UpdateRecipeCommand`.
- **Validation:** FluentValidation kiểm tra `Title` (5-200 chars), `Servings > 0`.
- **Logic:** Update sẽ nhận `RowVersion` từ client. Cập nhật các fields. Nếu `DbUpdateConcurrencyException` xảy ra, throw custom `ConcurrencyException` để Pipeline trả về **409 Conflict**.
- **Side effect:** Gọi Redis Service để Invalidate cache list và detail (`recipes:*`).

#### Các thao tác Đổi trạng thái (FR-RCP-005, FR-RCP-006, FR-RCP-007)
- **Commands:** `PublishRecipeCommand`, `ArchiveRecipeCommand`, `DeleteRecipeCommand`.
- **Logic Publish:** Kiểm tra có ít nhất 1 step và 1 ingredient trước khi chuyển sang `Published`.
- **Logic Delete (đề xuất ban đầu):** Đặt `IsDeleted = true` rồi dọn ảnh qua Hangfire. Hành vi hiện tại xóa đối tượng ảnh đồng bộ trong luồng lệnh; chưa có job Hangfire riêng.

---

### Bước 4: Presentation / API Layer (Minimal APIs hoặc Controllers)

#### [x] `src/API/Endpoints/RecipeEndpoints.cs`
- **GET** `/api/v1/recipes` & `/api/v1/recipes/{slug}`: Mở cho Anonymous.
- **POST** `/api/v1/recipes`: Cần auth (`RequireAuthorization`).
- **PUT** `/api/v1/recipes/{id}`: Route update yêu cầu RowVersion trong body. Phân quyền dùng `IAuthorizationService` (Author-Owner policy).
- **PATCH** `/api/v1/recipes/{id}/publish`, `/archive`: Thao tác chuyển đổi trạng thái.
- **DELETE** `/api/v1/recipes/{id}`: Trả về `204 No Content`.

## Kế hoạch xác minh ban đầu (lưu để tham chiếu)

### Automated Tests (Unit & Integration)
- **CQRS Tests:** Sử dụng xUnit và Moq để test các Handler. Kiểm tra Exception được ném ra đúng khi RowVersion sai, hoặc unauthorized.
- **Validation Tests:** Chạy FluentValidation unit tests kiểm tra các cạnh của Input (ví dụ Title rỗng, MaxCookTime < 0).
- **Integration Tests (kế hoạch ban đầu):** Sử dụng WebApplicationFactory kết hợp Testcontainers PostgreSQL; chưa có bằng chứng về Testcontainers Redis cho recipe.
  - Verify gọi API GET danh sách nhận đúng schema `PagedResult`.
  - Verify Update API với `RowVersion` cũ trả đúng `HTTP 409 Conflict`.

### Manual Verification
- Sử dụng Swagger UI để test flow tạo -> update -> publish -> xóa mềm.
- Đăng nhập với 2 user khác nhau để test phân quyền (User A không thể update/xem bài draft của User B).
- Kiểm tra dữ liệu trong PGAdmin để đảm bảo `IsDeleted` hoạt động đúng thay vì mất record.
- Dùng Redis CLI (`monitor`) để verify cache hit/miss và invalidation.

## FR-RCP-010 implementation update (2026-09-25)

- Added `AddRecipeStepCommand`, `UpdateRecipeStepCommand`, and `DeleteRecipeStepCommand` with FluentValidation, recipe owner/admin checks, cache invalidation, soft deletion, and contiguous active-step renumbering.
- Added authenticated `POST`, `PUT`, and `DELETE` step endpoints. `StepNumber` is server-assigned on create and preserved on update.
- Added API exception middleware that returns validation errors as Problem Details and maps authorization, not-found, and conflict errors to HTTP status codes.
- Added PostgreSQL migration `20260925090000_AddActiveRecipeStepOrderIndex` to align Title length (200) and enforce unique active step order. The regular EF migration scaffold currently detects unrelated drift from the pre-existing snapshot, so this migration is intentionally scoped to the two FR-RCP-010 schema changes.
- Added handler and HTTP integration tests. Targeted FR-RCP-010 tests passed (9/9); full solution build passed with 0 warnings and 0 errors; full solution tests passed (68/68: Application 45, Integration 20, Architecture 3).
- `dotnet ef migrations list` discovers the new migration as pending. It was not applied because no PostgreSQL migration run was requested/configured for this verification.
- Remaining: exercise the migration and mutation flows against PostgreSQL/Testcontainers; complete the step editor UI; address `TECH-RISK-012` for concurrent add/delete and multi-save renumber operations.

## Trạng thái FR-RCP-010 hiện tại (2026-11-17)

- Step editor hiện có trong Recipe Editor; backend ingredient/step endpoints và
  các integration tests tương ứng cũng có trong repository.
- PostgreSQL Testcontainers được dùng cho migration và concurrency test.
  `PostgresMigrationTests` đạt 2/2 ngày 2026-11-17; các test khác không chạy lại
  trong lần cập nhật tài liệu này.
- Giữ `TECH-RISK-012` như giới hạn/rủi ro concurrency cho thao tác đánh số bước;
  ghi nhận cũ về “step editor còn thiếu” thuộc thời điểm 25/09/2026.
