# Checklist triển khai FR-SRCH-001

Phạm vi: tìm kiếm toàn văn công thức. Theo dõi theo trạng thái `[x]` hoàn tất, `[~]` đang chờ xác minh, `[ ]` còn lại.

## Code và lược đồ

- [x] API list nhận query `search` tùy chọn; giới hạn 100 ký tự.
- [x] Query trống/punctuation-only chuyển sang danh sách newest-first hiện có.
- [x] Chuẩn hóa Unicode token; loại cú pháp tsquery do người dùng gửi; AND prefix match.
- [x] PostgreSQL FTS dùng `unaccent`, `simple`, `tsvector`, GIN và `ts_rank_cd`.
- [x] Search chỉ lập chỉ mục Title + Description; trigger cập nhật khi insert/update hai trường này.
- [x] Search giới hạn visibility theo Published/owner/Admin giống luồng danh sách hiện tại.
- [x] Kết quả phân trang dùng `PaginatedResult<RecipeSummaryDto>` hiện hành.
- [x] Cache search theo query/visibility/page trong 60 giây theo CONFLICT-016.
- [x] Có migration backfill, trigger, index, extension và đường rollback.
- [x] Cập nhật model snapshot EF.

## Xác minh còn lại

- [x] Build toàn bộ solution: `dotnet build backend/CulinaryBlog.sln --no-restore --verbosity:minimal --maxcpucount:1 /nodeReuse:false` — thành công, 0 warning, 0 error.
- [ ] Apply migration lên PostgreSQL đúng phiên bản và xác nhận quyền cài extension `unaccent`.
- [ ] Xác nhận truy vấn không dấu tìm được nội dung có dấu và ngược lại.
- [ ] Xác nhận exact/prefix, nhiều term (AND), punctuation và Unicode input.
- [ ] Xác nhận thứ tự relevance và tính ổn định giữa các item cùng rank.
- [ ] Xác nhận anonymous chỉ thấy Published; author/Admin visibility đúng như Recipe Core.
- [ ] Xác nhận page/pageSize, count rỗng và query rỗng.
- [ ] Xác nhận Redis cache TTL 60 giây, tách user/page; PostgreSQL vẫn được dùng khi Redis mất kết nối.
- [ ] Cập nhật SRS API route discrepancy (`?search=` vs `/search?q=`) và trạng thái FR-SRCH-001 khi được nhóm thống nhất.

## Lưu ý

- Chưa thêm/chạy test theo chỉ dẫn ở lượt này.
- Hiện chưa apply migration lên database.
- Cache áp dụng mọi query hợp lệ; chưa có cơ chế đếm độ phổ biến để chỉ cache popular/common terms.
