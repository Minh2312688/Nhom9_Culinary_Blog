# Kế hoạch triển khai FR-RCP-008: Quản lý ảnh công thức

> Trạng thái: Core implementation hoàn tất; FR-JOB-002 thumbnail processing và live MinIO/PostgreSQL verification còn mở. Phạm vi gồm contract/API ảnh công thức, nối với MinIO foundation sẵn có, kiểm soát quyền sở hữu và chuyển `Recipe.Difficulty` sang enum domain type theo lựa chọn đã xác nhận.

## 1. Mục tiêu

Hoàn thiện luồng upload, đặt ảnh chính và xóa ảnh cho Recipe; đặt toàn bộ HTTP routes liên quan Recipe trong `backend/src/CulinaryBlog.API/Endpoints/`. Giữ API hiện có ổn định, chỉ thêm endpoint ảnh và endpoint module Recipe còn thiếu khi đã được các FR hiện hành mô tả.

## 2. Bằng chứng hiện trạng

- `RecipeEndpoints.cs` đã chứa list/detail/create/update/publish/archive/delete và nested step routes.
- `RecipeImage` entity, quan hệ Recipe, DTO detail, migration và `IApplicationDbContext.RecipeImages` đã có.
- `IFileStorageService`, `MinioFileStorageService`, object-storage adapter và `FileValidationService` đã có; validation kiểm tra JPEG/PNG/WebP/AVIF, 5 MB, extension/MIME và magic bytes.
- RecipeImage commands, HTTP routes và đăng ký DI cho `IFileStorageService`/MinIO client đã được triển khai; unit/integration tests liên quan đã có.
- Chưa có FR-JOB-002 thumbnail processor/queue; upload hiện chỉ tạo original image, chưa thể tuyên bố đã tạo Medium/Thumbnail.
- `Recipe.Difficulty` hiện là `string`; command handlers, mappings, projections, seeders và EF configuration phụ thuộc kiểu này.

## 3. Contract và quyết định

- Upload: `POST /api/v1/recipes/{id:guid}/images` với multipart `file`, `altText?`, `isPrimary?`; yêu cầu authenticated owner hoặc Admin; tối đa 10 ảnh; tối đa 5 MB.
- Đặt primary: `PATCH /api/v1/recipes/{id:guid}/images/{imageId:guid}/primary`.
- Xóa: `DELETE /api/v1/recipes/{id:guid}/images/{imageId:guid}`.
- Upload response: `201 Created` và `{ imageId, originalUrl, altText, isPrimary }`, theo quyết định DECIDED của CONFLICT-024. Không trả object name/internal bucket path.
- Dùng route đặt primary chuyên biệt theo quyết định DECIDED của CONFLICT-023.
- Invalid file trả 400 theo exception mapping hiện tại; recipe/image không tồn tại trả 404; anonymous 401; user không phải owner 403; object storage unavailable trả 503 Problem Details.
- `Recipe.Difficulty` đổi sang `RecipeDifficulty` enum; DTO và HTTP contract tiếp tục biểu diễn tên mức độ dạng string. EF chuyển enum sang string để giữ nguyên kiểu/cột hiện có.
- Enum giữ các difficulty hiện được lưu và được SRS data model cho phép; query filter SRCH-002 vẫn chỉ chấp nhận Easy/Medium/Hard.
- Không tự cài đặt giả FR-JOB-002. Tạo/giữ một seam rõ ràng để enqueue thumbnail khi queue contract được cung cấp; nếu dependency chưa có, đánh dấu phần resize là blocker thay vì no-op.

## 4. Cấu trúc endpoint Recipe

`RecipeEndpoints.cs` tiếp tục sở hữu:

- `GET /api/v1/recipes/`
- `GET /api/v1/recipes/{slug}`
- `POST /api/v1/recipes/`
- `PUT /api/v1/recipes/{id}`
- `PATCH /api/v1/recipes/{id}/publish`
- `PATCH /api/v1/recipes/{id}/archive`
- `DELETE /api/v1/recipes/{id}`
- `POST|PUT|DELETE /api/v1/recipes/{id}/steps...`

Tạo `RecipeImageEndpoints.cs` cho ba route FR-RCP-008; đăng ký qua `MapRecipeImageEndpoints()` trong `Program.cs`. Không sao chép hoặc đăng ký trùng các route đang tồn tại.

## 5. Công việc

### Task A: Domain difficulty enum và tương thích lưu trữ

- Tạo `RecipeDifficulty` trong Domain; đổi property `Recipe.Difficulty` sang enum và default `Easy`.
- Cấu hình EF `HasConversion<string>()`, giữ giới hạn/cột database hiện hành; xác nhận snapshot/migration không sinh thay đổi schema ngoài ý muốn.
- Cập nhật create/update validators và handlers để parse input một cách case-insensitive, từ chối giá trị ngoài enum, và lưu enum.
- Giữ DTO/API `Difficulty` là string; cập nhật mapper, list/search projections, seeders và test data để serialize tên enum.
- Test round-trip mapping/string conversion và validation các giá trị hợp lệ/không hợp lệ.

### Task B: Application use cases cho image

- Tạo request/response contracts cho upload và primary update.
- Tạo handlers cho upload, set primary và delete; kiểm tra recipe tồn tại và owner/Admin trước mọi thao tác storage.
- Upload: kiểm tra số ảnh hiện tại < 10; nếu upload `isPrimary=true`, bỏ primary cũ trong cùng save; gán `OrderIndex` tiếp theo; gọi storage với folder recipe images; lưu metadata; nếu save DB thất bại, cố gắng xóa object vừa upload để tránh orphan.
- Set primary: xác nhận image thuộc đúng recipe, bỏ primary các ảnh khác, đặt ảnh đích thành primary, lưu một lần và invalidate recipe cache.
- Delete: chỉ xóa ảnh thuộc recipe; giữ dữ liệu nhất quán giữa soft-delete và object storage; cache invalidation theo prefix recipe. Xác định rõ lỗi storage để trả 503 và không trả chi tiết nội bộ.
- Đảm bảo chỉ có một primary active image/recipe bằng cấu hình index/constraint PostgreSQL nếu compatible với migration hiện có; xử lý concurrency của giới hạn 10 trong persistence boundary hoặc ghi rõ rủi ro còn lại.

### Task C: Nối MinIO và background dependency

- Đăng ký `IFileValidationService`, `IObjectStorageClient`, `IFileStorageService` trong Infrastructure DI bằng cấu hình MinIO hiện có; không hard-code credential.
- Đảm bảo thiếu cấu hình MinIO fail-fast hoặc trả lỗi service unavailable có Problem Details, không để lỗi MinIO thành 500 không kiểm soát.
- Tìm/nhận contract FR-JOB-002 từ TV4. Chỉ enqueue khi abstraction thực có implementation; test verify enqueue được gọi. Nếu TV4 chưa cung cấp, để checklist ghi rõ thumbnail resize chưa hoàn thành.

### Task D: HTTP endpoints và bảo mật

- Tạo `RecipeImageEndpoints.cs` theo pattern Minimal API hiện tại, bind `IFormFile`/multipart và chuyển stream seekable vào Application request.
- Giới hạn upload 5 MB, đặt metadata multipart; không trust filename/MIME thay magic-byte validation hiện có.
- Require authorization cho mutations; xác thực owner/Admin trong handler để giữ quy tắc domain.
- Khai báo `Produces` cho 201/200/204 và Problem Details 400/401/403/404/503.
- Map module trong `Program.cs`; xác nhận endpoint routing không xung đột với `{slug}` hoặc `{id}`.

### Task E: Test và tài liệu

- Unit tests: file valid/invalid, 10-image boundary, owner/Admin, wrong recipe-image pair, primary replacement, deletion, cache invalidation, storage errors và DB save compensation.
- API integration tests: anonymous 401, owner success, forbidden owner 403, invalid file 400, not found 404, upload response shape, primary route 200/204 theo contract, delete 204.
- MinIO adapter tests mock object client; nếu có MinIO Testcontainer thì thêm smoke test upload/delete, không làm unit suite phụ thuộc service ngoài.
- Chạy focused tests, toàn bộ `CulinaryBlog.sln`, build API và frontend nếu contract DTO/API có ảnh hưởng UI.
- Cập nhật `docs/srs-audit/SRS-AUDIT.md`, `SRS-CONFLICTS-AND-DECISIONS.md` chỉ khi có quyết định chính thức mới; cập nhật traceability/checklist về trạng thái code đã test và blocker FR-JOB-002.

## 6. Acceptance criteria

- Tối đa 10 ảnh cho mỗi recipe; upload chỉ nhận JPEG/PNG/WebP/AVIF, magic bytes khớp MIME/extension, tối đa 5 MB.
- Chỉ owner/Admin thao tác; các route chỉ thay đổi đúng recipe và image id được truyền.
- Mỗi recipe có tối đa một ảnh primary active; chọn ảnh mới bỏ primary trước đó.
- Upload response đúng `{ imageId, originalUrl, altText, isPrimary }`; upload thành công trả 201, set primary trả 200, delete trả 204.
- File storage lỗi trả 503; error response không lộ credential/bucket internals.
- Recipe Difficulty là enum ở Domain; dữ liệu PostgreSQL tiếp tục lưu string; API/DTO duy trì string wire contract.
- Tất cả tests liên quan và full solution pass; route map đầy đủ, không trùng endpoint.
- Thumbnail resize chỉ được coi là hoàn tất khi FR-JOB-002 có queue/worker thực và integration test chứng minh job được enqueue.

## 7. Rủi ro/điều kiện còn mở

- Hangfire/FR-JOB-002 chưa xuất hiện trong backend hiện tại.
- Race giữa hai upload đồng thời có thể vượt giới hạn 10 nếu chỉ đếm trong handler; cần cơ chế serialize/transaction nếu acceptance yêu cầu concurrency guarantee.
- Xóa object storage và soft-delete database không nằm chung transaction; ưu tiên operation idempotent và cần xác định compensation/outbox khi MinIO hoặc DB lỗi giữa chừng.
- SRS audit đã được đồng bộ theo decision log: CONFLICT-023/024 ở trạng thái DECIDED; FR-JOB-002 và live MinIO/PostgreSQL verification vẫn là phần còn mở.

## 8. Tiến độ thực hiện và xác minh

- [x] Tạo `RecipeDifficulty` enum (Easy/Medium/Hard/Expert), đổi `Recipe.Difficulty`, lưu qua EF string conversion và cập nhật snapshot; DTO/API tiếp tục dùng string.
- [x] Cập nhật create/update validation, mapping, list/search projections, seeders và test fixtures cho enum.
- [x] Thêm Application commands upload/set-primary/delete, owner/Admin guard, giới hạn 10 ảnh, first-image primary, promotion khi xóa primary và cache invalidation.
- [x] Thêm bù trừ xóa object nếu DB save upload thất bại; MinIO operation errors được map thành 503 Problem Details.
- [x] Đăng ký MinIO client, object storage adapter, validator và `IFileStorageService` trong Infrastructure DI; nếu config MinIO thiếu thì các image operations dùng unavailable adapter và trả 503, không chặn startup phần còn lại.
- [x] Thêm `RecipeImageEndpoints.cs`, map vào `Program.cs`: POST upload, PATCH primary chuyên biệt, DELETE ảnh.
- [x] Unit tests cho image handlers/owner/primary/delete/limit, Difficulty validation và enum conversion; HTTP tests cho anonymous 401 và multipart validation 400.
- [x] Các test project backend chạy tuần tự với `--maxcpucount:1`: 494 passed, 0 failed (Application 435, Integration 56, Architecture 3).
- [x] Solution build thành công sau khi cập nhật model snapshot.
- [ ] Nối FR-JOB-002 Hangfire/thumbnail worker thật; không có queue/worker trong backend hiện tại.
- [ ] Chạy upload/delete thật với MinIO và kiểm tra extension/magic bytes/storage 503.
- [ ] Apply migration FTS/schema hiện hành và xác minh SQL/EF enum conversion trên PostgreSQL thật; chưa có recipe Testcontainers fixture.
- [ ] Bổ sung concurrency-safe enforcement cho giới hạn 10 ảnh/primary uniqueness nếu acceptance yêu cầu bảo đảm dưới request đồng thời.
