# Bằng chứng báo cáo cá nhân - TV4

## 1. Mục đích

File này dùng để ghi lại những gì TV4 thực sự đã làm, phục vụ báo cáo cá nhân. Chỉ ghi kết quả có bằng chứng từ code, test hoặc tài liệu đã tạo.

## 2. Nhật ký task TV4-01 Category Phase A (giữ nguyên kết quả đã chốt)

### Task:

TV4-01 Category Module - Phase A: domain logic + validation + unit test. Chỉ dùng tài liệu 00/01/05. Chưa triển khai MinIO (02), Hangfire (03) và Observability (04).

### Ngày thực hiện:

2026-09-22

### Mục tiêu:

- Hoàn thiện domain logic của Category: slug tiếng Việt không dấu, trim, giới hạn độ dài, Update sinh lại slug + UpdatedAt.
- Thêm validator cho `CreateCategoryCommand` và `GetCategoriesQuery`.
- Thêm unit test cho slug, trim, độ dài Name/Description, Page/PageSize, search/sort.
- Không sửa file ngoài phạm vi TV4 (xem mục "Dependency còn chờ").

### Trạng thái repository trước khi code (Bước 1):

- `git status`: sạch, branch `2312716-MaiQuyPhuoc-Category_File_Jobs_Observability`.
- `git diff --name-only --diff-filter=U`: rỗng, không có unmerged path/conflict.
- `git log --oneline --decorate -5`: `baa7302 Merge remote-tracking branch 'origin/2312695-NguyenPhamPhuNam-Auth_Infra' ...` là HEAD.
- Kết luận: repository không merge dở dang nên được phép tiếp tục.

### Đã hoàn thành:

- Slug tiếng Việt không dấu trong `Category.GenerateSlug`: chuẩn hóa FormD, loại bỏ `NonSpacingMark`, đổi `đ` → `d`, chỉ giữ `a-z0-9`, gộp khoảng trắng thừa và ký tự đặc biệt thành một dấu `-`, cắt `-` ở đầu/cuối. Ví dụ: "Món Tráng Miệng" → `mon-trang-mieng`, "!!!Đồ Ăn Vặt!!!" → `do-an-vat`, "Bánh Flan 100% Ngon!" → `banh-flan-100-ngon`.
- Trim Name và Description; Description rỗng/whitespace trở thành `null`.
- Giới hạn Name 2..50 ký tự và Description tối đa 500 ký tự bằng guard clause trong Domain (`ArgumentException`), dùng chung hằng số `Category.MinNameLength`, `Category.MaxNameLength`, `Category.MaxDescriptionLength`.
- `Category.Update` sinh lại slug, trim input, cập nhật `UpdatedAt`, giữ nguyên `CreatedAt`; input được chuẩn hóa trước khi gán nên input sai không để lại entity ở trạng thái sửa dở dang.
- Thêm `CreateCategoryCommandValidator`: Name bắt buộc, 2..50 ký tự sau trim; Description tối đa 500 ký tự sau trim. Khả năng sinh slug do Category domain kiểm tra, validator không lặp lại logic slug.
- Thêm `GetCategoriesQueryValidator`: `Page >= 1`, `PageSize` trong khoảng 1..100; thêm hằng số `GetCategoriesQuery.DefaultPageSize`/`MaxPageSize`. Search/SortBy giữ nguyên hành vi hiện có của handler (không thêm ràng buộc mới, không đổi HTTP status).
- Đăng ký `AddValidatorsFromAssembly` và `AddOpenBehavior(ValidationBehavior<,>)` trong `Application/DependencyInjection.cs` để validator thực sự chạy trong pipeline MediatR (xem mục "Quyết định cần team chốt").
- 90 unit test Category mới (domain, validator, handler với EF Core InMemory, DI/pipeline) - tất cả pass trong clean-room (xem mục "Test/build đã chạy").
- Sau review `CreateCategoryCommandValidator`: đã bỏ rule `ContainsSlugCharacter` vì rule này chạy trên tên gốc nên chỉ thấy ký tự ASCII, từ chối sai tên tiếng Việt hợp lệ ("Đồ", "Ăn", "Ức", "Ớt", "Ổi" đều sinh được slug `do`, `an`, `uc`, `ot`, `oi`). Quyền kiểm tra khả năng sinh slug giờ chỉ thuộc Domain (`Category.GenerateSlug` + guard slug rỗng); đã thêm test cho tên chỉ gồm ký tự có dấu và test khẳng định validator không tự chặn rule slug.

### File đã thay đổi:

Sửa:

- `backend/src/CulinaryBlog.Domain/Entities/Category.cs`
- `backend/src/CulinaryBlog.Application/Features/Categories/Queries/GetCategories/GetCategoriesQuery.cs` (thêm hằng số phân trang)
- `backend/src/CulinaryBlog.Application/DependencyInjection.cs` (đăng ký validator + open-generic behavior)
- `backend/tests/CulinaryBlog.Application.Tests/CulinaryBlog.Application.Tests.csproj` (thêm `Microsoft.EntityFrameworkCore.InMemory`, chỉ dùng cho test)

Tạo mới (source):

- `backend/src/CulinaryBlog.Application/Features/Categories/Commands/CreateCategory/CreateCategoryCommandValidator.cs`
- `backend/src/CulinaryBlog.Application/Features/Categories/Queries/GetCategories/GetCategoriesQueryValidator.cs`

Tạo mới (test, 90 test case):

- `backend/tests/CulinaryBlog.Application.Tests/Domain/Entities/CategorySlugTests.cs` (22)
- `backend/tests/CulinaryBlog.Application.Tests/Domain/Entities/CategoryTests.cs` (14)
- `backend/tests/CulinaryBlog.Application.Tests/Domain/Entities/CategoryUpdateTests.cs` (8)
- `backend/tests/CulinaryBlog.Application.Tests/Features/Categories/Commands/CreateCategory/CreateCategoryCommandValidatorTests.cs` (18)
- `backend/tests/CulinaryBlog.Application.Tests/Features/Categories/Commands/CreateCategory/CreateCategoryCommandHandlerTests.cs` (3)
- `backend/tests/CulinaryBlog.Application.Tests/Features/Categories/Commands/DeleteCategory/DeleteCategoryCommandHandlerTests.cs` (2)
- `backend/tests/CulinaryBlog.Application.Tests/Features/Categories/Queries/GetCategories/GetCategoriesQueryValidatorTests.cs` (7)
- `backend/tests/CulinaryBlog.Application.Tests/Features/Categories/Queries/GetCategories/GetCategoriesQueryHandlerTests.cs` (7)
- `backend/tests/CulinaryBlog.Application.Tests/Features/Categories/Queries/GetCategories/GetCategoriesQueryHandlerSortTests.cs` (5)
- `backend/tests/CulinaryBlog.Application.Tests/Features/Categories/CategoryDependencyInjectionTests.cs` (3)
- `backend/tests/CulinaryBlog.Application.Tests/Features/Categories/CategoryTestDbContext.cs` (test double `IApplicationDbContext` dùng EF Core InMemory, không phải integration test)
- `backend/tests/CulinaryBlog.Application.Tests/Features/Categories/CategoriesTestData.cs` (dữ liệu seed dùng chung cho test)

`git diff --stat` (file track sẵn): 4 files changed, 175 insertions(+), 46 deletions(-).

### Test/build đã chạy:

Bước 1 - kiểm tra repository trước khi code (repo thật):

```text
Command: git status
Result: On branch 2312716-MaiQuyPhuoc-Category_File_Jobs_Observability / nothing to commit, working tree clean

Command: git diff --name-only --diff-filter=U
Result: (rỗng - không có unmerged path, không có conflict)

Command: git log --oneline --decorate -5
Result: baa7302 (HEAD -> 2312716-MaiQuyPhuoc-Category_File_Jobs_Observability) Merge remote-tracking branch 'origin/2312695-NguyenPhamPhuNam-Auth_Infra' ...
        7ca140f add gitignore
        3833906 (origin/2312695-NguyenPhamPhuNam-Auth_Infra) feat(auth): implement week 2 auth flows and pages
        ec5ad12 (origin/main, origin/HEAD, main) Phân công tuần 2
        e2d5e3f Khởi tạo dự án
```

Bước 2 - 3 lệnh kiểm tra theo yêu cầu, chạy trong repo thật. Cả 3 FAILED do lỗi baseline đã có sẵn từ nhánh TV1 được merge (không phải do thay đổi của TV4):

```text
Command: dotnet build backend/src/CulinaryBlog.Domain/CulinaryBlog.Domain.csproj
Result: Build FAILED (exit code 1), 1 Error
        error NU1506: Warning As Error: Duplicate 'PackageVersion' items found ...
        (Microsoft.EntityFrameworkCore 10.0.12, Microsoft.EntityFrameworkCore 10.0.0;
         Npgsql.EntityFrameworkCore.PostgreSQL 10.0.0, Npgsql.EntityFrameworkCore.PostgreSQL 10.0.3)
        File gây lỗi: backend/Directory.Packages.props

Command: dotnet build backend/src/CulinaryBlog.Application/CulinaryBlog.Application.csproj
Result: Build FAILED (exit code 1), 2 Errors
        error NU1506 tại backend/Directory.Packages.props và
        backend/src/CulinaryBlog.Application/CulinaryBlog.Application.csproj

Command: dotnet test backend/tests/CulinaryBlog.Application.Tests/CulinaryBlog.Application.Tests.csproj
Result: Build FAILED (exit code 1)
        error NU1504: Warning As Error: Duplicate 'PackageReference' items found ...
        File gây lỗi: backend/src/CulinaryBlog.Infrastructure/CulinaryBlog.Infrastructure.csproj
```

Lỗi baseline thứ ba chỉ lộ ra khi tạm hạ mức NU1504/NU1506/NU1605 bằng cờ MSBuild (không sửa file):

```text
Command: dotnet build backend/src/CulinaryBlog.Infrastructure/CulinaryBlog.Infrastructure.csproj -p:NoWarn=NU1605%3BNU1504%3BNU1506
Result: Build FAILED (exit code 1)
        backend/src/CulinaryBlog.Infrastructure/Persistence/AuthDbContext.cs(13,48): error CS0104:
        'ApplicationUser' is an ambiguous reference between
        'CulinaryBlog.Infrastructure.Identity.ApplicationUser' and 'CulinaryBlog.Domain.Entities.ApplicationUser'
        (CONFLICT-025 vẫn đang mở)
        Cùng cờ này, CulinaryBlog.Domain và CulinaryBlog.Application build thành công.

Command: dotnet restore backend/CulinaryBlog.sln -p:TreatWarningsAsErrors=false
Result: FAILED (exit code 1) - error NU1605: downgrade
        Microsoft.Extensions.Configuration.Abstractions from 10.0.12 to 10.0.0 (Infrastructure)
```

Bước 3 - clean-room verification: copy repo ra ngoài workspace, chỉ sửa 3 lỗi baseline trên bản copy, sau đó chạy lại đúng 3 lệnh theo yêu cầu:

```text
Command: robocopy d:\Nhom9_Culinary_Blog %TEMP%\tv4-verify /E /XD .git obj bin .vs
Result: copy thành công (robocopy exit=1 = có file được copy)

Fix chỉ áp dụng trong bản copy, repo thật không bị sửa (xác nhận lại bằng git status):
1. backend/Directory.Packages.props: xóa 2 dòng PackageVersion bị trùng
   (Microsoft.EntityFrameworkCore 10.0.0, Npgsql.EntityFrameworkCore.PostgreSQL 10.0.0)
   và đổi Microsoft.Extensions.Configuration.Abstractions về 10.0.12 (lỗi NU1605).
2. backend/src/CulinaryBlog.Infrastructure/CulinaryBlog.Infrastructure.csproj:
   xóa 1 dòng PackageReference Microsoft.EntityFrameworkCore bị lặp.
3. backend/src/CulinaryBlog.Infrastructure/Persistence/AuthDbContext.cs:
   thêm alias 'using ApplicationUser = CulinaryBlog.Infrastructure.Identity.ApplicationUser;'

Command: dotnet build backend/src/CulinaryBlog.Domain/CulinaryBlog.Domain.csproj
Result: Build succeeded. 0 Warning(s), 0 Error(s)

Command: dotnet build backend/src/CulinaryBlog.Application/CulinaryBlog.Application.csproj
Result: Build succeeded. 0 Warning(s), 0 Error(s)

Command: dotnet test backend/tests/CulinaryBlog.Application.Tests/CulinaryBlog.Application.Tests.csproj
Result: Passed! - Failed: 0, Passed: 122, Skipped: 0, Total: 122 (lần chạy trước khi sửa validator theo review)

Command: dotnet test backend/tests/CulinaryBlog.Application.Tests/CulinaryBlog.Application.Tests.csproj --filter "FullyQualifiedName~Categories|FullyQualifiedName~Domain.Entities.Category"
Result: Passed! - Failed: 0, Passed: 81, Skipped: 0, Total: 81 (lần chạy trước khi sửa validator theo review)

Command: dotnet test backend/tests/CulinaryBlog.ArchitectureTests/CulinaryBlog.ArchitectureTests.csproj
Result: Passed! - Failed: 0, Passed: 3, Skipped: 0, Total: 3 (Domain/Application vẫn đúng Clean Architecture)

Command: dotnet build backend/CulinaryBlog.sln
Result: Build succeeded. 0 Warning(s), 0 Error(s)
```

Chạy lại sau khi sửa validator theo review (cùng clean-room copy, cùng 3 fix baseline):

```text
Command: dotnet test backend/tests/CulinaryBlog.Application.Tests/CulinaryBlog.Application.Tests.csproj
Result: Passed! - Failed: 0, Passed: 131, Skipped: 0, Total: 131

Command: dotnet test backend/tests/CulinaryBlog.Application.Tests/CulinaryBlog.Application.Tests.csproj --filter "FullyQualifiedName~Categories|FullyQualifiedName~Domain.Entities.Category"
Result: Passed! - Failed: 0, Passed: 90, Skipped: 0, Total: 90 (toàn bộ test Category của TV4 sau review)
```

Lỗi thật do code TV4, phát hiện trong lần chạy test đầu và đã sửa:

```text
1. error CS0103: The name 'DefaultPageSize' does not exist in the current context (GetCategoriesQuery.cs)
   - giá trị default của primary constructor record không tham chiếu được hằng số khai báo trong thân record.
   Đã đổi default về literal 10 và giữ hằng số DefaultPageSize.

2. CategoryDependencyInjectionTests.Send_InvalidCreateCategoryCommand_ShouldThrowValidationException FAIL
   "Expected a <FluentValidation.ValidationException> to be thrown, but found <System.ArgumentException>"
   Nguyên nhân: MediatR 12.5 không tự đăng ký open-generic IPipelineBehavior nên ValidationBehavior<,>
   không nằm trong pipeline (kiểm tra bằng cách dump ServiceDescriptor: chỉ có IValidator<> được đăng ký).
   Đã sửa bằng cfg.AddOpenBehavior(typeof(ValidationBehavior<,>)) trong Application/DependencyInjection.cs.

3. CategorySlugTests kỳ vọng sai: Name chỉ trim 2 đầu nên vẫn giữ khoảng trắng bên trong;
   việc gộp khoảng trắng thừa xảy ra ở slug. Đã sửa kỳ vọng của test, không sửa Domain.
```

Phần chưa kiểm tra được trong môi trường hiện tại: `CulinaryBlog.Integration.Tests` và API thật của Category (endpoint chưa được map trong `Program.cs`).

### Chưa hoàn thành:

- `GET /api/v1/categories/{slug}` kèm danh sách recipe phân trang (FR-CAT-002): chưa làm, cần chốt contract với TV2 (Recipe relationship + pagination của recipe).
- `UpdateCategoryCommand` / `PUT /api/v1/categories/{id}`: chưa tạo command/handler. Domain `Category.Update` đã hoàn thiện và đã có test, nhưng endpoint cần chốt field (ImageUrl/OrderIndex) trước.
- Kiểm tra trùng Name/Slug: chưa làm vì cần chốt HTTP status (409/400/422) và cần biết unique index trong migration của TV2.
- Delete guard "không xóa category nếu còn recipe": không được sửa `DeleteCategory` trong task này.
- Cache/invalidation sau create/update/delete: chưa làm vì nhóm chưa chốt IMemoryCache hay Redis (CONFLICT-020).
- Authorization Admin cho POST/PUT/DELETE: chờ Admin policy của TV1.
- MinIO (02), Hangfire (03), Observability (04): thuộc phase sau, chưa triển khai.
- Chưa có integration test/API test cho Category vì `CategoryEndpoints` chưa được map và API layer đang lỗi baseline.

### Dependency còn chờ:

- TV1 (Auth/Infra, commit `3833906` đã merge vào branch cá nhân): 3 lỗi baseline làm mọi lệnh build/test backend fail:
  1. `backend/Directory.Packages.props`: trùng `PackageVersion` (NU1506) và `Microsoft.Extensions.Configuration.Abstractions` bị hạ về 10.0.0 gây NU1605.
  2. `backend/src/CulinaryBlog.Infrastructure/CulinaryBlog.Infrastructure.csproj`: trùng `PackageReference` Microsoft.EntityFrameworkCore (NU1504).
  3. `backend/src/CulinaryBlog.Infrastructure/Persistence/AuthDbContext.cs`: CS0104 ambiguous `ApplicationUser` (CONFLICT-025 - `Domain.Entities.ApplicationUser` của TV2 vs `Infrastructure.Identity.ApplicationUser` của TV1).
- TV1: gọi `app.MapCategoryEndpoints()` trong `Program.cs` (hiện chưa được map) và bổ sung xử lý `ValidationException` cho `CategoryEndpoints` (Auth endpoints đã catch ValidationException → 400, Category thì chưa; dự án chưa có Global Exception Handler).
- TV2: `ApplicationDbContext` + migration cho Category (unique index Name/Slug), quyết định hard/soft delete và Recipe → Category relationship để làm delete guard và `GET /categories/{slug}`.
- TV2: xác nhận contract Recipe image/`RecipeImage` khi TV4 làm MinIO (phase sau).

### Quyết định cần team chốt:

- Việc TV4 thêm `AddValidatorsFromAssembly` + `AddOpenBehavior(ValidationBehavior<,>)` vào `Application/DependencyInjection.cs` (file được phép sửa) là bắt buộc để validator thực sự chạy. Thay đổi này cũng làm validator của TV1 (Register/Login/GoogleLogin) hoạt động, đúng với thiết kế endpoint của TV1 (đã catch `ValidationException` → 400). Cần TV1 xác nhận.
- HTTP status cho lỗi trùng Name/Slug và cách map exception ở tầng API (400/409/422) - thuộc CONFLICT-002/CONFLICT-017/CONFLICT-020.
- Có đăng ký luôn `LoggingBehavior<,>` (open generic, code của TV1) trong `AddApplication` hay để TV1 tự đăng ký.
- Tên chỉ gồm ký tự đặc biệt (ví dụ "!!!") hiện chỉ bị Domain chặn bằng `ArgumentException` (slug rỗng); validator không chặn nữa, nên nếu request tới được handler thì API cần map exception này sang 4xx thay vì 500. Cần team xác nhận behavior này.
- Không lặp lại logic sinh slug trong validator (rule "phải có ký tự a-z/0-9" đã bị bỏ sau review vì từ chối sai tên tiếng Việt chỉ gồm ký tự có dấu như "Đồ", "Ăn").
- Có thay `CategoryTestDbContext` (test double `IApplicationDbContext` + EF Core InMemory) bằng `ApplicationDbContext` thật sau khi Infrastructure build được hay không.

## 3. Nội dung có thể đưa vào báo cáo

Sau mỗi task, ghi một đoạn ngắn theo mẫu:

> Đã phân tích và triển khai [tên chức năng]. Công việc gồm [các bước thực tế]. Đã kiểm tra bằng [build/unit test/integration test]. Phần còn phụ thuộc [thành viên hoặc hạ tầng] chưa được tự ý thay đổi để tránh ảnh hưởng các module khác.

Đoạn đã viết cho task này:

> Đã phân tích và triển khai Phase A của TV4-01 Category Module. Công việc gồm: hoàn thiện domain logic `Category` (sinh slug tiếng Việt không dấu, trim Name/Description, giới hạn Name 2..50 và Description tối đa 500 ký tự, `Update` sinh lại slug và cập nhật `UpdatedAt`), thêm `CreateCategoryCommandValidator` và `GetCategoriesQueryValidator`, đăng ký validator và `ValidationBehavior` open-generic trong `Application/DependencyInjection.cs`, và viết 90 unit test cho slug/trim/độ dài/phân trang/search/sort/handler. Đã kiểm tra bằng build Domain + Application (0 warning, 0 error), `dotnet test` 131/131 pass và 3/3 architecture test pass trên bản clean-room copy có sửa 3 lỗi baseline của TV1; repo chính vẫn giữ nguyên vì các lệnh build/test trong repo hiện fail ở bước restore do lỗi baseline đó. Phần còn phụ thuộc TV1 (fix `Directory.Packages.props`, `Infrastructure.csproj`, `AuthDbContext.cs`, map `MapCategoryEndpoints`, xử lý `ValidationException` cho API Category) và TV2 (`ApplicationDbContext`, migration, delete strategy, Recipe relationship) chưa được tự ý thay đổi để tránh ảnh hưởng các module khác. Chưa commit, chưa push.

## 5. Quy tắc trung thực

- Không ghi code chưa tồn tại là đã hoàn thành.
- Không ghi test đã pass nếu chưa chạy.
- Không ghi quyết định đang chờ team là quyết định cuối cùng.
- Ghi rõ phần tạm thời và phần chưa thể tích hợp.
- Ghi rõ file và command để người khác có thể kiểm tra lại.

## 6. Tổng kết cuối buổi

### Chức năng đã làm được

- Sinh slug tiếng Việt không dấu cho Category (kèm gộp khoảng trắng thừa/ký tự đặc biệt), trim Name/Description, giới hạn Name 2..50 và Description tối đa 500 ký tự.
- `Category.Update` sinh lại slug + cập nhật `UpdatedAt`, không đổi `CreatedAt`, không để entity ở trạng thái sửa dở dang khi input sai.
- `CreateCategoryCommandValidator` và `GetCategoriesQueryValidator` (Page ≥ 1, PageSize 1..100).
- Đăng ký validator + `ValidationBehavior<,>` trong DI để validation thực sự chạy trong pipeline MediatR.
- 90 unit test cho Category (domain, validator, handler, DI/pipeline) với các failure thật được phát hiện và sửa trong quá trình test/review (CS0103 default value, thiếu đăng ký `ValidationBehavior<,>`, validator từ chối sai tên tiếng Việt có dấu).

### Bằng chứng code

- `backend/src/CulinaryBlog.Domain/Entities/Category.cs`
- `backend/src/CulinaryBlog.Application/Features/Categories/Commands/CreateCategory/CreateCategoryCommandValidator.cs`
- `backend/src/CulinaryBlog.Application/Features/Categories/Queries/GetCategories/GetCategoriesQueryValidator.cs`
- `backend/src/CulinaryBlog.Application/Features/Categories/Queries/GetCategories/GetCategoriesQuery.cs`
- `backend/src/CulinaryBlog.Application/DependencyInjection.cs`
- `backend/tests/CulinaryBlog.Application.Tests/**` (11 file test Category + `Microsoft.EntityFrameworkCore.InMemory` trong csproj, chỉ dùng cho test)
- Không file nào ngoài danh sách phạm vi TV4 bị sửa (`git status` xác nhận: 4 file sửa + 2 source mới + 12 file test mới, và 1 file docs sửa).

### Bằng chứng test

- Repo thật: 3 lệnh trong yêu cầu đều FAILED ở bước restore do lỗi baseline của TV1 (NU1506/NU1504/CS0104) - chi tiết ở mục "Test/build đã chạy".
- Clean-room copy (`%TEMP%\tv4-verify`, chỉ sửa 3 lỗi baseline trong bản copy): Domain build 0/0, Application build 0/0, `dotnet test` 131/131 pass (122 trước review validator), filter Category 90/90 pass (81 trước review), ArchitectureTests 3/3 pass, build solution 0/0.
- Chưa chạy được integration test/API test cho Category (endpoint chưa map).

### Chức năng chưa làm

- `GET /api/v1/categories/{slug}` kèm recipe phân trang, `PUT /api/v1/categories/{id}`, kiểm tra trùng Name/Slug, delete guard theo Recipe, cache/invalidation, Admin authorization, integration test cho Category.
- MinIO, Hangfire, Observability (TV4-02/03/04).

### Lý do chưa làm

- Cần team chốt contract: HTTP status cho lỗi trùng, cache technology (IMemoryCache/Redis), hard/soft delete, field update (ImageUrl/OrderIndex).
- Cần TV2 cung cấp `ApplicationDbContext`/migration/Recipe relationship; cần TV1 cung cấp Admin policy, map endpoint và xử lý `ValidationException` ở tầng API.
- Task hiện tại chỉ yêu cầu và cho phép làm Phase A.

### Tiến độ tự đánh giá

`TV4-01 Category: ~50% (Phase A xong, chưa có API/cache/auth/duplicate check) | Toàn bộ phạm vi TV4: ~15%`

## 3. Nhật ký task TV4-02 MinIO foundation (task hiện tại)

### Task:

TV4-02 MinIO foundation: FileValidationService + ObjectNameFactory + StorageFolders + contract IFileStorageService (chỉ contract) + unit test. Đây là bước chuẩn bị cho MinIO, chưa phải MinIO integration, chưa hoàn thành FR-FILE-001/FR-FILE-002.

### Ngày thực hiện:

2026-09-22

### Mục tiêu:

- Validate file tối đa 5 MiB (CONS-007), kiểm tra magic bytes thay vì tin extension/MIME của client (NFR-SEC-004).
- Hỗ trợ JPEG, PNG, WebP, AVIF (bao gồm AVIF major brand `avis` và compatible brand `avif`).
- Object name unique dạng `{folder}/{Guid:N}{ext}`, extension lấy từ định dạng phát hiện bằng magic bytes.
- `IFileValidationService` có implementation; `IFileStorageService` chỉ chốt contract (Upload/Delete) để TV2 dùng, chưa có implementation MinIO.
- Unit test cho file rỗng, file vượt quá 5 MB, extension không được hỗ trợ, magic bytes không hợp lệ, file hợp lệ, object name unique.

### Chưa được làm ở task này (theo yêu cầu):

- Chưa tạo `MinioFileStorageService` thật, chưa gọi MinIO, chưa upload/delete thật.
- Chưa thêm package MinIO; chưa sửa `Directory.Packages.props`, `Infrastructure.csproj`, `Infrastructure/DependencyInjection.cs`, Docker Compose.
- Chưa sửa `RecipeImage`/Recipe handler, chưa tạo API upload, chưa làm Hangfire job nghiệp vụ, Observability hay mở rộng Category.

### Đã hoàn thành (code đã tạo trong Application layer):

- `FileValidationService.Validate`: từ chối stream non-seekable (`ArgumentException`); size từ `Max(request.Length, stream.Length)` nên client khai báo thiếu vẫn bị chặn; `FILE_EMPTY` khi 0 byte; `FILE_TOO_LARGE` khi vượt 5 MiB; `FILE_TYPE_UNSUPPORTED` khi magic bytes không khớp JPEG/PNG/WebP/AVIF; `FILE_EXTENSION_UNSUPPORTED` khi extension không trong danh sách cho phép; `FILE_EXTENSION_MISMATCH` khi extension lệch định dạng phát hiện; `FILE_CONTENT_TYPE_MISMATCH` khi MIME khai báo lệch (cho phép thiếu MIME); đọc tối đa 32 byte từ đầu file rồi khôi phục `Position` ban đầu, không dispose stream của caller.
- `ImageMagicBytes.Detect`: JPEG `FF D8 FF`, PNG 8 byte signature, WebP `RIFF....WEBP`, AVIF box `ftyp` + major brand `avif`/`avis` hoặc compatible brand `avif` trong 32 byte đầu; ISO-BMFF brand khác (`mp42`) và `RIFF....WAVE` bị từ chối.
- `ObjectNameFactory.Create`: `{folder}/{Guid:N}{ext canonical}`, không chứa text client cung cấp nên không path traversal; folder phải qua `StorageFolders.IsValid`.
- `StorageFolders`: `categories`, `ForRecipe(Guid)` → `recipes/{id:N}`; `IsValid` chỉ cho segment `a-z0-9-` ngăn bởi `/`, chặn chữ hoa, backslash, `/` đầu/cuối, `//`, `..`, khoảng trắng, quá 200 ký tự.
- `FileErrorCodes`: `FILE_EMPTY`, `FILE_TOO_LARGE`, `FILE_TYPE_UNSUPPORTED`, `FILE_EXTENSION_UNSUPPORTED`, `FILE_EXTENSION_MISMATCH`, `FILE_CONTENT_TYPE_MISMATCH`; `InvalidFileException` mang `Code` để tầng API map sang ProblemDetails 400 sau này.
- 73 test case mới (`Common/Files`, 42 test method: 35 `[Fact]` + 7 `[Theory]`): `FileValidationServiceTests` 37 case (28 Fact + 9 case từ 2 Theory), `ObjectNameFactoryTests` 18 case (3 Fact gồm 2000 object name unique + 15 case từ 3 Theory), `StorageFoldersTests` 18 case (4 Fact + 14 case từ 2 Theory).

### File đã thay đổi (tạo mới 13 source + 4 test, 0 file sửa, 0 package mới):

- `backend/src/CulinaryBlog.Application/Contracts/Storage/ImageFileFormat.cs`
- `backend/src/CulinaryBlog.Application/Contracts/Storage/ImageFileFormats.cs`
- `backend/src/CulinaryBlog.Application/Contracts/Storage/FileErrorCodes.cs`
- `backend/src/CulinaryBlog.Application/Contracts/Storage/FileUploadRequest.cs`
- `backend/src/CulinaryBlog.Application/Contracts/Storage/FileValidationResult.cs`
- `backend/src/CulinaryBlog.Application/Contracts/Storage/FileUploadResult.cs`
- `backend/src/CulinaryBlog.Application/Contracts/Storage/StorageFolders.cs`
- `backend/src/CulinaryBlog.Application/Contracts/Storage/IFileValidationService.cs`
- `backend/src/CulinaryBlog.Application/Contracts/Storage/IFileStorageService.cs`
- `backend/src/CulinaryBlog.Application/Common/Files/ImageMagicBytes.cs`
- `backend/src/CulinaryBlog.Application/Common/Files/FileValidationService.cs`
- `backend/src/CulinaryBlog.Application/Common/Files/ObjectNameFactory.cs`
- `backend/src/CulinaryBlog.Application/Common/Exceptions/InvalidFileException.cs`
- `backend/tests/CulinaryBlog.Application.Tests/Common/Files/TestImageFixtures.cs`
- `backend/tests/CulinaryBlog.Application.Tests/Common/Files/FileValidationServiceTests.cs`
- `backend/tests/CulinaryBlog.Application.Tests/Common/Files/ObjectNameFactoryTests.cs`
- `backend/tests/CulinaryBlog.Application.Tests/Common/Files/StorageFoldersTests.cs`
- `docs/TV4/06-REPORT-EVIDENCE.md` (thêm mục task TV4-02 này)

### Test/build đã chạy:

Repo thật (`d:\Nhom9_Culinary_Blog`, .NET SDK 10.0.401):

```text
Command: dotnet build backend/src/CulinaryBlog.Application/CulinaryBlog.Application.csproj --nologo -v:m
Result: Build FAILED (exit code 1) — lỗi baseline TV1 ở bước restore, chưa chạm tới code TV4:
        error NU1506: Warning As Error: Duplicate 'PackageVersion' items found ...
        (Microsoft.EntityFrameworkCore 10.0.12, Microsoft.EntityFrameworkCore 10.0.0;
         Npgsql.EntityFrameworkCore.PostgreSQL 10.0.0, Npgsql.EntityFrameworkCore.PostgreSQL 10.0.3)
        File gây lỗi: backend/Directory.Packages.props
Passed/Failed: FAILED (baseline, repo giữ nguyên không sửa)
```

Clean-room `%TEMP%\tv4-verify-minio` (copy toàn bộ repo, chỉ sửa 4 lỗi baseline của TV1 trong bản copy, repo chính giữ nguyên):

```text
Fix chỉ áp dụng trong bản copy:
1. backend/Directory.Packages.props: xóa 2 dòng PackageVersion bị trùng
   (Microsoft.EntityFrameworkCore 10.0.0, Npgsql.EntityFrameworkCore.PostgreSQL 10.0.0)
   và nâng Microsoft.Extensions.Configuration.Abstractions 10.0.0 → 10.0.12 (lỗi NU1605).
2. backend/src/CulinaryBlog.Infrastructure/Persistence/AuthDbContext.cs:
   thay 'using CulinaryBlog.Domain.Entities;' bằng
   'using ApplicationUser = CulinaryBlog.Infrastructure.Identity.ApplicationUser;'
   + 'using CulinaryBlog.Domain.Entities;' (sửa CS0104 nhưng vẫn giữ được RefreshToken,
   khác với Phase A chỉ dùng alias đơn — Phase A không cần RefreshToken vì chưa có file này
   hoặc chưa được test project tham chiếu).

Command: dotnet build backend/src/CulinaryBlog.Application/CulinaryBlog.Application.csproj --nologo -v:m
Result: Build succeeded. 0 Warning(s), 0 Error(s)

Command: dotnet test backend/tests/CulinaryBlog.Application.Tests/CulinaryBlog.Application.Tests.csproj --nologo --filter "FullyQualifiedName~Common.Files"
Result: Passed! - Failed: 0, Passed: 73, Skipped: 0, Total: 73
        (khớp đúng 73 case mới: 37 FileValidationService + 18 ObjectNameFactory + 18 StorageFolders)

Command: dotnet test backend/tests/CulinaryBlog.Application.Tests/CulinaryBlog.Application.Tests.csproj --nologo
Result: Passed! - Failed: 0, Passed: 204, Skipped: 0, Total: 204
        (131 test Category cũ vẫn pass + 73 test MinIO foundation mới, không hồi quy)

Command: dotnet test backend/tests/CulinaryBlog.ArchitectureTests/CulinaryBlog.ArchitectureTests.csproj --nologo
Result: Passed! - Failed: 0, Passed: 3, Skipped: 0, Total: 3
        (Application layer mới không vi phạm Clean Architecture)

Command: dotnet build backend/CulinaryBlog.sln --nologo -v:m
Result: Build succeeded. 0 Warning(s), 0 Error(s)
        (lần chạy đầu báo CS2012 do file test dll còn bị tiến trình test trước giữ lock;
         chạy lại sau 10 giây thì succeeded — không phải lỗi code)
```

Không có test bị bỏ qua (Skipped: 0). Không có failure nào do code TV4; toàn bộ failure trong repo thật đều là lỗi baseline TV1 ở bước restore.

Xác minh phạm vi TV4-02 (`git status --short`): 17 file mới hoàn toàn là file MinIO foundation (13 source + 4 test) + `docs/TV4/06-REPORT-EVIDENCE.md` ở dạng untracked; không có file nào trong danh sách cấm bị tôi sửa (`Directory.Packages.props`, `Infrastructure/DependencyInjection.cs`, `docker-compose.yml`, `Program.cs`, `RecipeImage`, Recipe handler, API upload, `ApplicationDbContext`/migration). Lưu ý: `backend/src/CulinaryBlog.Infrastructure/CulinaryBlog.Infrastructure.csproj` đang staged (xóa 1 dòng `PackageReference Microsoft.EntityFrameworkCore` trùng) — đây là thay đổi còn lại từ Phase A, không phải của task TV4-02; `git diff` (unstaged) hiện rỗng. Chưa commit, chưa push.

### Trạng thái TV4-02:

- MinIO foundation: đã làm (code + test đã tạo trong Application layer, 0 file cấm bị sửa).
- MinIO integration thật: chưa làm, chờ xử lý dependency (repo lỗi baseline NU1506 của TV1 + contract RecipeImage/MinIO, bucket/public URL config, upload response shape CONFLICT-024, SDK MinIO, ai sửa DI chưa được team chốt).
- FR-FILE-001/FR-FILE-002: chưa hoàn thành toàn bộ.

## 4. Nội dung có thể đưa vào báo cáo

### Ghi chú rà soát phạm vi sau khi hoàn tất foundation

- `dotnet-tools.json` là file tooling ngoài phạm vi TV4-02 và đã được loại khỏi workspace.
- Thay đổi staged trong `backend/src/CulinaryBlog.Infrastructure/CulinaryBlog.Infrastructure.csproj` là thay đổi baseline còn lại từ Phase A, không được gộp vào phần MinIO foundation.
- Không thêm MinIO SDK, không tạo `MinioFileStorageService`, không đăng ký DI, không tạo API upload/delete và không sửa Docker hoặc các contract Recipe.
- Hangfire và Observability vẫn chưa triển khai vì thuộc giai đoạn sau, không phải phạm vi hiện tại.

### Cập nhật sau khi xử lý lỗi central package versions (22/09/2026)

Đã sửa `backend/Directory.Packages.props` để loại bỏ các khai báo trùng và đồng nhất phiên bản:

- Giữ `Microsoft.EntityFrameworkCore` ở `10.0.12`.
- Giữ `Npgsql.EntityFrameworkCore.PostgreSQL` ở `10.0.3`.
- Nâng `Microsoft.Extensions.Configuration.Abstractions` từ `10.0.0` lên `10.0.12` để tránh lỗi downgrade `NU1605`.

Kết quả kiểm tra trong repo thật:

```text
dotnet restore backend/CulinaryBlog.sln --nologo
Result: Succeeded — All projects are up-to-date for restore.

dotnet build backend/src/CulinaryBlog.Domain/CulinaryBlog.Domain.csproj --no-restore
Result: Build succeeded — 0 Warning(s), 0 Error(s).

dotnet build backend/src/CulinaryBlog.Application/CulinaryBlog.Application.csproj --no-restore
Result: Build succeeded — 0 Warning(s), 0 Error(s).

dotnet build backend/CulinaryBlog.sln --no-restore
Result: Failed tại AuthDbContext.cs(13,48), lỗi CS0104 do
ApplicationUser trùng giữa Infrastructure.Identity và Domain.Entities.

dotnet test backend/tests/CulinaryBlog.Application.Tests/CulinaryBlog.Application.Tests.csproj
Result: Chưa chạy được test vì project test tham chiếu Infrastructure và gặp cùng lỗi CS0104.
```

Thay đổi package là thay đổi dùng chung cho backend, nhưng chỉ loại bỏ bản khai báo trùng và chọn các phiên bản patch đã tồn tại trong repo; không thay đổi API nghiệp vụ. Lỗi còn lại thuộc file Auth/Infrastructure của TV1 và chưa tự ý sửa để tránh ảnh hưởng module của thành viên khác.

### Workaround tích hợp tạm thời cho AuthDbContext

Sau khi nhóm thống nhất cho phép sửa tạm để chạy kiểm tra tích hợp, đã thêm alias:

```csharp
using ApplicationUser = CulinaryBlog.Infrastructure.Identity.ApplicationUser;
```

Mục đích là giải quyết `CS0104` do đồng thời tồn tại `ApplicationUser` trong `Infrastructure.Identity` và `Domain.Entities`. Alias chỉ làm rõ `AuthDbContext` dùng model Identity của Auth; không thay đổi flow đăng nhập, JWT, schema hay contract API.

Đây là workaround tích hợp tạm thời, cần được TV1/nhóm trưởng rà soát lại khi xử lý các conflict Auth/Domain sau này. Không xem đây là quyết định kiến trúc cuối cùng.

Kết quả kiểm tra sau workaround:

```text
dotnet build backend/CulinaryBlog.sln --no-restore
Result: Build succeeded — 0 Warning(s), 0 Error(s).

dotnet test backend/tests/CulinaryBlog.Application.Tests/CulinaryBlog.Application.Tests.csproj --no-build
Result: Passed — 204/204.

dotnet test backend/tests/CulinaryBlog.ArchitectureTests/CulinaryBlog.ArchitectureTests.csproj --no-build
Result: Passed — 3/3.

dotnet test backend/tests/CulinaryBlog.Integration.Tests/CulinaryBlog.Integration.Tests.csproj --no-build
Result: Passed — 11/11 sau khi hoàn tất wiring tích hợp tối thiểu:
đăng ký ApplicationDbContext/IApplicationDbContext trong Infrastructure
và gọi MapCategoryEndpoints() trong Program.cs.

Các wiring này không tạo migration, không đổi entity/contract Recipe và chỉ kết nối
những thành phần Category đã có sẵn vào API.
```
