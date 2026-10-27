# Đặc tả giao diện Public, Author Dashboard và NFR

**Ngày:** 2026-10-14  
**Trạng thái:** Bản cập nhật theo phạm vi đã duyệt, chờ người dùng rà soát
**Phạm vi:** Hoàn thiện frontend, thêm API public hồ sơ tác giả và thời điểm xuất bản recipe; làm mới nhận diện trên toàn bộ giao diện.

## 1. Mục tiêu

Hoàn thiện các giao diện public, author dashboard, hồ sơ tác giả, NFR-USE-001..004
và NFR-SEO-001. Làm mới toàn bộ nhận diện thành “Culinary Blog”, cung cấp dữ
liệu tác giả/ngày xuất bản từ API và database để hiển thị hồ sơ public cùng
Recipe JSON-LD. Chỉnh sửa tăng dần trên kiến trúc hiện tại, không thay đổi
nghiệp vụ recipe hiện có ngoài timestamp xuất bản; không thêm tính năng khác.

## 2. Căn cứ và hiện trạng

- `frontend/src/app/layout.tsx` cung cấp document root và metadata cơ bản.
- `frontend/src/app/site-ui.tsx` và `site-ui.module.css` đã có `SitePage`,
  header/footer public cùng một số thành phần dùng chung.
- Các trang public và dashboard hiện dùng chung `SitePage`; cần tách bố cục
  điều hướng quản trị khỏi header/footer public mà vẫn giữ các trang nội dung.
- Các màn hình được nêu đã có route và giao diện. Các trang recipe đang dùng
  client-side API, các trạng thái tải/lỗi cơ bản và CSS Modules.
- `ApplicationUser` đã có `DisplayName`, `AvatarUrl`, `Bio`; hồ sơ hiện tại
  thuộc API xác thực và không có endpoint đọc hồ sơ public.
- `RecipeDetailDto` có `AuthorId`, nguyên liệu, bước nấu, ảnh và dinh dưỡng
  tùy chọn; entity `Recipe` chưa có thời điểm xuất bản.
- Frontend dùng CSS Modules, Tailwind và `lucide-react`; nhận diện hiện tại là
  “Culinary Table”, màu xanh rừng/cà chua và serif.
- Quyết định `CONFLICT-011` đã chốt lỗi validation API dùng HTTP 400 với
  Problem Details; giao diện không được giả định 422.

Tài liệu tham chiếu: `README.md` (phạm vi tuần 6 và màn hình), `docs/srs-audit/SRS-AUDIT.md`
(NFR và route), `docs/srs-audit/SRS-CONFLICTS-AND-DECISIONS.md`,
`docs/srs-audit/TEAM-REQUIREMENT-TRACEABILITY.md`,
`docs/run-project.md`, `docs/docker-guide.md` và các trang hiện có trong
`frontend/src/app`.

## 3. Phạm vi

### 3.1 Route và layout

- Public: `/`, `/recipes`, `/recipes/[slug]`, `/categories`,
  `/categories/[slug]`, `/search`.
- Author area: `/dashboard` (tổng quan), `/dashboard/recipes`,
  `/dashboard/recipes/new`, `/dashboard/recipes/[id]/edit`.
- Public author profile: `/authors/[id]`; chỉ hiển thị tên, avatar, bio và
  danh sách recipe Published có phân trang. Email, roles và recipe không
  công khai tuyệt đối không được trả trong contract này.
- Bố cục public dùng điều hướng public và footer blog.
- Bố cục author có điều hướng quản trị riêng, liên kết rõ giữa tổng quan,
  danh sách, tạo/chỉnh sửa công thức và đường quay lại public.
- `/dashboard/categories`, authentication và profile cá nhân tiếp tục hoạt
  động; authentication, profile cá nhân và dashboard cũng được cập nhật nhận
  diện, nhưng giữ nguyên luồng, quyền và payload hiện hữu.
- Chỉ thêm nghiệp vụ hồ sơ tác giả public và lưu ngày xuất bản; không thêm
  ratings, favorites, follows hoặc các tính năng nghiệp vụ khác.

### 3.2 Chức năng phi chức năng

- **NFR-USE-001:** Mobile 320–767 px một cột, tablet 768–1199 px hai cột,
  desktop từ 1200 px; không tràn ngang, thao tác phù hợp cảm ứng. Dùng các
  breakpoint cụ thể của NFR này khi đối chiếu phạm vi breakpoint khác trong SRS.
- **NFR-USE-002:** Semantic HTML, điều hướng bàn phím, trạng thái focus rõ,
  nhãn/accessible name cho điều khiển và văn bản thay thế ảnh phù hợp;
  hướng tới WCAG 2.1 AA, tương phản tối thiểu 4.5:1 cho văn bản và 3:1 cho
  thành phần giao diện.
- **NFR-USE-003:** Lỗi tải/lưu dễ hiểu và có thể hành động; lỗi trường hiển thị
  sát trường nhập. Hiểu Problem Details theo hợp đồng hiện hành và HTTP 400;
  bổ sung contract đọc public theo allowlist; không đổi validation, payload
  ghi, authorization hay semantics thao tác recipe hiện có.
- **NFR-USE-004:** Trạng thái chờ/tải, thành công, lỗi và rỗng nhất quán; khóa
  thao tác gửi lặp; dùng optimistic update cho thao tác nhanh, có thể hoàn tác
  khi thất bại mà không đổi semantics. Tiến độ upload chỉ hiển thị nếu API
  upload cung cấp tiến độ thực; không mô phỏng phần trăm.
- **NFR-SEO-001:** Trang chi tiết recipe có JSON-LD `Recipe` được tuần tự hóa
  an toàn, nhất quán với nội dung hiển thị và chỉ dành cho recipe public đã
  xuất bản. Dùng các trường có dữ liệu nguồn: tên, mô tả, ảnh, thời gian,
  khẩu phần, nguyên liệu, hướng dẫn, dinh dưỡng khi có, `author.name` và
  `datePublished` khi có giá trị xuất bản đáng tin cậy.

### 3.3 Data/API additions

- Thêm `Recipes.PublishedAt` nullable (UTC). Khi recipe chuyển thành Published,
  ghi thời điểm hiện tại; archive giữ lại giá trị cũ; lần publish lại ghi thời
  điểm mới. Không thay đổi payload ghi hoặc trạng thái nghiệp vụ hiện có.
- Migration không suy ngày xuất bản từ `CreatedAt`; recipe lịch sử chưa có
  timestamp giữ `PublishedAt = null`.
- Bổ sung `GET /api/v1/authors/{id}?page={page}&pageSize={pageSize}` trả
  allowlist `{ id, displayName, avatarUrl, bio, recipes: PaginatedResult }`;
  chỉ account active và recipe Published được trả. Không trả email/roles.
- Thêm author display data và `publishedAt` vào response recipe public cần
  hiển thị/SEO; giữ nguyên các trường hiện có và response ghi.

## 4. Ràng buộc thiết kế và hành vi

1. Ưu tiên tái sử dụng cấu trúc, component, CSS Modules, API client, form state,
   tham số truy vấn, payload, điều kiện xác thực và điều hướng đang dùng.
2. Thay thương hiệu “Culinary Table” bằng “Culinary Blog”; dùng nhận diện hiện
   đại, rực rỡ (coral/cam, vàng nghệ, teal đậm, nền kem), typography và logo
   mark thống nhất trên mọi route frontend. Giữ CSS Modules/Tailwind; giữ
   `lucide-react`, thêm Motion cho chuyển động nhỏ và tôn trọng reduced motion.
3. Chỉ thêm schema/API cho `PublishedAt` và public author profile; giữ nguyên
   nghiệp vụ, phân quyền và contract ghi recipe hiện có. Không đưa dữ liệu giả
   vào UI hoặc metadata.
4. Dashboard dùng shell riêng; thay đổi shell phải không vô tình mở quyền truy
   cập hoặc làm thay đổi kiểm tra token hiện có.
5. Kiểm tra `RecipeStatus` và trạng thái quyền từ API trước khi phát JSON-LD.
   Nội dung draft/archived không được phát markup công khai.
6. JSON-LD dùng display name và `PublishedAt` do API trả; recipe lịch sử có
   `PublishedAt = null` thì không phát `datePublished`. Không dùng `AuthorId`
   làm tên, `CreatedAt` làm ngày xuất bản hoặc rating giả. Nutrition có thể
   null và tiếp tục chỉ phát khi có dữ liệu.

## 5. Tiêu chí nghiệm thu

- Mọi route trong phạm vi hoạt động với API client và luồng hiện tại; tạo/sửa
  công thức giữ nguyên payload, concurrency token, xác thực và đích điều hướng.
- Hồ sơ `/authors/[id]` và endpoint tương ứng chỉ tiết lộ allowlist đã nêu,
  loại recipe không Published, hỗ trợ phân trang và hiển thị lỗi/empty/loading.
- Publish ghi timestamp UTC; archive không xóa; publish lại cập nhật timestamp.
  Recipe cũ thiếu ngày vẫn render được và không có `datePublished` trong schema.
- Nhận diện “Culinary Blog” nhất quán trên public, auth, profile và dashboard;
  contrast được kiểm chứng, Motion tôn trọng reduced motion.
- Public pages cùng dùng public shell; dashboard dùng shell quản trị riêng,
  có liên kết đến `/dashboard` và các route công thức.
- Các trang hiển thị được ở 320, 768, 1024, 1200 px, không xuất hiện tràn
  ngang; kiểm tra riêng bảng/danh sách, biểu mẫu nhiều trường và ảnh.
- Điều hướng và form dùng được bằng bàn phím; heading, landmark, label,
  alternative text và thông báo live phù hợp; tương phản được kiểm tra.
- Mọi thao tác bất đồng bộ có trạng thái phù hợp; lỗi và trạng thái rỗng không
  che nội dung hoặc tạo thành công giả.
- JSON-LD có author/datePublished khi API trả dữ liệu; escape an toàn và khớp
  recipe hiển thị. Draft/archived không có schema public; không khai báo rating.
- Không hồi quy: backend unit/integration/architecture tests, migration check,
  frontend lint/type-check/build, E2E route/form/responsive/SEO và smoke test.

## 6. Ngoài phạm vi

- Ratings, reviews, favorites, follows và các tính năng nghiệp vụ khác ngoài
  hồ sơ tác giả/ngày xuất bản.
- Đổi thư viện form/toast hoặc thay `lucide-react`; không chuyển sang UI kit.
- Thay đổi cache/ISR ngoài mức cần thiết cho fetch profile/recipe SSR.

## 7. Điều kiện trước khi triển khai

- Người dùng rà soát và duyệt bản đặc tả cập nhật này trước khi lập kế hoạch
  triển khai chi tiết.
