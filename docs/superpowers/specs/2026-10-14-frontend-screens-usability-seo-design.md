# Đặc tả giao diện Public, Author Dashboard và NFR

**Ngày:** 2026-10-14  
**Trạng thái:** Chờ người dùng duyệt  
**Phạm vi:** Tài liệu hóa kế hoạch chỉnh sửa frontend hiện có; chưa thực hiện thay đổi giao diện.

## 1. Mục tiêu

Rà soát và hoàn thiện có kiểm soát các giao diện public, author dashboard,
khả năng sử dụng NFR-USE-001..004 và dữ liệu có cấu trúc NFR-SEO-001. Xây dựng
bố cục chung phù hợp cho blog nhưng không làm mất hành vi, API, nội dung hoặc
phong cách đang có. Các route đã tồn tại sẽ được chỉnh sửa tại chỗ; không tạo
lại ứng dụng hoặc thiết kế lại diện rộng.

## 2. Căn cứ và hiện trạng

- `frontend/src/app/layout.tsx` cung cấp document root và metadata cơ bản.
- `frontend/src/app/site-ui.tsx` và `site-ui.module.css` đã có `SitePage`,
  header/footer public cùng một số thành phần dùng chung.
- Các trang public và dashboard hiện dùng chung `SitePage`; cần tách bố cục
  điều hướng quản trị khỏi header/footer public mà vẫn giữ các trang nội dung.
- Các màn hình được nêu đã có route và giao diện. Các trang recipe đang dùng
  client-side API, các trạng thái tải/lỗi cơ bản và CSS Modules.
- `RecipeDetailDto`/`RecipeDetail` có nguyên liệu, bước nấu, ảnh, dinh dưỡng
  tùy chọn và `AuthorId`; chưa có tên tác giả hoặc ngày xuất bản trong hợp
  đồng API/kiểu dữ liệu frontend.
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
- Bố cục public dùng điều hướng public và footer blog.
- Bố cục author có điều hướng quản trị riêng, liên kết rõ giữa tổng quan,
  danh sách, tạo/chỉnh sửa công thức và đường quay lại public.
- `/dashboard/categories`, authentication, profile và các API/service hiện
  hữu tiếp tục hoạt động; không nằm trong phạm vi redesign. Nếu cần dùng layout
  mới để giữ luồng điều hướng nhất quán, thay đổi chỉ giới hạn ở shell và phải
  giữ nguyên nội dung/hành vi trang.

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
  không sửa API, validation nghiệp vụ hoặc payload trong phạm vi này.
- **NFR-USE-004:** Trạng thái chờ/tải, thành công, lỗi và rỗng nhất quán; khóa
  thao tác gửi lặp; dùng optimistic update cho thao tác nhanh, có thể hoàn tác
  khi thất bại mà không đổi semantics. Tiến độ upload chỉ hiển thị nếu API
  upload cung cấp tiến độ thực; không mô phỏng phần trăm.
- **NFR-SEO-001:** Trang chi tiết recipe có JSON-LD `Recipe` được tuần tự hóa
  an toàn, nhất quán với nội dung hiển thị và chỉ dành cho recipe public đã
  xuất bản. Dùng các trường có dữ liệu nguồn: tên, mô tả, ảnh, thời gian,
  khẩu phần, nguyên liệu, hướng dẫn và dinh dưỡng khi có.

## 4. Ràng buộc thiết kế và hành vi

1. Ưu tiên tái sử dụng cấu trúc, component, CSS Modules, API client, form state,
   tham số truy vấn, payload, điều kiện xác thực và điều hướng đang dùng.
2. Giữ ngôn ngữ hình ảnh hiện tại của Culinary Table (nền sáng, xanh rừng,
   điểm nhấn màu cà chua, kiểu chữ serif cho tiêu đề); chỉ sửa layout/spacing/
   responsive/feedback khi cần để đáp ứng tiêu chí.
3. Không thay đổi schema/database/backend DTO, business logic, trạng thái recipe,
   phân quyền, endpoint hay semantics thao tác. Không đưa dữ liệu giả vào UI
   hoặc metadata.
4. Dashboard dùng shell riêng; thay đổi shell phải không vô tình mở quyền truy
   cập hoặc làm thay đổi kiểm tra token hiện có.
5. Kiểm tra `RecipeStatus` và trạng thái quyền từ API trước khi phát JSON-LD.
   Nội dung draft/archived không được phát markup công khai.
6. API chưa cung cấp `author.name`, `datePublished` hoặc rating; `nutrition`
   cũng có thể null. Không thể tuyên bố đáp ứng đầy đủ Schema.org/Rich Results
   theo SRS nếu thiếu dữ liệu này. Trong giới hạn không đổi API, chỉ phát các
   giá trị có căn cứ, bỏ thuộc tính không có dữ liệu và ghi nhận NFR-SEO-001
   còn phụ thuộc quyết định/mở rộng hợp đồng riêng. Không dùng `AuthorId` làm
   tên tác giả, ngày tạo làm ngày xuất bản hoặc rating giả.

## 5. Tiêu chí nghiệm thu

- Mọi route trong phạm vi hoạt động với API client và luồng hiện tại; tạo/sửa
  công thức giữ nguyên payload, concurrency token, xác thực và đích điều hướng.
- Public pages cùng dùng public shell; dashboard dùng shell quản trị riêng,
  có liên kết đến `/dashboard` và các route công thức.
- Các trang hiển thị được ở 320, 768, 1024, 1200 px, không xuất hiện tràn
  ngang; kiểm tra riêng bảng/danh sách, biểu mẫu nhiều trường và ảnh.
- Điều hướng và form dùng được bằng bàn phím; heading, landmark, label,
  alternative text và thông báo live phù hợp; tương phản được kiểm tra.
- Mọi thao tác bất đồng bộ có trạng thái phù hợp; lỗi và trạng thái rỗng không
  che nội dung hoặc tạo thành công giả.
- JSON-LD hợp lệ về cú pháp, escape dữ liệu an toàn, khớp recipe đang hiển thị;
  nội dung draft/archived không có structured data public. Báo cáo rõ trường
  nào bị chặn vì thiếu dữ liệu API; không tuyên bố Google Rich Results/star
  rating đạt khi chưa đủ dữ liệu/kiểm chứng.
- Không hồi quy: lint/type-check/build frontend, E2E route và form hiện có;
  kiểm tra thủ công responsive, bàn phím, thông báo tải/lỗi và Rich Results
  Test khi có URL/môi trường phù hợp.

## 6. Ngoài phạm vi

- Viết hoặc sửa giao diện trong đợt đặc tả/kế hoạch này.
- Thay đổi API/backend/database để bổ sung author profile hoặc publish date.
- Làm mới toàn bộ nhận diện thương hiệu, thêm tính năng nghiệp vụ, đổi thư viện
  form/toast hoặc sửa các trang auth/profile/category administration.
- Thay đổi cách SSR/ISR/API caching ngoài phần tối thiểu được xác định trong
  kế hoạch và được duyệt trước khi triển khai.

## 7. Điều kiện trước khi triển khai

- Người dùng duyệt đặc tả này.
- Người sở hữu API quyết định cách cung cấp tên tác giả và ngày xuất bản nếu
  yêu cầu Rich Results đầy đủ vẫn là điều kiện bắt buộc; rating chỉ được khai
  báo nếu có dữ liệu review hợp lệ. Các quyết định đó là thay đổi hợp đồng
  riêng, không tự bao gồm trong phạm vi frontend hiện tại.
