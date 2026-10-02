# Đặc tả triển khai FR-SRCH-001 — Tìm kiếm toàn văn công thức

## 1. Phạm vi

Triển khai tìm công thức bằng từ khóa không dấu, sử dụng PostgreSQL Full-Text Search. API tìm trong tiêu đề và mô tả công thức, chỉ trả bản tóm tắt có phân trang và sắp xếp theo độ liên quan. Nếu có filter FR-SRCH-002, API áp dụng đồng thời tất cả điều kiện theo AND trước khi đếm và phân trang; nếu chỉ có filter thì tiếp tục dùng luồng danh sách hiện có.

## 2. Hợp đồng API

- **Phương thức/đường dẫn chuẩn:** `GET /api/v1/recipes/search` theo SRS §8.3.
- **Tham số truy vấn:** `q` (tùy chọn), `page` (mặc định 1), `pageSize` (mặc định 12); có thể kết hợp filter FR-SRCH-002.
- **Phản hồi:** `200 OK` với `PaginatedResult<RecipeSummaryDto>`; khi không có kết quả vẫn trả danh sách rỗng và `totalCount = 0`.
- **Phân quyền:** Khách chưa đăng nhập được tìm kiếm; mọi người thấy công thức Published. Tác giả đăng nhập còn thấy công thức của mình; Admin giữ quyền xem hiện có.
- **Kiểm tra dữ liệu:** `search` tối đa 100 ký tự. Nếu sau khi bỏ dấu câu/khoảng trắng không còn từ khóa thì dùng luồng danh sách mới nhất mặc định.
- **Phân trang:** Giữ nguyên page/pageSize, giới hạn pageSize 1–50 và envelope hiện có của Recipe Core.

Route chuẩn theo SRS §8.3 là `/api/v1/recipes/search?q=...`. Route list `/api/v1/recipes/` tiếp tục dùng cho danh sách và filter; tham số `search` cũ trên list còn được giữ tương thích.

## 3. Chuẩn hóa và truy vấn

1. Bộ kiểm tra Application giới hạn độ dài từ khóa. Handler bỏ qua `search` rỗng hoặc chỉ có ký tự không phải chữ/số.
2. Tách từ theo nhóm ký tự Unicode là chữ/số để loại cú pháp `tsquery` do người dùng nhập; chuyển chữ thường, nối các từ bằng AND và thêm `:*` vào từng từ để tìm tiền tố.
3. Hạ tầng gọi `unaccent` trên biểu thức `tsquery` và `to_tsquery('simple', ...)`. Cấu hình `simple` không biến đổi từ theo ngôn ngữ; `unaccent` giúp truy vấn tiếng Việt có dấu và không dấu tương đương.
4. Lọc vector bằng `SearchVector @@ query`, đếm kết quả rồi sắp xếp `ts_rank_cd(SearchVector, query)` giảm dần. Nếu đồng hạng, sắp theo `CreatedAt` giảm dần (entity hiện tại chưa có `PublishedAt`).
5. Áp dụng quy tắc hiển thị của Recipe Core trước khi đếm/phân trang, sau đó chiếu kết quả thành `RecipeSummaryDto`.

Tách từ chỉ hỗ trợ truy vấn theo từ; dấu câu bị loại bỏ. Các từ trong truy vấn được kết hợp bằng AND. Không hỗ trợ tìm chuỗi con nằm giữa một từ; tìm tiền tố bắt đầu từ đầu lexeme.

## 4. Lược đồ và cập nhật search vector

- Cột `Recipes.SearchVector` kiểu `tsvector`, NOT NULL.
- Từ điển nội dung: `Title` và `Description`.
- Migration tạo extension PostgreSQL `unaccent` nếu chưa có, backfill vector hiện hữu, tạo GIN index `IDX_Recipe_Search`, và cài trigger trước INSERT hoặc UPDATE của `Title`/`Description`.
- Trigger tính `to_tsvector('simple', unaccent(coalesce(Title,'') || ' ' || coalesce(Description,'')))`. Trigger được dùng vì PostgreSQL không coi `unaccent` là immutable cho generated column/index expression.
- Rollback xóa trigger, hàm, index và cột tìm kiếm; không xóa extension dùng chung của database.

Migration cần quyền tạo extension `unaccent` trong cơ sở dữ liệu. Nếu tài khoản chạy migration production không có quyền này, DBA/nhóm triển khai phải bật extension trước khi áp dụng migration.

## 5. Cache

Chính sách đã chọn cho CONFLICT-016: chỉ đếm truy vấn search không có filter; cùng query/phạm vi visibility được gọi ít nhất 3 lần trong 5 phút mới cache kết quả 60 giây. Key gồm query chuẩn hóa, trang, page size, sort và phạm vi guest/user/admin. Ưu điểm là chỉ cache truy vấn lặp lại và giảm tải PostgreSQL cho từ khóa phổ biến. Nhược điểm là bộ đếm đọc-tăng-ghi không atomic nên gần đúng khi đồng thời; kết quả có thể stale tối đa 60 giây; filter động không cache. Redis lỗi được coi là cache miss và handler tiếp tục xuống PostgreSQL.

## 6. Thành phần code

- `GetRecipesQuery` / `GetRecipesQueryHandler`: nhận `search`, kiểm tra dữ liệu đầu vào, áp dụng popularity gate và gọi persistence.
- `IRecipeRepository.SearchAsync`: hợp đồng Application cho truy vấn search và kết quả phân trang.
- `RecipeRepository.SearchAsync`: Npgsql FTS, quy tắc hiển thị, xếp hạng, đếm và phân trang.
- `RecipeConfiguration`: EF shadow property `SearchVector` và GIN index mapping.
- `20260930120000_AddRecipeFullTextSearch`: extension, cột, backfill, trigger, index.
- `ApplicationDbContextModelSnapshot`: ghi nhận cột và index mới cho các migration tiếp theo.

## 7. Checklist triển khai

- [x] Thêm `search` tùy chọn và giới hạn 100 ký tự trong bộ kiểm tra của truy vấn danh sách.
- [x] Bỏ qua truy vấn chỉ có dấu câu/khoảng trắng và dùng luồng danh sách mặc định mới nhất.
- [x] Thêm tìm kiếm trong repository với tách từ Unicode, truy vấn tiền tố an toàn, `unaccent`, `simple`, AND và `ts_rank_cd`.
- [x] Áp dụng quy tắc hiển thị trước khi đếm và phân trang.
- [x] Triển khai popularity gate 3 lần/5 phút và TTL 60 giây cho search không filter; filter động không dùng cache.
- [x] Thêm EF shadow property và ánh xạ chỉ mục GIN.
- [x] Migration FTS đã có trong `__EFMigrationsHistory` của DB cục bộ `culinary_blog`; extension, cột vector, trigger và GIN index có mặt, mọi vector đã được điền.
- [x] Đồng bộ EF model snapshot.
- [x] Validator FR-SRCH-002 yêu cầu `maxCookTime`/`minServings` > 0 và difficulty là Easy/Medium/Hard, không phân biệt hoa thường.
- [x] Kết hợp các filter với nhau và với full-text search bằng AND.
- [x] Bổ sung kiểm thử token Unicode/dấu câu, truy vấn punctuation-only, biên 100/101 ký tự, visibility và AND filter.
- [x] Kiểm thử API cho filter sai và truy vấn quá dài trả 400 Problem Details.
- [x] Kiểm thử Application: 435/435 đạt; Search API validation: 6/6 đạt; integration Search API: 6/6 đạt.
- [x] Backend test projects chạy tuần tự với `--maxcpucount:1`: 494/494 (Application 435, Integration 56, Architecture 3); không có test Auth 429 trong lần kiểm tra này.
- [x] PostgreSQL/API chỉ đọc: list/search/filter/empty/punctuation trả 200; search có 100 kết quả, phân trang 12; filter AND trả recipe mẫu; truy vấn không khớp trả 0.
- [x] PostgreSQL xác nhận `unaccent`, vector không NULL, trigger, GIN, FTS prefix/AND và `EXPLAIN` có thể dùng `IDX_Recipe_Search` khi tắt sequential scan.
- [x] Recipe tiếng Việt tạm: API tìm được title có dấu và không dấu trên `SearchVector` đã lưu.
- [x] Kiểm tra TypeScript, build production và smoke test trình duyệt `/search` với API giả lập.
- [x] Kiểm tra trigger sau insert/update title/description bằng recipe thử; xác nhận cleanup sau kiểm tra.
- [x] Kiểm tra rollback migration trên PostgreSQL cluster/database tạm riêng; `Up` tạo FTS artifacts, `Down` gỡ cột/index/trigger/function và giữ migration trước đó; DB hiện tại không rollback.
- [x] PostgreSQL verifier tự seed Draft/Archived trong transaction, xác nhận guest bị ẩn và author/Admin thấy ở list/search, sau đó rollback dữ liệu tạm.
- [x] Kiểm thử cache popularity hit/miss/fallback bằng test double: cache sau lần quan sát thứ 3, hit ở lần sau, filter bypass cache, Redis connection/JSON hỏng fallback database; policy popularity đã triển khai.

## 8. Bằng chứng và việc cần xác nhận

Kiểm thử hiện tại: Application tests 435/435; Integration tests 56/56; Architecture tests 3/3; tổng cộng 494/494 khi chạy tuần tự với `--maxcpucount:1`. Search API validation 6/6 và cache regression 3/3 đều nằm trong các bộ test tương ứng. Frontend TypeScript check và production build đạt; build cần chạy ngoài giới hạn sandbox vì Next.js tạo worker.

Verifier PostgreSQL chạy ngày 2026-10-02 trên database `culinary_blog`: build verifier và API đều exit 0 khi dùng output riêng; verifier exit 0. Verifier tự seed một recipe Draft và một recipe Archived trong transaction, kiểm tra 12 trường hợp visibility (search/list × guest/author/Admin), tất cả đạt, sau đó rollback. Database sau kiểm tra không còn recipe tạm. Rollback migration chạy trên cluster tạm port 55432: trước rollback FTS column/GIN/trigger/function/history đều có; sau rollback đều không còn, migration `20260921101739_AddRowVersionDefaults` vẫn còn; rollback verifier exit 0. Cluster tạm đã dừng và xóa.

Migration đã có trong history của `culinary_blog`; kiểm tra schema dùng transaction chỉ đọc. Recipe thử tạm đã được thêm/xóa có chủ đích ở lần xác minh trước. Verifier visibility PostgreSQL đã seed Draft/Archived tạm và rollback thành công. Redis, MinIO và Docker không khả dụng; cache fallback đã có regression test với test double cho connection failure và JSON hỏng. Route chuẩn theo SRS §8.3 là `/api/v1/recipes/search?q=`. Popularity policy 3 lần/5 phút, TTL 60 giây được ghi ở CONFLICT-016. Rollback migration đã chạy trên cluster riêng; không rollback database đang dùng.
