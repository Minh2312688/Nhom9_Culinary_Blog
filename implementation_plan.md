# Kế hoạch triển khai Recipe Core và dữ liệu báo cáo

## Trạng thái thực hiện

Quy ước: `[x]` đã triển khai và kiểm tra build; `[~]` triển khai một phần; `[ ]` còn thiếu.

- [x] Domain entities: Recipe, Category, RecipeIngredient, RecipeStep, RecipeNutrition, RecipeImage.
- [x] EF Core DbContext, Fluent configurations, soft delete và RowVersion.
- [x] CQRS/API cho FR-RCP-001 đến FR-RCP-007.
- [x] Redis distributed cache cho chi tiết recipe, TTL 5 phút.
- [x] Tạo migration `InitialRecipeSchema` trong Infrastructure.
- [x] Tạo `AuthDbContextFactory` để scaffold migration; `ApplicationDbContextFactory` cũ đã bỏ khỏi workflow chuẩn.
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

Migration core cũ của `ApplicationDbContext` được giữ làm legacy history:

`backend/src/CulinaryBlog.Infrastructure/Persistence/Migrations/20260921083055_InitialRecipeSchema.cs`

Schema runtime hiện do migration chain `AuthDbContext` quản lý, bắt đầu tại `20260921191147_Lab02PersonalInitialDatabase`; migration `AlignAuthDbContextRecipeSchema` đưa model hiện tại vào cùng chain này.

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

## User Review Required

> [!IMPORTANT]
> **Quyết định giải quyết xung đột (Conflict Resolutions)**
> Để đảm bảo tính nhất quán và tuân thủ các best practice của Clean Architecture và .NET, các điểm mâu thuẫn đã được chọn phương án tối ưu nhất. Nhóm hoặc Giảng viên cần xác nhận các quyết định này:
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

#### [x] `src/Infrastructure/Persistence/AuthDbContext.cs`
- Thêm `DbSet<Recipe>`, `DbSet<RecipeStep>`, `DbSet<RecipeIngredient>`, v.v.
- Implement `IApplicationDbContext`; ghi đè `SaveChangesAsync` để quản lý audit timestamp và soft delete.

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

<<<<<<< HEAD
## Cập nhật triển khai FR-RCP-010 (2026-09-25)

- Đã thêm `AddRecipeStepCommand`, `UpdateRecipeStepCommand` và `DeleteRecipeStepCommand` cùng FluentValidation, kiểm tra quyền owner/admin, invalidation cache, xóa mềm và đánh lại số liên tục cho các step đang hoạt động.
- Đã thêm endpoint step `POST`, `PUT`, `DELETE` yêu cầu xác thực. Server tự cấp `StepNumber` khi tạo và giữ nguyên số này khi cập nhật.
- Đã thêm API exception middleware để trả lỗi validation dạng Problem Details và ánh xạ lỗi phân quyền, không tìm thấy, xung đột sang HTTP status tương ứng.
- Đã thêm migration PostgreSQL `20260925090000_AddActiveRecipeStepOrderIndex` để đồng bộ độ dài Title (200) và bảo đảm thứ tự step đang hoạt động là duy nhất. Vì lệnh scaffold migration EF phát hiện schema drift cũ không liên quan, migration này chỉ chứa hai thay đổi thuộc FR-RCP-010.
- Đã thêm handler test và HTTP integration test. Bộ test FR-RCP-010 đạt 9/9; build solution thành công, 0 warning và 0 error; toàn bộ solution test đạt 68/68 (Application 45, Integration 20, Architecture 3).
- Tại thời điểm ghi chú này, `dotnet ef migrations list` cho thấy migration mới đang chờ áp dụng. Trạng thái đã được cập nhật trong phần bàn giao ngày 2026-09-28 sau khi migration được xác minh trên PostgreSQL.
- Các hạng mục tiếp theo tại thời điểm 2026-09-25: kiểm tra migration và mutation trên PostgreSQL/Testcontainers, hoàn thiện UI chỉnh sửa step và tiếp tục theo dõi `TECH-RISK-012` cho các tình huống add/delete đồng thời và đánh lại số qua nhiều lần lưu.

## Trạng thái FR-RCP-009/010 và bàn giao phát triển (2026-09-28)

- FR-RCP-009 đã có backend và giao diện chỉnh sửa nguyên liệu. Handler test kiểm tra validation, phân quyền, quan hệ recipe-con, xóa mềm và invalidation cache chi tiết/danh sách; HTTP test kiểm tra các mã 401/400/403/404/201/200/204 và việc ẩn ingredient đã xóa khỏi recipe detail.
- FR-RCP-010 đã có command, route, migration, handler test và xác minh mutation trên PostgreSQL. HTTP test kiểm tra server tự gán số step, cập nhật giữ nguyên số và recipe detail không trả step đã xóa. API test dùng InMemory cache/no-op mutation lock; hành vi PostgreSQL advisory lock được kiểm tra riêng bằng PostgreSQL verifier.
- Đã xác minh trực tiếp qua API/PostgreSQL: đăng ký/đăng nhập trả 201/200; tạo recipe, ingredient và step đều trả 201. Truy vấn `culinary_blog_auth` xác nhận có 1 recipe, 1 ingredient và 1 step cùng ID. Sau đó tạo recipe mẫu `canh-chua-ca` gồm 7 nguyên liệu và 5 bước; PostgreSQL xác nhận số dòng 1/7/5. Dữ liệu chẩn đoán đã được dọn; recipe mẫu còn lại được mô tả tại [`recipe-management-fr009-fr010-spec.md`](docs/recipe-management-fr009-fr010-spec.md).
- Đã sửa lỗi tạo recipe có tiêu đề Unicode: `Location` hiện dùng slug ASCII được sinh từ title. Regression test `CreateRecipe_WithVietnameseTitle_ReturnsCreatedWithAsciiSlugLocation` đạt 1/1.
- Thứ tự xây dựng có thể lặp lại được ghi tại mục 3.3 (FR-RCP-009) và 4.3 (FR-RCP-010) trong tài liệu đặc tả: schema/contract → handler và phân quyền → lưu dữ liệu/cache → endpoint → test → xác minh PostgreSQL → UI. Mục 8 có ví dụ PowerShell và câu lệnh SQL kiểm tra.
- Chưa xác minh build frontend: `npm install --offline --ignore-scripts --no-save --package-lock=false` không tìm thấy package `@hookform/resolvers` trong cache; không có file dependency nào bị thay đổi.
- Payload API, quy tắc, vị trí file, thứ tự triển khai và tiêu chí nghiệm thu được ghi tại [`recipe-management-fr009-fr010-spec.md`](docs/recipe-management-fr009-fr010-spec.md).

## DbContext/model/schema reconciliation (2026-09-28)

- [x] Chọn `AuthDbContext` làm context runtime và migration chuẩn; `IApplicationDbContext` được resolve từ cùng instance để recipe handlers và advisory lock cùng dùng một context.
- [x] Đưa DbSet `RecipeImages`/`RecipeNutritions`, audit timestamp, soft delete và RowVersion vào `AuthDbContext`.
- [x] Loại bỏ cấu hình `RecipeStep`/`RecipeIngredient` trùng có giới hạn cột khác nhau.
- [x] Lưu `Recipe.Difficulty` bằng enum số theo Auth schema (Easy=1, Medium=2, Hard=3, Expert=4); API giữ tên enum dạng chuỗi.
- [x] Xóa `Domain.Entities.ApplicationUser` có cột `Role`; Identity user chuẩn là `Infrastructure.Identity.ApplicationUser`, role dùng ASP.NET Identity.
- [x] Scaffold migration `AlignAuthDbContextRecipeSchema`: đổi `TimerMinutes` thành `DurationMinutes`, cho phép `RecipeSteps.Title` nullable, bổ sung default RowVersion, tạo bảng RecipeImages/RecipeNutritions và partial unique index cho bước chưa xóa mềm.
- [x] Bỏ production registration/design-time factory của `ApplicationDbContext`. Context và migration cũ được giữ làm legacy cho integration test/lịch sử; không tạo hoặc áp dụng migration mới bằng context này.
- [x] Kiểm tra SQL migration sinh từ EF trước khi đối chiếu DB: không drop table/column, nhưng migration này giả định schema Auth baseline và không thể áp trực tiếp vào database đích.
- [x] Chạy integration test project hiện có: 22/22 pass. Bộ test hiện dùng EF InMemory/WebApplicationFactory, không thay thế xác minh trực tiếp PostgreSQL.
- [x] `dotnet ef migrations has-pending-model-changes --context AuthDbContext`: không còn drift giữa model và snapshot.
- [x] Đọc PostgreSQL đích ở transaction read-only: migration history hiện có `InitialRecipeSchema` và `AddRowVersionDefaults` (ApplicationDbContext); bảng user là `ApplicationUser` có cột `Role`, chưa có `AspNetUsers`/`AspNetRoles`; database hiện có 20 Categories, 100 Recipes, 500 RecipeSteps và không có active duplicate step number.
- [x] Chọn chiến lược database mới rồi nhập dữ liệu; giữ database `culinary_blog` nguyên trạng làm nguồn. Database đích đề xuất `culinary_blog_auth`, chưa tạo.
- [x] Viết runbook mapping/schema preflight/import/validation tại `docs/postgresql-data-migration-plan.md`.
- [x] Xây dựng tool `backend/tools/CulinaryBlog.DataMigration`: dry-run mặc định; `--apply` yêu cầu xác nhận backup; source read-only; target tách biệt, đã migrate và rỗng; kiểm tra role/difficulty/FK/length/precision; COPY theo transaction; đối soát count/ID trước commit.
- [x] Thêm mapping/guard tests cho difficulty, role, target database separation, backup acknowledgment, Category defaults và data comparison helpers; 24/24 pass.
- [x] Build toàn solution với single-node/shared-compilation off: 0 warnings, 0 errors; full test suite 102/102 pass (Application 53, Integration 22, Architecture 3, DataMigration 24).
- [x] Thử preflight chỉ đọc bằng kết nối nguồn đang lưu ở User scope; PostgreSQL trả SQLSTATE `28P01` (sai mật khẩu), dừng trước khi đọc/ghi database đích. Công cụ báo SQLSTATE nhưng không in chuỗi kết nối.
- [x] Thử lại read-only preflight bằng host `::1` theo thông tin pgAdmin; toàn bộ kiểm tra source đã qua. Preflight dừng tại database chẩn đoán `postgres` vì không có `__EFMigrationsHistory` (SQLSTATE `42P01`); chưa kiểm tra target thật, không có dữ liệu bị ghi.
- [x] Người dùng xác nhận đã backup nguồn và tạo `culinary_blog_auth`; áp ba migration `AuthDbContext` lên target. `dotnet ef migrations list` xác nhận cả ba migration đã áp dụng.
- [x] Chạy importer dry-run rồi `--apply`: source gồm 1 user, 20 categories, 100 recipes, 1.000 ingredients, 500 steps, 100 nutritions, 0 images; preflight đạt và import commit sau khi kiểm tra count, ID và mapped values. Source được đọc read-only.
- [x] PostgreSQL verifier phát hiện RowVersion không đổi khi UPDATE; thêm migration `20260928142200_AddPostgresRowVersionTriggers` áp dụng trigger cho 6 entity tables. Kiểm tra stale RowVersion, Identity role membership, Category/Recipe soft delete/filter, add/delete/renumber step, hai add đồng thời và add/delete đồng thời trên `culinary_blog_auth` đã pass; verifier dọn các row tạm sau chạy.
- [x] PostgreSQL verifier tạo/xóa user tạm, xác nhận password validation và role Author qua `IIdentityService`; không dùng hoặc tiết lộ credential tài khoản đã import.
- [x] Người dùng restore `dulieu.backup` bằng pgAdmin vào database tạm `culinary_blog_backup_verify`; truy vấn read-only xác nhận schema bảng nghiệp vụ và số dòng khớp preflight/import: 1 user, 20 categories, 100 recipes, 1.000 ingredients, 500 steps, 100 nutritions, 0 images.
- [ ] Người dùng đăng nhập bằng tài khoản đã import để xác nhận password/hash tương thích; verifier hiện chỉ xác nhận luồng Identity bằng user tạm. Do frontend dev server thiếu package `next`, có thể kiểm tra endpoint `/api/v1/auth/login` trên API đang chạy tại `http://localhost:5000`.
- [x] API smoke test Production trỏ `culinary_blog_auth`: root trả `running`; `GET /api/v1/recipes?page=1&pageSize=3` trả 100 tổng và 3 items. Test phát hiện validator `sortBy` so sánh sai casing; bổ sung hồi quy và sửa để giá trị mặc định `createdAt` được chấp nhận.
- [x] Tìm thấy `dulieu.backup` ở project root; chữ ký `PGDMP` xác nhận PostgreSQL custom format. Restore pgAdmin vào DB tạm thành công; đã đối chiếu schema và row counts read-only.
- [x] Chuyển User-scope `ConnectionStrings__Postgres` sang `culinary_blog_auth`, giữ nguyên host/credential; tạo `Jwt__Key` ngẫu nhiên 512-bit trong User-scope vì app chưa cấu hình key. Production API đang chạy trên `http://127.0.0.1:5000`; root và GET recipes trả thành công (100 tổng, 3 item). Chạy Production nên không migration/seed database lúc startup.
- [ ] Hoàn tất nghiệm thu sau khi người dùng đăng nhập được bằng tài khoản import; giữ nguồn để rollback trong cửa sổ cutover.
=======
## FR-RCP-010 implementation update (2026-09-25)

- Added `AddRecipeStepCommand`, `UpdateRecipeStepCommand`, and `DeleteRecipeStepCommand` with FluentValidation, recipe owner/admin checks, cache invalidation, soft deletion, and contiguous active-step renumbering.
- Added authenticated `POST`, `PUT`, and `DELETE` step endpoints. `StepNumber` is server-assigned on create and preserved on update.
- Added API exception middleware that returns validation errors as Problem Details and maps authorization, not-found, and conflict errors to HTTP status codes.
- Added PostgreSQL migration `20260925090000_AddActiveRecipeStepOrderIndex` to align Title length (200) and enforce unique active step order. The regular EF migration scaffold currently detects unrelated drift from the pre-existing snapshot, so this migration is intentionally scoped to the two FR-RCP-010 schema changes.
- Added handler and HTTP integration tests. Targeted FR-RCP-010 tests passed (9/9); full solution build passed with 0 warnings and 0 errors; full solution tests passed (68/68: Application 45, Integration 20, Architecture 3).
- `dotnet ef migrations list` discovers the new migration as pending. It was not applied because no PostgreSQL migration run was requested/configured for this verification.
- Remaining: exercise the migration and mutation flows against PostgreSQL/Testcontainers; complete the step editor UI; address `TECH-RISK-012` for concurrent add/delete and multi-save renumber operations.
>>>>>>> origin/main
