# Checklist triển khai FR-SRCH-001 và FR-SRCH-002

Phạm vi: tìm kiếm toàn văn và lọc đa tiêu chí công thức. Trạng thái: `[x]` đã hoàn tất, `[~]` chờ xác minh, `[ ]` chưa hoàn tất.

## Code và lược đồ

- [x] Endpoint chuẩn `GET /api/v1/recipes/search?q=` theo SRS §8.3; list cũ còn tương thích; giới hạn từ khóa 100 ký tự.
- [x] Truy vấn rỗng hoặc chỉ có dấu câu chuyển sang danh sách mặc định sắp xếp mới nhất.
- [x] Tách token Unicode, loại cú pháp `tsquery` do người dùng nhập, ghép AND và tìm tiền tố an toàn.
- [x] Mã nguồn FTS dùng `unaccent`, `simple`, `tsvector`, GIN và `ts_rank_cd`.
- [x] Vector tìm kiếm chỉ chứa Title + Description; trigger cập nhật khi thêm/sửa hai trường này.
- [x] Mã nguồn áp dụng quy tắc hiển thị Published/owner/Admin giống luồng danh sách.
- [x] Kết quả phân trang dùng `PaginatedResult<RecipeSummaryDto>` hiện hành.
- [x] Cache search phổ biến theo CONFLICT-016: ngưỡng 3 lần/5 phút, cache kết quả 60 giây; chỉ query không filter; key tách theo query/phân trang/sort/visibility.
- [x] Migration FTS đã áp dụng trên DB cục bộ `culinary_blog`; extension/vector/trigger/GIN đã xác minh, không còn vector NULL.
- [x] Đồng bộ EF model snapshot.
- [x] FR-SRCH-002 nhận `categoryId`, `difficulty`, `maxCookTime`, `minServings`; kết hợp tất cả điều kiện bằng AND trong list và search.
- [x] Validator yêu cầu `maxCookTime > 0`, `minServings > 0`, difficulty thuộc `Easy|Medium|Hard` (không phân biệt hoa thường).
- [x] Trang `/search` có debounce, bộ lọc, trạng thái tải/rỗng/lỗi, phân trang và đồng bộ query lên URL.
- [x] Sửa kiểm tra `sortBy` để giá trị mặc định `createdAt` được chấp nhận sau khi chuẩn hóa chữ thường.

## Xác minh còn lại

- [x] Application 435/435; Search API validation 6/6; integration Search API 6/6; cache regression 3/3.
- [x] Backend test projects chạy tuần tự với `--maxcpucount:1`: 494/494 (Application 435, Integration 56, Architecture 3).
- [x] PostgreSQL/API thật: list, search, filter kết hợp, kết quả rỗng và punctuation-only fallback đều trả 200; search có 100 kết quả, trang đầu 12.
- [x] PostgreSQL: migration, `unaccent`, cột/vector đã điền, trigger, GIN index, prefix/AND, `ts_rank_cd` và khả năng dùng GIN trong `EXPLAIN` đã xác minh.
- [x] Thêm recipe tiếng Việt tạm; API tìm được cả có dấu và không dấu trên vector đã lưu.
- [x] Endpoint từ chối filter sai và search quá 100 ký tự bằng 400 Problem Details.
- [x] Frontend TypeScript, production build và smoke test `/search` với API giả lập đạt; viewport 390px không tràn ngang.
- [x] InMemory tests xác nhận visibility guest/author/Admin và AND filter; PostgreSQL verifier tự seed Draft/Archived, xác nhận guest/author/Admin ở list/search rồi rollback transaction.
- [x] PostgreSQL verifier chạy trên `culinary_blog`: build 0, verifier exit 0; 12/12 visibility checks Draft/Archived đạt và temporary data rollback.
- [x] Kiểm tra trigger sau insert/update title/description với recipe thử; cleanup recipe sau kiểm tra.
- [x] Rollback migration trên cluster/database tạm riêng đạt; FTS artifacts bị gỡ đúng và DB `culinary_blog` không rollback.
- [x] Kiểm thử popularity hit/miss và fallback cache: làm nóng sau lần quan sát thứ 3, hit ở lần sau, filter động bypass cache, Redis connection/JSON hỏng fallback vào database.
- [x] Route chuẩn `/recipes/search?q=` được thống nhất theo SRS §8.3; SRS audit, decision log, frontend và verifier đã cập nhật.

## Lưu ý

- Migration FTS đã có trong `__EFMigrationsHistory` của DB cục bộ `culinary_blog`; kiểm tra schema chạy transaction chỉ đọc. Chỉ kiểm tra API/trigger mới thêm/xóa một recipe thử có chủ đích.
- Phát hiện connection override `ConnectionStrings__Postgres` trỏ `culinary_blog_auth`, còn appsettings/verifier dùng `culinary_blog`. API smoke test chỉ pass khi khởi động riêng với DB `culinary_blog`; không đổi biến môi trường toàn máy.
- Docker, Redis và MinIO không khả dụng ở lần kiểm tra gần nhất; cache đã được kiểm chứng bằng test double, chưa smoke-test Redis thực.
- `CONFLICT-016` ghi rõ ngưỡng 3 lần/5 phút và TTL kết quả 60 giây; bộ đếm Redis là gần đúng vì thao tác đọc-tăng-ghi không atomic.
- Rollback migration đã được xác minh trên cluster riêng; verifier visibility đã chạy trên PostgreSQL bằng dữ liệu Draft/Archived tạm trong transaction và không để lại dữ liệu.
