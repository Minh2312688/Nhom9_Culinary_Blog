# Đặc tả triển khai Recipe Core và dữ liệu báo cáo

## Phạm vi

Module gồm domain recipe, persistence EF Core/PostgreSQL, CQRS, API `/api/v1/recipes`, cache detail bằng Redis, migration và dataset ngẫu nhiên phục vụ báo cáo.

## Hợp đồng dữ liệu báo cáo
...
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

Migration `InitialRecipeSchema` nằm trong Infrastructure và được API áp dụng qua `Database.MigrateAsync()` trước seed. Migration tạo ApplicationUsers, Categories, Recipes, RecipeIngredients, RecipeSteps, RecipeImages và RecipeNutritions, bao gồm foreign keys, unique recipe slug và các index cần cho truy vấn.

`ApplicationDbContextFactory` cung cấp design-time options cho EF CLI, vì vậy migration có thể được scaffold mà không cần khởi chạy web server hoặc kết nối database.

## Async random seeding

`RandomDataSeeder.SeedAsync` là lớp duy nhất chịu trách nhiệm tạo dữ liệu báo cáo. Nó nhận `ApplicationDbContext`, `ILogger` và `CancellationToken`, dùng toàn bộ API bất đồng bộ của EF Core.

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

Tài liệu này ghi lại trạng thái triển khai module công thức nấu ăn theo `implementation_plan.md`, tập trung vào FR-RCP-001 đến FR-RCP-007. Đây là tài liệu kỹ thuật dùng để đọc code, kiểm thử API và phát triển các nghiệp vụ tiếp theo.

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
| Xóa | Soft delete: gọi `Remove` sẽ được `ApplicationDbContext.SaveChangesAsync` chuyển thành `IsDeleted = true`. |
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

`ApplicationDbContext` đăng ký các DbSet recipe và gọi `ApplyConfigurationsFromAssembly`. `SaveChangesAsync`:

1. Gán `CreatedAt` cho entity mới.
2. Gán `UpdatedAt` cho entity sửa.
3. Chuyển thao tác delete thành update `IsDeleted = true`.

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
- Difficulty không rỗng, tối đa 20 ký tự.
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
- `UnauthorizedException`: 401.
- `ConcurrencyException` hoặc `ConflictException`: 409.
- Lỗi chưa phân loại: 500.

Response lỗi dùng Problem Details; lỗi validation trả thêm dictionary `errors` theo tên field. Middleware được đăng ký tại API composition root.

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
7. Bổ sung endpoint quản lý Steps và Images độc lập theo FR-RCP-008/010.
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
| FR-RCP-009 | DONE (backend) | Ingredient commands/endpoints, owner/Admin checks, validation, soft delete, cache invalidation; UI form còn lại |
| Redis cache | PARTIAL | Redis registration có; prefix invalidation chưa có |
| Hangfire cleanup | NOT STARTED | Chưa có Hangfire job trong repository |
| Integration verification | NOT STARTED | Chưa có Testcontainers recipe suite |
| Mock/demo data implementation | REMOVED | Seeder, seed configuration and generated-data Robot suite removed |


## 10. Quyết định conflict chính thức

Ngày 2026-09-23, đại diện nhóm xác nhận áp dụng các phương án đề xuất cho toàn bộ 25 conflict. Decision log ghi rõ phương án và người xác nhận tại [SRS-CONFLICTS-AND-DECISIONS.md](srs-audit/SRS-CONFLICTS-AND-DECISIONS.md). Trạng thái `DECIDED` xác nhận đã thống nhất hướng thiết kế, không đồng nghĩa các module tương ứng đã được code. Chỉ CONFLICT-004, 005 và 011 được ghi `IMPLEMENTED` cùng với phần FR-RCP-009 đã triển khai.

Các quyết định ngoài phạm vi FR-RCP-009 cần được thực hiện theo module: soft delete Recipe/Category; query sort và pagination; Step/Nutrition schema; Auth/profile/OAuth/token; concurrency HTTP 409; Redis TTL/cache policy; Category update; Recipe author visibility; browser support; image API contract; và ngoại lệ kế thừa Identity của ApplicationUser. Decision log là nguồn chuẩn để triển khai các phần này.

## 11. FR-RCP-009 — Quản lý ingredients riêng lẻ

### API contract

- `POST /api/v1/recipes/{id}/ingredients` thêm ingredient, trả `201 Created` và `Location` tới ingredient mới.
- `PUT /api/v1/recipes/{id}/ingredients/{ingredientId}` thay thế toàn bộ các field có thể sửa; `Quantity`, `Unit` và `Notes` gửi `null` sẽ xóa giá trị cũ.
- `DELETE /api/v1/recipes/{id}/ingredients/{ingredientId}` xóa mềm ingredient và trả `204 No Content`.
- Cả ba route yêu cầu đăng nhập. Handler kiểm tra owner/Admin, và đảm bảo ingredient thuộc recipe trong route.
- Payload yêu cầu `Name` và `OrderIndex`; `Quantity` và `Unit` nullable theo CONFLICT-004. Quantity nếu có phải lớn hơn 0; `OrderIndex` không âm.
- Mỗi mutation xóa cache `recipes:slug:{slug}` và gọi invalidation prefix hiện có.
- Lỗi trả theo Problem Details: validation 400, authentication 401, forbidden 403, resource không tồn tại/không khớp route 404.

### Verification FR-RCP-009

Test coverage gồm handler trên EF Core InMemory, validation/API auth và middleware Problem Details. Lần chạy mới nhất: 13 test FR-RCP-009 pass; solution có 45 Application, 24 Integration và 3 Architecture tests pass (72 tổng). Các handler tests dùng InMemory, chưa xác minh trên PostgreSQL/Testcontainers. UI ingredient form của TV3 chưa nằm trong phần backend này.
