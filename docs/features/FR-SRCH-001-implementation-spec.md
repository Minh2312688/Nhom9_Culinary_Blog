# Đặc tả triển khai FR-SRCH-001 — Tìm kiếm toàn văn công thức

## 1. Phạm vi

Triển khai tìm công thức bằng từ khóa không dấu, dùng PostgreSQL Full-Text Search. API tìm trong tiêu đề và mô tả công thức, chỉ trả summary đã phân trang, xếp thứ tự theo độ liên quan. FR-SRCH-002 (lọc đa tiêu chí) không thuộc phạm vi thay đổi của tài liệu này.

## 2. Hợp đồng API

- **Method/route:** `GET /api/v1/recipes/` (route danh sách hiện hữu).
- **Query:** `search` (tùy chọn), `page` (mặc định 1), `pageSize` (mặc định 12).
- **Response:** `200 OK` với `PaginatedResult<RecipeSummaryDto>`; không có kết quả vẫn trả danh sách rỗng và `totalCount = 0`.
- **Authorization:** Anonymous; recipe Published hiển thị cho mọi người. Tác giả đăng nhập còn thấy recipe của mình, Admin giữ quyền xem hiện có.
- **Validation:** `search` tối đa 100 ký tự. Không có từ khóa sau khi loại punctuation/whitespace thì dùng luồng danh sách mặc định mới nhất.
- **Phân trang:** Giữ nguyên page/pageSize, giới hạn pageSize 1–50 và envelope hiện có của Recipe Core.

Đã chọn query parameter `search` trên route danh sách để tương thích endpoint hiện có và mô tả FR-SRCH-001 tại §3.4. SRS §8.3 còn ghi route `/recipes/search?q=...`; cần nhóm cập nhật/audit quyết định route để tài liệu API chính thức không còn hai cách ghi.

## 3. Chuẩn hóa và truy vấn

1. Application validator giới hạn độ dài từ khóa. Handler bỏ qua trường `search` trống hoặc chỉ có ký tự không phải chữ/số.
2. Tách token theo Unicode letters/digits để loại ký tự cú pháp của `tsquery`, lowercase và nối token bằng toán tử AND; mỗi token thêm hậu tố `:*` để hỗ trợ prefix matching.
3. Hạ tầng gọi `unaccent` trên biểu thức `tsquery` và `to_tsquery('simple', ...)`. `simple` không stemming; `unaccent` làm cho truy vấn tiếng Việt có/không dấu tương đương.
4. Lọc vector với `SearchVector @@ query`, đếm kết quả, rồi sắp xếp `ts_rank_cd(SearchVector, query)` giảm dần. Khi đồng hạng, sắp theo `CreatedAt` giảm dần (entity hiện hành chưa có `PublishedAt`).
5. Áp dụng visibility Recipe Core trước count/page và chiếu thành `RecipeSummaryDto`.

Token hóa chỉ hỗ trợ truy vấn chứa từ; punctuation bị loại. Các từ trong input được kết hợp AND. Tìm kiếm chuỗi con giữa từ không được hỗ trợ; prefix match bắt đầu từ đầu lexeme.

## 4. Lược đồ và cập nhật search vector

- Cột `Recipes.SearchVector` kiểu `tsvector`, NOT NULL.
- Từ điển nội dung: `Title` và `Description`.
- Migration tạo extension PostgreSQL `unaccent` nếu chưa có, backfill vector hiện hữu, tạo GIN index `IDX_Recipe_Search`, và cài trigger trước INSERT hoặc UPDATE của `Title`/`Description`.
- Trigger tính `to_tsvector('simple', unaccent(coalesce(Title,'') || ' ' || coalesce(Description,'')))`. Trigger được dùng vì PostgreSQL không coi `unaccent` là immutable cho generated column/index expression.
- Rollback xóa trigger, hàm, index và cột tìm kiếm; không xóa extension dùng chung của database.

Migration cần quyền tạo extension `unaccent` trong database. Nếu tài khoản migration production không có quyền này, DBA/deployment phải bật extension trước khi apply migration.

## 5. Cache

Handler cache kết quả trang tìm kiếm trong `IRecipeCache` với TTL 60 giây theo quyết định `CONFLICT-016` đã ghi trạng thái `DECIDED`. Key có version, visibility (admin/user/anonymous), page, pageSize và query đã chuẩn hóa để không dùng nhầm dữ liệu giữa người dùng hoặc trang.

Cache hiện áp dụng cho các query hợp lệ nói chung, không có bộ đếm popularity riêng. Thay đổi nội dung có thể còn stale tối đa 60 giây vì cache prefix hiện tại không hỗ trợ xóa key Redis theo prefix một cách portable. Cache lỗi kết nối tiếp tục fallback xuống PostgreSQL theo `DistributedRecipeCache`.

## 6. Thành phần code

- `GetRecipesQuery` / `GetRecipesQueryHandler`: bind `search`, validate, gọi search persistence và cache kết quả; luồng list thông thường không đổi.
- `IRecipeRepository.SearchAsync`: hợp đồng Application cho truy vấn search và kết quả phân trang.
- `RecipeRepository.SearchAsync`: Npgsql FTS, visibility, ranking, count/page.
- `RecipeConfiguration`: EF shadow property `SearchVector` và GIN index mapping.
- `20260930120000_AddRecipeFullTextSearch`: extension, cột, backfill, trigger, index.
- `ApplicationDbContextModelSnapshot`: ghi nhận cột và index mới cho các migration tiếp theo.

## 7. Checklist triển khai

- [x] Thêm `search` tùy chọn và validator giới hạn 100 ký tự vào list query.
- [x] Bỏ qua query chỉ gồm punctuation/whitespace và giữ luồng mặc định newest-first.
- [x] Thêm repository search với token hóa Unicode, prefix query an toàn, `unaccent`, `simple`, AND và rank `ts_rank_cd`.
- [x] Áp dụng visibility hiện tại trước khi đếm và phân trang.
- [x] Thêm cache theo query/user/page với TTL 60 giây theo CONFLICT-016.
- [x] Thêm EF shadow property + GIN index mapping.
- [x] Thêm migration tạo extension/cột/backfill/index/trigger và rollback tương ứng.
- [x] Đồng bộ EF model snapshot.
- [x] Biên dịch toàn bộ `backend/CulinaryBlog.sln` thành công: 0 warning, 0 error.
- [ ] Apply migration và xác nhận kết quả FTS trên PostgreSQL hỗ trợ tiếng Việt.
- [ ] Kiểm thử API: từ có dấu/không dấu, prefix, punctuation, query dài, query rỗng, visibility, rank, pagination, cache Redis/fallback.

## 8. Bằng chứng và việc cần xác nhận

Đã xác nhận build toàn bộ solution bằng `dotnet build backend/CulinaryBlog.sln --no-restore --verbosity:minimal --maxcpucount:1 /nodeReuse:false`: thành công, 0 warning, 0 error. Chưa chạy test hoặc migration/database.

Trước khi đánh dấu hoàn tất cần chạy build lại trong môi trường MSBuild ổn định và apply migration lên PostgreSQL. Cũng cần reconcile route trong SRS §8.3 với query parameter `search` ở endpoint danh sách, và cập nhật trạng thái FR-SRCH-001 trong SRS audit sau khi xác nhận.
