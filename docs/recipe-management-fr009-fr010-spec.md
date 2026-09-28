# Đặc tả quản lý nguyên liệu và bước nấu (FR-RCP-009, FR-RCP-010)

> Tài liệu dành cho lập trình viên triển khai và rà soát backend Recipe. Nội dung đối chiếu mã nguồn, kiểm thử và kết quả ghi PostgreSQL ngày 2026-09-28 với SRS và các quyết định về mâu thuẫn đặc tả.

## 1. Trạng thái hiện tại

**Context quản lý schema:** `AuthDbContext` là context chạy thật và là chuẩn migration cho các bảng Identity/Category/Recipe. Chỉ tạo migration bằng `--context AuthDbContext`. Migration cũ của `ApplicationDbContext` là di sản, không dùng để cập nhật database.

| Yêu cầu | Backend hiện có | Còn thiếu / cần xác minh | Trạng thái |
|---|---|---|---|
| FR-RCP-009 — quản lý nguyên liệu | Thực thể, cấu hình EF, DTO/ánh xạ, dữ liệu con khi tạo/cập nhật; contract POST/PUT/DELETE riêng, command, validation, phân quyền chủ sở hữu/admin, xóa mềm, invalidation cache, route, giao diện và kiểm thử handler/HTTP tự động. | Chưa xác nhận build frontend trong lượt này. | **Đã triển khai; API và ghi nguyên liệu vào PostgreSQL đã xác nhận** |
| FR-RCP-010 — quản lý bước nấu | Command Add/Update/Delete, validation, endpoint yêu cầu xác thực, xóa mềm/đánh lại số bước, PostgreSQL advisory transaction lock cho add/delete, migration, kiểm thử handler/HTTP và giao diện. | Chưa xác nhận build frontend trong lượt này. | **Đã triển khai; đăng nhập, API và ghi bước nấu vào PostgreSQL đã xác nhận** |

`RecipeEndpoints` có các route `/ingredients` và `/steps`. Trang frontend `/dashboard/recipes/{slug}/components` cho phép chủ recipe sửa nguyên liệu và bước nấu. Các migration AuthDbContext đã được áp dụng trên database đích `culinary_blog_auth`; kết quả PostgreSQL được ghi ở phần cuối tài liệu.

## 2. Quyết định và quy tắc dùng chung

- API gốc: `/api/v1/recipes`.
- Các lệnh thay đổi dữ liệu yêu cầu đăng nhập. Chỉ chủ sở hữu recipe hoặc admin được thêm/sửa/xóa nguyên liệu và bước nấu; không tìm thấy recipe/con trả 404, sai quyền trả 403.
- Lỗi validation trả **400 Problem Details** theo CONFLICT-011; dù checklist SRS cũ có chỗ nêu 422, quyết định chính thức là 400.
- Xóa mềm dùng `IsDeleted` và global query filter. GET chi tiết chỉ trả các con chưa bị xóa.
- Khi thay đổi nguyên liệu/bước, xóa cache chi tiết recipe theo `recipes:slug:{slug}` và yêu cầu invalidation danh sách qua prefix `recipes:` theo cache contract hiện có.
- Các quyết định liên quan được ghi tại `docs/srs-audit/SRS-CONFLICTS-AND-DECISIONS.md`: CONFLICT-004 (`Quantity`/`Unit` cho phép null), -005 (`OrderIndex`), -006 (`DurationMinutes`), -007 (có `Title`), -011 (HTTP 400), -015 (server tự sinh `StepNumber`).

## 3. FR-RCP-009 — quản lý nguyên liệu

### 3.1 Dữ liệu

Entity `RecipeIngredient` có `Id`, `RecipeId`, `Name`, `Quantity`, `Unit`, `Notes`, `OrderIndex` và các trường kế thừa `BaseEntity`. Mapping hiện có: `Name` bắt buộc tối đa 200 ký tự; `Quantity` nullable, precision `(10,3)`; `Unit` nullable tối đa 50; `Notes` nullable tối đa 500; query filter loại bản ghi `IsDeleted`.

Quy tắc nghiệp vụ:

- `name`: bắt buộc, sau trim không rỗng, tối đa 200 ký tự.
- `quantity`: có thể `null`; nếu có giá trị thì phải lớn hơn 0.
- `unit`: tùy chọn, tối đa 50 ký tự; hỗ trợ nguyên liệu “vừa đủ”.
- `notes`: tùy chọn, tối đa 500 ký tự.
- `orderIndex`: số nguyên không âm dùng để sắp xếp; `OrderIndex` là tên chuẩn, không dùng `SortOrder`.
- Khi đọc recipe detail, sắp xếp ingredients theo `OrderIndex` tăng dần.

### 3.2 Hợp đồng API

Tất cả route bên dưới nằm trong `/api/v1/recipes` và yêu cầu xác thực.

| Method | Route | Mục đích | Thành công |
|---|---|---|---|
| POST | `/{id:guid}/ingredients` | Thêm một nguyên liệu vào recipe | `201 Created` + ingredient DTO |
| PUT | `/{id:guid}/ingredients/{ingredientId:guid}` | Thay thế các trường có thể sửa | `200 OK` + ingredient DTO |
| DELETE | `/{id:guid}/ingredients/{ingredientId:guid}` | Xóa mềm nguyên liệu | `204 No Content` |

Nội dung request cho POST và PUT:

```json
{
  "name": "Muối",
  "quantity": 2.5,
  "unit": "g",
  "notes": null,
  "orderIndex": 0
}
```

`quantity`, `unit`, `notes` có thể là `null`; `orderIndex` dùng tên chuẩn theo CONFLICT-005. Response trả `RecipeIngredientDto` gồm `id`, `name`, `quantity`, `unit`, `notes`, `orderIndex`.

### 3.3 Các bước triển khai FR-RCP-009

Trình tự dưới đây ghi lại cách xây dựng chức năng và trạng thái từng phần; backend, kiểm thử tự động và ghi dữ liệu PostgreSQL đã hoàn tất.

1. ~~**Entity và schema**~~ — định nghĩa `RecipeIngredient` tại `backend/src/CulinaryBlog.Domain/Entities/RecipeIngredient.cs`; cấu hình độ dài, precision, khóa ngoại và query filter tại `backend/src/CulinaryBlog.Infrastructure/Persistence/Configurations/RecipeIngredientConfiguration.cs`.
2. ~~**Hợp đồng và ánh xạ**~~ — tạo contract request POST/PUT tại `backend/src/CulinaryBlog.API/Contracts/Recipes/RecipeIngredientRequests.cs`; dùng `OrderIndex`, không nhận `RecipeId` từ body. Ánh xạ recipe detail trả danh sách ingredient theo `OrderIndex`.
3. ~~**Command, kiểm tra dữ liệu và phân quyền**~~ — cài handler Add/Update/Delete tại `backend/src/CulinaryBlog.Application/Features/Recipes/Commands/RecipeIngredientCommands.cs`; kiểm tra tên, `quantity` có thể null nhưng nếu có phải > 0, đồng thời xác minh recipe/chủ sở hữu và quan hệ ingredient-recipe trước update/delete.
4. ~~**Lưu dữ liệu và cache**~~ — POST thêm entity, PUT cập nhật các trường được phép, DELETE đặt `IsDeleted`; sau khi lưu thì xóa cache recipe detail theo slug và gọi cache contract theo prefix.
5. ~~**Endpoint HTTP**~~ — nối POST/PUT/DELETE tại `backend/src/CulinaryBlog.API/Endpoints/RecipeEndpoints.cs`; yêu cầu bearer token và giữ response 201/200/204, 400/401/403/404.
6. ~~**Kiểm thử handler**~~ — kiểm tra thêm/sửa/xóa mềm, validation, owner/admin, recipe của user khác, child không thuộc recipe và invalidation cache chi tiết/prefix tại `backend/tests/CulinaryBlog.Integration.Tests/Features/Recipes/RecipeIngredientCommandHandlerTests.cs`.
7. ~~**Kiểm thử tích hợp HTTP**~~ — kiểm tra auth (401), forbidden (403), not found (404), validation 400 Problem Details, create 201, update 200, delete 204 và GET detail không trả ingredient đã xóa tại `backend/tests/CulinaryBlog.Integration.Tests/Endpoints/RecipeIngredientEndpointsTests.cs`.
8. ~~**Migration và xác minh PostgreSQL**~~ — đối chiếu schema `AuthDbContext` trên `culinary_blog_auth`; gọi POST ingredient qua API Production, rồi truy vấn trực tiếp `RecipeIngredients` theo `RecipeId` để xác nhận dữ liệu được lưu. Kết quả thật ở mục 8.

## 4. FR-RCP-010 — quản lý bước nấu

### 4.1 Dữ liệu và quy tắc nghiệp vụ

Entity `RecipeStep` có `Id`, `RecipeId`, `StepNumber`, `Title`, `Description`, `DurationMinutes`, `ImageUrl` và các trường kế thừa `BaseEntity`.

- `title`: bắt buộc trên API, tối đa 200 ký tự; CONFLICT-007 chọn có Title.
- `description`: bắt buộc, trim trước khi lưu.
- `durationMinutes`: tùy chọn; nếu có thì >= 0. Dùng `DurationMinutes`, không dùng `TimerMinutes`.
- `imageUrl`: tùy chọn, tối đa 500 ký tự; chuỗi rỗng được chuẩn hóa thành `null`.
- `stepNumber`: server quản lý. POST gán `Max(StepNumber) + 1` tính cả hàng đã xóa mềm; client không gửi trường này. PUT không đổi thứ tự.
- DELETE xóa mềm target và đánh lại các bước active liên tục từ 1. Hàng đã xóa được chuyển sang số âm để tránh va chạm unique index với lịch sử.
- Unique index `(RecipeId, StepNumber)` chỉ áp dụng cho hàng active; entity có query filter `!IsDeleted`.

### 4.2 API hiện có

| Method | Route | Body | Thành công |
|---|---|---|---|
| POST | `/api/v1/recipes/{id:guid}/steps` | `{ "title", "description", "durationMinutes", "imageUrl" }` | `201 Created` + `RecipeStepDto` |
| PUT | `/api/v1/recipes/{id:guid}/steps/{stepId:guid}` | Các trường nội dung như POST; không có `stepNumber` | `200 OK` + `RecipeStepDto` |
| DELETE | `/api/v1/recipes/{id:guid}/steps/{stepId:guid}` | Không có | `204 No Content` |

Các route đang nối trong `RecipeEndpoints.cs`; logic nằm tại `RecipeStepCommands.cs`, contract tại `RecipeStepRequests.cs`. Không thêm lại `StepNumber` vào request.

### 4.3 Các bước triển khai/hoàn tất FR-RCP-010

Trình tự triển khai FR-RCP-010; backend, kiểm thử và xác nhận ghi PostgreSQL đã hoàn tất. Giao diện frontend đã có trong mã nguồn nhưng chưa xác nhận build.

1. ~~**Thực thể và schema**~~ — định nghĩa `RecipeStep` tại `backend/src/CulinaryBlog.Domain/Entities/RecipeStep.cs`; cấu hình Fluent tại `backend/src/CulinaryBlog.Infrastructure/Persistence/Configurations/RecipeStepConfiguration.cs` quy định cột, query filter và unique index cho step đang hoạt động `(RecipeId, StepNumber)`.
2. ~~**Migration**~~ — tạo migration `20260925090000_AddActiveRecipeStepOrderIndex` để điều chỉnh độ dài Title và unique index thứ tự step đang hoạt động; các migration AuthDbContext đã áp dụng trên `culinary_blog_auth`, migration history đã được verifier kiểm tra.
3. ~~**Hợp đồng request/response**~~ — dùng `RecipeStepRequests.cs` và DTO với `Title`, `Description`, `DurationMinutes`, `ImageUrl`; không nhận `StepNumber` từ client.
4. ~~**Command và nghiệp vụ**~~ — cài Add/Update/Delete tại `backend/src/CulinaryBlog.Application/Features/Recipes/Commands/RecipeStepCommands.cs`; kiểm tra payload/quyền owner-admin, xác minh step thuộc recipe, trim nội dung, xóa mềm và đánh lại số step đang hoạt động liên tục sau delete.
5. ~~**Đồng bộ thứ tự khi ghi**~~ — dùng `IRecipeMutationLock` với PostgreSQL transaction-scoped advisory lock để tuần tự hóa add/delete cùng recipe; verifier đã kiểm tra add/add đồng thời và add/delete đồng thời.
6. ~~**Cache và endpoint**~~ — xóa cache detail/prefix sau khi lưu; nối POST/PUT/DELETE trong `backend/src/CulinaryBlog.API/Endpoints/RecipeEndpoints.cs`.
7. ~~**Kiểm thử tự động**~~ — kiểm thử handler tại `RecipeStepCommandHandlerTests.cs`, kiểm thử HTTP tại `RecipeStepEndpointsTests.cs`; kiểm tra server tự cấp số step, update giữ nguyên thứ tự, soft delete/đánh lại số, phân quyền và validation.
8. ~~**API và xác minh PostgreSQL**~~ — đăng ký/đăng nhập API trả 201/200; POST step trả 201 và truy vấn `RecipeSteps` theo `RecipeId` xác nhận bản ghi đã lưu trên `culinary_blog_auth`. Recipe mẫu có 5 bước được giữ lại; kết quả và truy vấn SQL tại mục 8.
9. **Frontend** — giao diện chung tại `frontend/src/app/dashboard/recipes/[slug]/components/page.tsx` đã có chức năng thêm/sửa/xóa nguyên liệu và step; chạy frontend build khi cài đủ dependencies.

## 5. Ma trận lỗi và tiêu chí nghiệm thu

| Tình huống | HTTP |
|---|---:|
| Body sai validation | 400 Problem Details, `errors` theo field |
| Chưa đăng nhập | 401 |
| Không phải owner/admin | 403 |
| Recipe hoặc child không tồn tại/không thuộc recipe | 404 |
| Conflict dữ liệu/thứ tự hoặc concurrency | 409 |

Nghiệm thu FR-RCP-009 khi ba endpoint nguyên liệu hoạt động, kiểm thử tự động về phân quyền/validation/cache đạt, thao tác xóa là xóa mềm, recipe detail sắp xếp đúng `OrderIndex` và PostgreSQL có bản ghi ingredient sau POST. Nghiệm thu FR-RCP-010 khi các route hoạt động, migration đã áp dụng, kiểm thử xác nhận xóa/đánh lại số step liên tục và PostgreSQL có bản ghi step sau POST.

## 6. Tài liệu tham chiếu

- `docs/recipe-core-implementation-spec.md` — trạng thái kỹ thuật Recipe Core.
- `implementation_plan.md` — kế hoạch triển khai tổng thể.
- `docs/srs-audit/SRS-71-PAGE-MASTER-CHECKLIST.md` — SRS pages 35–36, 57, 65.
- `docs/srs-audit/SRS-CONFLICTS-AND-DECISIONS.md` — CONFLICT-004, -005, -006, -007, -011, -015.
- `docs/srs-audit/TEAM-REQUIREMENT-TRACEABILITY.md` — phân công và trạng thái yêu cầu.

## 7. Trạng thái xác minh PostgreSQL đích (2026-09-28)

Trước khi migration, database PostgreSQL nguồn được kiểm tra ở chế độ chỉ đọc. Nguồn chứa lịch sử migration cũ của `ApplicationDbContext` và dữ liệu nghiệp vụ; không áp dụng migration lên database nguồn đang có dữ liệu. Phương án được chọn là giữ nguyên `culinary_blog`, tạo `culinary_blog_auth` theo chuỗi migration của `AuthDbContext`, rồi nhập dữ liệu nghiệp vụ đã qua kiểm tra.

**Cập nhật thực hiện (2026-09-28; thay thế trạng thái database còn chờ nêu phía trên):** người dùng xác nhận đã sao lưu nguồn và tạo database đích. Cả ba migration `AuthDbContext` đã được áp dụng lên `culinary_blog_auth`. Các bước kiểm tra trước khi nhập và trong transaction đều đạt; đã nhập 1 user, 20 category, 100 recipe, 1.000 ingredient, 500 step, 100 nutrition và 0 ảnh. Database nguồn chỉ được đọc.

Kiểm tra PostgreSQL phát hiện RowVersion cũ vẫn được chấp nhận khi cập nhật; lỗi đã được sửa bằng migration `AddPostgresRowVersionTriggers`. Tool `backend/tools/CulinaryBlog.PostgresVerification` đã kiểm tra đạt lịch sử migration, mật khẩu/vai trò Identity của user tạm, RowVersion cũ của Category, xóa mềm/query filter của Category và Recipe, thêm/xóa/đánh lại số step tuần tự, thêm đồng thời và thêm/xóa đồng thời. Tool dọn các bản ghi tạm sau khi chạy. Người dùng đã khôi phục bản sao lưu nguồn vào `culinary_blog_backup_verify`; kiểm tra schema và số dòng ở chế độ chỉ đọc khớp với bước kiểm tra trước khi nhập. Connection của ứng dụng đã chuyển sang `culinary_blog_auth`; API Production trả về đúng tổng số recipe đã nhập. Mật khẩu thật của tài khoản được nhập chưa được xác minh qua đăng nhập của người dùng. Smoke test API Production phát hiện lỗi phân biệt chữ hoa/thường trong validator `sortBy` mặc định; lỗi đã được sửa và có regression test. Build thành công, không có warning/error; toàn bộ 102 test solution đạt. Ghi chép chi tiết tại [`postgresql-data-migration-plan.md`](postgresql-data-migration-plan.md).

## 8. Các bước gọi API và xác nhận ghi PostgreSQL (2026-09-28)

### 8.1 Luồng thực hiện

1. Khởi động API với connection string trỏ tới `culinary_blog_auth`; dùng đúng địa chỉ API đó cho các request tiếp theo. Nếu cổng mặc định đang bị chiếm, chọn cổng khác và cập nhật `$base`.
2. Đăng ký/đăng nhập, lấy `accessToken` và gửi header `Authorization: Bearer <token>` trong các lệnh thay đổi dữ liệu.
3. Tạo recipe bằng `POST /api/v1/recipes`. Dùng `categoryId` đang tồn tại; response `201` trả `id` và `slug` để dùng ở các bước sau.
4. Với `id` vừa tạo, gọi `POST /api/v1/recipes/{id}/ingredients` cho từng nguyên liệu. Body nhận `name`, `quantity`, `unit`, `notes`, `orderIndex`; `quantity`, `unit`, `notes` có thể null.
5. Gọi `POST /api/v1/recipes/{id}/steps` cho từng bước. Body gồm `title`, `description`, `durationMinutes`, `imageUrl`; không gửi `stepNumber` vì server tự cấp số thứ tự.
6. Truy vấn PostgreSQL theo recipe ID để xác nhận dữ liệu trong `Recipes`, `RecipeIngredients`, `RecipeSteps`; chỉ đếm các bản ghi con chưa bị xóa mềm.

### 8.2 Mẫu PowerShell gọi API

Đoạn dưới giả sử đã đăng nhập và `$login.accessToken`, `$categoryId` là ID của category đang tồn tại. Gửi JSON bằng UTF-8 để giữ nguyên tiêu đề/nguyên liệu tiếng Việt, đặc biệt trên Windows PowerShell 5.1.

```powershell
$base = "http://127.0.0.1:5000/api/v1" # đổi port nếu API chạy port khác
$headers = @{ Authorization = "Bearer $($login.accessToken)" }

$recipeJson = @{
  title = "Canh chua cá"
  description = "Canh chua cá nấu cùng thơm, cà chua và rau thơm."
  categoryId = [guid]$categoryId
  prepTimeMinutes = 20
  cookTimeMinutes = 30
  servings = 4
  difficulty = "Easy"
} | ConvertTo-Json -Depth 8
$recipe = Invoke-RestMethod -Method Post -Uri "$base/recipes" -Headers $headers `
  -ContentType "application/json; charset=utf-8" -Body ([Text.Encoding]::UTF8.GetBytes($recipeJson))

$ingredientJson = @{
  name = "Cá lóc"; quantity = 500; unit = "g"; notes = $null; orderIndex = 0
} | ConvertTo-Json -Depth 8
$ingredient = Invoke-RestMethod -Method Post -Uri "$base/recipes/$($recipe.id)/ingredients" `
  -Headers $headers -ContentType "application/json; charset=utf-8" `
  -Body ([Text.Encoding]::UTF8.GetBytes($ingredientJson))

$stepJson = @{
  title = "Sơ chế cá"; description = "Làm sạch cá, cắt khúc vừa ăn và để ráo.";
  durationMinutes = 5; imageUrl = $null
} | ConvertTo-Json -Depth 8
$step = Invoke-RestMethod -Method Post -Uri "$base/recipes/$($recipe.id)/steps" `
  -Headers $headers -ContentType "application/json; charset=utf-8" `
  -Body ([Text.Encoding]::UTF8.GetBytes($stepJson))
```

Recipe, ingredient và step mới phải thuộc recipe mà user hiện tại có quyền sửa. Lặp POST ingredient với `orderIndex` tăng dần và POST step cho từng dòng; server tự gán `stepNumber`. Các POST thành công trả 201. Response tạo recipe dùng slug ASCII trong `Location`, không dùng title Unicode.

### 8.3 Kết quả chạy trực tiếp

Đã chạy đăng ký (201), đăng nhập (200), tạo recipe (201), thêm nguyên liệu (201) và thêm bước nấu (201) qua API Production kết nối PostgreSQL `culinary_blog_auth`. Truy vấn trực tiếp trước khi dọn dữ liệu chẩn đoán xác nhận `Recipes = 1`, `RecipeIngredients = 1`, `RecipeSteps = 1` cho cùng một recipe ID. Các bản ghi chẩn đoán và tài khoản tạm đã được xóa sau khi kiểm tra.

Sau đó đã tạo recipe **“Canh chua cá”** qua API: slug `canh-chua-ca`, 7 nguyên liệu và 5 bước nấu. Truy vấn PostgreSQL xác nhận đủ `1 / 7 / 5` bản ghi tương ứng; các bản ghi này được giữ lại. Recipe thuộc tài khoản demo được tạo trong phiên thao tác; thông tin đăng nhập đã gửi riêng trong hội thoại, không ghi mật khẩu vào tài liệu.

Trong lần kiểm tra, tiêu đề Unicode làm HTTP `Location` header không hợp lệ vì endpoint đưa trực tiếp `command.Title` vào header. Endpoint đã được sửa để dùng `recipe.Slug`; kiểm thử hồi quy với tiêu đề “Canh chua cá” đã đạt. Cần khởi động lại API cũ để nạp bản sửa.

### 8.4 Truy vấn xác nhận tham khảo

```sql
SELECT r."Id", r."Title", r."Slug",
       (SELECT count(*) FROM "RecipeIngredients" i
        WHERE i."RecipeId" = r."Id" AND NOT i."IsDeleted") AS "IngredientCount",
       (SELECT count(*) FROM "RecipeSteps" s
        WHERE s."RecipeId" = r."Id" AND NOT s."IsDeleted") AS "StepCount"
FROM "Recipes" r
WHERE r."Slug" = 'canh-chua-ca';
```
