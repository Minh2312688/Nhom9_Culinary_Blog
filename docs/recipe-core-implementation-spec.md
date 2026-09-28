# Đặc tả triển khai Recipe Core và dữ liệu báo cáo

## Phạm vi

Module gồm domain recipe, persistence EF Core/PostgreSQL, CQRS, API `/api/v1/recipes`, cache detail bằng Redis, migration và dataset ngẫu nhiên phục vụ báo cáo.

## Hợp đồng dữ liệu báo cáo

Dataset báo cáo phải thỏa các bất biến sau:

| Bất biến | Điều kiện |
|---|---|
| Category count | `COUNT(Categories) >= 20` |
| Recipe count | `COUNT(Recipes WHERE IsDeleted = false) >= 100` |
| Ingredients | Mỗi recipe báo cáo có `COUNT(RecipeIngredients) >= 10` |
| Steps | Mỗi recipe báo cáo có `COUNT(RecipeSteps) >= 5` |
| Visibility | Recipe report có `Status = Published` |
| Repeatability | Chạy seeder nhiều lần không tạo duplicate slug |

## Migration

Schema runtime do migration chain của `AuthDbContext` sở hữu. `Lab02PersonalInitialDatabase` tạo Identity (`AspNetUsers`, `AspNetRoles`), Category, Recipe, RecipeIngredient và RecipeStep; `AlignAuthDbContextRecipeSchema` thêm RecipeImages/RecipeNutritions, chuẩn hóa RowVersion, cột thời lượng và index bước. `InitialRecipeSchema` thuộc migration chain cũ của `ApplicationDbContext`, không được API áp dụng.

`AuthDbContextFactory` cung cấp design-time options cho EF CLI. Đặt `ConnectionStrings__Postgres` trong User Secrets hoặc environment khi cần truy cập database; migration mới phải chỉ định `--context AuthDbContext`.

## Async random seeding

`RandomDataSeeder.SeedAsync` nhận `AuthDbContext`, `UserManager<ApplicationUser>`, `RoleManager<IdentityRole>`, `ILogger` và `CancellationToken`; Identity user/role được tạo qua ASP.NET Identity, dữ liệu recipe/category dùng cùng context schema chuẩn.

Seeder dùng author cố định, prefix slug `report-category-`/`report-recipe-` và random seed `20260921`. Nó tìm dữ liệu đã tồn tại, tạo phần còn thiếu, rồi gọi `SaveChangesAsync`. Recipe mới có:

- Title, Description, Difficulty, PrepTime, CookTime, Servings.
- 10 ingredients với Name, Quantity, Unit, Notes và OrderIndex.
- 5 steps với StepNumber, Title, Description và DurationMinutes.
- Nutrition gồm Calories, Protein, Carbohydrates, Fat, Fiber và Sodium.

Nếu dữ liệu đã tồn tại nhưng thiếu children, seeder bổ sung children đến ngưỡng tối thiểu. Cơ chế này giúp báo cáo có thể chạy lại sau khi database đã được tạo.

## Kích hoạt

Seed chỉ chạy khi `Seed:Enabled` là `true`. Docker Compose dùng biến môi trường `Seed__Enabled=true`; local không tự tạo dữ liệu nếu không bật cấu hình này.

## Robot Framework report

`robot/recipe_report.robot` dùng RequestsLibrary để gọi API public. Suite không chèn dữ liệu; nó kiểm chứng dữ liệu do `RandomDataSeeder` tạo:

1. Kiểm tra list categories có tối thiểu 20 items.
2. Kiểm tra list recipes có tối thiểu 100 items.
3. Đọc detail từng recipe và kiểm tra ingredients >= 10, steps >= 5.

Cài và chạy:

```powershell
python -m pip install -r robot/requirements.txt
robot -d robot/results robot/recipe_report.robot
```

`BASE_URL` mặc định là `http://localhost:5000`; có thể override bằng biến Robot:

```powershell
robot --variable BASE_URL:http://localhost:5000 -d robot/results robot/recipe_report.robot
```

## Luồng triển khai báo cáo

1. Khởi động PostgreSQL, Redis và API bằng Docker Compose.
2. API đọc `Seed__Enabled=true`.
3. API chạy `MigrateAsync`, tạo schema nếu migration chưa áp dụng.
4. API chạy `RandomDataSeeder.SeedAsync`.
5. Chờ log `Report random data ready`.
6. Chạy Robot suite và lưu kết quả trong `robot/results`.

## Giới hạn

- Migration hiện được lưu trong source; cần chạy PostgreSQL thật để xác nhận SQL theo phiên bản server triển khai.
- Seeder dành cho báo cáo, không nên bật trong production nghiệp vụ.
- Robot cần API và database đang chạy; nó không thay thế migration hoặc seeder.
- Khi Redis không chạy, cache read/write/remove được bỏ qua có log cảnh báo và request detail tiếp tục đọc PostgreSQL.
# Đặc tả triển khai Recipe Core

## 1. Mục đích và phạm vi

Tài liệu này ghi lại trạng thái triển khai module công thức nấu ăn theo `implementation_plan.md`, gồm FR-RCP-001 đến FR-RCP-010. Đây là tài liệu kỹ thuật dùng để đọc code, kiểm thử API và phát triển các nghiệp vụ tiếp theo. Đặc tả API và các bước triển khai riêng cho FR-RCP-009/010 nằm tại [`recipe-management-fr009-fr010-spec.md`](recipe-management-fr009-fr010-spec.md).

Phạm vi đã triển khai gồm:

- Mô hình domain `Recipe` và các thực thể con.
- Persistence bằng EF Core/PostgreSQL.
- CQRS bằng MediatR cho đọc, tạo, sửa và vòng đời recipe.
- Minimal API dưới `/api/v1/recipes`.
- Soft delete, optimistic concurrency bằng `RowVersion` và cache-aside cho chi tiết.
- FluentValidation và test cho các input chính.

Phạm vi chưa hoàn tất được ghi rõ ở cuối tài liệu, đặc biệt là Hangfire cleanup ảnh, integration test với Testcontainers, migration production và authentication JWT đầy đủ. Module không tự tạo dữ liệu demo hoặc dữ liệu kiểm thử ngẫu nhiên.

## 2. Quyết định nghiệp vụ

Các quyết định trong plan được dùng làm hợp đồng hiện tại:

| Chủ đề | Quyết định |
|---|---|
| Xóa | Soft delete: gọi `Remove` sẽ được `AuthDbContext.SaveChangesAsync` chuyển thành `IsDeleted = true`. |
| Trạng thái | `Draft = 0`, `Published = 1`, `Archived = 2`. |
| Hiển thị danh sách | Recipe `Published` được xem công khai; recipe khác chỉ hiện cho tác giả hoặc Admin. |
| Hiển thị chi tiết | `Draft`/`Archived` yêu cầu owner hoặc Admin; recipe Published cho anonymous. |
| Sort | Dùng `sortBy` và `sortOrder`, không dùng prefix `-`. |
| Pagination | Dùng `PaginatedResult<T>` dạng phẳng: `items`, `totalCount`, `page`, `pageSize`, `totalPages`, `hasNextPage`, `hasPreviousPage`. |
| Concurrency | RowVersion không khớp trả HTTP 409 Conflict. |
| Cache detail | Redis distributed cache, key `recipes:slug:{slug}`, TTL 5 phút. |
| Publish | Bắt buộc có ít nhất một ingredient và một step. |
| Step number | Server tự tạo số bước tuần tự khi tạo recipe. |
| Nutrition | Sáu chỉ số theo mỗi khẩu phần: calories, protein, carbohydrates, fat, fiber, sodium. |

## 3. Cấu trúc code

### Domain

- `backend/src/CulinaryBlog.Domain/Entities/BaseEntity.cs`: chứa `Id`, audit timestamps, `IsDeleted`, `RowVersion`.
- `Recipe.cs`: thông tin chính, Category, Author và các collection con.
- `RecipeIngredient.cs`: `Name`, `Quantity?`, `Unit?`, `Notes?`, `OrderIndex`.
- `RecipeStep.cs`: `StepNumber`, `Title?`, `Description`, `DurationMinutes?`, `ImageUrl?`.
- `RecipeImage.cs`: URL gốc, URL medium/thumbnail, alt text, primary và order.
- `RecipeNutrition.cs`: sáu trường dinh dưỡng.
- `RecipeStatus.cs`: enum vòng đời recipe.

Các entity con kế thừa `BaseEntity`, nên cũng chịu global query filter. `ApplicationUser` vẫn kế thừa `IdentityUser<string>` và dùng `CreatedAt` riêng, không dùng BaseEntity.

### Infrastructure

`AuthDbContext` là context runtime/migration chuẩn, kế thừa `IdentityDbContext` và implement `IApplicationDbContext`. Nó đăng ký DbSet cho Identity, Category, Recipe và toàn bộ recipe children; `SaveChangesAsync`:

1. Gán `CreatedAt` cho entity mới.
2. Gán `UpdatedAt` cho entity sửa.
3. Chuyển thao tác delete thành update `IsDeleted = true`.

`ApplicationDbContext` cùng migration chain `ApplicationDbContextModelSnapshot` là legacy artifacts giữ lại cho lịch sử và integration test hiện có. Runtime không đăng ký context này; không scaffold hoặc apply migration bằng context đó. Chỉ `AuthDbContext` sở hữu schema production.

`RecipeConfiguration` cấu hình:

- Bảng `Recipes`, khóa chính `Id` và unique index trên `Slug`.
- `Title` bắt buộc tối đa 200 ký tự, `Slug` tối đa 250 ký tự.
- `RowVersion` là concurrency token.
- Global filter `!IsDeleted`.
- Category/Author dùng `Restrict`; Steps/Ingredients/Images/Nutrition dùng `Cascade`.

Các configuration con có filter tương tự và giới hạn độ dài cho dữ liệu text.

### Application

- `ICurrentUserService`: cung cấp `UserId`, `IsAdmin`, `IsAuthenticated`.
- `IRecipeCache`: abstraction cho cache, giúp Application không phụ thuộc Redis.
- `RecipeDtos.cs`: DTO summary/detail và DTO cho nested data.
- `GetRecipes.cs`: query danh sách.
- `GetRecipeBySlug.cs`: query chi tiết.
- `RecipeCommands.cs`: create, update, publish, archive, delete.
- `RecipeMapping.cs`: map entity sang DTO.

### API

`RecipeEndpoints.cs` map các route Minimal API. `Program.cs` đăng ký current user, cache, exception handler, authentication/authorization middleware và gọi `MapRecipeEndpoints()`.

### Dữ liệu ứng dụng

Ứng dụng không tự động tạo user, category, recipe hoặc payload kiểm thử. Dữ liệu phải được cung cấp qua API, migration chính thức hoặc quy trình triển khai riêng của môi trường.

## 4. API contract

Base path: `/api/v1/recipes`.

### GET `/api/v1/recipes`

Query parameters:

- `page`: mặc định 1, tối thiểu 1.
- `pageSize`: mặc định 12, từ 1 đến 50.
- `categoryId`: GUID tùy chọn.
- `difficulty`: tùy chọn.
- `maxCookTime`: số nguyên không âm.
- `minServings`: số nguyên dương.
- `sortBy`: `title`, `createdAt`, `cookTime`, `prepTime`.
- `sortOrder`: `asc` hoặc `desc`.

Handler áp dụng visibility trước, sau đó filter, count, sort và Skip/Take. Kết quả là `PaginatedResult<RecipeSummaryDto>`.

### GET `/api/v1/recipes/{slug}`

Handler đọc cache trước. Cache miss sẽ query recipe cùng Ingredients, Steps, Images và Nutrition bằng eager loading. Recipe không tồn tại trả 404. Recipe không Published và không thuộc owner/Admin trả 403. Kết quả hợp lệ được cache 5 phút.

### POST `/api/v1/recipes`

Yêu cầu authorization. Body dùng `CreateRecipeCommand`; recipe luôn bắt đầu ở Draft. Server kiểm tra category, tạo slug từ title và thêm hậu tố `-2`, `-3` nếu slug đã tồn tại. Steps được đánh số từ 1.

Validation chính:

- Title 5-200 ký tự.
- CategoryId khác empty.
- Prep/Cook time không âm.
- Servings lớn hơn 0.
- Difficulty thuộc `Easy`, `Medium`, `Hard`, `Expert`; domain lưu enum integer (1–4), API giữ contract string.
- Ingredient name không rỗng.
- Step description không rỗng.

### PUT `/api/v1/recipes/{id}`

Yêu cầu authorization. Route id được gán vào command, nên client không thể cập nhật nhầm resource khác route. Body phải chứa `RowVersion` byte array. Handler tải recipe, kiểm tra owner/Admin, đặt `OriginalValue` của RowVersion rồi lưu. EF concurrency exception được đổi thành `ConcurrencyException`, middleware trả 409.

Update hiện thay thế các ingredient/step đang có bằng tập nested mới và giữ các entity cũ ở trạng thái soft-deleted. Nutrition được thay thế theo payload.

### PATCH `/api/v1/recipes/{id}/publish`

Yêu cầu owner/Admin. Chỉ chuyển sang Published khi recipe có ít nhất một ingredient và một step. Vi phạm điều kiện trả 400.

### PATCH `/api/v1/recipes/{id}/archive`

Yêu cầu owner/Admin. Chuyển status sang Archived.

### DELETE `/api/v1/recipes/{id}`

Yêu cầu owner/Admin. `DbContext` biến delete thành soft delete, vì vậy record vẫn còn trong database nhưng không xuất hiện qua query thông thường. API trả 204.

## 5. Authorization và lỗi

`CurrentUserService` lấy user id từ `ClaimTypes.NameIdentifier` hoặc claim `sub`, và Admin từ role claim. Application layer vẫn kiểm tra quyền tại handler, không chỉ dựa vào endpoint authorization.

Các lỗi được middleware ánh xạ:

- `ValidationException` hoặc `InvalidOperationException`: 400.
- `ForbiddenAccessException`: 403.
- `NotFoundException`: 404.
- `ConcurrencyException`: 409.
- Lỗi chưa phân loại: 500.

Response lỗi dùng `Results.Problem`, tương thích Problem Details ở mức cơ bản. Khi tích hợp Identity/JWT hoàn chỉnh, cần bổ sung policy và kiểm tra token thực tế.

## 6. Cache và vận hành

`DistributedRecipeCache` serialize DTO bằng `System.Text.Json`. Khi có `Redis:ConnectionString`, Infrastructure đăng ký `AddStackExchangeRedisCache`; khi không có cấu hình Redis, app dùng `AddDistributedMemoryCache` để chạy local.

Detail cache dùng key `recipes:slug:{slug}`. Sau create/update/publish/archive/delete, handler gọi invalidation. `IDistributedCache` không cung cấp API scan key portable, nên `RemoveByPrefixAsync` hiện là no-op. Vì danh sách chưa cache, điều này không làm stale list cache; nhưng nếu sau này cache list, cần bổ sung key registry hoặc Redis-specific `SCAN` adapter.

## 7. Kiểm thử đã thực hiện

Đã thêm `RecipeValidatorsTests.cs` với các trường hợp:

- Create từ chối title ngắn, servings không hợp lệ và input core sai.
- Update bắt buộc RowVersion.
- Query từ chối sort field và sort order không hỗ trợ.

Lệnh xác nhận:

```powershell
dotnet restore backend\CulinaryBlog.sln
dotnet build backend\CulinaryBlog.sln --no-restore
dotnet test backend\tests\CulinaryBlog.Application.Tests\CulinaryBlog.Application.Tests.csproj --no-restore
```

Kết quả lần chạy cuối: toàn bộ solution tests `13 passed, 0 failed`, trong đó Application tests `8 passed` và Integration tests `2 passed`; build thành công. Trong quá trình kiểm tra, composition root cũng được bổ sung mapping cho System endpoints và root status để integration tests không nhận 404. Build còn cảnh báo dependency hiện hữu do `Microsoft.EntityFrameworkCore.Relational` 10.0.4 và 10.0.12 cùng xuất hiện trong dependency graph.

## 8. Việc còn lại và hướng phát triển

1. Thêm migration PostgreSQL và seed Category/User/Recipe mẫu.
2. Viết integration tests bằng WebApplicationFactory với PostgreSQL và Redis Testcontainers.
3. Kiểm thử HTTP 409 bằng hai update request có cùng RowVersion.
4. Hoàn thiện JWT/Identity và resource authorization thực tế.
5. Thêm Hangfire job xóa các URL ảnh sau soft delete, hoặc xác định rõ chính sách giữ ảnh.
6. Tạo cache key registry/Redis adapter để invalidation theo prefix thực sự hoạt động.
7. Bổ sung endpoint quản lý Ingredients, Steps và Images độc lập theo FR-RCP-008..010.
8. Chuẩn hóa toàn bộ package EF Core về cùng một phiên bản để loại cảnh báo build.
9. Chạy manual flow: create Draft -> add nested data -> publish -> archive -> delete; kiểm tra record vẫn tồn tại với `IsDeleted = true`.

## 9. Ma trận trạng thái

| Requirement | Trạng thái | Bằng chứng |
|---|---|---|
| FR-RCP-001 | DONE | `GetRecipesQuery`, validator, handler, endpoint |
| FR-RCP-002 | DONE | `GetRecipeBySlugQuery`, eager loading, cache, endpoint |
| FR-RCP-003 | DONE | `CreateRecipeCommand`, slug generation, nested mapping |
| FR-RCP-004 | DONE | `UpdateRecipeCommand`, original RowVersion, 409 mapping |
| FR-RCP-005 | DONE | `PublishRecipeCommand`, child-data precondition |
| FR-RCP-006 | DONE | `ArchiveRecipeCommand` |
| FR-RCP-007 | DONE | `DeleteRecipeCommand`, DbContext soft delete |
| FR-RCP-009 | DONE (kiểm thử handler/HTTP và ghi PostgreSQL đã xác minh 2026-09-28) | Contract ingredient riêng, command, validation, quyền chủ sở hữu/admin, xóa mềm, invalidation cache, endpoint và giao diện chỉnh sửa nguyên liệu/bước nấu |
| FR-RCP-010 | DONE (kiểm thử handler/HTTP và ghi PostgreSQL đã xác minh 2026-09-28; tiếp tục theo dõi TECH-RISK-012) | Command step, endpoint có xác thực, validation, xóa mềm, đánh lại số thứ tự, advisory lock, migration và giao diện chỉnh sửa nguyên liệu/bước nấu |
| Redis cache | PARTIAL | Redis registration có; prefix invalidation chưa có |
| Hangfire cleanup | NOT STARTED | Chưa có Hangfire job trong repository |
| Xác minh tích hợp PostgreSQL | PARTIAL | Verifier đã kiểm tra lịch sử migration, Category có RowVersion cũ, xóa mềm/query filter của Category/Recipe, mutation step tuần tự, add/add và add/delete đồng thời; tiếp tục theo dõi các tình huống tranh chấp bổ sung |
| Mock/demo data implementation | REMOVED | Seeder, seed configuration and generated-data Robot suite removed |

## FR-RCP-010 — Quản lý bước nấu

Backend cung cấp các route quản lý step yêu cầu xác thực tại `/api/v1/recipes/{id}/steps`:

- `POST /api/v1/recipes/{id}/steps` tạo step mới. Server tự gán `StepNumber` bằng số lớn nhất hiện có (kể cả hàng đã xóa mềm) cộng một; client không tự chọn số.
- `PUT /api/v1/recipes/{id}/steps/{stepId}` thay thế `Title`, `Description`, `DurationMinutes` tùy chọn và `ImageUrl` tùy chọn. Server giữ nguyên `StepNumber`.
- `DELETE /api/v1/recipes/{id}/steps/{stepId}` xóa mềm step đã chọn và đánh lại số các step đang hoạt động liên tục từ 1. Hàng đã xóa được chuyển sang số âm chưa sử dụng để tránh trùng với số thứ tự trong lịch sử.

Chỉ chủ sở hữu recipe hoặc admin được thay đổi step. Không tìm thấy recipe/step trả 404; payload không hợp lệ trả 400 Problem Details; không đủ quyền trả 403. Sau mutation, handler xóa cache recipe detail và gọi cache contract để invalidation danh sách.

Migration PostgreSQL `AddActiveRecipeStepOrderIndex` tăng độ dài `RecipeSteps.Title` từ 150 lên 200 ký tự và thêm unique index `(RecipeId, StepNumber)` chỉ áp dụng cho step đang hoạt động. Handler test FR-RCP-010 kiểm tra cấp số step, cập nhật nội dung, xóa mềm/đánh lại số, phân quyền, validation và trường hợp số cũ đã bị xóa. HTTP integration test kiểm tra xác thực và phản hồi validation dạng Problem Details. Chuỗi migration `AuthDbContext`, bao gồm unique index step đang hoạt động, đã áp dụng trên `culinary_blog_auth`. PostgreSQL verifier kiểm tra lịch sử migration, từ chối Category có RowVersion cũ, xóa mềm/query filter của Category, thêm/xóa/đánh lại số step tuần tự, hai lệnh add đồng thời và add/delete đồng thời bằng advisory lock. Một lần kiểm tra ghi qua API Production cũng đã tạo recipe và step rồi xác nhận trực tiếp hai bản ghi trong PostgreSQL. Tiếp tục theo dõi `TECH-RISK-012` cho các tình huống đồng thời khác ngoài phạm vi verifier.

## FR-RCP-009 — Trạng thái quản lý nguyên liệu

Dữ liệu ingredient được hỗ trợ dạng thành phần của recipe và qua các route POST/PUT/DELETE riêng. Contract và CQRS command đã triển khai cùng phân quyền owner/admin, quantity cho phép null nhưng nếu có phải là số dương, dùng `OrderIndex` làm thứ tự chuẩn, xóa mềm và invalidation cache chi tiết. Handler/HTTP automated tests đạt; kiểm tra trực tiếp qua API xác nhận POST ingredient ghi được bản ghi vào PostgreSQL. Giao diện tại `/dashboard/recipes/{slug}/components` có form thêm/sửa/xóa ingredient và step; chưa xác minh build frontend. Contract, tiêu chí nghiệm thu, kết quả database và cách kiểm tra nằm tại [`recipe-management-fr009-fr010-spec.md`](recipe-management-fr009-fr010-spec.md).

## Conflict compliance audit — FR-RCP-001 through FR-RCP-007

Checked against the decisions recorded in `docs/srs-audit/SRS-CONFLICTS-AND-DECISIONS.md` on 2026-09-25:

| Requirement / decision | Finding |
|---|---|
| FR-RCP-001 — CONFLICT-003, -012, -021 | Compliant: uses explicit `sortBy` / `sortOrder`, flat pagination metadata, and includes an author's own Draft and Archived recipes plus Published recipes. |
| FR-RCP-002 — CONFLICT-005 through -008, -019 | Compliant: detail includes ingredients ordered by `OrderIndex`, step `DurationMinutes` and `Title`, all six nutrition values, and uses cache-aside with a five-minute TTL. |
| FR-RCP-003 — CONFLICT-004 through -008 | Fixed: Create now permits a null ingredient quantity but rejects supplied quantities `<= 0`; optional Unit remains nullable. Nested steps use `DurationMinutes` and include optional Title. |
| FR-RCP-004 — CONFLICT-014, -019 | Fixed: Update invalidates the old detail slug as well as the new slug after a title change; optimistic concurrency is mapped to HTTP 409. |
| FR-RCP-005 | Compliant: publish requires at least one active ingredient and one active step. |
| FR-RCP-006 | Compliant: archive changes status to Archived and invalidates recipe detail cache. |
| FR-RCP-007 — CONFLICT-001 | Compliant: `DbContext` converts entity deletion to `IsDeleted`; the global filter hides deleted recipes. A regression test verifies the row remains in storage and is hidden from normal queries. |
| Cross-cutting — CONFLICT-011, -025 | Compliant: validation returns HTTP 400 Problem Details; domain entities use one `BaseEntity`, while `ApplicationUser` derives directly from `IdentityUser<string>`. The unused duplicate BaseEntity declaration was removed. |

Regression tests were added for nullable/positive ingredient quantities on Create and Update, old/new slug cache invalidation, and recipe soft deletion. Final verification: solution build succeeded with 0 warnings and 0 errors; all tests passed (72 total: Application 47, Integration 22, Architecture 3). PostgreSQL-specific concurrency behavior and slug collisions under simultaneous creates remain operational risks; they are not decisions in these conflicts.

## PostgreSQL schema adoption status (2026-09-28)

The legacy source `culinary_blog` retains its ApplicationDbContext migration history and source schema. The new `culinary_blog_auth` target uses AuthDbContext. The user confirmed that a source backup was created before import.

All three AuthDbContext migrations are applied to the target: `Lab02PersonalInitialDatabase`, `AlignAuthDbContextRecipeSchema`, and `AddPostgresRowVersionTriggers`. The importer copied 1 user, 20 categories, 100 recipes, 1,000 ingredients, 500 steps and 100 nutrition rows; there were 0 recipe images. Counts, IDs and mapped values passed checks before the import transaction committed.

The first live RowVersion check showed stale updates were accepted. Migration `AddPostgresRowVersionTriggers` now changes the bytea token on update for all six BaseEntity tables. The PostgreSQL verifier passed checks for migration history, temporary Identity password validation and role membership, stale Category updates, Category/Recipe soft delete/query filtering, sequential step add/delete/renumbering, two concurrent adds receiving consecutive numbers and a concurrent add/delete retaining consecutive numbering. It cleans its temporary rows. The source backup was restored into the isolated `culinary_blog_backup_verify` database; read-only schema and row-count checks matched the importer preflight. The app User-scope connection now targets `culinary_blog_auth`; the Production API is running and its recipe-list smoke check passed. The imported user's actual password remains to be verified by user sign-in. A Production API smoke test against the target also passed; it exposed and fixed case-sensitive validation of the default recipe sort field. See [`postgresql-data-migration-plan.md`](postgresql-data-migration-plan.md) for the run record and mappings.
