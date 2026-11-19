# Đặc tả triển khai Recipe Core và dữ liệu báo cáo

> **Trạng thái hiện tại: 19/11/2026.** Tài liệu này gồm ghi nhận báo cáo và đặc
> tả backend/frontend qua nhiều thời điểm. Mục mang ngày cũ là lịch sử; quyền
> hiện tại, sample seed và giới hạn còn lại được cập nhật theo code/tests.

## Phạm vi

Module gồm domain recipe, persistence EF Core/PostgreSQL, CQRS, API
`/api/v1/recipes`, cache, migration, sample seed Development và UI công thức.
Dataset báo cáo ngẫu nhiên là một helper riêng, không được gọi tự động khi API
khởi động.

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

`ApplicationDbContext` là context duy nhất cho Identity, refresh token, category, recipe và search. Toàn bộ migration được áp dụng từ `Program.cs` trong Development trước khi API nhận request. `UnifyIdentityAndBlogSchema` hợp nhất bảng user/role và recipe, đồng thời chuyển author/role legacy từ `ApplicationUser` sang `AspNetUsers`.

`ApplicationDbContextFactory` yêu cầu `ConnectionStrings__Postgres` từ môi trường design-time; không lưu mật khẩu trong source. Xem `docs/postgresql-setup.md` để tạo database, migrate và kiểm tra dữ liệu. Database từng migrate bằng `AuthDbContext` không tự tương thích; giữ nguyên và tạo database mới thay vì xóa volume.

## Async random seeding

`RandomDataSeeder.SeedAsync` là helper tạo dataset báo cáo, nhận
`ApplicationDbContext`, `ILogger` và `CancellationToken`, dùng API bất đồng bộ
của EF Core. Helper còn trong source nhưng không có call site trong startup;
đừng coi nó là seed mặc định của Compose.

Seeder dùng author cố định, prefix slug `report-category-`/`report-recipe-` và random seed `20260921`. Nó tìm dữ liệu đã tồn tại, tạo phần còn thiếu, rồi gọi `SaveChangesAsync`. Recipe mới có:

- Title, Description, Difficulty, PrepTime, CookTime, Servings.
- 10 ingredients với Name, Quantity, Unit, Notes và OrderIndex.
- 5 steps với StepNumber, Title, Description và DurationMinutes.
- Nutrition gồm Calories, Protein, Carbohydrates, Fat, Fiber và Sodium.

Nếu dữ liệu đã tồn tại nhưng thiếu children, seeder bổ sung children đến ngưỡng tối thiểu. Cơ chế này giúp báo cáo có thể chạy lại sau khi database đã được tạo.

## Kích hoạt

Khi `Seed:Enabled=true` trong Development, `Program.cs` gọi
`Lab02DataSeeder` trên cùng `ApplicationDbContext`, tạo danh mục cần thiết và
nạp 20 công thức từ `tools/sample-data/20_cong_thuc_mon_an.json`; ảnh được tải
từ `tools/sample-data/images/` lên MinIO. Compose mount bộ dữ liệu chỉ đọc vào
API container. `RandomDataSeeder` là helper riêng cho dataset báo cáo, không
phải seeder mặc định và không được startup gọi.

## Robot Framework report

`robot/recipe_report.robot` dùng RequestsLibrary để gọi API public và không tự
chèn dữ liệu. Suite vẫn yêu cầu tối thiểu 100 recipes cùng 10 ingredients/5
steps mỗi recipe, trong khi sample seed mặc định chỉ nạp 20 công thức. Vì
`RandomDataSeeder` không được gọi từ startup, suite này là legacy report
verification và không xác nhận được môi trường Compose mặc định:

1. Kiểm tra list categories có tối thiểu 20 items.
2. Kiểm tra list recipes có tối thiểu 100 items.
3. Đọc detail từng recipe và kiểm tra ingredients >= 10, steps >= 5.

Cài và chạy:

```powershell
python -m pip install -r robot/requirements.txt
robot -d robot/results robot/recipe_report.robot
```

`BASE_URL` mặc định là `http://localhost:5000`; chỉ chạy khi database đã được
nạp dataset phù hợp. Có thể override bằng biến Robot:

```powershellcd D:\Nhom9_Culinary_Blog
if (-not (Test-Path .env)) { Copy-Item .env.example .env }
docker compose up -d minio
docker compose ps minio
robot --variable BASE_URL:http://localhost:5000 -d robot/results robot/recipe_report.robot
```

## MinIO integration tests

Six tests under `MinioStorageIntegrationTests` require a live MinIO and explicit test-process environment variables. For Compose, first copy `.env.example` to `.env` and start MinIO. Set the test access key/secret to match `MINIO_ROOT_USER`/`MINIO_ROOT_PASSWORD`; do not commit credentials:

```powershell
docker compose up -d minio
$env:MINIO_TEST_ENDPOINT = "127.0.0.1:9000"
$env:MINIO_TEST_ACCESS_KEY = "<MinIO root user>"
$env:MINIO_TEST_SECRET_KEY = "<MinIO root password>"
$env:MINIO_TEST_BUCKET = "culinary-blog-it"
dotnet test backend\tests\CulinaryBlog.Integration.Tests\CulinaryBlog.Integration.Tests.csproj --filter "FullyQualifiedName~MinioStorageIntegrationTests"
```

Tests apply a public-read-only policy only to the isolated test bucket and create/delete temporary objects. Without a live service or any required variable they report `Skipped`, not `Passed`.

Compose pins `bitnamilegacy/minio:2025.7.23-debian-12-r5` because the configured Quay image returned 401 and the upstream Docker Hub image is no longer published. This archived image is a local test workaround, not a recommended production image. On Docker Desktop, use IPv4 loopback (`127.0.0.1`) for the test endpoint; `localhost` caused intermittent connection resets during SDK uploads.

## Luồng triển khai báo cáo

1. Khởi động PostgreSQL, Redis, MinIO, API và frontend bằng Docker Compose.
2. Xác nhận migration `ApplicationDbContext` thành công trên database mới.
3. Trong Development, `Seed__Enabled=true` gọi `Lab02DataSeeder` để tạo 20 công thức mẫu.
4. Chỉ chạy Robot report suite khi đã có dataset tối thiểu 100 recipe được chuẩn bị riêng; suite hiện không phải verification mặc định của sample seed.

## Giới hạn

- Database cũ được khởi tạo bằng migration `AuthDbContext` cần quy trình chuyển dữ liệu riêng; không xóa volume để né lỗi.
- `Lab02DataSeeder` là dữ liệu mẫu Development; không bật seeding trong môi trường production nghiệp vụ.
- `RandomDataSeeder` là helper report còn trong source nhưng không được startup gọi; Robot suite phụ thuộc dataset riêng và không thay thế migration/seeder.
- Khi Redis không chạy, cache read/write/remove được bỏ qua có log cảnh báo và request detail tiếp tục đọc PostgreSQL. Nếu không cấu hình Redis, DI dùng distributed memory cache.
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

Đặc tả này ghi cả kết quả cũ và trạng thái hiện hành. Backend có migration,
sample seeding Development, Auth.js/BFF integration, ingredient/step/image
routes và các PostgreSQL Testcontainers tests. Backend vẫn là nơi quyết định
quyền truy cập Recipe. Việc xóa ảnh nằm trong luồng lệnh hiện tại; chưa có
Hangfire job riêng để dọn ảnh sau khi xóa recipe.

## 2. Quyết định nghiệp vụ

Các quyết định trong plan được dùng làm hợp đồng hiện tại:

| Chủ đề | Quyết định |
|---|---|
| Xóa | Soft delete: gọi `Remove` sẽ được `ApplicationDbContext.SaveChangesAsync` chuyển thành `IsDeleted = true`. |
| Trạng thái | `Draft = 0`, `Published = 1`, `Archived = 2`. |
| Hiển thị danh sách | Khách và Admin dùng danh sách/tìm kiếm thông thường chỉ thấy Published; Author đã đăng nhập còn thấy các trạng thái của recipe mình. API quản trị riêng cho Admin trả mọi trạng thái của tất cả tác giả. |
| Hiển thị chi tiết công khai theo slug | Published cho anonymous; recipe chưa Published chỉ tác giả sở hữu xem được qua route slug. |
| Xem chi tiết riêng tư theo ID | Author xem recipe của mình; Admin xem recipe ở mọi trạng thái của tất cả tác giả. |
| Mutation | Author quản lý recipe của mình; Admin quản lý recipe của mọi tác giả, gồm tạo/sửa/xuất bản/lưu trữ/xóa và ingredient/step/image operations. |
| Sort | Dùng `sortBy` và `sortOrder`, không dùng prefix `-`. |
| Pagination | Dùng `PaginatedResult<T>` dạng phẳng: `items`, `totalCount`, `page`, `pageSize`, `totalPages`, `hasNextPage`, `hasPreviousPage`. |
| Concurrency | RowVersion không khớp trả HTTP 409 Conflict. |
| Cache detail | Distributed cache-aside, key `recipes:slug:{slug}`, TTL 5 phút. |
| Cache list | Distributed cache-aside cho query không có Search, TTL 60 giây; cache key gồm filter, sort, trang và visibility scope. |
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
3. Sinh `RowVersion` 8 byte mới khi thêm, sửa hoặc soft-delete entity.
4. Chuyển thao tác delete thành update `IsDeleted = true`.

`RecipeConfiguration` cấu hình:

- Bảng `Recipes`, khóa chính `Id` và unique index trên `Slug`.
- `Title` bắt buộc tối đa 200 ký tự, `Slug` tối đa 250 ký tự.
- `RowVersion` là concurrency token do ứng dụng sinh; PostgreSQL lưu `bytea` và không tự tăng token.
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

### Frontend Recipe Editor

`frontend/src/app/dashboard/recipe-editor.tsx` giữ giao diện một trang, dùng React Hook Form quản lý các trường Recipe chính và Zod resolver để kiểm tra trước khi gọi API. Luồng bước nấu, dinh dưỡng và quản lý ảnh giữ nguyên. Các thao tác riêng tư của Recipe Editor dùng API helper không nhận access token từ trình duyệt; helper gọi BFF cùng origin như mô tả dưới đây.

### Xác thực frontend và BFF

Frontend dùng Auth.js v5 với Credentials provider cho email/mật khẩu và Google Identity Services ID token. Đây là luồng Credentials tới backend hiện có, không phải OAuth redirect. Auth.js giữ backend access/refresh token trong JWT session cookie được mã hóa, `HttpOnly`, `SameSite=Lax` và `Secure` trong production; Session gửi tới trình duyệt chỉ chứa thông tin người dùng, không chứa token backend. Tài khoản được làm mới token trước hạn 30 giây; refresh đồng thời được gộp trong phạm vi một tiến trình.

Các request riêng tư từ browser gọi `/api/backend/{resource}`. Route Handler này xác thực session, kiểm tra allowlist resource/method, xác thực `Origin` cho request thay đổi dữ liệu rồi mới gắn Bearer token khi gọi backend qua `API_INTERNAL_URL` chỉ dùng phía server. Backend vẫn là nơi quyết định quyền nghiệp vụ và quyền tác giả. Các API công khai tiếp tục gọi public API mà không gửi credential. Đăng xuất gọi thu hồi token ở phía server; lỗi thu hồi không ngăn việc xóa session cục bộ.

Thiết kế refresh single-flight hiện dựa trên một Next.js instance. Nếu triển khai nhiều instance, cần thay thế bằng cơ chế phối hợp refresh dùng chung; mỗi môi trường triển khai cũng phải cấu hình `AUTH_SECRET`, `API_INTERNAL_URL` và HTTPS phù hợp.

### TanStack Query ở frontend

`frontend/src/app/providers.tsx` tạo một `QueryClient` cho ứng dụng. Cache query mặc định tươi trong 30 giây; mutation không tự retry. Query key của dữ liệu riêng tư có user ID để tách cache theo tài khoản. Khi một phiên đã xác thực đăng xuất hoặc đổi danh tính, QueryClient được xóa; lần chuyển đầu từ khách sang người dùng không xóa cache.

Các trang duyệt Recipe, Category, Search, Author và Dashboard dùng query cho server state; Recipe Editor giữ nội dung form và file đang chọn trong state cục bộ. Trang Profile và header dùng chung query Profile; cập nhật Profile ghi dữ liệu mới vào cache để cả hai nơi phản ánh ngay. Các thao tác ghi dùng mutation với invalidation theo domain. Đăng ký, quên mật khẩu, đặt lại mật khẩu và đổi mật khẩu cũng dùng mutation; Auth.js vẫn phụ trách đăng nhập/đăng xuất.

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

Handler đọc cache trước. Cache miss sẽ query recipe cùng Ingredients, Steps,
Images và Nutrition bằng eager loading với `AsSplitQuery()` để tránh nhân chéo
dữ liệu từ nhiều collection. Recipe không tồn tại trả 404. Recipe không
Published chỉ cho owner xem qua route slug; Admin dùng route chi tiết riêng tư
theo ID để xem Draft/Archived.
Kết quả hợp lệ được cache 5 phút.

### GET `/api/v1/admin/recipes`

Route yêu cầu `AdminPolicy` và trả danh sách mọi trạng thái của tất cả tác giả.
Đây là endpoint quản trị; `GET /api/v1/recipes/` và các route public/search
không thay đổi visibility và vẫn chỉ trả Published cho Admin.

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

Yêu cầu authorization. Route id được gán vào command, nên client không thể cập nhật nhầm resource khác route. Body phải chứa `RowVersion` byte array. Owner sửa recipe của mình; Admin được sửa recipe của mọi tác giả. Handler đặt `OriginalValue` của RowVersion rồi lưu. EF concurrency exception được đổi thành `ConcurrencyException`, middleware trả 409.

Update hiện thay thế các ingredient/step đang có bằng tập nested mới và giữ các entity cũ ở trạng thái soft-deleted. Nutrition được thay thế theo payload.

### PATCH `/api/v1/recipes/{id}/publish`

Yêu cầu owner hoặc Admin. Chỉ chuyển sang Published khi recipe có ít nhất một ingredient và một step. Vi phạm điều kiện trả 400.

### PATCH `/api/v1/recipes/{id}/archive`

Yêu cầu owner hoặc Admin để chuyển trạng thái sang Archived.

### DELETE `/api/v1/recipes/{id}`

Owner được xóa recipe của mình; Admin được xóa recipe của mọi tác giả ở mọi trạng thái. `DbContext` biến delete thành soft delete, vì vậy record vẫn còn trong database nhưng không xuất hiện qua query thông thường. API trả 204.

Ingredient, step và image mutations cho phép owner thao tác trên recipe của
mình hoặc Admin thao tác trên mọi recipe.

## 5. Authorization và lỗi

`Program.cs` đăng ký `AdminPolicy` (role Admin) và `AuthorPolicy` (role Author
hoặc Admin), rồi bật authentication/authorization middleware trước khi map các
endpoint. `CurrentUserService` lấy user id từ `ClaimTypes.NameIdentifier` hoặc
claim `sub`, và Admin từ role claim. Application layer vẫn kiểm tra quyền tại
handler, không chỉ dựa vào endpoint authorization.

Các lỗi được middleware ánh xạ:

- `ValidationException` hoặc `InvalidOperationException`: 400.
- `ForbiddenAccessException`: 403.
- `NotFoundException`: 404.
- `ConcurrencyException`: 409.
- Lỗi chưa phân loại: 500.

Response lỗi dùng `Results.Problem`, tương thích Problem Details ở mức cơ bản. Khi tích hợp Identity/JWT hoàn chỉnh, cần bổ sung policy và kiểm tra token thực tế.

## 6. Cache và vận hành

`DistributedRecipeCache` serialize DTO bằng `System.Text.Json`. Khi có `Redis:ConnectionString`, Infrastructure đăng ký `AddStackExchangeRedisCache`; khi không có cấu hình Redis, app dùng `AddDistributedMemoryCache` để chạy local.

Detail cache dùng key `recipes:slug:{slug}` với TTL 5 phút. List không có Search dùng cache-aside TTL 60 giây; cache key bao gồm phân trang, filter, sort và scope guest/user/admin để không dùng chung dữ liệu có visibility khác nhau. Search phổ biến giữ chính sách cache riêng.

Sau các thay đổi Recipe, handler gọi `RemoveByPrefixAsync("recipes:")`. Cache list và search thêm generation hiện tại vào resolved key; tăng generation làm các kết quả cũ không còn được đọc mà không cần quét key Redis. Detail cache được xóa theo slug. Khi không cấu hình Redis, app dùng distributed memory cache; lỗi kết nối Redis được ghi log và request tiếp tục truy vấn database.

## 7. Kết quả kiểm thử được ghi nhận tại thời điểm 2026-10-06

> Các lệnh và kết quả dưới đây là bằng chứng lịch sử, không phải kết quả chạy
> lại trong lần cập nhật tài liệu ngày 17/11/2026.

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

Kết quả xác minh ngày 2026-10-06: MinIO thật chạy từ Compose bằng image legacy đã pin; riêng MinIO tests đạt `6/6`, toàn bộ Integration project đạt `76/76` (không Skip). Test upload riêng cũng đạt 5 lần liên tục. Dùng `MINIO_TEST_ENDPOINT=127.0.0.1:9000`; `localhost:9000` gây lỗi reset kết nối không ổn định trên Docker Desktop. FR-JOB-002 thumbnail queue chưa có trong backend.

## 8. Trạng thái hiện tại và việc cần xác minh theo môi trường (2026-11-17)

1. Migration hợp nhất và seed Development bằng 20 công thức mẫu đã có; `RandomDataSeeder` không được gọi tự động.
2. PostgreSQL Testcontainers tests cho migration, sample seeding và recipe-step concurrency đã có. `PostgresMigrationTests` bao gồm cả nâng cấp schema có dữ liệu legacy: giữ RowVersion và Identity/role, backfill/duy trì `SearchVector`, đặt `VariantStatus=Pending` cho ảnh cũ và giữ `PublishedAt=NULL` cho recipe lịch sử. Lượt test migration gần nhất ngày 2026-11-17 đạt 2/2; không có bằng chứng về Testcontainers Redis trong phạm vi tài liệu này.
3. Migration mới nhất được ghi nhận là chưa áp dụng vào PostgreSQL tại thời điểm báo cáo FR-RCP-010; cần xác minh với đúng database đích trước khi triển khai.
4. Cấu hình `AUTH_SECRET`, `API_INTERNAL_URL`, HTTPS/Secure cookie và giới hạn instance phụ thuộc môi trường triển khai; Auth.js/BFF không thay thế authorization ở backend.
5. Ảnh được dọn đồng bộ trong luồng xóa recipe; chưa có Hangfire job riêng cho cleanup.
6. Smoke-test cache list/search với Redis thật và manual flow CRUD phụ thuộc dịch vụ/môi trường, chưa được xác nhận trong lần cập nhật tài liệu này.
7. FR-RCP-008 image workflow và FR-RCP-009 ingredient endpoints đã có; FR-RCP-010 step endpoints và editor cũng đã có.
8. Các kết quả build/test và manual verification dưới đây giữ nguyên ngày ghi nhận ban đầu; không xem là xác nhận môi trường hiện tại.

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
| FR-RCP-008 | IMPLEMENTED; live-service verification is environment-dependent | Image routes, handlers, and image processing workflow exist; MinIO integration requires the service and was not re-run for this documentation update |
| FR-RCP-009 | IMPLEMENTED | Dedicated ingredient endpoints, owner-only checks, editor and PostgreSQL integration tests exist |
| FR-RCP-010 | DONE (backend) | Step commands, authenticated endpoints, validation, soft delete and renumbering; PostgreSQL migration and handler/HTTP tests |
| FR-JOB-002 thumbnail processing | IMPLEMENTED | Recipe image job enqueuer and processing job exist; deployment/runtime verification is environment-dependent |
| Recipe cache | IMPLEMENTED | Detail cache-aside 5 phút; list cache-aside 60 giây, key có visibility/filter/pagination/sort; generation invalidation cho list/search |
| Image cleanup on recipe delete | IMPLEMENTED synchronously | Luồng xóa recipe dọn object ảnh; chưa có Hangfire cleanup job riêng |
| PostgreSQL integration tests | IMPLEMENTED | Testcontainers tests có cho migration rỗng, nâng cấp dữ liệu legacy, sample seeding và step concurrency; `PostgresMigrationTests` đạt 2/2 ngày 2026-11-17 |
| Redis live-service verification | ENVIRONMENT-DEPENDENT | Không ghi nhận kết quả chạy lại Redis thật trong lần cập nhật tài liệu |
| Sample/report data | PARTIAL / distinct paths | Development sample seed nạp 20 recipes; report helper và Robot suite còn nhưng không tự chạy |

## FR-RCP-010 — Recipe step management

The backend exposes authenticated step management routes under `/api/v1/recipes/{id}/steps`:

- `POST /api/v1/recipes/{id}/steps` creates a step. The server assigns `StepNumber` as the current maximum (including soft-deleted rows) plus one; clients cannot choose the number.
- `PUT /api/v1/recipes/{id}/steps/{stepId}` replaces `Title`, `Description`, optional `DurationMinutes`, and optional `ImageUrl`. The server-owned `StepNumber` is unchanged.
- `DELETE /api/v1/recipes/{id}/steps/{stepId}` soft-deletes the selected step and renumbers active steps contiguously from 1. Existing deleted rows are moved to unused negative numbers to avoid collisions with historical step numbers.

Only the recipe owner or an administrator can mutate its steps. Missing recipes/steps return 404; invalid payloads return 400 Problem Details; unauthorized ownership returns 403. Mutations invalidate the recipe detail cache and request recipe-list prefix invalidation through the existing cache contract.

The `AddActiveRecipeStepOrderIndex` PostgreSQL migration increases `RecipeSteps.Title` from 150 to 200 characters and adds a unique `(RecipeId, StepNumber)` index filtered to active rows. FR-RCP-010 handler tests cover number assignment, content updates, soft delete/renumbering, authorization, validation and historical deleted-number collisions. HTTP integration tests cover authentication and Problem Details validation responses. Verification recorded with this implementation: targeted tests passed 9/9; full solution build passed with 0 warnings and 0 errors; all solution tests passed 68/68 (Application 45, Integration 20, Architecture 3). EF tooling discovered the migration as pending at that time; it was not applied to PostgreSQL. PostgreSQL Testcontainers migration/concurrency tests exist in the repository. The step-renumbering race documented as `TECH-RISK-012` is not fully eliminated by these changes. No tests were rerun for this documentation update.

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
