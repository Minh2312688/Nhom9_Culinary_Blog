# Checklist triển khai FR-SRCH-003 và FR-SRCH-004

> **Trạng thái hiện tại (17/11/2026):** Sorting và pagination đã có ở backend
> và UI. Các kết quả build/test/live probe dưới đây được giữ làm bằng chứng
> theo thời điểm chạy; không chạy lại ứng dụng khi cập nhật tài liệu này.
>
> **Cập nhật nghiệm thu:** Đại diện nhóm xác nhận toàn bộ sorting/pagination
> đã được kiểm thử trên Docker và hoàn thành. Các probe offline bên dưới là
> kết quả lịch sử, không còn thể hiện trạng thái hiện tại.

Quy ước: `[x]` hoàn tất và có bằng chứng; `[~]` một phần/giới hạn môi trường; `[ ]` chưa làm.

## FR-SRCH-003 — Sort

- [x] Chốt hợp đồng `sortBy`/`sortOrder` theo CONFLICT-003; chỉ nhận `createdAt`, `cookTime`, `prepTime`, `title`.
- [x] Backend list áp dụng ASC/DESC; sort input không hợp lệ bị validation từ chối.
- [x] Backend search nhận sort params và giữ FTS relevance khi không yêu cầu sort.
- [x] Sort có secondary ordering ổn định trước khi phân trang.
- [x] UI `/search` chọn tiêu chí/chiều sort, gửi query params và đồng bộ URL.
- [x] Thay đổi sort reset về trang 1; chuyển trang giữ query, filters và sort.
- [x] Unit/integration tests cho sort backend và validation.
- [x] Frontend type-check/build.
- [x] Playwright UI test chạy bằng Microsoft Edge, xác minh sort params, đồng bộ URL và reset page.
- [x] API/PostgreSQL live đã được nhóm kiểm thử trên Docker và nghiệm thu.

## FR-SRCH-004 — Pagination

- [x] `page` mặc định 1; `pageSize` mặc định 12, giới hạn 1–50.
- [x] List/search đếm tổng sau khi áp visibility/filter trước khi `Skip/Take`.
- [x] Giữ flat response: `items`, `totalCount`, `page`, `pageSize`, `totalPages`, `hasNextPage`, `hasPreviousPage`.
- [x] UI điều hướng trang trước/sau và giữ nguyên tiêu chí tìm kiếm.
- [x] Trang danh sách công thức, chi tiết danh mục và dashboard chia kết quả thành trang 12 items; đồng bộ `page` vào URL.
- [x] Pagination dùng metadata API (`totalPages`, `hasNextPage`, `hasPreviousPage`) để khóa/mở nút đúng.
- [x] Application tests kiểm tra metadata/sort trước khi phân trang; Integration tests kiểm tra `page`/`pageSize` không hợp lệ trên list và search.
- [x] Page vượt quá số trang trả rỗng, giữ `totalCount`/page metadata, không tính offset gây overflow.

## Kết quả kiểm thử

- [x] Backend Application tests: **472/472 passed**.
- [x] Backend Integration (lần chạy mới nhất không truyền `MINIO_TEST_*`): **75 passed, 6 skipped**; lần chạy có credentials MinIO trước đó đạt **81/81**, gồm MinIO live 6/6.
- [x] Backend Architecture tests: **3/3 passed**.
- [x] Frontend production build: `npm run build` thành công.
- [x] E2E Playwright `e2e/srs-routes.spec.ts`: **8/8 passed** trên Microsoft Edge; xác minh phân trang list, category, dashboard và search; request API được mock.
- [x] Trong phiên xác minh, frontend Next.js tại `http://localhost:3001/search` trả HTTP 200 và hiển thị sort controls; dev server sau đó đã dừng.
- [x] Người dùng xác nhận API list/search trả HTTP 200 và đọc được dữ liệu PostgreSQL.
- [x] API/PostgreSQL sorting và pagination đã được nhóm kiểm thử trên Docker; các probe offline trước đó là lịch sử.
- [x] `git diff --check` không phát hiện lỗi whitespace.

## Giới hạn

- [x] MinIO live test đã pass 6/6 khi cấu hình credentials từ `.env`.
- [x] Nhóm xác nhận API/PostgreSQL sorting và pagination đã chạy tốt trên Docker; probe offline và E2E mock là bằng chứng lịch sử.
