# Kế hoạch triển khai tìm kiếm và lọc công thức

> Trạng thái: FR-SRCH-001 và FR-SRCH-002 đã được triển khai và xác minh trên Application/PostgreSQL/API. Route search chuẩn, visibility Draft/Archived và chính sách popularity cache đã được kiểm tra; Redis smoke test thực vẫn chờ vì môi trường không có Redis/Docker.

**Cập nhật:** 2026-10-02

Kế hoạch theo từng yêu cầu: [FR-SRCH-001](2026-10-02-fr-srch-001-plan.md) và [FR-SRCH-002](2026-10-02-fr-srch-002-plan.md).

## Mục tiêu

Hoàn thiện FR-SRCH-001 (tìm kiếm toàn văn) và FR-SRCH-002 (lọc đa tiêu chí) trên CQRS, EF Core, PostgreSQL và API hiện hữu. Tái sử dụng `GetRecipesQuery`, `PaginatedResult<RecipeSummaryDto>` và route danh sách hiện tại.

## Phạm vi và hợp đồng đang dùng

- Yêu cầu nhắc FR-SRCH-001 hai lần; mục thứ hai được hiểu là FR-SRCH-002, theo thứ tự trong SRS.
- Route chuẩn: `GET /api/v1/recipes/search?q=...`; route list `GET /api/v1/recipes/?search=...` vẫn được giữ tương thích và các tham số lọc có thể kết hợp cùng search.
- Tham số: `search`, `categoryId`, `difficulty`, `maxCookTime`, `minServings`, `page`, `pageSize`, `sortBy`, `sortOrder`.
- Phản hồi dùng dạng phẳng `PaginatedResult<RecipeSummaryDto>` theo quyết định CONFLICT-012.
- CONFLICT-016 đã quyết định truy vấn search không filter được cache sau 3 lần trong 5 phút, với TTL kết quả 60 giây; search có filter không cache theo FR-SRCH-002.
- SRS §8.3 và code thống nhất route chuẩn `/api/v1/recipes/search?q=...`; route list với tham số `search` vẫn được giữ để tương thích.
- `difficulty` của FR-SRCH-002 chỉ nhận `Easy`, `Medium`, `Hard`; kiểm tra không phân biệt hoa thường. `maxCookTime > 0`, `minServings > 0`; mọi filter ghép bằng AND.
- FTS dùng title/description, `unaccent`, cấu hình `simple`, token Unicode, nối AND, hậu tố prefix `:*`, GIN và `ts_rank_cd`. `simple` không stemming.

## Hiện trạng và phần đã hoàn tất

- [x] `GetRecipesQuery` xử lý list/filter/search, phân trang, sort và visibility.
- [x] Validator FR-SRCH-002 từ chối max cook time/servings không dương và difficulty ngoài tập cho phép.
- [x] Search kết hợp mọi filter trong persistence query; filter động bypass cache và search phổ biến dùng popularity gate.
- [x] FTS repository xây tsquery an toàn, xếp hạng và thêm `CreatedAt` giảm dần để ổn định khi đồng hạng.
- [x] Migration `20260930120000_AddRecipeFullTextSearch` đã áp dụng trên DB `culinary_blog`; extension, vector/backfill, trigger và GIN index đã xác minh.
- [x] API giữ route list hiện hành; validation trả Problem Details 400.
- [x] Frontend `/search` có debounce, bốn filter, phân trang, trạng thái tải/rỗng/lỗi và đồng bộ query lên URL.
- [x] Có test Application/API cho validation, filter propagation, visibility, punctuation-only fallback, token Unicode an toàn và biên độ dài.
- [x] API/PostgreSQL thật xác nhận list/search/filter/empty/punctuation trả 200; search phân trang 12/100, filter AND và no-match trả 0.
- [x] API tìm recipe tiếng Việt tạm bằng từ khóa có dấu/không dấu; cập nhật title xác nhận trigger bỏ term cũ/thêm term mới; recipe thử đã được cleanup.
- [x] Application tests đạt 435/435; Search API validation tests đạt 6/6; integration Search API đạt 6/6; cache regression đạt 3/3.
- [x] Backend test projects chạy tuần tự với `--maxcpucount:1` đạt 494/494 (Application 435, Integration 56, Architecture 3); không có lỗi Auth 429 trong lần kiểm tra này.
- [x] Tài liệu liên quan được chuẩn hóa sang tiếng Việt; route conflict và giới hạn môi trường ghi rõ bên dưới.

## Việc còn lại

### 1. Kiểm chứng PostgreSQL và migration

- [x] Migration FTS có trong history DB `culinary_blog`; `unaccent`, cột `SearchVector`, vector không NULL, trigger và GIN index đã xác minh.
- [x] Kiểm thử prefix/AND và accent-insensitive trên vector đã lưu bằng recipe tiếng Việt tạm; kiểm tra trigger sau cập nhật và xóa recipe thử.
- [x] Kiểm tra `ts_rank_cd`, tie-break `CreatedAt`, count rỗng, phân trang và `EXPLAIN` dùng GIN khi tắt sequential scan.
- [x] Chạy migration rollback trên PostgreSQL cluster thử nghiệm riêng; không rollback DB `culinary_blog` đang dùng.
- [x] Guest visibility, filter AND, phân trang và kết quả rỗng đã xác minh qua API/PostgreSQL; author/Admin với Draft/Archived đã kiểm tra bằng verifier transaction và rollback.

### 2. Chính sách cache và hợp đồng API

- [x] Đã xác định và triển khai ngưỡng popularity 3 lần/5 phút, TTL kết quả 60 giây cho query search không filter.
- [x] Đã thống nhất route chính thức `GET /api/v1/recipes/search?q=` theo SRS §8.3 và cập nhật decision log/frontend/verifier.
- [x] Đã kiểm thử hit/miss, fallback connection/JSON corruption và tách biệt theo visibility/page bằng test double; Redis smoke test thực vẫn chờ khi môi trường có Redis.

### 3. Kiểm thử giao diện và hồi quy

- [x] Type-check và production build frontend.
- [x] Browser smoke test với API giả lập: kết quả, category/difficulty, URL query, trạng thái giao diện và viewport 390px không tràn ngang.
- [x] Chạy API smoke test PostgreSQL cho list/search/AND filters/no-match/punctuation-only và recipe tiếng Việt tạm; request trả 200, trigger sau cập nhật được xác minh.
- [ ] Chạy smoke test Redis/cache sau khi Redis khả dụng và popularity threshold được thống nhất.
- [x] Chạy toàn bộ `backend/CulinaryBlog.sln` sau khi thêm test Search/Filter; ghi nhận kết quả và các lỗi Auth 429 riêng.

## Bằng chứng hiện có và giới hạn

- Migration đã có trong history DB `culinary_blog`; verifier mặc định chạy transaction chỉ đọc. Chế độ opt-in thêm/xóa một recipe thử để kiểm tra API tiếng Việt và trigger.
- Biến `ConnectionStrings__Postgres` từng trỏ `culinary_blog_auth`, khác `culinary_blog`; API live smoke test chỉ pass khi khởi động với DB đích đúng, không đổi biến môi trường toàn máy.
- Docker, Redis (`6379`) và MinIO (`9000`) không khả dụng; Redis cache TTL/fallback đã kiểm chứng bằng test double nhưng chưa smoke-test Redis thực.
- Chưa có PostgreSQL/Testcontainers fixture tự động; `backend/tools/CulinaryBlog.PostgresVerification` hỗ trợ kiểm tra thủ công. Rollback migration cần DB thử nghiệm riêng.
- Không tự đánh dấu các việc môi trường/nhóm là hoàn tất chỉ dựa vào code hoặc unit test.
