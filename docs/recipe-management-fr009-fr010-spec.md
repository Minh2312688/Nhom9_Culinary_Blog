# Đặc tả quản lý nguyên liệu và bước nấu (FR-RCP-009, FR-RCP-010)

> Tài liệu dành cho lập trình viên triển khai và rà soát backend Recipe. Nội dung đối chiếu mã nguồn hiện tại ngày 2026-09-28 với SRS, quyết định conflict và test đang có. Các mục “Bước triển khai” là checklist để hoàn thiện chức năng còn thiếu.

## 1. Trạng thái hiện tại

**Schema owner:** `AuthDbContext` là context runtime và migration chuẩn cho các bảng Identity/Category/Recipe. Chỉ scaffold migration bằng `--context AuthDbContext`. `ApplicationDbContext` migrations cũ là legacy, không dùng để cập nhật database.

| Yêu cầu | Backend hiện có | Còn thiếu / cần xác minh | Trạng thái |
|---|---|---|---|
| FR-RCP-009 — quản lý nguyên liệu | Entity, EF configuration, DTO/mapping, nested Create/Update; standalone POST/PUT/DELETE contracts, commands, validation, owner/admin authorization, soft delete, cache invalidation, routes và editor UI. | Automated tests cho handler/HTTP contract. | **Đã triển khai code; test xác nhận còn thiếu** |
| FR-RCP-010 — quản lý bước nấu | Add/Update/Delete commands, validation, authenticated endpoints, soft delete/đánh lại số bước, PostgreSQL advisory transaction lock cho add/delete, migration, handler/HTTP tests và editor UI. | Áp dụng migration và kiểm chứng concurrency với PostgreSQL/Testcontainers. | **Code đã triển khai; xác minh database còn tiếp tục** |

Ở trạng thái hiện tại, `RecipeEndpoints` có các route `/ingredients` và `/steps`. Trang frontend `/dashboard/recipes/{slug}/components` cho phép chủ recipe sửa ingredients và steps. Migration FR-RCP-010 vẫn cần được apply và xác nhận trên PostgreSQL trước khi coi schema đã triển khai lên môi trường database.

## 2. Quyết định và quy tắc dùng chung

- API gốc: `/api/v1/recipes`.
- Các lệnh thay đổi dữ liệu yêu cầu đăng nhập. Chỉ chủ sở hữu recipe hoặc admin được thêm/sửa/xóa nguyên liệu và bước nấu; không tìm thấy recipe/con trả 404, sai quyền trả 403.
- Lỗi validation trả **400 Problem Details** theo CONFLICT-011; dù checklist SRS cũ có chỗ nêu 422, quyết định chính thức là 400.
- Xóa mềm dùng `IsDeleted` và global query filter. GET chi tiết chỉ trả các con chưa bị xóa.
- Khi thay đổi nguyên liệu/bước, xóa cache chi tiết recipe theo `recipes:slug:{slug}` và yêu cầu invalidation danh sách qua prefix `recipes:` theo cache contract hiện có.
- Các quyết định liên quan được ghi tại `docs/srs-audit/SRS-CONFLICTS-AND-DECISIONS.md`: CONFLICT-004 (Quantity/Unit nullable), -005 (`OrderIndex`), -006 (`DurationMinutes`), -007 (có `Title`), -011 (HTTP 400), -015 (server sinh `StepNumber`).

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

### 3.2 API contract cần có

Tất cả route bên dưới nằm trong `/api/v1/recipes` và yêu cầu xác thực.

| Method | Route | Mục đích | Thành công |
|---|---|---|---|
| POST | `/{id:guid}/ingredients` | Thêm một nguyên liệu vào recipe | `201 Created` + ingredient DTO |
| PUT | `/{id:guid}/ingredients/{ingredientId:guid}` | Thay thế các trường có thể sửa | `200 OK` + ingredient DTO |
| DELETE | `/{id:guid}/ingredients/{ingredientId:guid}` | Xóa mềm nguyên liệu | `204 No Content` |

Request body cho POST và PUT:

```json
{
  "name": "Muối",
  "quantity": 2.5,
  "unit": "g",
  "notes": null,
  "orderIndex": 0
}
```

`quantity`, `unit`, `notes` có thể là `null`; `orderIndex` dùng tên chuẩn theo CONFLICT-005. Response dùng `RecipeIngredientDto`: `id`, `name`, `quantity`, `unit`, `notes`, `orderIndex`.

### 3.3 Các bước triển khai FR-RCP-009

Các bước 1–5 đã được triển khai trong working tree; bước kiểm thử tự động chưa được thực hiện.

1. ~~Thêm request contracts~~ — đã có tại `backend/src/CulinaryBlog.API/Contracts/Recipes/RecipeIngredientRequests.cs`; dùng `OrderIndex`, không nhận `RecipeId` từ body.
2. ~~Thêm commands, validators và handlers~~ — đã có tại `backend/src/CulinaryBlog.Application/Features/Recipes/Commands/RecipeIngredientCommands.cs`; kiểm tra recipe/owner và xác minh ingredient thuộc recipe trước PUT/DELETE.
3. ~~Thực hiện thao tác dữ liệu~~ — POST tạo, PUT cập nhật, DELETE đánh `IsDeleted = true`.
4. ~~Invalidation cache~~ — xóa detail key theo slug và yêu cầu prefix invalidation theo cache contract.
5. ~~Nối endpoint~~ — ba route ingredient được nối trong `backend/src/CulinaryBlog.API/Endpoints/RecipeEndpoints.cs`.
6. **Viết handler tests** kiểm tra thêm, sửa, xóa mềm, validation, owner/admin, user khác, recipe không tồn tại, ingredient không thuộc recipe và cache bị invalidated.
7. **Viết HTTP integration tests** kiểm tra auth (401), forbidden (403), not found (404), validation 400 Problem Details, create 201, update 200 và delete 204; xác nhận GET detail không còn trả ingredient đã xóa.
8. **Rà schema/migration:** entity/configuration và cột cần thiết đã có; chỉ tạo migration nếu model thay đổi. Kiểm tra migration snapshot và apply trên PostgreSQL trước khi đánh dấu hoàn tất.

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

1. ~~Giữ contract thống nhất~~ — request, validator và DTO cùng dùng `Title`, `Description`, `DurationMinutes`, `ImageUrl`; client không truyền `StepNumber`.
2. ~~Triển khai command/handler~~ — owner/admin, membership, 404/403, trim text và giới hạn `int.MaxValue` đã được xử lý trong `RecipeStepCommands.cs`.
3. ~~Xóa mềm và renumber cơ bản~~ — target được đánh `IsDeleted`, các bước active đánh lại từ 1, deleted rows được chuyển sang số âm.
4. ~~Invalidation cache~~ — handler xóa detail key và gọi invalidation prefix sau khi lưu.
5. **Migration database:** migration đã có trong source; cần apply lên PostgreSQL và kiểm tra migration history.
6. **Kiểm thử:** handler và HTTP test files đã có; cần chạy chúng, sau đó bổ sung PostgreSQL/Testcontainers test cho unique index và thao tác đồng thời.
7. ~~Serialize thay đổi thứ tự theo recipe~~ — `IRecipeMutationLock` dùng PostgreSQL transaction-scoped advisory lock để add/delete cùng recipe lần lượt thực thi; delete giữ lock trong toàn bộ chuỗi save và rollback nếu lỗi. Cần xác minh hành vi cạnh tranh trên PostgreSQL/Testcontainers để đóng TECH-RISK-012.
8. ~~Hoàn thiện UI bước nấu~~ — đã có editor chung cho nguyên liệu và bước tại `frontend/src/app/dashboard/recipes/[slug]/components/page.tsx`; frontend chưa được build trong lượt này.

## 5. Ma trận lỗi và tiêu chí nghiệm thu

| Tình huống | HTTP |
|---|---:|
| Body sai validation | 400 Problem Details, `errors` theo field |
| Chưa đăng nhập | 401 |
| Không phải owner/admin | 403 |
| Recipe hoặc child không tồn tại/không thuộc recipe | 404 |
| Conflict dữ liệu/thứ tự hoặc concurrency | 409 |

Nghiệm thu FR-RCP-009 khi ba endpoint ingredient hoạt động, automated authorization/validation/cache tests đạt, xóa là soft delete và recipe detail sort đúng `OrderIndex`. Nghiệm thu FR-RCP-010 khi các route hoạt động, migration đã apply, tests xác nhận delete/renumber và concurrency verification trên PostgreSQL đạt.

## 6. Tài liệu tham chiếu

- `docs/recipe-core-implementation-spec.md` — trạng thái kỹ thuật Recipe Core.
- `implementation_plan.md` — kế hoạch triển khai tổng thể.
- `docs/srs-audit/SRS-71-PAGE-MASTER-CHECKLIST.md` — SRS pages 35–36, 57, 65.
- `docs/srs-audit/SRS-CONFLICTS-AND-DECISIONS.md` — CONFLICT-004, -005, -006, -007, -011, -015.
- `docs/srs-audit/TEAM-REQUIREMENT-TRACEABILITY.md` — phân công và trạng thái yêu cầu.
