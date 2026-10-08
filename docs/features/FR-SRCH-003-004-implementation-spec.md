# Đặc tả triển khai FR-SRCH-003 và FR-SRCH-004

## Phạm vi và trạng thái

- FR-SRCH-003: sắp xếp kết quả công thức; triển khai trong backend và trang tìm kiếm.
- FR-SRCH-004: phân trang offset-based cho danh sách và tìm kiếm; API và UI hiện đã có cơ chế nền, cần xác minh và giữ tương thích.
- Tác nhân: khách, tác giả và quản trị viên; giữ nguyên visibility hiện tại của Recipe Core.
- Số yêu cầu trong phạm vi: 2.

## Quyết định theo tài liệu

- `CONFLICT-003`: dùng `sortBy` và `sortOrder` (`asc`/`desc`), không dùng cú pháp `sort=-title`.
- Trường sort được cho phép: `createdAt`, `cookTime`, `prepTime`, `title`; giá trị không hợp lệ trả `400` Problem Details.
- Tìm kiếm không chỉ định sort tiếp tục xếp hạng theo độ liên quan; giao diện cho phép chọn relevance hoặc một trong bốn trường.
- `CONFLICT-012`: giữ response phân trang phẳng: `items`, `totalCount`, `page`, `pageSize`, `totalPages`, `hasNextPage`, `hasPreviousPage`.
- Page bắt đầu từ 1; `pageSize` mặc định 12, trong khoảng 1–50. Tham số phân trang không hợp lệ trả `400` Problem Details.

## FR-SRCH-003 — Sắp xếp kết quả

### Mục tiêu

Người dùng chọn thứ tự công thức theo ngày tạo, thời gian nấu, thời gian chuẩn bị hoặc tiêu đề, tăng dần hoặc giảm dần. Sắp xếp áp dụng cho danh sách và kết quả tìm kiếm, trước khi phân trang.

### Tiêu chí chấp nhận

1. `GET /api/v1/recipes/` và `GET /api/v1/recipes/search` chấp nhận `sortBy` cùng `sortOrder`.
2. Hỗ trợ chính xác `createdAt`, `cookTime`, `prepTime`, `title` và `asc`, `desc` không phân biệt hoa thường.
3. Giá trị ngoài whitelist trả `400` với Problem Details; không đưa giá trị sort tùy ý vào truy vấn.
4. Khi không có sort tường minh trên tìm kiếm, giữ xếp hạng FTS hiện hành; khi không có sort trên danh sách, giữ `createdAt desc`.
5. Trang tìm kiếm cho phép chọn tiêu chí và chiều sort; thay đổi sort đưa người dùng về trang 1 và đồng bộ lên URL.
6. Kết quả bằng nhau được sắp xếp ổn định để chuyển trang không gây đảo thứ tự tùy ý.

## FR-SRCH-004 — Phân trang kết quả

### Mục tiêu

Chia danh sách và kết quả tìm kiếm thành các trang offset-based, giữ kết quả tổng hợp sau khi áp dụng visibility, tìm kiếm, filter và sort.

### Tiêu chí chấp nhận

1. `page` mặc định 1; `pageSize` mặc định 12 trên trang tìm kiếm, danh sách công thức, chi tiết danh mục và dashboard; API tối đa 50.
2. Tổng số kết quả được đếm sau filter/visibility nhưng trước phân trang.
3. Chỉ trả item của trang đã yêu cầu; metadata flat phản ánh đúng trang, kích thước, tổng số và khả năng điều hướng.
4. `page < 1`, `pageSize < 1` hoặc `pageSize > 50` trả `400` Problem Details.
5. Không có kết quả trả `200`, `items` rỗng, `totalCount = 0`; trang vượt quá tổng số trang cũng trả danh sách rỗng, không tự đổi trang.
6. Điều hướng trang trước/sau giữ nguyên từ khóa/filter/sort hoặc danh mục hiện tại; trang public list/dashboard phản ánh `page` trên URL.

## Giới hạn và ngoài phạm vi

- Không đổi schema dữ liệu, quy tắc phân quyền/visibility, hợp đồng JSON flat hay cơ chế filter FR-SRCH-002.
- Không bổ sung sort theo các trường ngoài whitelist, cursor pagination hoặc chế độ phân trang mới.
- Thời gian phản hồi phụ thuộc quy mô dữ liệu và database; các yêu cầu này không xác lập SLA mới.

## Trạng thái triển khai và xác minh

- FR-SRCH-003/004 đã triển khai trong backend và trang tìm kiếm; chi tiết task/kết quả nằm trong [kế hoạch](../superpowers/plans/2026-10-07-fr-srch-003-004-plan.md) và [checklist](FR-SRCH-003-004-implementation-checklist.md).
- Backend: Application 472/472; Integration lần chạy mới nhất 75 passed/6 MinIO skipped nếu không truyền env; lần chạy với `MINIO_TEST_*` đạt 81/81, MinIO 6/6; Architecture 3/3.
- Frontend build thành công; Playwright route suite 8/8 đạt trên Edge với API mock, gồm list/category/dashboard pagination.
- Người dùng xác nhận API list/search trả HTTP 200 và dữ liệu truy xuất từ PostgreSQL. Probe độc lập sau đó không tái lập được vì API offline; lần probe Testing trước đó gặp list 500/search timeout.
- E2E dùng API mock, không tính là xác minh PostgreSQL trực tiếp.
