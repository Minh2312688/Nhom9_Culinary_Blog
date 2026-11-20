# Hướng dẫn đọc luồng code và triển khai tính năng

## 1. Mục đích và phạm vi

Tài liệu này giúp Dương Văn Minh lần theo luồng xử lý khi sửa hoặc triển khai tính năng thuộc:

- Recipe backend, dữ liệu, tìm kiếm và cache của TV2.
- Frontend Core & UI/UX được chuyển giao từ TV3: shared layout, public pages, category/search pages và Recipe dashboard.

README là nguồn tra cứu phân công hiện tại. Trần Hữu Phan Lâm đã rời nhóm; phạm vi Frontend Core & UI/UX ban đầu của TV3 hiện do Dương Văn Minh tiếp nhận. Phân công không xác định ai là tác giả ban đầu của từng file.

Trong frontend, nhiều thành phần là **function component** hoặc **hook**, không phải class theo cú pháp C#. Tài liệu dùng “class/thành phần” theo nghĩa rộng khi lập bản đồ. Các lớp và file cốt lõi được giải thích theo luồng; danh sách chi tiết, kịch bản nghiệm thu và quy tắc từng tính năng đã có tài liệu riêng thì dẫn liên kết thay vì chép lại.

## 2. Cách đọc theo luồng xử lý

Hãy bắt đầu ở nơi người dùng tương tác, rồi lần theo dữ liệu đến nơi lưu trữ và quay lại:

1. **Route/page**: URL nào mở màn hình?
2. **Component**: component nào vẽ form, danh sách hoặc chi tiết?
3. **Hook/query/mutation**: dữ liệu được tải hoặc gửi đi bằng hook nào?
4. **API client/BFF**: URL, HTTP method và payload thực tế là gì?
5. **API endpoint**: endpoint nhận request và chuyển nó vào Application thế nào?
6. **Command/query handler**: validation, quyền và nghiệp vụ ở đâu?
7. **Domain/repository/context**: dữ liệu được đọc, thay đổi và lưu thế nào?
8. **Cache/DTO/response**: cache nào bị ảnh hưởng, kết quả nào được trả về giao diện?

### 2.1 Luồng tạo hoặc sửa công thức

```text
/dashboard/recipes/new hoặc /dashboard/recipes/{id}/edit
  -> NewRecipePage / EditRecipePage
  -> RecipeEditor
  -> React Hook Form + Zod
  -> useCreateRecipe / useUpdateRecipe
  -> createRecipe / updateRecipe trong recipes-api.ts
  -> /api/backend/recipes (BFF, gắn Bearer token)
  -> RecipeEndpoints
  -> MediatR pipeline (audit, logging, validation)
  -> CreateRecipeCommandHandler / UpdateRecipeCommandHandler
  -> IRecipeRepository hoặc IApplicationDbContext
  -> Recipe, Ingredients, Steps, Nutrition
  -> ApplicationDbContext.SaveChangesAsync()
  -> PostgreSQL
  -> xóa/invalidate cache
  -> RecipeDetailDto trả về UI, React Query cập nhật cache giao diện
```

Ảnh được xử lý thành các request riêng sau khi recipe được lưu, vì upload ảnh cần `recipeId` (và ảnh bước cần `stepId`). Khi cập nhật, ảnh/xóa ảnh được xử lý theo trạng thái đang chờ trong editor; nếu upload lỗi, UI báo rằng recipe đã lưu nhưng phần ảnh chưa hoàn tất để người dùng có thể thử lại.

### 2.2 Luồng đọc và tìm kiếm công thức công khai

```text
/ hoặc /recipes hoặc /search hoặc /recipes/{slug}
  -> HomePage / danh sách / RecipeSearchPage / RecipeDetailPage
  -> useRecipes / useSearchRecipes hoặc server recipes-api
  -> publicRequest tới API (không qua BFF xác thực)
  -> RecipeEndpoints / RecipeSearchEndpoints
  -> GetRecipesQuery + validator + GetRecipesQueryHandler
  -> IRecipeRepository; với tìm kiếm: RecipeRepository.SearchAsync
  -> PostgreSQL, bộ lọc/quyền/sắp xếp/phân trang
  -> IRecipeCache / DistributedRecipeCache khi phù hợp
  -> RecipeSummaryDto hoặc RecipeDetailDto
  -> component dựng kết quả
```

Tìm kiếm công khai dùng `GET /api/v1/recipes/search`; client chuyển tham số tìm kiếm thành `q`, bộ lọc, sắp xếp và phân trang. PostgreSQL Full-Text Search, chuẩn hóa dấu và xếp hạng nằm phía backend, không nằm trong component giao diện.

## 3. Bản đồ file và class/thành phần chính

### 3.1 Frontend: route, component và data hooks

| Route / vai trò | File hoặc thành phần | Công dụng trong luồng |
|---|---|---|
| Layout dùng chung | [layout.tsx](../frontend/src/app/layout.tsx), [providers.tsx](../frontend/src/app/providers.tsx) | Khung ứng dụng, font/metadata và provider dùng chung như session, React Query. |
| Điều hướng và UI dùng chung | [site-ui.tsx](../frontend/src/app/site-ui.tsx) | `SiteHeader`, `SiteFooter`, `SitePage`, `SectionTitle`, `RecipeCard`, trạng thái loading/error, phân trang. |
| Trang chủ `/` | [page.tsx](../frontend/src/app/page.tsx) | `HomePage` tải các công thức mới nhất bằng `useRecipes`, hiển thị thẻ công thức. |
| Danh sách `/recipes` | [recipes/page.tsx](../frontend/src/app/recipes/page.tsx) | Danh sách công khai và điều khiển phân trang/lọc nếu có. |
| Chi tiết `/recipes/{slug}` | [recipes/\[slug\]/page.tsx](<../frontend/src/app/recipes/[slug]/page.tsx>), [recipe-detail-client.tsx](<../frontend/src/app/recipes/[slug]/recipe-detail-client.tsx>) | Server component tải dữ liệu/metadata; client component hiển thị nội dung và tương tác. |
| Danh mục | [categories/page.tsx](../frontend/src/app/categories/page.tsx), [categories/\[slug\]/page.tsx](<../frontend/src/app/categories/[slug]/page.tsx>) | Trang danh sách danh mục và công thức của một danh mục. |
| Tìm kiếm `/search` | [search/page.tsx](../frontend/src/app/search/page.tsx) | `RecipeSearchPage` quản lý state filter, URL, debounce và `useSearchRecipes`. |
| Hồ sơ tác giả | [authors/\[id\]/page.tsx](<../frontend/src/app/authors/[id]/page.tsx>), [author-profile-client.tsx](<../frontend/src/app/authors/[id]/author-profile-client.tsx>) | Tải và trình bày thông tin tác giả cùng danh sách công thức công khai. |
| Hồ sơ cá nhân | [profile/page.tsx](../frontend/src/app/profile/page.tsx) | Đọc/sửa profile và đổi mật khẩu qua session, query/mutation hooks. |
| Đăng nhập/đăng ký/khôi phục mật khẩu | [auth/](../frontend/src/app/auth) | Form UI nối NextAuth và API register/forgot/reset password. |
| Tổng quan dashboard | [dashboard/page.tsx](../frontend/src/app/dashboard/page.tsx), [dashboard-shell.tsx](../frontend/src/app/dashboard/dashboard-shell.tsx) | Overview và navigation trong dashboard. |
| Quản lý recipe | [dashboard/recipes/page.tsx](../frontend/src/app/dashboard/recipes/page.tsx) | `DashboardRecipesPage` tải danh sách của user hoặc toàn hệ thống nếu là admin; xử lý publish/archive/delete. |
| Quản lý category (admin) | [dashboard/categories/page.tsx](../frontend/src/app/dashboard/categories/page.tsx) | Tạo/sửa/xóa category; upload ảnh và cho retry khi ảnh lỗi sau khi category đã lưu. |
| Xem recipe private | [dashboard/recipes/\[id\]/page.tsx](<../frontend/src/app/dashboard/recipes/[id]/page.tsx>) | Tải recipe theo quyền owner/admin; hiển thị loading/error hoặc private detail. |
| Tạo/sửa recipe | [dashboard/recipes/new/page.tsx](../frontend/src/app/dashboard/recipes/new/page.tsx), [dashboard/recipes/\[id\]/edit/page.tsx](<../frontend/src/app/dashboard/recipes/[id]/edit/page.tsx>), [recipe-editor.tsx](../frontend/src/app/dashboard/recipe-editor.tsx) | Hai route dùng chung `RecipeEditor`; prop `editing` chọn chế độ tạo hoặc sửa. |
| Query hooks | [queries/recipes.ts](../frontend/src/lib/queries/recipes.ts) | `useRecipes`, `useSearchRecipes`, `useDashboardRecipes`, `useRecipeForEdit`. Quản lý query key, trạng thái tải và gọi API client. |
| Mutation hooks | [queries/recipe-mutations.ts](../frontend/src/lib/queries/recipe-mutations.ts) | Tạo/sửa/xóa/publish/archive/upload ảnh; invalidates các query liên quan để UI đồng bộ. |
| API client | [recipes-api.ts](../frontend/src/lib/recipes-api.ts) | TypeScript request/response types, `publicRequest`, `privateRequest` và các hàm HTTP recipe. |
| BFF riêng tư | [api/backend/\[...path\]/route.ts](<../frontend/src/app/api/backend/[...path]/route.ts>) | Kiểm tra path/method, đọc hoặc refresh session token, proxy request xác thực sang API .NET. |

### 3.2 Backend: API → Application → Domain → Infrastructure

| Tầng | Class / nhóm file | Công dụng |
|---|---|---|
| Startup | [Program.cs](../backend/src/CulinaryBlog.API/Program.cs) | Đăng ký service, middleware, policy và endpoint; trong Development chạy migration rồi gọi các seed theo cấu hình. |
| Seed tài khoản Admin | [AdminAccountSeeder.cs](../backend/src/CulinaryBlog.Infrastructure/Persistence/Seed/AdminAccountSeeder.cs) | Khi `AdminSeed:Enabled` bật, bảo đảm role Admin và tài khoản cấu hình tồn tại; tạo user/role còn thiếu, không đổi mật khẩu user đã có. |
| Seed dữ liệu mẫu | [Lab02DataSeeder.cs](../backend/src/CulinaryBlog.Infrastructure/Persistence/Seed/Lab02DataSeeder.cs) | Khi `Seed:Enabled` bật, bảo đảm category, ảnh category, recipe mẫu và ảnh recipe; bỏ qua ảnh category đã có `ImageUrl`. |
| Nạp sample data | [SampleRecipeDataLoader.cs](../backend/src/CulinaryBlog.Infrastructure/Persistence/Seed/SampleRecipeDataLoader.cs), [SampleCategoryImageLoader.cs](../backend/src/CulinaryBlog.Infrastructure/Persistence/Seed/SampleCategoryImageLoader.cs) | Đọc JSON recipe và ánh xạ ảnh category theo slug tên file trong `tools/sample-data/images/danhmuc/`; kiểm tra folder và từng ảnh trước khi upload. |
| Category query/cache | [GetCategoriesQueryHandler.cs](../backend/src/CulinaryBlog.Application/Features/Categories/Queries/GetCategories/GetCategoriesQueryHandler.cs), [DistributedCategoryCache.cs](../backend/src/CulinaryBlog.Infrastructure/Persistence/DistributedCategoryCache.cs) | Cache danh sách; khi cache hit vẫn đọc `ImageUrl` hiện tại từ DB để ảnh mới seed không bị giá trị cũ che khuất. |
| HTTP API | [RecipeEndpoints.cs](../backend/src/CulinaryBlog.API/Endpoints/RecipeEndpoints.cs) | Khai báo GET/POST/PUT/PATCH/DELETE cho recipe, kiểm tra authorization ở biên HTTP và gửi request qua `ISender`. |
| HTTP API phụ | [RecipeSearchEndpoints.cs](../backend/src/CulinaryBlog.API/Endpoints/RecipeSearchEndpoints.cs), `RecipeIngredientEndpoints.cs`, `RecipeStepEndpoints.cs`, `RecipeImageEndpoints.cs` | Route riêng cho tìm kiếm, nguyên liệu, bước nấu và ảnh. `RecipeEndpoints` gọi các hàm `Map...Endpoints` để đăng ký nhóm này. |
| Application đăng ký | [DependencyInjection.cs](../backend/src/CulinaryBlog.Application/DependencyInjection.cs) | Đăng ký MediatR handlers, behaviors, FluentValidation validators và mapper. |
| Commands | [Commands/](../backend/src/CulinaryBlog.Application/Features/Recipes/Commands) | Thay đổi recipe: create/update/delete, publish/archive, ingredient, step và image. Mỗi request thường đi cùng handler; một số có validator. |
| Queries | [Queries/](../backend/src/CulinaryBlog.Application/Features/Recipes/Queries) | Đọc danh sách, chi tiết riêng tư theo ID và chi tiết công khai theo slug. |
| Query/search chính | [GetRecipes.cs](../backend/src/CulinaryBlog.Application/Features/Recipes/Queries/GetRecipes.cs) | Query contract, validator và handler cho danh sách/lọc/tìm kiếm/phân trang/cache. |
| Command tạo chính | [CreateRecipeCommand.cs](../backend/src/CulinaryBlog.Application/Features/Recipes/Commands/CreateRecipe/CreateRecipeCommand.cs), [CreateRecipeCommandValidator.cs](../backend/src/CulinaryBlog.Application/Features/Recipes/Commands/CreateRecipe/CreateRecipeCommandValidator.cs) | Payload yêu cầu, handler tạo recipe và kiểm tra input trước khi xử lý. |
| Command sửa chính | [UpdateRecipeCommand.cs](../backend/src/CulinaryBlog.Application/Features/Recipes/Commands/UpdateRecipe/UpdateRecipeCommand.cs) | Sửa recipe, children/nutrition, kiểm tra quyền sở hữu và RowVersion concurrency. |
| Mapping/response | [RecipeMapping.cs](../backend/src/CulinaryBlog.Application/Features/Recipes/RecipeMapping.cs), [RecipeDtos.cs](../backend/src/CulinaryBlog.Application/DTOs/Recipes/RecipeDtos.cs) | Chuyển Domain entity thành DTO được trả qua API. |
| Abstraction dữ liệu | [IRecipeRepository.cs](../backend/src/CulinaryBlog.Application/Contracts/Persistence/IRecipeRepository.cs), `IApplicationDbContext`, `IRecipeCache` | Application phụ thuộc interface, không gắn trực tiếp với lớp lưu trữ hoặc Redis. |
| Domain | [Recipe.cs](../backend/src/CulinaryBlog.Domain/Entities/Recipe.cs), [RecipeIngredient.cs](../backend/src/CulinaryBlog.Domain/Entities/RecipeIngredient.cs), [RecipeStep.cs](../backend/src/CulinaryBlog.Domain/Entities/RecipeStep.cs), [RecipeNutrition.cs](../backend/src/CulinaryBlog.Domain/Entities/RecipeNutrition.cs), [RecipeImage.cs](../backend/src/CulinaryBlog.Domain/Entities/RecipeImage.cs), `RecipeStatus`, `RecipeDifficulty` | Mô hình dữ liệu/nghiệp vụ; `Recipe.Publish` bảo vệ quy tắc phải có ingredient và step. |
| Repository | [RecipeRepository.cs](../backend/src/CulinaryBlog.Infrastructure/Repositories/RecipeRepository.cs) | Truy vấn EF Core và PostgreSQL Full-Text Search; tạo/xóa entity qua `IApplicationDbContext`. |
| Database | [ApplicationDbContext.cs](../backend/src/CulinaryBlog.Infrastructure/Persistence/ApplicationDbContext.cs), [RecipeConfiguration.cs](../backend/src/CulinaryBlog.Infrastructure/Persistence/Configurations/RecipeConfiguration.cs) | DbSet, mapping schema/quan hệ/index/query filter, concurrency token và xử lý RowVersion/soft delete khi lưu. |
| Cache | [DistributedRecipeCache.cs](../backend/src/CulinaryBlog.Infrastructure/Persistence/DistributedRecipeCache.cs) | Redis-backed cache, chuẩn hóa key, TTL và generation key để vô hiệu hóa nhóm list/search. |
| Ảnh | [UploadRecipeImageCommand.cs](../backend/src/CulinaryBlog.Application/Features/Recipes/Commands/UploadRecipeImage/UploadRecipeImageCommand.cs), [RecipeImageJobEnqueuer.cs](../backend/src/CulinaryBlog.Infrastructure/Images/RecipeImageJobEnqueuer.cs), [RecipeImageProcessingJob.cs](../backend/src/CulinaryBlog.Infrastructure/Images/RecipeImageProcessingJob.cs), [RecipeImageProcessor.cs](../backend/src/CulinaryBlog.Infrastructure/Images/RecipeImageProcessor.cs) | Lưu ảnh qua storage/job, sinh các biến thể và cập nhật trạng thái xử lý; xem thêm endpoint ảnh và integration tests. |

Các request ingredient/step/image và vòng đời draft/published/archive nằm trong cùng feature Recipe nhưng có endpoint và command riêng. Khi sửa một luồng con, bắt đầu trong thư mục [Commands](../backend/src/CulinaryBlog.Application/Features/Recipes/Commands) hoặc endpoint tương ứng; không thêm logic nghiệp vụ vào `Program.cs`.

## 4. Đọc các điểm khởi đầu quan trọng

### 4.1 `Program.cs`: cách ứng dụng API được lắp ráp

`Program.cs` dùng **top-level statements**: không cần tự khai báo `class Program` hay `Main`. Runtime tạo entry point từ các câu lệnh ở cấp file. Đọc file theo thứ tự:

| Câu lệnh / khối | Ý nghĩa và tác dụng |
|---|---|
| `using ...;` | Đưa namespace vào scope để tên như `RateLimiter`, `RecipeEndpoints` hoặc `DbContext` có thể dùng trực tiếp. `using` ở đây không tạo object/dispose; khác với `using var` ở khối seed bên dưới. |
| `var builder = WebApplication.CreateBuilder(args);` | Tạo builder từ arguments/configuration/environment. `var` yêu cầu compiler suy ra kiểu `WebApplicationBuilder`; nó không có nghĩa biến không có kiểu. |
| `builder.AddObservabilitySerilog();` | Cài cấu hình logging Serilog trước khi build ứng dụng. |
| `builder.Services.AddApplication()` | Đăng ký Application handlers, validators, behaviors và mapper. |
| `AddInfrastructure(builder.Configuration, builder.Environment)` | Đăng ký database, repository, cache và các dịch vụ hạ tầng theo cấu hình/environment. |
| `AddObservabilityHealthChecks`, `AddObservabilityOpenTelemetry`, `AddHttpContextAccessor`, `AddScoped<ICurrentUserService,...>` | Đăng ký health/telemetry, truy cập HTTP context và service đọc user hiện tại trong request. `AddScoped` tạo một instance trong scope của mỗi HTTP request. |
| `GetSection(...).Get<string[]>()` và `ValidateAndFilterAllowedOrigins(...)` | Đọc CORS origins từ config rồi kiểm tra/lọc theo chính sách môi trường. Không mặc định dùng wildcard. |
| `AddCors(options => { ... })` | Đăng ký default CORS policy; lambda `options =>` truyền cấu hình vào callback. Chỉ gọi `WithOrigins` khi danh sách đã được kiểm tra không rỗng. |
| `AddAuthorization(...AddPolicy...)` | Định nghĩa policy Admin và Author. Policy chỉ định role yêu cầu; endpoint phải gắn `RequireAuthorization(...)` để dùng policy. |
| `AddRateLimiter(...)` | Cấu hình mã HTTP 429, response `Retry-After`, global per-IP limiter và các policy giới hạn cho auth/upload. `async`/`await` trong `OnRejected` cho phép ghi JSON response bất đồng bộ. |
| Cấu hình bên trong rate limiter | `GlobalLimiter` dùng fixed window 100 request/phút/IP; auth policy dùng sliding window 10/phút với 6 segment; upload policy dùng fixed window 5/phút. `PartitionedRateLimiter` giữ quota theo IP. Header `X-Test-Client-IP` chỉ được dùng để phân vùng trong Development/Testing. |
| `var app = builder.Build();` | Kết thúc cấu hình service và tạo web application để gắn middleware/route. |
| `UseMiddleware<CorrelationIdMiddleware>()` | Gắn correlation ID vào request/response để liên kết log giữa các tầng. |
| `UseSerilogRequestLogging(...)` | Ghi request; `EnrichDiagnosticContext` thêm HTTP method/user ID, còn `GetLevel` chọn mức log theo exception/status/path (health check ở Debug, lỗi server ở Error). |
| `UseMiddleware<ApiExceptionMiddleware>()`, `UseCors()`, `UseRateLimiter()`, `UseAuthentication()`, `UseMiddleware<RequestLogContextMiddleware>()`, `UseAuthorization()` | Đăng ký middleware theo thứ tự. Exception handler chuyển lỗi thành response; CORS và rate limit kiểm soát request; authentication dựng danh tính; request log context đọc user sau authentication; authorization thực thi policy. Đổi thứ tự có thể làm middleware không thấy thông tin mà nó cần. |
| `app.MapGet("/", ...)` | Đăng ký health/info route ở `/`. Lambda trả một object qua `Results.Ok`, được serialize thành JSON. |
| `app.MapRecipeEndpoints()` và `Map...Endpoints()` khác | Gọi extension methods để đăng ký route theo feature. Đây là điểm nối giữa startup với class endpoint, không phải nơi chứa logic CRUD. |
| `MapRecipeEndpoints` bên trong `RecipeEndpoints` | `MapGroup("/api/v1/recipes")` gom prefix/tag; `MapGet/MapPost/MapPut/...` khai báo route và callback; `RequireAuthorization` chặn request chưa đủ quyền; `WithName` đặt tên endpoint; `Produces`/`ProducesProblem` mô tả HTTP response cho API metadata/OpenAPI. |
| `if (app.Environment.IsDevelopment())` | Chỉ chạy migration/seed trong Development; bỏ qua seed nếu provider là EF InMemory. `using var scope = ...` tạo scope service và tự dispose ở cuối khối. `GetRequiredService<T>()` ném lỗi nếu service bắt buộc chưa được đăng ký. |
| `await db.Database.MigrateAsync()` | Áp dụng migrations trước khi seed. Sau đó `AdminSeed:Enabled` điều khiển seed tài khoản Admin (mặc định false trong API); `Seed:Enabled` điều khiển sample data (mặc định true). Compose có thể override các giá trị này bằng biến môi trường. |
| `AdminAccountSeeder.SeedAsync(...)` | Bảo đảm role Admin tồn tại; tìm user theo email cấu hình; chỉ tạo user nếu chưa có và chỉ gán role nếu user chưa là Admin. Email/password rỗng hoặc Identity operation thất bại sẽ dừng startup bằng exception rõ ràng. |
| `Lab02DataSeeder.SeedAsync(...)` | Đọc `Seed:SampleDataPath` (Compose mount `/app/sample-data`), đảm bảo category và upload ảnh category có tên file khớp slug khi `ImageUrl` còn trống; sau đó seed recipe/ảnh mẫu. Seed bổ sung dữ liệu thiếu, không xóa dữ liệu hiện có. |
| `if (!app.Environment.IsEnvironment("Testing")) ... MapHangfireDashboard(...)` | Không đăng ký Hangfire dashboard trong test environment; khi đăng ký, dashboard gắn filter Admin. |
| `app.Run();` | Khởi chạy web server và bắt đầu nhận request. |

### 4.2 Luồng tạo recipe: frontend

1. `NewRecipePage` chỉ dựng `<RecipeEditor />`; trang sửa dựng `<RecipeEditor editing />`. Hai route dùng chung form để không nhân đôi nghiệp vụ giao diện.
2. `RecipeEditor` tạo React Hook Form bằng `useForm<RecipeEditorFormValues>`. `zodResolver(recipeEditorSchema)` nối schema Zod vào form; lỗi field được lưu trong `formState.errors`.
3. `useFieldArray` quản lý danh sách ingredient có thể thêm/xóa. Step và ảnh có state riêng vì editor hỗ trợ bước có cấu trúc, preview, upload muộn và retry.
4. `onSubmit(values)` chỉ được gọi qua `handleSubmit(onSubmit)`, nên validation form chạy trước. Hàm chuyển giá trị HTML (thường là string) thành kiểu API (`Number(...)`, `null`, arrays) để tạo `RecipeInput`.
5. Nếu `editing`, mutation gửi `id`, payload và `rowVersion`; nếu tạo mới, mutation gọi `createRecipe`. `rowVersion` giúp backend phát hiện hai lần sửa đồng thời.
6. `useCreateRecipe`/`useUpdateRecipe` dùng React Query `useMutation`; khi thành công `invalidateRecipeQueries` đánh dấu list/detail/search liên quan để lần đọc sau lấy dữ liệu mới.
7. API client `privateRequest` gửi request tới `/api/backend/...`; BFF kiểm tra route/method, lấy/refresh access token và proxy sang `/api/v1/...` với header Bearer. Không gửi token từ browser trực tiếp đến API.
8. Khi recipe đã lưu, editor upload ảnh ở request riêng. Vì vậy lỗi upload được báo riêng, không giả vờ rằng việc lưu recipe thất bại.

### 4.3 Luồng tạo recipe: backend

1. `RecipeEndpoints.MapRecipeEndpoints` đăng ký `POST /api/v1/recipes`, yêu cầu xác thực và bind JSON body vào `CreateRecipeCommand`.
2. Endpoint gọi `sender.Send(command, ct)`. `ISender` gửi request vào MediatR; endpoint chỉ chuyển HTTP request thành application request và đổi kết quả thành HTTP `201 Created`.
3. `DependencyInjection.AddApplication` quét assembly để đăng ký handler và validator. Các behaviors ghi audit/log và chạy validation trước handler theo pipeline đã đăng ký.
4. `CreateRecipeCommandValidator` kiểm tra title, category ID, thời gian, servings, difficulty, ingredient và step. Validation bảo vệ input, nhưng không thay cho kiểm tra quyền/nghiệp vụ trong handler hoặc Domain.
5. `CreateRecipeCommandHandler.Handle` xác nhận user được phép sửa, kiểm tra category tồn tại, sinh slug không trùng, tạo `Recipe` ở trạng thái `Draft`, thêm ingredient/step/nutrition rồi gọi repository và `SaveChangesAsync`.
6. Handler xóa cache `recipes:` sau khi ghi DB để list/search không trả dữ liệu cũ; sau đó `ToDetail()` chuyển entity thành DTO response.
7. `RecipeConfiguration` quyết định schema, quan hệ, unique slug, concurrency token và soft-delete filter. `ApplicationDbContext.SaveChangesAsync` đặt audit time/RowVersion và đổi delete thành soft delete cho entity kế thừa `BaseEntity`.

### 4.4 Luồng tìm kiếm

1. `RecipeSearchPage` giữ filter trong state, đồng bộ query string và debounce 300 ms để tránh request trên mỗi phím gõ.
2. `useSearchRecipes` canonicalizes query params rồi dùng chúng làm React Query key; `searchRecipes` gọi API public với query string.
3. `RecipeSearchEndpoints` bind `q` và `RecipeSearchRequest`, sau đó tạo `GetRecipesQuery` và gửi qua MediatR.
4. `GetRecipesQueryValidator` giới hạn pagination, filter và sort fields. `GetRecipesQueryHandler` áp quyền (published/mine/admin), cache phù hợp, và chọn nhánh search hoặc list.
5. Search gọi `RecipeRepository.SearchAsync`, dùng `SearchVector` PostgreSQL, `unaccent`, ranking, filter, sort và paging. List không có search dùng query EF bình thường.
6. Handler/repository trả `PaginatedResult<RecipeSummaryDto>`; component hiển thị loading/error/empty/result thay vì biết chi tiết PostgreSQL.

## 5. Từ khóa và khái niệm cần nhận ra

Mỗi mục chỉ giải thích một lần; trong các phần luồng bên trên, hãy tra cứu lại mục này thay vì gán nghĩa mới cho cùng thuật ngữ.

| Từ / ký hiệu | Ý nghĩa trong repo |
|---|---|
| `Guid` | Kiểu .NET biểu diễn định danh 128-bit (UUID), thường dùng cho ID recipe/category/step. Không phải từ khóa ngôn ngữ. Nếu “guild” trong yêu cầu là `Guid`, đây là kiểu mà `Guid?` có thể không có giá trị. |
| `Task` / `Task<T>` | Kiểu .NET biểu diễn công việc bất đồng bộ; `Task<T>` còn mang kết quả kiểu `T`. Khác “task” trong kế hoạch dự án, là một đầu việc. |
| `async` / `await` | `async` cho phép method dùng `await`; `await` chờ I/O mà không block thread trong lúc chờ. Dùng với DB, cache, HTTP. |
| `CancellationToken` (`ct`) | Tín hiệu để hủy request/DB operation khi client ngắt hoặc request bị dừng; cần truyền tiếp xuống dependency bất đồng bộ. |
| `record` | Kiểu dữ liệu C# phù hợp cho request/DTO thiên về dữ liệu; có value-based equality và hỗ trợ `with` để tạo bản sao có thay đổi. `CreateRecipeCommand` là record, handler mới là class. |
| `interface` (`IRecipeRepository`) | Hợp đồng các thao tác; Application phụ thuộc interface, còn Infrastructure cung cấp implementation. Giảm phụ thuộc trực tiếp giữa các layer. |
| DI / `AddScoped` | Dependency Injection cấp implementation qua constructor; `Scoped` thường dùng một instance trong suốt một HTTP request. |
| MediatR `IRequest`, `IRequestHandler`, `ISender` | Request mô tả thao tác; handler xử lý; sender gửi request mà không cần endpoint tự khởi tạo handler. |
| Validator / FluentValidation | Quy tắc kiểm tra request trước khi handler chạy; lỗi validation thường trở thành HTTP 400 qua exception middleware/behavior. |
| DTO | Data Transfer Object: hình dạng dữ liệu API trả/nhận. Không nên trả EF entity trực tiếp vì entity còn có trạng thái/quan hệ nội bộ. |
| `IQueryable<T>` | Mô tả truy vấn có thể được EF dịch thành SQL; query chưa chạy đến khi gọi terminal operation như `ToListAsync` hoặc `CountAsync`. |
| `DbSet<T>` / `DbContext` | `DbSet<T>` đại diện tập entity trong EF; `DbContext` quản lý truy vấn, tracking, mapping và lưu thay đổi. |
| React function component | Hàm trả về UI React; `RecipeEditor` là function component, không phải `class RecipeEditor extends React.Component`. |
| React hook (`useState`, `useEffect`) | API dùng trong function component để giữ state hoặc đồng bộ side effect theo vòng đời. |
| React Query `useQuery` / `useMutation` | `useQuery` tải và cache dữ liệu đọc; `useMutation` gửi thay đổi và chạy callback cập nhật/invalidate cache UI. |
| Zod / `zodResolver` | Zod mô tả schema validation phía frontend; resolver nối schema đó vào React Hook Form. Backend vẫn phải validate độc lập. |
| BFF | Backend-for-Frontend tại `/api/backend`; giữ token phía server, kiểm soát method/path và proxy request riêng tư. Không thay authorization policy của API .NET. |
| Cache invalidation | Làm cho entry cache cũ không còn được sử dụng sau khi data đổi. Recipe cache dùng generation key cho list/search; React Query invalidation là cache riêng ở browser. |
| Extension method `MapRecipeEndpoints(this ...)` | Method tĩnh được gọi như method instance nhờ tham số đầu tiên có `this`; giúp `Program.cs` đăng ký route ngắn gọn. |
| `=>` | Lambda expression: biểu thức hoặc thân hàm ngắn truyền vào method khác; trong C# có thể là callback/endpoint, trong TypeScript thường là callback/hàm. |
| `?`, `??`, `!` | `?` có thể đánh dấu nullable type hoặc optional chaining tùy ngôn ngữ/context; `??` chọn giá trị dự phòng khi null; `!` sau biểu thức C# là null-forgiving, báo compiler rằng code đảm bảo không null (không thêm runtime check). |
| `with { ... }` | C# tạo bản sao record với một hoặc nhiều property được đổi, ví dụ endpoint gắn route `id` vào `UpdateRecipeCommand`. |

> **Phân biệt hai loại cache:** Redis/backend cache tránh truy vấn lặp ở server; React Query/browser cache giữ kết quả giữa các lần render/navigate. Invalidate một cache không đồng nghĩa cache kia tự được xóa.

## 6. Các bước triển khai một tính năng mới

1. **Chốt yêu cầu và phạm vi.** Tìm requirement ID, tiêu chí nghiệm thu, quyền, trạng thái và lỗi mong đợi. Kiểm tra phân công hiện tại trong [README](../README.md); đừng suy ra ownership từ lịch sử commit.
2. **Tìm đường đi hiện có.** Tìm route/component, endpoint, query/command, entity và test cùng loại trước khi tạo file mới. Dùng các mẫu đã có trong feature Recipe.
3. **Vẽ request/response và luồng.** Ghi rõ browser → API/BFF → handler → DB/cache → response; phân biệt public API với private API qua BFF.
4. **Chốt data contract.** Nếu có input mới, cập nhật frontend type/schema, API request binding, command/query và response DTO tương ứng. Giữ tên/kiểu đồng nhất.
5. **Đặt validation đúng tầng.** UX validation ở Zod; request validation ở FluentValidation; quyền ở endpoint/policy và handler; quy tắc bất biến của entity ở Domain.
6. **Viết Application behavior.** Tạo command/handler cho thay đổi hoặc query/handler cho đọc. Handler điều phối use case, không đưa SQL/React/UI vào đây.
7. **Cập nhật data/infrastructure nếu cần.** Thêm property/quan hệ/config/migration cho DB; repository chỉ khi cần truy vấn đặc thù; cập nhật cache key/invalidation sau thay đổi ảnh hưởng kết quả.
8. **Nối HTTP endpoint.** Khai báo method, route, authorization, status code và gửi request bằng `ISender`. Nếu tạo endpoint group mới, đăng ký extension method trong startup theo pattern hiện có.
9. **Nối UI.** Thêm/chỉnh page/component, form state và loading/error/empty/success; gọi API qua `recipes-api.ts`, query/mutation qua hooks; không đặt URL/HTTP rải rác trong component.
10. **Kiểm tra trải nghiệm và bảo mật.** Thử vai trò chủ sở hữu/admin/guest phù hợp; rà soát IDOR, không lộ draft, concurrency, CSRF/BFF và responsive/accessibility nếu UI thay đổi.
11. **Thêm test theo ranh giới.** Unit test handler/validator/domain rule; integration test endpoint/DB/cache khi phù hợp; Playwright cho user flow. Viết test chứng minh yêu cầu, bao gồm lỗi và quyền.
12. **Chạy xác minh nhỏ nhất có ý nghĩa.** Chạy test class/feature, lint/typecheck hoặc build tương ứng; sau đó chạy rộng hơn nếu test phụ thuộc hoặc thay đổi xuyên nhiều layer.
13. **Cập nhật tài liệu liên quan.** Sửa spec/checklist nếu trạng thái yêu cầu đổi; không sao chép lại phần đã có trong tài liệu này.

## 7. Chọn test để đọc theo luồng

| Mục đích | Test/nhóm test bắt đầu từ |
|---|---|
| Endpoint create/detail/search | `backend/tests/CulinaryBlog.Integration.Tests/Endpoints/RecipeCreationEndpointsTests.cs`, `RecipePrivateDetailEndpointTests.cs`, `RecipeSearchEndpointsTests.cs` |
| Ingredient/step/image endpoints | `RecipeIngredientEndpointsTests.cs`, `RecipeStepEndpointsTests.cs`, `RecipeImageEndpointsTests.cs` |
| CRUD và wizard trên UI | `frontend/e2e/recipe-crud-flow.spec.ts`, `frontend/e2e/srs-routes.spec.ts` |
| API private/BFF và quyền | `frontend/e2e/backend-bff.spec.ts`, `frontend/e2e/admin-users.spec.ts` |
| Query cache sau điều hướng | `frontend/e2e/query-cache.spec.ts` |
| Search behavior | `frontend/e2e/srs-routes.spec.ts`, `frontend/e2e/query-cache.spec.ts` |

Tên test thể hiện hành vi mong đợi; khi điều tra bug, đọc test trước để biết setup, role, request và assertion, rồi lần ngược theo bản đồ ở mục 2.

## 8. Tài liệu chi tiết đã có

- [README: phân công và trạng thái hiện tại](../README.md)
- [Traceability yêu cầu và owner](./srs-audit/TEAM-REQUIREMENT-TRACEABILITY.md)
- [Recipe core implementation spec](./recipe-core-implementation-spec.md)
- [FR-RCP-009: kế hoạch và checklist editor/database](./features/FR-RCP-009-recipe-editor-testcontainers-plan.md), [checklist](./features/FR-RCP-009-recipe-editor-testcontainers-checklist.md)
- [FR-RCP-010: spec bước nấu/cache/E2E](./features/FR-RCP-010-recipe-steps-cache-e2e-spec.md), [checklist](./features/FR-RCP-010-recipe-steps-cache-e2e-checklist.md)
- [FR-SRCH-001 spec](./features/FR-SRCH-001-implementation-spec.md), [FR-SRCH-003/004 spec](./features/FR-SRCH-003-004-implementation-spec.md)
- [Frontend UI implementation checklist](./features/FR-UI-001-implementation-checklist.md)

## 9. Backend: công dụng và câu lệnh của từng hàm

`Program.cs` không khai báo hàm/class tường minh vì dùng top-level statements. Phần khai báo service, callback middleware, route và seed đã được giải thích theo thứ tự chạy ở mục 4.1; không lặp lại ở đây.

Trong các bảng dưới, “khai báo ngoài hàm” gồm field, property, constant, record parameter và dependency constructor. Phần “các câu lệnh” liệt kê thao tác/nhánh theo đúng thứ tự thực thi; cú pháp chung như `Task`, `Guid`, lambda và DI đã giải thích ở mục 5.

### 9.1 API endpoints

#### `RecipeEndpoints.MapRecipeEndpoints`

- **Ngoài hàm:** `RecipeEndpoints` là `static class`, không giữ state; hàm nhận `IEndpointRouteBuilder` và trả lại builder để chain đăng ký route.
- **Câu lệnh trong hàm:**
  1. `MapGroup("/api/v1/recipes").WithTags("Recipes")` tạo prefix và nhóm OpenAPI.
  2. Route admin nhận page/pageSize, gán mặc định 1/12, gửi `GetRecipesQuery(IncludeUnpublished: true)`, trả `200`, gắn Admin policy và metadata response.
  3. Route list công khai bind query string vào `GetRecipesQuery`, gửi MediatR và trả `200`.
  4. Route `/mine` tạo query `OnlyMine: true`, bắt buộc đăng nhập, trả danh sách của user.
  5. Route `/{id:guid}` đọc chi tiết riêng tư theo ID và yêu cầu authorization; route `/{slug}` đọc chi tiết public theo slug.
  6. `POST /` bind `CreateRecipeCommand`, gửi command, escape slug trong `Location` và trả `201 Created`.
  7. `PUT /{id:guid}` dùng ID trong route ghi đè ID trong body bằng `command with { Id = id }`, sau đó gửi handler và trả `200`.
  8. `PATCH .../publish`, `PATCH .../archive`, `DELETE /{id:guid}` tạo command tương ứng và trả `204 No Content`.
  9. Gọi các hàm đăng ký search, step, ingredient; trả lại `endpoints`.

#### Các hàm map endpoint phụ

| Hàm | Câu lệnh/nhánh chính |
|---|---|
| `RecipeSearchEndpoints.MapRecipeSearchEndpoints` | Tạo cùng recipe group; bind query `q` và `[AsParameters] RecipeSearchRequest`; chép filter vào `GetRecipesQuery`; gửi qua `ISender`; trả `200` và ghi metadata lỗi 400. |
| `RecipeIngredientEndpoints.MapRecipeIngredientEndpoints` | Đăng ký POST/PUT/DELETE với authorization; bind route IDs và body record; tạo command; gửi handler; trả `201`, `200` hoặc `204`. |
| `RecipeStepEndpoints.MapRecipeStepEndpoints` | Đăng ký POST/PUT/DELETE step và POST ảnh step; với multipart kiểm tra content type/file trước khi lấy stream; gửi command; trả DTO/status phù hợp. |
| `RecipeImageEndpoints.MapRecipeImageEndpoints` | Tạo nhóm route có authorization và rate-limit upload; POST kiểm tra multipart, file và `isPrimary`, gửi upload command; PATCH chọn ảnh chính; DELETE xóa ảnh. |

#### Request records

- `RecipeSearchRequest`: `Page=1`, `PageSize=12`, `CategoryId`, `Difficulty`, `MaxCookTimeMinutes`, `MinServings`, `MaxServings`, `SortBy`, `SortOrder`. Default page/page size cho phép bỏ query parameter.
- `AddRecipeIngredientRequest`: `Name`, `Quantity`, `Unit`, `Notes`, `OrderIndex`; `ReplaceRecipeIngredientRequest` có cùng các trường để PUT thay thế giá trị child.
- `AddRecipeStepRequest`: `Title`, `Description`, `DurationMinutes`, `ImageUrl`; `ReplaceRecipeStepRequest` mang cùng các trường để thay thế step. `StepNumber` do server tính, không tin client gửi.

### 9.2 Đăng ký Application, contracts và mapping

#### `DependencyInjection.AddApplication`

- **Ngoài hàm:** static extension class; các `using` chỉ rút gọn tên namespace.
- **Theo thứ tự trong hàm:** thêm MediatR và quét assembly hiện tại để tìm handlers; đăng ký `AuditLoggingBehavior`, `LoggingBehavior`, `ValidationBehavior`; quét FluentValidation validators; đăng ký `IAuditTrail`; cấu hình Mapster quét mapping; đăng ký config singleton và `IMapper` scoped; trả `IServiceCollection` để tiếp tục chain.
- Cách tìm đúng handler: tìm `IRequest<T>`/`IRequestHandler<TRequest,TResponse>` cùng loại request; behavior bọc request chung, không thay thế handler.

#### Persistence và service interfaces

- `IRecipeRepository`: `Query`/`Steps` trả `IQueryable`; `SearchAsync` nhận query, user/role, filter/sort/page và cancellation token; `Add`, `Remove`, `AddStep` mô tả thao tác mà implementation phải cung cấp.
- `IApplicationDbContext`: các `DbSet` cho Recipe và child entities; `SetOriginalRowVersion` hỗ trợ concurrency; `SaveChangesAsync` là cổng ghi thay đổi.
- `IRecipeCache`: hợp đồng Get/Set/Remove/RemoveByPrefix; application có thể invalidate mà không biết Redis details.
- `IRecipeImageJobEnqueuer`: chỉ lộ `EnqueueAsync(imageId, ct)` để handler không phụ thuộc trực tiếp Hangfire.
- `IFileStorageService`, `IFileValidationService`, `FileUploadRequest`, `FileUploadResult`, `FileValidationResult`: mô tả upload/download/delete, kiểm tra file và dữ liệu kết quả; handler nhận abstraction thay vì tự thao tác object storage.
- `StorageFolders.ForRecipe` từ chối ID rỗng rồi tạo thư mục theo GUID; `IsValid` kiểm tra độ dài/từng segment; `IsValidSegment` từ chối segment rỗng hoặc ký tự không hợp lệ.
- `ImageFileFormats` cung cấp giới hạn/allowlist và ánh xạ extension/MIME/display name; các hàm kiểm tra MIME và extension bảo đảm không chỉ tin tên file do client gửi.
- `ImageMagicBytes.Detect` thử signature PNG, JPEG, RIFF/WebP rồi ISO-BMFF/AVIF; `MatchesAt` bounds-check trước khi so byte; `IsAvifBrand` kiểm tra vùng brand. Kết quả không hợp lệ là `null`.
- `RecipeMetrics.RecordCreated/RecordPublished` tăng counter; `RecordDuration` chỉ ghi metric cho operation/outcome được hỗ trợ, kèm tag để phân tích.

#### DTO và mapping

- Các record trong `RecipeDtos.cs` mô tả shape response cho summary, detail, author, ingredient, step, image, nutrition và upload.
- `RecipeMapping.ToSummary`: lọc ảnh `IsPrimary`, sắp theo `OrderIndex`, chọn ảnh đầu; dựng DTO từ thuộc tính scalar và RowVersion.
- `RecipeMapping.ToDetail`: dựng DTO scalar; sắp xếp ingredients/steps/images theo thứ tự; map từng child; nutrition thành null hoặc DTO; author để null để handler profile có thể bổ sung khi cần.

### 9.3 Query handlers

#### `GetRecipesQueryValidator`

Constructor đăng ký rule theo field: page ≥ 1; page size 1–50; cook-time/servings dương nếu có; difficulty thuộc allowlist; search tối đa 100 ký tự; không cho Search khi `OnlyMine`; sort field thuộc allowlist; chỉ nhận asc/desc; không nhận sort order nếu thiếu sort field. Mỗi `When` chỉ áp rule khi field có giá trị liên quan.

#### `GetRecipesQueryHandler`

- **Ngoài hàm:** constants đặt ngưỡng popularity và TTL; fields giữ repository, current user, optional cache; constructor nhận chúng từ DI.
- **`Handle(request, cancellationToken)`:**
  1. Nếu query muốn draft nhưng user không admin, ném Forbidden.
  2. Nếu có search, tách token chữ/số và chuẩn hóa lowercase; chỉ cache search khi không có filter khó đưa vào key.
  3. Với cacheable query, thử đọc result; nếu miss thì tăng popularity counter và chỉ lưu kết quả search khi đạt ngưỡng.
  4. Nếu không trả từ cache, gọi `recipes.SearchAsync` với user/admin scope, paging/filter/sort.
  5. Không có search: tạo list cache key; trả cache hit nếu có.
  6. Bắt đầu `recipes.Query.AsNoTracking()`, giới hạn admin/owner/public theo status và user ID.
  7. Áp category, difficulty, time và servings filters nếu được truyền.
  8. Chọn thứ tự sort có tie-breaker ID để paging ổn định; sort field lạ ném lỗi.
  9. Đếm tổng; nếu page vượt totalPages, trả page rỗng nhưng giữ total metadata và cache kết quả rỗng.
  10. Skip/Take theo page; project chỉ scalar và ảnh primary; gọi `ToListAsync`.
  11. Map row sang `RecipeSummaryDto`, tạo paginated result, ghi cache nếu bật và trả kết quả.
- `NormalizeDifficulty`: TryParse không phân biệt hoa thường, chỉ trả enum nếu defined; input không hợp lệ thành null.
- `BuildSearchCacheKey`: ghép query/page/sort/onlyMine/scope, SHA-256 rồi tạo prefix search.
- `BuildListCacheKey`: ghép các filter đã normalize cùng scope, hash SHA-256 rồi tạo prefix list. Version `v3` tách key mới khỏi định dạng cũ.

#### `GetRecipeBySlugQueryHandler`

- Query record giữ slug; validator yêu cầu không rỗng và giới hạn độ dài.
- `Handle`: thử cache slug; nếu miss thì query recipe theo slug với image/ingredient/step/nutrition, dùng split query/no tracking; nếu không có thì 404; `CanView` kiểm tra published hoặc chủ sở hữu; nếu không được xem thì 403; map detail, thêm author public profile, cache kết quả trong TTL và trả.
- `IncludePublicAuthorAsync`: nếu author ID/identity service không hợp lệ thì giữ DTO không có author; nếu profile hoạt động, tạo bản DTO có author name/avatar.
- `CanView`: cho xem Published; nếu chưa publish chỉ current owner có thể xem.

#### `GetPrivateRecipeByIdQueryHandler`

Primary constructor giữ `IApplicationDbContext` và current user. `Handle` load detail graph theo ID bằng Include/split/no-tracking; không thấy thì 404; user không phải admin và không là owner thì 403; ngược lại map sang `RecipeDetailDto`.

### 9.4 Command handlers và validators

#### Tạo và cập nhật recipe

Các luồng chính đã được mô tả ở mục 4.2–4.3. Bổ sung chi tiết ngoài hàm:

- `CreateRecipeCommand` positional fields là request body; implementation `IAuditCommand` cung cấp action/entity metadata mà không thêm constructor parameter.
- Constructor `CreateRecipeCommandHandler` giữ repository, category repository, unit of work, user context và cache.
- `EnsureCanEdit`: nếu chưa xác thực hoặc không có user ID thì ném Forbidden; không tự trả success.
- `Slugify`: delegate cho `RecipeCommandSlug.Slugify`.
- `AddChildren`: vòng `foreach` tạo ingredient đã trim; vòng step tăng `StepNumber` từ 1; nếu Nutrition có thì tạo child nutrition.
- `CreateRecipeCommandValidator`: các `RuleFor` kiểm tra title/description markup, category, times, servings, difficulty; `RuleForEach(...).ChildRules(...)` áp rule tới từng ingredient/step; `IsSupportedDifficulty` parse enum, xác nhận enum defined và giá trị chuỗi phù hợp.
- `UpdateRecipeCommandHandler.Handle`: load recipe với child graph; check owner/category; giữ slug cũ và so sánh slug của title; set original RowVersion; update scalar fields; đổi slug nếu title-base thay đổi; nếu collection được gửi thì soft-delete/clear và thêm child thay thế; nutrition có thể xóa, restore hoặc update; save và đổi EF concurrency exception thành domain/application conflict; invalidate slug cũ/mới và prefix cache; trả detail DTO.
- `EnsureOwner`: admin có thể sửa mọi recipe; user thường phải trùng `AuthorId`.
- `UpdateRecipeCommandValidator`: giống nhóm kiểm tra create, cộng ID/RowVersion và các rule children; `IsSupportedDifficulty` dùng cùng kiểm tra enum như create.

#### Các request record còn lại

| Record | Properties và tác dụng |
|---|---|
| `CreateRecipeCommand` | `Title`, `Description`, `CategoryId`, `PrepTimeMinutes`, `CookTimeMinutes`, `Servings`, `Difficulty`, `Ingredients`, `Steps`, `Nutrition`; child collection/nutrition mặc định null để handler phân biệt không gửi với danh sách rỗng. |
| `UpdateRecipeCommand` | Có các field cơ bản như Create, thêm `Id`, `RowVersion`; `Ingredients`, `Steps`, `Nutrition` là optional để handler chỉ thay children khi payload có cung cấp. |
| `AddRecipeIngredientCommand` | `RecipeId`, `Name`, `Quantity`, `Unit`, `Notes`, `OrderIndex`; xác định recipe đích và giá trị ingredient mới. |
| `UpdateRecipeIngredientCommand` | Thêm `IngredientId` để chỉ rõ child cần sửa, cộng các field value như add. |
| `DeleteRecipeIngredientCommand` | `RecipeId`, `IngredientId`; xác định child cần xóa, không cần DTO body. |
| `AddRecipeStepCommand` | `RecipeId`, `Title`, `Description`, `DurationMinutes`, `ImageUrl`; tạo step gắn recipe, số thứ tự do server xác định. |
| `UpdateRecipeStepCommand` | Thêm `StepId` để chỉ rõ step cần sửa, cộng các giá trị step. |
| `DeleteRecipeStepCommand` | `RecipeId`, `StepId`; xác định step cần xóa và sắp lại thứ tự. |
| `UploadRecipeImageCommand` | `RecipeId`, `Content`, `FileName`, `ContentType`, `Length`, `AltText`, `IsPrimary=false`; content stream và metadata đi cùng tùy chọn ảnh chính. `MaxImagesPerRecipe=10`. |
| `UploadRecipeStepImageCommand` | `RecipeId`, `StepId`, `Content`, `FileName`, `ContentType`, `Length`; gắn file upload vào đúng step. |
| `DeleteRecipeImageCommand` | `RecipeId`, `ImageId`; handler xóa object storage và xử lý ảnh primary thay thế. |
| `SetPrimaryRecipeImageCommand` | `RecipeId`, `ImageId`; chọn một ảnh làm primary. |
| `PublishRecipeCommand`, `ArchiveRecipeCommand`, `DeleteRecipeCommand` | Mỗi record có `Id`; Publish/Delete kèm metadata audit theo contract `IAuditCommand`. Tách thao tác lifecycle để handler/audit xử lý đúng use case. |

`RecipeImageProcessor` tạo thumbnail `300×300` và medium `800×600`; `RecipeImageJobEnqueuer` đưa job vào Hangfire queue `image-processing` với 3 retry. Cache generation key có TTL 24 giờ. Các hằng số này ảnh hưởng vận hành, không phải properties của request records.

Các request record chỉ mang input; chúng không tự thực hiện ghi DB. Tác dụng của từng giá trị được thấy khi đối chiếu validator và handler tương ứng trong các thư mục liên kết ở mục 3.2.

#### Publish/archive/delete

- `PublishRecipeCommand`, `ArchiveRecipeCommand`, `DeleteRecipeCommand` là request records chỉ giữ ID; Publish/Delete có thêm audit metadata.
- `RecipeLifecycleHandler` fields giữ DbContext, current user, cache, storage optional; các overload `Handle(Publish...)` và `Handle(Archive...)` chỉ gọi `ChangeStatus` với status/flag phù hợp.
- `Handle(DeleteRecipeCommand)`: load recipe đã kiểm tra quyền; gọi xóa file storage cho từng URL khác nhau; nếu storage trả lỗi thì vẫn invalidate cache rồi ném lỗi đầu tiên; nếu xóa object thành công, remove entity, save DB, invalidate slug/prefix cache.
- `DeleteStoredObjectsAsync`: loại URL null/trùng; thử xóa từng object; lưu exception đầu tiên nhưng tiếp tục cleanup các object còn lại; trả exception hoặc null.
- `ChangeStatus`: load recipe; publish gọi Domain `Recipe.Publish` để enforce ingredient/step; archive đổi status; save DB; invalidate caches.
- `GetEditableRecipe`/`GetRecipeForDelete`: query recipe và required children; không tồn tại -> 404; không có quyền -> 403.

#### Ingredient và step

| Hàm/validator | Các câu lệnh theo chức năng |
|---|---|
| `RecipeIngredientCommandHelpers.GetEditableRecipeAsync` | Load recipe; 404 nếu không thấy; 403 nếu không authenticated/owner/admin; trả entity cho handler. |
| `RecipeIngredientCommandHelpers.InvalidateAsync` | Xóa cache theo slug và prefix recipe. |
| `TrimToNull` / `ToIngredientDto` | Chuỗi trống thành null, nếu có thì trim; map entity sang DTO. |
| `AddRecipeIngredientCommandHandler.Handle` | Lấy recipe editable; tạo ingredient với tên trim, field và order index; add vào context; save; invalidate cache; trả DTO. |
| `UpdateRecipeIngredientCommandHandler.Handle` | Lấy recipe editable; tìm ingredient thuộc recipe; 404 nếu không có; thay field; save; invalidate. |
| `DeleteRecipeIngredientCommandHandler.Handle` | Lấy recipe editable; tìm child; 404 nếu không có; remove; save; invalidate. |
| `AddRecipeIngredientCommandValidator` | Check recipe ID, tên required/maxlength/no markup; quantity dương khi có; unit/notes độ dài; order index không âm. |
| `UpdateRecipeIngredientCommandValidator` | Rule add cộng ID ingredient bắt buộc. |
| `RecipeStepCommandHelpers` | Cùng pattern quyền/cache/trim như ingredient; `ToStepDto` map step properties thành DTO. |
| `AddRecipeStepCommandHandler.Handle` | Lấy recipe editable; truy vấn số step lớn nhất kể cả soft-deleted; chặn overflow; tạo step kế tiếp; save; invalidate; trả DTO. |
| `UpdateRecipeStepCommandHandler.Handle` | Tìm recipe/step thuộc recipe; 404 nếu thiếu; trim/update các field; save; invalidate; trả DTO. |
| `DeleteRecipeStepCommandHandler.Handle` | Load các step; tìm active target; đặt step đã xóa sang số âm để tránh unique-index conflict; soft-delete; save; đánh số lại các step còn lại và lưu; invalidate. |
| `AddRecipeStepCommandValidator` / `UpdateRecipeStepCommandValidator` | Validate ID, description required, title/image length/markup, duration không âm; update yêu cầu step ID. |
| `DeleteRecipeStepCommandHandler` | Remove theo quy tắc soft-delete/reorder nói trên; không trả DTO. |

#### Ảnh recipe và ảnh bước

- `RecipeImageAccess.GetOwnedRecipeAsync`: load recipe; 404 nếu không tồn tại; 403 nếu user không xác thực hoặc không là owner/admin; trả recipe.
- `RecipeImageAccess.InvalidateRecipeCacheAsync`: xóa cache detail theo slug và invalidates prefix.
- `UploadRecipeImageCommandValidator`: yêu cầu recipe ID, stream, file name hợp lệ và giới hạn alt text.
- `UploadRecipeImageCommandHandler.Handle`: xác nhận quyền; kiểm tra giới hạn số ảnh; lưu file gốc; quyết định ảnh primary (ảnh đầu tiên luôn primary); reset primary cũ khi cần; thêm entity với order tiếp theo; save; nếu DB save lỗi, xóa object vừa upload rồi ném lỗi; invalidate cache; enqueue sinh biến thể theo cách best-effort; trả upload DTO.
- `UploadRecipeStepImageCommandHandler.Handle`: xác nhận quyền/step; upload ảnh mới; đổi URL; save; nếu save lỗi xóa object mới; sau khi thành công thử xóa URL cũ; invalidate cache; trả Step DTO.
- `DeleteRecipeImageCommandHandler.Handle`: xác nhận quyền; tìm ảnh trong recipe; nếu xóa ảnh primary thì chọn ảnh đầu còn lại; xóa original/medium/thumbnail; ghi nhận lỗi đầu tiên nhưng thử cleanup cả ba; nếu lỗi thì ném lỗi; nếu sạch thì remove entity/save/invalidate.
- `SetPrimaryRecipeImageCommandHandler.Handle`: xác nhận quyền/ảnh thuộc recipe; đặt `IsPrimary` chỉ trên ảnh đã chọn; save; invalidate; map DTO.
- `RecipeImageJobEnqueuer.EnqueueAsync`: từ chối GUID rỗng; kiểm tra cancellation; enqueue `RecipeImageProcessingJob.ProcessAsync` vào Hangfire với token job.
- `NoOpRecipeImageJobEnqueuer.EnqueueAsync`: dùng khi không bật Hangfire/test; kiểm tra cancellation và log cảnh báo thay vì ném lỗi.
- `RecipeImageProcessingJob.ProcessAsync`: gọi core; audit success, canceled hoặc failure rồi giữ exception để Hangfire retry.
- `ProcessCoreAsync`: validate ID; load image + Recipe; download original; xử lý variants; nhánh Unsupported lưu status và invalidate; nhánh Ready upload thumbnail/medium, gán URL/status rồi save; nếu lỗi dọn các variant đã upload; invalidate recipe sau khi cập nhật.
- `InvalidateRecipeCacheAsync`: xóa detail key theo slug và prefix list/search.
- `CleanupVariantAsync`: nếu variant tồn tại, cố xóa object; lỗi cleanup được log và không che lỗi xử lý chính.
- `RecipeImageProcessor.CreateVariantsAsync`: kiểm tra stream; giữ/khôi phục vị trí stream; đọc bytes; AVIF trả Unsupported; decode ảnh khác; decode thất bại ném lỗi; tạo thumbnail và medium PNG; trả bytes/content type/extension/status.
- `CreatePngVariant`: tạo bitmap RGBA đích; crop theo tỉ lệ vào vùng giữa; render, flush và encode PNG.
- `Decode`: gọi decoder; unsupported input thành null.
- `IsAvif`/`HasAvifBrand`: kiểm tra container brand/compatible brand trong byte header để tránh gửi format không hỗ trợ qua decoder.

### 9.5 Domain, repository, database và cache

#### Domain declarations và methods

- `BaseEntity`: `Id` mặc định `Guid.NewGuid`; `CreatedAt`/`UpdatedAt` chứa audit time; `IsDeleted` hỗ trợ soft delete; `RowVersion` dùng concurrency. `Delete()` bật cờ xóa và cập nhật UTC time.
- `Recipe`: scalar properties mô tả nội dung/owner/category/status; navigation collections biểu diễn ingredients/steps/images và nutrition. `Publish(publishedAt)` từ chối nếu thiếu ingredient hoặc step; lần đầu set PublishedAt UTC; cuối cùng chuyển status Published.
- `RecipeIngredient`, `RecipeStep`, `RecipeImage`, `RecipeNutrition`: child entities; properties tương ứng field editor/API. Không có business method riêng trong các file này; invariant/quan hệ được enforce ở handler/EF configuration.
- `RecipeStatus`, `RecipeDifficulty`, `RecipeImageVariantStatus`: enums cho trạng thái publish, mức khó và trạng thái variant; không khai báo hàm.

#### Repository và DbContext

- `RecipeRepository.Query`/`Steps`: trả `DbSet` dưới dạng `IQueryable`, query được compose ở Application.
- `SearchAsync`: tokenize terms thành prefix terms; nếu rỗng trả trang rỗng; build `to_tsquery` với `unaccent`; giới hạn status/owner/admin; áp filter category/difficulty/time/servings; lọc `SearchVector`; count và xử lý page ngoài phạm vi; sort theo rank hoặc allowlist; paging; project summary fields/primary image; map DTO.
- `Add`, `Remove`, `AddStep`: chỉ gọi `DbSet.Add/Remove`; ghi thật xảy ra khi UnitOfWork/DbContext save.
- `ApplicationDbContext` constructor: forward `DbContextOptions` sang `IdentityDbContext`.
- Các `DbSet` properties: expose bảng user/token/category/recipe/children cho EF.
- `SetOriginalRowVersion`: lấy tracked entry rồi đặt `RowVersion` OriginalValue; EF dùng giá trị cũ trong điều kiện update concurrency.
- `OnModelCreating`: gọi base Identity mapping; áp tất cả configuration trong persistence namespace; cấu hình refresh token key/index/required properties; chỉ với Npgsql mới thêm `SearchVector` tsvector và GIN index.
- `SaveChangesAsync`: duyệt `ChangeTracker` trên BaseEntity; Added đặt CreatedAt và RowVersion mới; Modified đặt UpdatedAt/RowVersion mới; Deleted chuyển sang Modified và bật IsDeleted; cuối cùng gọi EF base save.
- `ApplicationDbContextFactory.CreateDbContext`: đọc connection string design-time; ném lỗi nếu thiếu; cấu hình Npgsql options và dựng context.

#### EF configuration

Các class `IEntityTypeConfiguration<T>.Configure(builder)` là mapping, không được gọi thủ công từ handler. `OnModelCreating` quét class thuộc namespace persistence.

- `RecipeConfiguration.Configure`: chọn bảng/key; required/max length; enum lưu chuỗi; timestamp type; unique slug; author/category FK và delete behavior; child relationship cascade; RowVersion concurrency; query filter `!IsDeleted`.
- `RecipeIngredientConfiguration.Configure`: table/key; tên/unit/notes, quantity precision; order index; concurrency và soft-delete filter.
- `RecipeStepConfiguration.Configure`: table/key; độ dài title/description/image; unique active step index và soft-delete filter.
- `RecipeImageConfiguration.Configure`: table/key, URL/alt length, variant enum/status mặc định, filter soft-delete.
- `RecipeNutritionConfiguration.Configure`: table/key, precision các giá trị dinh dưỡng, filter soft-delete.
- Hai file mapping ở namespace root `CulinaryBlog.Infrastructure.Configurations` có class tên giống persistence configurations. `ApplicationDbContext` hiện quét **namespace persistence**; khi thay mapping phải xác định đúng class đang được nạp, tránh sửa nhầm file trùng tên.

#### `DistributedRecipeCache`

- Ngoài hàm: constants giữ generation key, generation đầu và TTL; fields giữ `IDistributedCache`/logger; constructor nhận qua DI.
- `GetAsync<T>`: resolve key theo generation; lấy string; null trả default; deserialize JSON; lỗi Redis/timeout/JSON log warning rồi fallback default để query DB.
- `SetAsync<T>`: resolve key; serialize JSON; đặt absolute TTL; lỗi Redis/timeout log warning và tiếp tục không cache.
- `RemoveAsync`: normalize key rồi xóa; lỗi Redis/timeout log warning.
- `RemoveByPrefixAsync`: sinh generation GUID mới, ghi generation với TTL và log; việc đổi generation làm list/search keys cũ không được tra lại; lỗi Redis/timeout ghi warning.
- `ResolveKeyAsync`: normalize; chỉ list/search prefix mới được nối generation; các key khác trả normalized key.
- `NormalizeKey`: trim whitespace và lowercase.

### 9.6 Storage validation và metric hỗ trợ image

- `ImageFileFormat`: enum cho JPEG/PNG/WebP/AVIF; chỉ là tập lựa chọn, không có method.
- `ImageFileFormats`: constants cho 5 MiB và bytes sniff; list format/extension/MIME; switch helpers đổi format thành extension/MIME/name; allowlist helpers kiểm tra extension/MIME và cặp extension-format.
- `FileErrorCodes`: constants ổn định để client/test phân biệt loại validation failure.
- `ImageMagicBytes`: signatures byte theo format; `Detect` chọn format theo header thay vì chỉ tin extension; `MatchesAt` kiểm bounds rồi so từng byte; `IsAvifBrand` xét brand header.
- `StorageFolders.ForRecipe`: bảo đảm GUID khác rỗng và format folder; `IsValid` kiểm tổng độ dài và mọi path segment; `IsValidSegment` chặn segment rỗng/ký tự đường dẫn không cho phép.
- `RecipeMetrics`: meter/counter/histogram là field cấp class; `RecordCreated`/`RecordPublished` tăng counter; `RecordDuration` kiểm operation/outcome, bỏ metric không hỗ trợ và ghi duration với tags nếu hợp lệ.

### 9.7 Startup seed tài khoản và dữ liệu mẫu

- `AdminAccountSeeder.SeedAsync(userManager, roleManager, email, password)`: từ chối email/password rỗng; tạo role `Admin` nếu chưa có; tìm user theo email đã trim; tạo tài khoản `Administrator` nếu chưa tồn tại; thêm role Admin nếu tài khoản hiện tại chưa có role. `EnsureSucceeded` gom lỗi Identity và ném `InvalidOperationException`; seeder không đổi password của tài khoản đã tồn tại.
- `Lab02DataSeeder.SeedAsync(...)`: gọi loader để kiểm tra sample JSON/ảnh; bảo đảm các category chuẩn và category phát sinh từ `categorySlug`; upload ảnh category có filename stem khớp slug khi category chưa có `ImageUrl`; sau đó tạo author và recipe còn thiếu, thêm nutrition chỉ khi sample có dữ liệu, và upload các ảnh recipe còn thiếu theo `OrderIndex`. Seed không xóa recipe, không ghi đè nutrition có sẵn và không thay ảnh category đã có URL.
- `SampleCategoryImageLoader.Load(imagesDirectory)`: chỉ nhận `.webp`, `.jpg`, `.jpeg`, `.png`; dùng tên file bỏ extension làm category slug; báo lỗi nếu hai file trùng slug.
- `SampleRecipeDataLoader.Load(sampleDataDirectory)`: đọc `20_cong_thuc_mon_an.json`, hiện có 51 công thức; kiểm tra category/author/difficulty, ingredients/steps, nutrition nếu được cung cấp, thư mục và từng file ảnh. `nutrition: null` được giữ trống; không tự ước lượng số liệu.
- Ảnh category đặt trong `tools/sample-data/images/danhmuc/` và dùng tên khớp slug, ví dụ `mon-hap.jpg`. Ảnh recipe đặt trong thư mục tương ứng dưới `tools/sample-data/images/`; JSON khai báo `folder` và danh sách `images`.
- Chạy lại seed phát triển từ thư mục gốc repo:

  ```powershell
  docker compose up --build -d api
  docker compose logs --tail 100 api
  ```

  Lệnh này không xóa volume; database giữ dữ liệu cũ và seeder chỉ bổ sung dữ liệu còn thiếu. Không dùng `docker compose down -v` nếu cần giữ database hoặc ảnh MinIO.

## 10. Frontend: chi tiết từng component, hook và hàm

Mỗi file được đọc theo thứ tự khai báo ngoài component/function rồi đến các hàm và câu lệnh bên trong. Các luồng tổng quan ở mục 2 và 4 là sơ đồ; phần dưới là danh mục tra cứu chi tiết.

### 10.1 Shared UI, layout và providers

#### `layout.tsx`, `providers.tsx`

- `siteUrl`/`metadata` ở cấp module: lấy base URL và đặt title, description, Open Graph/Twitter mặc định.
- `RootLayout({ children })`: nhận nội dung route; render `<html lang="vi">`; gắn font/global CSS qua imports; bọc nội dung bằng `Providers`.
- `SessionQueryCacheSync()`: lấy session/status và previous user ID; khi session đã tải và ID người dùng đổi thì xóa React Query cache để không hiển thị dữ liệu của tài khoản cũ.
- `Providers({ children })`: khởi tạo QueryClient một lần bằng `useState`; bọc children lần lượt trong `QueryClientProvider`, `SessionProvider`, component đồng bộ cache và `FeedbackProvider`.

#### `site-ui.tsx`

- **Ngoài hàm:** import React, Link, Image, icon, session/profile hook, RecipeSummary type và CSS module; các import này quyết định component có thể render gì và lấy style nào.
- `BrandLink()`: trả link về `/`, logo và nhãn accessible.
- `SiteHeader()`: đọc Auth.js session/status; bật profile query chỉ khi đã đăng nhập; chọn tên/avatar fallback; xác định Admin; render nav public rồi nhánh link Admin/“Công thức của tôi” + profile hoặc link login.
- `SiteFooter({ message })`: render brand/footer; dùng chuỗi mặc định khi không truyền message.
- `SitePage({ children, className, footerMessage })`: lọc class rỗng, ghép CSS class; bọc header, nội dung, footer.
- `SectionTitle({ eyebrow, title, description })`: render eyebrow/h1; chỉ render description khi có.
- `RecipeCard({ recipe })`: link theo slug; ảnh primary nếu có; alt fallback sang title; render độ khó, thời gian, khẩu phần và description có điều kiện.
- `ErrorNotice`/`LoadingNotice`: render alert/status semantic để screen reader nhận lỗi/loading.
- `LoadingSkeleton({ variant })`: chọn nhãn và số skeleton theo `cards/detail/form`; render từng phần tử với key/index.
- `Pagination(...)`: nếu chỉ một trang thì trả `null`; nếu nhiều trang render previous/current/next; disable nút theo hasPrevious/hasNext; click gọi `onPageChange`.

#### Feedback và chuyển động

- `getFormErrorDetails(error, fieldNames)`: nếu không phải `ApiRequestError`, tạo summary từ Error/fallback; nếu là API error, đọc danh sách lỗi theo field, chuẩn hóa field name, ghép field khớp form, gom lỗi còn lại vào summary và trả cấu trúc fieldErrors.
- `FeedbackProvider`: giữ toast và timer; `dismissToast` clear timer rồi xóa toast; `showToast` đặt toast và timer khác nhau theo success/error; effect cleanup timer; render context provider cùng toast UI.
- `useFeedback()`: lấy context; nếu component nằm ngoài provider thì throw lỗi lập trình thay vì trả context giả.
- `BrandMotion({ children })`: đọc `prefers-reduced-motion`; render motion container với y/opacity animation; khi người dùng yêu cầu giảm chuyển động, duration bằng 0.
- `DashboardShell({ children })`: đọc pathname/session; xác định admin; lọc navigation categories/users khi không phải admin; render link active dựa trên pathname và children.
- `DashboardLayout({ children })`: chỉ bọc children trong `DashboardShell`, không tự tải dữ liệu.

### 10.2 Public pages và hàm tải dữ liệu

#### Trang chủ và danh sách

- `HomePage()`: gọi `useRecipes` trang 1, 6 item; lấy `items` hoặc mảng rỗng; render hero và brand motion; hiển thị pending/error/empty theo state; map từng recipe thành `RecipeCard`.
- `RecipesPage()`: giữ `page` và cờ `initialized`; query chỉ chạy khi state đã khởi tạo; effect đầu đọc page từ URL và xác nhận page hợp lệ; effect khác ghi page vào URL; render loading/error/empty/list/pagination theo kết quả.

#### Chi tiết recipe

- `generateMetadata({ params }, parent)`: await route slug; gọi server API lấy recipe; tạo metadata; khi recipe không có ảnh thì đọc OG/Twitter ảnh từ parent và ghép vào; lỗi fetch trả metadata dự phòng và `noindex`.
- `RecipeDetailPage({ params })`: await slug; server fetch; lỗi 404 và lỗi khác chọn message khác nhau cho `RecipeDetailError`; thành công tạo Recipe JSON-LD đã serialize, render `<script type="application/ld+json">` và `RecipeDetailClient`.
- `RecipeDetailClient({ recipe })`: khởi tạo detail query bằng dữ liệu server; sắp xếp ảnh/ingredients/steps; chọn primary/secondary image; render breadcrumb/header; render PublishedAt, author, description, ingredients, steps, nutrition và ảnh phụ khi có.
- `RecipeDetailError({ message })`: bọc `ErrorNotice` trong `SitePage`.
- `RecipeDetailLoading()`: render `LoadingSkeleton` variant detail.

#### Danh mục và tác giả

- `CategoriesPage()`: gọi categories query; nhánh loading/error/empty; render category card, image hoặc placeholder, description/count.
- `CategoryDetailPage()`: đọc slug; giữ page/initialized; gọi category detail và paginated recipe query; effect đọc page URL, effect đồng bộ URL; xử lý thiếu slug/loading/error; render category, recipe grid và pagination.
- `AuthorProfilePage({ params, searchParams })`: await params/query song song; parse page với fallback hợp lệ; server fetch profile; lỗi 404/lỗi chung tách biệt; render client profile.
- `AuthorProfileClient({ author })`: hydrate author query; render avatar hoặc chữ cái fallback; nhánh không có recipe; map list và tạo previous/next URL theo page.
- `AuthorProfileLoading()`: render cards skeleton.

#### `RecipeSearchPage` và helper

- Ngoài hàm, `SearchFilters` mô tả state form; `emptyFilters` là giá trị mặc định.
- `readFiltersFromUrl()`: tạo `URLSearchParams`; đọc q/search và các filter; chỉ nhận sort field allowlist; chuẩn hóa sortOrder; page không hợp lệ thành 1.
- `buildQuery(filters)`: luôn thêm page/pageSize; chỉ thêm keyword/filter có giá trị; sort mặc định relevance thì không gửi sortBy/order.
- `RecipeSearchPage()`: khởi tạo filters, initialized và debouncedFilters; gọi category/search hooks; effect đầu hydrate từ URL; effect ghi URL khi filter đổi; effect timer 300 ms cập nhật debounced filter và cleanup timer; `updateFilter` thay field và reset page nếu đổi filter; `clearFilters` reset tất cả; render category error, filter controls, loading/error/empty/results/pagination.

### 10.3 Dashboard recipe và `RecipeEditor`

#### `DashboardRecipesPage`

- Khai báo state `page`, `initialized`, `actionError`; đọc session/user/admin; query chọn scope all hoặc mine; tạo mutation publish/archive/delete.
- Effect khởi tạo page từ URL, effect khác đồng bộ URL và xóa action error; effect query error hiển thị toast.
- `handlePublish(id)`: lấy slug hiện có; gọi mutation; clear action error và toast success; catch lỗi, ghi action error và toast error.
- `handleArchive(id)`: cùng cấu trúc publish nhưng gọi archive và thông báo lưu trữ.
- `handleDelete(id)`: hỏi xác nhận trước; lấy slug; gọi delete; success clear error/toast; catch báo lỗi.
- `statusLabel(status)`: map status enum/string sang nhãn tiếng Việt; giá trị không nhận diện trả String(status).
- JSX cuối render heading/toolbar; branch loading/error/empty/table; mỗi row có link xem/sửa; action button phụ thuộc status/role; phân trang gọi `setPage`.

#### Khai báo module của `recipe-editor.tsx`

- `StructuredStep`: shape một bước gồm ID/number/title/description/duration/image và file/preview tạm.
- `ImageEntry`: ảnh đã lưu (ID, URL, alt, primary, order); `PendingGalleryImage`: ảnh mới local (file/preview/alt).
- `RecipeWizardStep`, `wizardSteps`: union và danh sách các bước wizard general → ingredients → steps → review.
- `generalFields`, `ingredientFormFields`: allowlist tên field truyền vào validation theo từng bước.
- `recipeEditorSchema`: schema Zod trim/giới hạn title; kiểm category; kiểm chuỗi thời gian/khẩu phần dạng số; giới hạn difficulty; validate từng ingredient; nhận text steps. `RecipeEditorFormValues` suy kiểu từ schema để tránh định nghĩa type lặp.
- Component state: RHF quản lý form; React state quản lý bước wizard, saving/error/conflict, nutrition, ảnh đang chờ, ảnh primary và retry ID. Derived values như `visibleImages`, pending flags, review values được tính từ state thay vì lưu bản sao thứ hai.

#### Helper và effect của `RecipeEditor`

- `parseStepsText(text)`: split newline → trim → bỏ dòng rỗng → tạo step có number bắt đầu 1, các field title/duration/image trống.
- Preview effects: general image tạo URL và revoke khi đổi; gallery/step effects so URL mới với tập URL đang giữ và revoke URL đã bỏ; effect unmount revoke toàn bộ URL còn lại để tránh leak.
- Default-category effect: chỉ ở create mode, nếu chưa chọn category và danh sách có dữ liệu thì chọn category đầu.
- Recipe-hydration effect: bỏ qua khi thiếu recipe/key hoặc đã hydrate đúng user/ID; nếu khác thì gọi `hydrateRecipe` và ghi hydration key.
- Missing-ID effect: đợi session hết loading; trong edit mode mà thiếu route ID thì set lỗi.
- Query-error effect: chọn category error hoặc private recipe error theo mode; lấy message và toast.
- `hydrateRecipe(detail)`: reset error/form; map/sort ingredient và đổi quantity thành string; map/sort structured steps hoặc clear nếu không có; set textarea steps; bật/map nutrition nếu có; map gallery images và khởi tạo primary/deletion state.
- `reloadRecipe()`: clear conflict/errors; refetch với `throwOnError`; nếu có data thì hydrate/ghi key; toast success; catch hiển thị lỗi reload.
- `clearFieldError(field)`: xóa server field error; switch map field API sang RHF field và clear lỗi tương ứng; clear global error.
- `addIngredientRow()`: append object ingredient rỗng; clear lỗi ingredients.
- `addStepRow()`: append step rỗng; gán số thứ tự tiếp theo; clear lỗi steps.
- `removeStepRow(index)`: lọc step theo index; đánh số lại; clear lỗi.
- `handleStepImageSelection(index, event)`: lấy file đầu; reject >5 MB và reset input; nếu hợp lệ tạo object URL; chỉ update step đúng index; clear lỗi.
- `updateStepField(index, field, value)`: copy array; return nếu index không tồn tại; update field của đúng step; clear lỗi.
- `moveStep(index, direction)`: tính target; return nếu ngoài bounds; swap hai phần tử; renumber; clear lỗi.
- `syncStepsFromText()`: bỏ qua textarea rỗng; parse thành structured steps; clear lỗi; toast thành công.
- `advanceWizard()`: ở general trigger general fields; ở ingredients trigger ingredients; nếu validation fail thì dừng; ở steps tìm description rỗng và focus field sai; parse text-only steps và reject rỗng; nếu qua hết mới sang review.
- `goToPreviousStep()`: tìm index; nếu index > 0 thì lùi một bước.

#### Helper ảnh và submit

- `handlePendingGalleryImageSelection(file)`: return nếu không có file; reject >5 MB; reject nếu đã đủ 10 ảnh; tạo pending item/preview; chọn primary mặc định nếu cần; clear field errors và reset input.
- `uploadPendingImage(targetRecipeId)`: return nếu không có file chờ; gọi upload mutation cùng primary/alt; thêm response vào gallery đã lưu; clear pending state/input.
- `handlePendingImageSelection(file)`: missing file clear pending; file quá lớn toast/reset; file hợp lệ lưu state và clear global error.
- `applyPendingGalleryChanges(targetRecipeId)`: tính cờ xóa ảnh cuối; snapshot danh sách và primary; upload từng ảnh pending; remap pending primary ID sang server ID; cập nhật local images; xóa từng ảnh đã queue; nếu primary bị xóa chọn ảnh còn lại; gọi set-primary mutation khi khác hiện tại; cập nhật local primary và trả cờ xóa hết.
- `uploadPendingStepImages(targetRecipeId, savedSteps)`: lặp step; skip không có file; chọn step ID cũ hoặc ID vừa lưu; thiếu ID thì throw; upload; cập nhật URL/preview local.
- `retryPendingImageUpload()`: return nếu chưa có saved recipe ID; set saving/clear error; retry ảnh chung và ảnh steps; thành công clear retry ID/toast/redirect; catch báo partial-save; finally clear saving.
- `handleSetPrimary(imageId)`: chỉ cập nhật pending primary ID.
- `handleDeleteImage(imageId)`: return nếu recipe ID thiếu hoặc user hủy confirm; remove ảnh khỏi gallery pending; queue ID ảnh đã lưu để xóa; nếu xóa primary thì chọn ảnh còn lại đầu tiên.
- `onSubmit(values)`: reject edit khi chưa load recipe; set saving và clear error/conflict; nếu có retry ID thì gọi retry rồi return; tạo step list từ structured state hoặc split textarea; map form fields sang `RecipeInput` (number/null/trim/arrays/nutrition); create hoặc update với RowVersion; copy step IDs trả về; áp pending gallery changes khi edit; upload ảnh chung/step; lỗi ảnh báo partial-save và dừng; success chọn toast theo create/edit/xóa ảnh cuối rồi redirect; catch 409 bật conflict UI, lỗi khác map server field errors và chuyển wizard tới bước chứa lỗi.
- `renderFieldError(field)`: map tên thời gian API về RHF name; ưu tiên server error rồi form error; không có lỗi trả null; có lỗi render `<span role="alert">` với ID tương ứng.
- Render bên ngoài hàm con: branch loading; branch conflict có nút reload; branch edit recipe lỗi; nếu hợp lệ render progress nav, form `handleSubmit(onSubmit)` và section đúng `activeStep`; nút trước/tiếp/lưu và control ảnh nằm trong JSX theo trạng thái.

### 10.4 Query hooks và cache giao diện

#### `queries/recipes.ts`

- `useRecipes(params)`: gọi `useQuery`; key gồm page/pageSize; queryFn gọi `getRecipes` và truyền signal; enabled theo params; giữ data cũ khi đổi trang.
- `useSearchRecipes(params, enabled)`: copy/sort URLSearchParams để query key canonical; queryFn tạo params mới rồi gọi `searchRecipes`; giữ data cũ trong lúc query mới.
- `useRecipe(slug, initialData)`: key detail theo slug; queryFn gọi public detail; server initialData giúp UI render trước query refetch.
- `useDashboardRecipes(userId, page, scope, enabled)`: query key phân biệt user/page/scope; nếu scope all gọi admin API, còn lại mine; chỉ bật khi userId có giá trị và caller cho phép.
- `useMyRecipes`: wrapper chuyển tiếp `scope="mine"`.
- `useRecipeForEdit`: private detail query keyed theo user/id/accessScope; chỉ gọi khi enabled và ID không rỗng.

#### `queries/recipe-mutations.ts`

- `invalidateRecipeQueries(...)`: lấy detail key; nếu mutation trả recipe thì ghi ngay vào query cache; xác định recipe public qua option/status; luôn invalidate dashboard list và detail; nếu public còn invalidate public lists/search và slug detail; await toàn bộ invalidation.
- `useCreateRecipe`: mutationFn `createRecipe`; success ghi response và invalidate key liên quan.
- `useUpdateRecipe`: nhận `{id,payload}`, gọi API update; success cập nhật detail và invalidate.
- `useDeleteRecipe`: gọi delete theo ID; success invalidate, xóa cả public cache.
- `usePublishRecipe`: gọi publish; success invalidate các list/detail public có thể thay đổi.
- `useArchiveRecipe`: gọi archive; success invalidate public list/search/detail.
- `useUploadRecipeImage`: mutation variables gồm recipeId/file/primary/alt; gọi upload; success invalidate recipe query.
- `useUploadRecipeStepImage`: mutation variables gồm recipeId/stepId/file; upload step image; invalidate recipe.
- `useSetPrimaryRecipeImage`: gọi API set primary theo recipe/image ID; invalidate.
- `useDeleteRecipeImage`: gọi API delete theo recipe/image ID; invalidate.

#### Các query hooks liên quan route được bàn giao

- `useCategories(enabled)`: query danh sách category theo key; enabled chặn request khi chưa cần.
- `useCategory(slug, enabled)`: query detail theo slug.
- `useCategoryRecipes(categoryId, page, enabled)`: nếu category ID rỗng thì disable; tạo query gồm category/page/pageSize; gọi `useSearchRecipes`; giữ data cũ khi paging.
- `useDashboardCategories(userId, enabled)`: query categories dashboard theo user cache key.
- `useAuthorProfile(id, page, initialData)`: author query keyed theo ID/page và hydrate từ server-provided data.
- `queryKeys` trong `query-keys.ts`: `recipes.all/lists/list/details/detail` tạo prefix/list/detail key; `categories.all/list/detail/recipes` tạo key danh mục; `search.all/recipes` canonicalize query thành key; `dashboard.recipeLists/recipes/recipeDetails/recipeDetail/categories` phân biệt user, page, scope và ID; `profile.detail`, `admin.users`, `authors.profile` giữ cache theo user/ID/page. Các hàm factory trả tuple `as const` để giữ literal types, và prefix là cơ sở invalidate theo nhóm.
- `createQueryClient()`: cấu hình stale time/retry; query lỗi mạng hoặc 5xx retry một lần; mutation không tự retry để tránh lặp write.

#### Auth/profile query và category mutations

- `useProfile(userId, enabled)`: query key theo user; queryFn truyền AbortSignal vào `getProfileApi`; bật query chỉ khi `enabled` và `userId` không rỗng.
- `useAdminUsers(enabled)`: dùng admin users key; gọi API có signal; `enabled` chặn truy vấn nếu màn hình/role chưa phù hợp.
- `useUpdateProfile(userId)`: tạo mutation gọi `updateProfileApi`; khi success ghi profile mới trực tiếp vào cache detail đúng user, tránh fetch lại ngay.
- `useChangePassword`, `useRegisterAccount`, `useForgotPassword`, `useResetPassword`: mỗi hook trả `useMutation` gọi API tương ứng; không tự retry vì đây là POST action.
- `invalidateCategoryQueries(userId, queryClient)`: chạy đồng thời invalidate dashboard categories của user, tất cả public category, recipe lists và search vì đổi category có thể ảnh hưởng các kết quả đó.
- `useCreateCategory(userId)`: mutationFn gọi create API; success đợi `invalidateCategoryQueries`.
- `useUpdateCategory(userId)`: nhận `{id,payload}`, gọi update với fields name/description/image/order; success invalidate category/list/search.
- `useDeleteCategory(userId)`: mutation nhận ID, gọi delete API, rồi invalidate.
- `useUploadCategoryImage(userId)`: nhận categoryId/file, gọi upload API, rồi invalidate để cập nhật ảnh.

### 10.5 Recipe API client và response mapping phía frontend

#### Khai báo ngoài function trong `recipes-api.ts`

- `API_BASE_URL`: endpoint public backend từ environment hoặc localhost fallback.
- Interfaces `PaginatedResult`, `RecipeSummary`, `RecipeDetail`, `RecipeIngredient`, `RecipeStep`, `RecipeCategory`, `RecipeNutritionInput`, `RecipeInput`, `ApiProblemDetails`: shape có kiểm soát cho request/response; `rowVersion` được truyền khi edit; optional/null phân biệt dữ liệu không bắt buộc.
- `ApiRequestError`: giữ HTTP status và ProblemDetails để component phân biệt lỗi; constructor ưu tiên detail, title, validation messages, rồi fallback message.

#### Hàm request dùng chung

- `parseProblemDetails(value)`: từ chối null/array/non-object; kiểm tra `errors`; chuẩn hóa message đơn lẻ thành mảng; chỉ giữ giá trị string; trả title/detail/errors hợp lệ.
- `request<T>(url, init, isPrivate)`: gọi `fetch`; khi response lỗi thì private 401 chuyển tới login; thử đọc JSON lỗi; throw `ApiRequestError`; 204 trả `undefined`; các status thành công khác parse JSON thành `T`.
- `publicRequest(path, init)`: nối path với API base, giữ init, đặt `credentials: "omit"`.
- `privateRequest(path, init)`: gửi tới `/api/backend${path}`, đặt `cache: "no-store"` và bật nhánh lỗi private.

#### Hàm HTTP theo use case

| Hàm | Câu lệnh chính trong hàm |
|---|---|
| `searchRecipes(query, signal)` | Clone query; đổi tham số `search` thành `q`; gọi public `/api/v1/recipes/search` kèm abort signal. |
| `getRecipes(signal, pageSize, page)` | Tạo URL list với page/pageSize; public GET. |
| `getMyRecipes(signal, pageSize, page)` | Tạo `/recipes/mine`; private GET qua BFF. |
| `getAdminRecipes(signal, pageSize, page)` | Tạo `/admin/recipes`; private GET qua BFF. |
| `getPrivateRecipe(id, signal)` | URL-encode ID; private GET theo ID. |
| `getRecipe(slug, signal)` | URL-encode slug; public GET theo slug. |
| `getAuthorProfile(id, page, signal)` | Tạo query page/pageSize; URL-encode author ID; public GET. |
| `getRecipeCategories(signal)` | Gọi category list pageSize=100; unwrap `items`. |
| `getCategory(slug, signal)` | URL-encode slug; public GET category detail. |
| `createRecipe(payload)` | Private POST JSON tới `/recipes`. |
| `updateRecipe(id, payload)` | URL-encode ID; private PUT JSON gồm RecipeInput + RowVersion. |
| `deleteRecipe(id)` | Private DELETE theo ID; nhận 204/no content. |
| `publishRecipe(id)` / `archiveRecipe(id)` | Private PATCH tới action endpoint tương ứng. |
| `createCategory(payload)` | Private POST JSON category. |
| `updateCategory(id, payload)` | Private PUT JSON tới category ID. |
| `uploadCategoryImage(categoryId, file)` | Tạo FormData, thêm `file`, private POST image. |
| `deleteCategory(id)` | Private DELETE theo ID. |
| `addRecipeIngredient(recipeId, payload)` | Private POST JSON ingredient vào recipe. |
| `updateRecipeIngredient(recipeId, ingredientId, payload)` | Private PUT JSON theo cả hai ID. |
| `deleteRecipeIngredient(recipeId, ingredientId)` | Private DELETE child ingredient. |
| `uploadRecipeImage(recipeId, file, isPrimary, altText)` | Tạo FormData; thêm file/primary/alt nếu có; private POST; trả upload result. |
| `uploadRecipeStepImage(recipeId, stepId, file)` | Tạo FormData file; private POST image cho step. |
| `setPrimaryRecipeImage(recipeId, imageId)` | Private PATCH action primary. |
| `deleteRecipeImage(recipeId, imageId)` | Private DELETE ảnh theo recipe/image ID. |

Mỗi hàm ở bảng chủ yếu chuyển input thành URL, HTTP method và body đúng contract rồi gọi request wrapper; không nhúng quy tắc nghiệp vụ hay tự sửa React Query cache.


### 10.6 SEO, metadata và server-side public data

- `getPublicRecipe(slug)` trong [server/recipes-api.ts](../frontend/src/lib/server/recipes-api.ts): encode slug vào URL; fetch không dùng cache; kiểm tra status; đọc response; từ chối recipe không ở trạng thái Published; trả `RecipeDetail`.
- `getAllPublishedRecipes()` cùng file: lặp page size 50, tối đa 20 trang; gọi API; kiểm tra lỗi; chỉ giữ Published; dừng khi đã lấy đủ hoặc hết page; trả danh sách đã gom.
- `getPublicAuthorProfile(id, page, pageSize)` trong [server/authors-api.ts](../frontend/src/lib/server/authors-api.ts): URL-encode ID; thêm pagination; fetch public API; map status 404 sang lỗi không tìm thấy và status khác sang lỗi API; trả profile.
- `buildRecipeMetadata(recipe, siteUrl)` trong [recipe-metadata.ts](../frontend/src/lib/recipe-metadata.ts): chọn primary image; chuẩn hóa base URL; tạo title/description/canonical URL/Open Graph/Twitter metadata.
- `isoDuration(minutes)` trong [recipe-json-ld.ts](../frontend/src/lib/recipe-json-ld.ts): đổi phút sang dạng ISO 8601 `PT...M`.
- `buildRecipeJsonLd(recipe)`: sắp xếp ingredients/steps/images; tạo Recipe schema; thêm author/published/nutrition khi có; trả object có cấu trúc JSON-LD.
- `serializeRecipeJsonLd(value)`: `JSON.stringify`; escape `<` thành `\u003c` để không tạo thẻ script injection khi nhúng JSON vào HTML.
- `getSiteUrl()` trong [site-config.ts](../frontend/src/lib/site-config.ts): lấy `NEXT_PUBLIC_SITE_URL`, fallback localhost, bỏ slash cuối.
- `sitemap()` trong [sitemap.ts](../frontend/src/app/sitemap.ts): tạo danh sách route tĩnh; gọi API lấy Published recipes; map mỗi recipe thành URL/lastModified; nếu API lỗi dùng nhánh fallback rỗng trong implementation thay vì làm hỏng toàn bộ sitemap.
- `robots()` trong [robots.ts](../frontend/src/app/robots.ts): trả quy tắc crawler và các route cần chặn.
- `Image()` trong [opengraph-image.tsx](../frontend/src/app/opengraph-image.tsx) và [twitter-image.tsx](../frontend/src/app/twitter-image.tsx): render ảnh chia sẻ mặc định theo `alt`, `size`, `contentType` khai báo ngoài hàm.

### 10.7 Profile, auth UI và session

Các trang auth ở frontend được giải thích ở đây trong vai trò giao diện/điểm nối. Backend Identity và nghiệp vụ phát hành token không thuộc Recipe Application.

- `ProfilePage()` trong [profile/page.tsx](../frontend/src/app/profile/page.tsx): đọc session và profile query; khởi tạo update/password mutations; giữ state edit/password/logout; nếu không authenticated thì chuyển login; effect xử lý session/API 401 bằng sign-out; các handler validate input, gọi mutation, cập nhật feedback/error; cuối cùng render profile hoặc edit/password forms theo state.
- `credentialErrorMessage(code, fallback)` trong [auth/login/page.tsx](../frontend/src/app/auth/login/page.tsx): đổi error code từ provider thành thông báo phù hợp; nếu không map được dùng fallback.
- `LoginPage()`: khai báo form với Zod/RHF; nếu session đã đăng nhập thì điều hướng phù hợp; submit gọi credentials sign-in; phân nhánh error/success; phần Google khởi tạo GIS và xử lý credential callback; render form hoặc thông báo trạng thái.
- `RegisterPage()` trong [auth/register/page.tsx](../frontend/src/app/auth/register/page.tsx): schema kiểm tra password/confirmation; submit gửi email/password/displayName, không gửi confirm password; mutation result chọn màn hình success hoặc errors.
- `ForgotPasswordPage()` trong [auth/forgot-password/page.tsx](../frontend/src/app/auth/forgot-password/page.tsx): validate email; gọi forgot mutation; render message trung tính/success; reset form/mutation khi cần.
- `ResetPasswordForm()` trong [auth/reset-password/page.tsx](../frontend/src/app/auth/reset-password/page.tsx): đọc email/token từ URL; validate password confirmation; submit reset mutation; nhánh thiếu token hiển thị hướng dẫn hoặc form nhập tay; render success/errors. `ResetPasswordPage()` chỉ bọc form trong `Suspense` cho query/search params.
- Auth provider/callback, session token, middleware, BFF và module augmentation được giải thích tại mục 10.8; không lặp lại chi tiết xử lý ở đây.

### 10.8 API auth, BFF và server helpers

#### `auth-api.ts` và `api.ts`

- Ngoài hàm, `API_BASE_URL`, `UserProfile`, `AdminUser` và request records mô tả response/input; chúng định nghĩa type contract cho auth UI.
- `auth-api.request<T>`: fetch; private 401 gọi redirect helper; đọc JSON response; nếu response lỗi đọc ProblemDetails và tạo `ApiError`; 204 trả `undefined`; success parse JSON.
- `privateRequest`: nối `/api/backend`, tắt cache, đánh dấu request private.
- `publicRequest`: nối API base với `/api/v1` cho auth public, credentials omit.
- `getProfileApi(signal)`: GET `/auth/me`.
- `getAdminUsersApi(signal)`: GET `/admin/users` qua private request.
- `updateProfileApi(body)`: PATCH `/auth/me` JSON.
- `changePasswordApi(body)`: POST `/auth/change-password` JSON.
- `forgotPasswordApi(body)`: POST public forgot-password JSON.
- `resetPasswordApi(body)`: POST public reset-password JSON.
- `registerApi(payload)` trong [api.ts](../frontend/src/lib/api.ts): POST register JSON; nếu lỗi parse body và chọn message theo conflict/server/general failure; throw `ApiError`; success parse JSON thành register result.
- `getLoginCallbackUrl()` trong [auth-redirect.ts](../frontend/src/lib/auth-redirect.ts): server-side hoặc reason không phải session-expired thì trả `/`; còn lại kiểm callback rỗng, relative, backslash/control chars và cùng origin; URL lỗi/khác origin trả `/`.
- `redirectToLoginForCurrentPage()`: không chạy ngoài browser hoặc ngay tại login page; lấy path/query/hash hiện tại; encode callback rồi `location.replace` sang login với reason session-expired.

#### `auth.ts` và route middleware

- Module constants đặt refresh skew 30 giây, tên session cookie và session age 7 ngày; `AuthenticatedUser` mô tả user sau backend login; `SafeCredentialsSignin` giữ mã lỗi Auth.js.
- `SafeCredentialsSignin(code)`: gọi constructor cha rồi đặt code chuẩn để UI có thể map thông báo.
- `credentialsError(error)`: status 401 → thông tin đăng nhập sai; 423 → khóa tài khoản; còn lại → lỗi xác thực chung.
- `authenticate(tokenPair)`: lấy profile backend bằng access token; kết hợp profile và token pair thành user Auth.js gồm ID/email/name/avatar/roles/expiry.
- Credentials provider `authorize`: kiểm tra email/password string; login backend; gọi authenticate; lỗi backend đổi thành `SafeCredentialsSignin`.
- Google provider `authorize`: kiểm tra ID token; gọi Google login backend; authenticate; chuẩn hóa lỗi.
- JWT callback: khi có user lần đầu copy field/profile/token vào JWT; các lần sau kiểm tra access token còn hạn hay gần hết hạn; nếu còn đủ skew trả token hiện tại; nếu cần refresh gọi hàm refresh đã deduplicate; lỗi refresh gắn error code để route/middleware xử lý.
- Session callback: chuyển các field an toàn từ JWT ra `session.user`; không đưa refresh token vào client; đánh dấu `RefreshTokenError` nếu refresh thất bại.
- Sign-out callback: gửi access/refresh token lên backend logout; lỗi được log để không chặn thao tác sign-out phía browser.
- `localCallback(value)` trong [middleware.ts](../frontend/src/middleware.ts): từ chối URL rỗng, protocol-relative, backslash/control characters và URL khác origin; trả path nội bộ an toàn hoặc `/`.
- `authMiddleware`: đọc user; nếu chưa đăng nhập hoặc refresh error, tạo redirect login và gắn callback/reason; nếu vào category/user admin page mà không có Admin role thì redirect dashboard; còn lại trả response tiếp tục.
- `middleware(request)`: gọi auth wrapper; khi redirect vì session hết hạn, tạo header expire cho các session cookie đang có.
- `config.matcher`: giới hạn middleware cho `/dashboard/:path*` và `/profile`.
- Route `api/auth/[...nextauth]/route.ts`: export lại GET/POST handlers từ Auth.js để framework gắn endpoint auth; không thêm nghiệp vụ.
- `next-auth.d.ts`: module augmentation thêm ID/email/name/avatar/roles vào Session user và error; JWT thêm roles/access/refresh token/expiry/error để TypeScript hiểu payload mở rộng. Callback chỉ expose các field an toàn ra Session.

#### BFF route `/api/backend/[...path]`

- Constants `COOKIE_CHUNK_SIZE` và `UPSTREAM_RESPONSE_HEADERS` giới hạn kích thước cookie chunk và header được phép copy; method unions đóng tập HTTP verbs.
- `isIdentifier(segment)`: chỉ chấp nhận ký tự ASCII chữ/số/`_`/`-`; loại `.` và `..`.
- `allowedMethods(segments)`: match resource/segment count/id; trả allowlist method đúng route (recipe/category/ingredient/image/step/auth/admin); path không được khai báo trả null.
- `pathSegments(request)`: kiểm prefix `/api/backend/`; từ chối path rỗng, backslash, encoded slash/dot, percent còn lại và segment rỗng/dot; trả array segment đã kiểm tra.
- `hasSameOrigin(request, origin)`: từ chối thiếu Origin/Host; xác định protocol từ forwarded header hoặc request URL; chỉ nhận HTTP(S); parse origin và so sánh origin chính xác; URL lỗi trả false.
- `serializedSessionCookie(name,value,secure)`: dựng cookie HttpOnly/SameSite=Lax/Path/Max-Age; thêm Secure khi cookie name yêu cầu.
- `existingSessionCookieNames(request)`: tách Cookie header; chỉ trả đúng session cookie hoặc chunk có suffix số.
- `expiredSessionCookieHeaders(request)`: tìm mọi session cookie/chunk đang có; nếu không có thì tạo header cho cookie chính; tạo Max-Age=0 cho từng cookie.
- `sessionCookieHeaders(request,value)`: chia encoded token thành chunk theo giới hạn; tính tên cookie mới; expire chunk cũ không còn dùng; serialize và trả Set-Cookie headers mới.
- `encodeSessionToken(token)`: nếu thiếu `AUTH_SECRET` trả null; nếu có thì Auth.js encode token với cookie salt và max-age.
- `jsonResponse(status,message,cookies)`: dựng JSON/no-store header; append từng cookie; trả object error JSON với status truyền vào.
- `getValidAccessToken(request, forceRefresh, currentToken)`: yêu cầu secret; lấy token hiện tại hoặc decode cookie; reject missing/invalid identity/refresh error; kiểm tra access/refresh/expiry types; trả token nếu chưa gần hết hạn; nếu hết hạn gọi refresh backend; encode token/cookie mới; các nhánh refresh/encode fail expire session và trả state không access token.
- `proxy(request,method)`: lấy token trước; nếu chưa có trả 401; validate path/method, trả 404/405; mutation yêu cầu same-origin; build upstream URL, copy query; chỉ gửi body với method không phải GET; thêm Bearer và content-type/accept; nếu upstream 401 refresh rồi retry; copy allowlisted headers/cookies; trả response upstream; lỗi URL/network thành 502.
- `GET`, `POST`, `PUT`, `PATCH`, `DELETE`, `HEAD`, `OPTIONS`: mỗi exported handler chỉ gọi `proxy(request, "VERB")`; toàn bộ guard/proxy logic ở một nơi.

#### `server/auth-backend.ts`

- Module `API_INTERNAL_URL` là địa chỉ backend nội bộ; `refreshFlights` lưu các promise refresh đang chạy để tránh refresh cùng token đồng thời.
- `endpoint(path)`: kiểm tra base URL cấu hình HTTP(S); từ chối URL không thuộc `/api/v1/` hoặc path không an toàn; nối path vào internal URL.
- `internalApiEndpoint(path)`: gọi `endpoint` rồi tạo `URL`.
- `postJson<T>(path, body)`: POST JSON với `cache:no-store`; network failure được map thành auth backend error; đọc status/body; ném lỗi với status/message nếu backend trả lỗi; parse success JSON.
- `tokenPair(value)`: xác nhận object có access/refresh token string và expiry hợp lệ; trả typed pair hoặc ném lỗi.
- `loginWithPassword` / `loginWithGoogleIdToken`: POST payload login vào endpoint tương ứng rồi validate bằng `tokenPair`.
- `getProfile(accessToken)`: GET `/auth/me` kèm Bearer; kiểm status; validate profile fields/roles; map về `BackendProfile`.
- `refreshBackendTokens(refreshToken)`: POST refresh rồi validate response.
- `refreshBackendTokensOnce(refreshToken)`: SHA-256 token thành key; nếu đã có promise thì dùng lại; nếu chưa thì tạo refresh flight; finally xóa flight tương ứng để lần sau có thể refresh mới.
- `logoutFromBackend(accessToken, refreshToken)`: POST logout với Bearer/body; chấp nhận 204; các status lỗi trả error để callback ghi log.

### 10.9 Dashboard category admin và private recipe view

#### `DashboardCategoriesPage`

- Ngoài hàm, component giữ session/feedback; categories query và create/update/delete/upload mutations; `selected/name/description`; pending image/preview/retry category ID; saving/errors/fieldErrors. `queryError` và `displayedError` được tính từ query/action state thay vì lưu bản sao.
- Preview effect: không có file thì clear preview; nếu có thì tạo object URL; cleanup revoke URL khi file đổi/unmount.
- Query-error effect: khi queryError thay đổi thì gọi error toast.
- `startEdit(category)`: lưu category đang chọn; copy name/description vào form; clear pending image/retry state và lỗi.
- `resetForm()`: clear selection, text input, pending image/retry ID, field/global errors.
- `onImageSelected(event)`: lấy file; nếu >5 MB reset input/file và ghi lỗi; nếu hợp lệ lưu file và clear global error.
- `finishImageUpload(categoryId)`: nếu không có file pending thì return true; thử upload; success clear retry ID, reset form, toast create/update và return true; catch ghi lỗi partial-save, giữ category ID để retry, toast và return false.
- `clearFieldError(field)`: immutable remove field trong map lỗi và clear global error.
- `onSubmit(event)`: prevent default; bật saving/clear errors; nếu có retry ID thì gọi upload retry rồi return; khi edit gọi update với trimmed name/description và giữ image/order; khi create gọi create với trimmed input; nếu có ảnh thì giữ category ID rồi upload; nếu không thì reset form/toast; catch dùng `getFormErrorDetails` gán field/global errors và toast; `finally` luôn tắt saving.
- `handleDelete(category)`: hỏi confirm; hủy thì return; gọi delete mutation; success toast; catch ghi/toast lỗi.
- JSX: render form tạo/sửa, field errors, preview ảnh đang chờ hoặc ảnh đã lưu, progress/retry, loading/empty/table; edit gọi `startEdit`, delete gọi `handleDelete`.

#### `DashboardRecipeViewPage`

- Đọc dynamic ID và session; tạo scope admin/owner; bật `useRecipeForEdit` khi ID có và session không loading.
- Tính error theo thứ tự: thiếu ID; API 403; Error message; fallback lỗi không xác định; không có lỗi thì null.
- Error trả `RecipeDetailError`; loading/session chưa sẵn sàng/không có recipe trả skeleton; thành công render `RecipeDetailClient` với back link tới danh sách dashboard.






