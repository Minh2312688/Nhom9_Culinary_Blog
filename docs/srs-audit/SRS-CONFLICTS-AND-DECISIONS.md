# SRS Conflicts & Architectural Decision Log

Tài liệu này ghi nhận toàn bộ các điểm mâu thuẫn thực tế, chưa thống nhất giữa các phần trong tài liệu SRS (Software Requirements Specification) v1.0.0 (71 trang) và thiết kế hệ thống của CulinaryBlog cho toàn bộ 4 thành viên (TV1, TV2, TV3, TV4).

---

## 1. Quy tắc quản lý trạng thái quyết định

- **OPEN:** Điểm mâu thuẫn đang mở, chưa được thống nhất chính thức. **Tuyệt đối không được implement** các đoạn mã phụ thuộc trực tiếp vào mục này nếu có thể trì hoãn.
- **DECIDED:** Đã có quyết định cuối cùng được giảng viên hoặc toàn bộ team thống nhất và xác nhận bằng biên bản.
- **IMPLEMENTED:** Mã nguồn đã được xây dựng hoàn tất theo đúng quyết định được duyệt.
- **SUPERSEDED:** Quyết định cũ bị thay thế bởi một quyết định mới hơn.

> **CẢNH BÁO QUAN TRỌNG:**
> - Không bất kỳ thành viên hay AI agent nào được tự ý chuyển trạng thái từ `OPEN` sang `DECIDED` mà không có quyết định chính thức.
> - Tuyệt đối không tự ý bổ sung các thuộc tính hoặc giải thích suy diễn nếu chưa có bằng chứng xác thực trong tài liệu SRS.

---

## 2. Bảng tổng hợp 25 mâu thuẫn & quyết định kiến trúc

| ID | Chủ đề | Mô tả mâu thuẫn | Phương án A | Phương án B | Status | Quyết định cuối | Người xác nhận | Ngày | Module ảnh hưởng |
|---|---|---|---|---|---|---|---|---|---|
| **CONFLICT-001** | Recipe Delete Strategy | Mâu thuẫn giữa mô tả xóa công thức trong Functional Requirement và Data Model. | **Hard Delete:** Dùng `DbSet.Remove(recipe)` và cascade delete toàn bộ bảng con (Steps, Ingredients, Images). | **Soft Delete:** `IsDeleted = true` + EF Core Global Query Filter. *(Lưu ý: `DeletedAt` chưa được SRS xác nhận, không tự bổ sung).* | DECIDED | Adopt soft delete using IsDeleted and the EF Core global query filter; do not add DeletedAt. | Đại diện nhóm (xác nhận chính thức trong hội thoại) | 2026-09-23 | TV2 Recipe / DB / API |
| **CONFLICT-002** | Category Delete Strategy | Mâu thuẫn về phương thức xóa danh mục giữa yêu cầu nghiệp vụ và cấu trúc entity. | **Hard Delete:** Xóa bản ghi danh mục khỏi cơ sở dữ liệu. | **Soft Delete:** Cập nhật cờ `IsDeleted = true`. | DECIDED | Adopt soft delete using IsDeleted and the EF Core global query filter. | Đại diện nhóm (xác nhận chính thức trong hội thoại) | 2026-09-23 | TV4 Category / DB |
| **CONFLICT-003** | Sorting Convention | Mâu thuẫn định dạng query parameter cho chức năng sắp xếp trên API. | **Sort prefix:** `sort=title`, `sort=-title`, `sort=-createdAt`. | **Explicit params:** `sortBy=title&sortOrder=asc`, `sortBy=createdAt&sortOrder=desc`. | DECIDED | Use explicit sortBy and sortOrder query parameters. | Đại diện nhóm (xác nhận chính thức trong hội thoại) | 2026-09-23 | TV2 Search / TV3 Frontend |
| **CONFLICT-004** | RecipeIngredient Quantity / Unit | Mâu thuẫn về ràng buộc dữ liệu định lượng nguyên liệu trong validation và form nhập liệu. | **Strict:** `Quantity > 0`, `Unit` bắt buộc không rỗng. | **Flexible:** `Quantity` nullable, `Unit` nullable để hỗ trợ ghi chú dạng "vừa đủ". | IMPLEMENTED | Quantity and Unit are nullable; when Quantity is supplied it must be greater than zero. | Đại diện nhóm (xác nhận chính thức trong hội thoại) | 2026-09-23 | TV2 Recipe / Validation / DB / UI |
| **CONFLICT-005** | Ingredient Ordering Name | Mâu thuẫn tên trường số thứ tự sắp xếp nguyên liệu giữa các phần tài liệu. | **`SortOrder`** | **`OrderIndex`** | IMPLEMENTED | Use OrderIndex as the canonical ingredient ordering property. | Đại diện nhóm (xác nhận chính thức trong hội thoại) | 2026-09-23 | TV2 Recipe / DB |
| **CONFLICT-006** | RecipeStep Duration | Mâu thuẫn tên trường thời gian thực hiện bước nấu ăn. | **`DurationMinutes`** | **`TimerMinutes`** | DECIDED | Use DurationMinutes as the canonical recipe-step duration property. | Đại diện nhóm (xác nhận chính thức trong hội thoại) | 2026-09-23 | TV2 Recipe / API / UI |
| **CONFLICT-007** | RecipeStep Title | Mâu thuẫn về sự tồn tại của trường tiêu đề từng bước nấu ăn. | **Không có Title:** Gồm `StepNumber` + `Description`. | **Có Title:** Gồm `StepNumber` + `Title` + `Description`. | DECIDED | Include Title on each recipe step. | Đại diện nhóm (xác nhận chính thức trong hội thoại) | 2026-09-23 | TV2 Recipe / UI / DB |
| **CONFLICT-008** | RecipeNutrition Fields | Mâu thuẫn số lượng trường dinh dưỡng (4 fields vs. 6 fields). Phạm vi tính toán: **PER SERVING** – đã được Data Model xác định rõ. | **4 chỉ số cơ bản:** Calories, Protein, Carbs, Fat. | **6 chỉ số:** Calories, Protein, Carbohydrates, Fat, Fiber, Sodium. *(Nutrition calculation scope: PER SERVING – đã được Data Model xác định).* | DECIDED | Use six per-serving nutrition values: Calories, Protein, Carbohydrates, Fat, Fiber, and Sodium. | Đại diện nhóm (xác nhận chính thức trong hội thoại) | 2026-09-23 | TV2 Recipe / DTO / UI |
| **CONFLICT-009** | Auth User Name Fields | Mâu thuẫn cấu trúc trường định danh người dùng giữa phần Account và User Profile. | **`FullName` + `UserName`** | **`DisplayName`** (+ `Bio` trong profile) | DECIDED | Use DisplayName and Bio for the user profile; keep Identity UserName as the authentication identifier. | Đại diện nhóm (xác nhận chính thức trong hội thoại) | 2026-09-23 | TV1 Auth (Phase 2) / TV3 Frontend |
| **CONFLICT-010** | Register Response | Mâu thuẫn payload trả về sau khi đăng ký tài khoản thành công. | **Kèm Token:** Trả về cặp `accessToken` + `refreshToken` (cho phép đăng nhập ngay). | **Chỉ thông tin User:** Trả về `userId`, `email`, `displayName` (yêu cầu chuyển sang Login). | DECIDED | Registration returns userId, email, and displayName only; users obtain tokens through Login. | Đại diện nhóm (xác nhận chính thức trong hội thoại) | 2026-09-23 | TV1 Auth + TV3 Frontend |
| **CONFLICT-011** | Validation HTTP Status | Mâu thuẫn mã trạng thái HTTP khi dữ liệu đầu vào không hợp lệ. | **`422 Unprocessable Entity`** | **`400 Bad Request`** | IMPLEMENTED | Return HTTP 400 for validation errors using Problem Details. | Đại diện nhóm (xác nhận chính thức trong hội thoại) | 2026-09-23 | Toàn bộ API + TV3 Frontend |
| **CONFLICT-012** | Pagination Response Shape | Mâu thuẫn cấu trúc JSON trả về cho danh sách có phân trang. | **Flat shape:** `{ items, totalCount, page, pageSize, totalPages, hasNextPage, hasPreviousPage }` | **Nested shape:** `{ data: [...], meta: { page, pageSize, total, totalPages } }` | DECIDED | Use the flat pagination response: items, totalCount, page, pageSize, totalPages, hasNextPage, hasPreviousPage. | Đại diện nhóm (xác nhận chính thức trong hội thoại) | 2026-09-23 | TV2 Recipe API / TV4 Category API / TV3 Frontend |
| **CONFLICT-013** | Google OAuth Contract | Mâu thuẫn payload và luồng xác thực Google OAuth2 giữa các chương. | **ExternalLoginInfo / Code + PKCE:** §3.1 Frontend gửi ExternalLoginInfo; §5.3 quy định Auth Code + PKCE. | **Client ID Token:** §8.1 endpoint contract nhận `{ idToken: "string" }`. | DECIDED | Use Google OAuth Authorization Code flow with PKCE and a backend callback. | Đại diện nhóm (xác nhận chính thức trong hội thoại) | 2026-09-23 | TV1 Auth (Phase 2) / TV3 Frontend |
| **CONFLICT-014** | Concurrency Conflict HTTP Status | Mâu thuẫn mã trạng thái HTTP trả về khi xảy ra xung đột đồng thời (RowVersion). | **`HTTP 409 Conflict`:** Chuẩn RESTful và FR-RCP-004. | **`HTTP 422 Unprocessable Entity`:** Quy định trong bảng Error Codes Phụ lục A/B. | DECIDED | Return HTTP 409 Conflict for optimistic concurrency failures. | Đại diện nhóm (xác nhận chính thức trong hội thoại) | 2026-09-23 | TV2 Recipe / Exception Handler |
| **CONFLICT-015** | RecipeStep StepNumber Generation | Mâu thuẫn trách nhiệm sinh số thứ tự bước nấu ăn (client truyền hay server tự tăng). | **Server tự tăng:** Server tự tính `Max(StepNumber) + 1`, client không truyền. | **Client chỉ định:** Payload request yêu cầu client truyền tường minh `stepNumber: int`. | DECIDED | The server assigns StepNumber as Max(StepNumber) + 1; clients do not send it. | Đại diện nhóm (xác nhận chính thức trong hội thoại) | 2026-09-23 | TV2 Recipe Steps / API |
| **CONFLICT-016** | Search Query Cache TTL | Mâu thuẫn thời gian sống (TTL) bộ nhớ đệm kết quả tìm kiếm giữa FR và NFR. | **TTL = 5 phút** (hoặc không cache kết quả dynamic). | **TTL = 1 phút (60s)** cho các query phổ biến trong Redis. | DECIDED | Cache popular/common search queries in Redis for 60 seconds. | Đại diện nhóm (xác nhận chính thức trong hội thoại) | 2026-09-23 | TV2 Search / Redis Cache |
| **CONFLICT-017** | Category Update Allowed Fields | Mâu thuẫn các trường được phép sửa khi gọi `PUT /api/v1/categories/{id}`. | **Chỉ Name + Description:** Ngăn client tự ý đổi ImageUrl/OrderIndex qua endpoint này. | **Name + Description + ImageUrl + OrderIndex:** Cho phép cập nhật toàn bộ thuộc tính. | DECIDED | Category update accepts Name, Description, ImageUrl, and OrderIndex. | Đại diện nhóm (xác nhận chính thức trong hội thoại) | 2026-09-23 | TV4 Category API / UI |
| **CONFLICT-018** | Refresh Token Entropy | Mâu thuẫn về độ dài và entropy của Refresh Token giữa FR-AUTH-001 và NFR-SEC-002. | **512-bit entropy:** Theo đặc tả FR-AUTH-001 (trang 18). | **128-bit crypto random:** Theo đặc tả NFR-SEC-002 (trang 41) kết hợp hash SHA-256. | DECIDED | Generate refresh tokens from 128-bit cryptographically secure randomness, store only SHA-256 hashes, and use a seven-day lifetime. | Đại diện nhóm (xác nhận chính thức trong hội thoại) | 2026-09-23 | TV1 Auth (Phase 2) |
| **CONFLICT-019** | Recipe Detail Cache TTL / Mechanism | Mâu thuẫn về cơ chế cache và TTL của chi tiết công thức nấu ăn. | **Output Cache TTL 60m:** ASP.NET Core Output Cache tagged "recipes" (FR-RCP-002 trang 28). | **Redis Cache TTL 5m:** Redis distributed cache theo cache-aside pattern (NFR-PERF-003 trang 40). | DECIDED | Use Redis distributed cache-aside for recipe detail with a five-minute TTL. | Đại diện nhóm (xác nhận chính thức trong hội thoại) | 2026-09-23 | TV2 Recipe / Cache |
| **CONFLICT-020** | Category Cache Technology / TTL | Mâu thuẫn về công nghệ cache và TTL của danh sách danh mục. | **IMemoryCache TTL 1h:** In-memory cache với key "categories:all" (FR-CAT-001/003 trang 24, 25). | **Redis Cache TTL 30m:** Redis distributed cache trong kiến trúc stateless (NFR-PERF-003, NFR-SCALE-001 trang 40, 44). | DECIDED | Use Redis distributed cache for Category with a 30-minute TTL. | Đại diện nhóm (xác nhận chính thức trong hội thoại) | 2026-09-23 | TV4 Category / Cache |
| **CONFLICT-021** | Recipe List Author Visibility | Mâu thuẫn về quyền xem bài viết của chính tác giả (Author) trong danh sách công thức. | **Xem cả Draft + Archived:** Author xem thêm Draft và Archived của mình (FR-RCP-001 Mô tả trang 28). | **Chỉ xem Draft:** Happy Path bước 4 chỉ filter `Published OR (Draft AND AuthorId == userId)` (trang 28). | DECIDED | Authors can see their own Draft and Archived recipes, as well as Published recipes visible to everyone. | Đại diện nhóm (xác nhận chính thức trong hội thoại) | 2026-09-23 | TV2 Recipe Query / TV3 UI |
| **CONFLICT-022** | Browser Version Support Matrix | Mâu thuẫn yêu cầu phiên bản trình duyệt tối thiểu giữa Chương 2 và Chương 5. | **Chrome 90+, Firefox 88+, Safari 14+:** Theo đặc tả môi trường client §2.4.3 (trang 14). | **Chrome 112+, Firefox 113+, Safari 16+:** Theo đặc tả giao diện phần cứng/trình duyệt §5.4.2 (trang 49). | DECIDED | Support Chrome and Edge 112+, Firefox 113+, and Safari 16+. | Đại diện nhóm (xác nhận chính thức trong hội thoại) | 2026-09-23 | TV3 Frontend UI |
| **CONFLICT-023** | Recipe Image Set Primary Contract | Mâu thuẫn thiết kế endpoint đặt ảnh chính cho công thức nấu ăn. | **Endpoint riêng:** `PATCH /api/v1/recipes/{id}/images/{imageId}/primary` (§3.3 FR-RCP-008 trang 34). | **Endpoint chung:** `PATCH /api/v1/recipes/{id}/images/{imageId}` với body `{ isPrimary: true }` (§8.4 trang 64). | DECIDED | Use the dedicated PATCH /api/v1/recipes/{id}/images/{imageId}/primary endpoint. | Đại diện nhóm (xác nhận chính thức trong hội thoại) | 2026-09-23 | TV2 Recipe / TV4 Storage / TV3 UI |
| **CONFLICT-024** | Recipe Image Upload Response Shape / URL Property Naming | Mâu thuẫn payload phản hồi upload ảnh giữa FR-RCP-008 và Chapter 8 §8.4 / Data Model §7.5. | **FR Response Shape:** Upload trả HTTP 201 với { url, isPrimary } (FR-RCP-008 p.34). | **Chapter 8 & Data Model:** Upload trả HTTP 201 với { imageId, originalUrl, altText, isPrimary } (§8.4 p.64) và cột DB là OriginalUrl (§7.5 p.58). | DECIDED | Upload returns imageId, originalUrl, altText, and isPrimary. | Đại diện nhóm (xác nhận chính thức trong hội thoại) | 2026-09-23 | TV2 Recipe / TV4 Storage / TV3 UI |
| **CONFLICT-025** | BaseEntity Inheritance vs ApplicationUser Identity Inheritance | Mâu thuẫn mô hình kế thừa thực thể giữa quy tắc chung tất cả entities kế thừa BaseEntity và bảng ApplicationUser kế thừa IdentityUser<string>. | **SRS §6.4 p.52 & §7.1 p.54:** Tất cả entities kế thừa BaseEntity (có Id, CreatedAt, UpdatedAt, IsDeleted, RowVersion). | **SRS §7.7 p.58–59:** ApplicationUser được đặc tả kế thừa trực tiếp IdentityUser<string> (không mô tả các trường BaseEntity). | DECIDED | ApplicationUser remains a direct IdentityUser<string> subtype and an explicit exception to BaseEntity; do not apply BaseEntity RowVersion or IsDeleted global filtering to Identity. | Đại diện nhóm (xác nhận chính thức trong hội thoại) | 2026-09-23 | TV1 Auth / TV2 DbContext |

---

## 3. Chi tiết bằng chứng đối chiếu 25 Conflicts (Traceability Evidence)

### CONFLICT-001: Recipe Delete Strategy
- **Mô tả:** FR-RCP-007 mô tả Hard Delete; trong khi Data Model và API mô tả Soft Delete.
- **Evidence A:** SRS §3.3 FR-RCP-007, pages 32–33 $\rightarrow$ Recipe hard delete + cascade delete child.
- **Evidence B:** SRS §7.1 BaseEntity (trang 54) / §8.3 Recipes API $\rightarrow$ `IsDeleted` / soft delete + Global Query Filter. *(Lưu ý: Không có trường `DeletedAt`).*
- **Decision:** Adopt soft delete using IsDeleted and the EF Core global query filter; do not add DeletedAt.
- **Confirmed by:** Đại diện nhóm (xác nhận chính thức trong hội thoại), 2026-09-23.
- **Status:** `DECIDED`

### CONFLICT-002: Category Delete Strategy
- **Mô tả:** Yêu cầu xóa danh mục có chỗ mô tả xóa hẳn, chỗ khác dùng cờ xóa mềm.
- **Evidence A:** SRS §3.2 FR-CAT-005 (trang 26–27) $\rightarrow$ Hard delete bản ghi khỏi database.
- **Evidence B:** SRS §7 BaseEntity $\rightarrow$ Cập nhật cờ `IsDeleted = true`.
- **Decision:** Adopt soft delete using IsDeleted and the EF Core global query filter.
- **Confirmed by:** Đại diện nhóm (xác nhận chính thức trong hội thoại), 2026-09-23.
- **Status:** `DECIDED`

### CONFLICT-003: Sorting Convention
- **Mô tả:** Sự khác biệt về định dạng query string cho sắp xếp kết quả.
- **Evidence A:** SRS §3.4 FR-SRCH-003 (trang 38) $\rightarrow$ Prefix convention: `sort=title`, `sort=-createdAt`.
- **Evidence B:** SRS §8.3 Recipes API (trang 63) $\rightarrow$ Explicit parameters: `sortBy=title&sortOrder=asc`.
- **Decision:** Use explicit sortBy and sortOrder query parameters.
- **Confirmed by:** Đại diện nhóm (xác nhận chính thức trong hội thoại), 2026-09-23.
- **Status:** `DECIDED`

### CONFLICT-004: RecipeIngredient Quantity / Unit
- **Mô tả:** Sự khác biệt giữa ràng buộc validation bắt buộc số lượng dương với nhu cầu thực tế hỗ trợ gia vị "vừa đủ".
- **Evidence A:** SRS §3.3 Input Validation rules (trang 30) $\rightarrow$ `Quantity > 0`, `Unit` chuỗi bắt buộc.
- **Evidence B:** SRS §7.4 RecipeIngredient model (trang 57) $\rightarrow$ `Quantity` nullable, `Unit` nullable.
- **Decision:** Quantity and Unit are nullable; when Quantity is supplied it must be greater than zero.
- **Confirmed by:** Đại diện nhóm (xác nhận chính thức trong hội thoại), 2026-09-23.
- **Status:** `IMPLEMENTED`

### CONFLICT-005: Ingredient Ordering Name
- **Mô tả:** Tên trường số thứ tự sắp xếp nguyên liệu không nhất quán.
- **Evidence A:** SRS §3.3 FR-RCP-002/009 & §7.4 (trang 28, 57) $\rightarrow$ Sử dụng tên trường `SortOrder`.
- **Evidence B:** SRS §8.4 Recipe DTOs (trang 64) $\rightarrow$ Sử dụng tên trường `OrderIndex`.
- **Decision:** Use OrderIndex as the canonical ingredient ordering property.
- **Confirmed by:** Đại diện nhóm (xác nhận chính thức trong hội thoại), 2026-09-23.
- **Status:** `IMPLEMENTED`

### CONFLICT-006: RecipeStep Duration
- **Mô tả:** Tên trường thời lượng cho từng bước nấu ăn.
- **Evidence A:** SRS §7.3 RecipeStep database schema (trang 56) $\rightarrow$ Sử dụng tên trường `DurationMinutes`.
- **Evidence B:** SRS §8.5 RecipeStep API contract (trang 65) $\rightarrow$ Sử dụng tên trường `TimerMinutes`.
- **Decision:** Use DurationMinutes as the canonical recipe-step duration property.
- **Confirmed by:** Đại diện nhóm (xác nhận chính thức trong hội thoại), 2026-09-23.
- **Status:** `DECIDED`

### CONFLICT-007: RecipeStep Title
- **Mô tả:** Một phần SRS không yêu cầu tiêu đề bước nấu, phần khác lại định nghĩa trường này.
- **Evidence A:** SRS §3.3 FR-RCP-010 (trang 35) $\rightarrow$ Chỉ gồm `StepNumber` + `Description`, không có `Title`.
- **Evidence B:** SRS §7.3 Data Model (trang 56) & §8.5 API (trang 65) $\rightarrow$ Gồm đầy đủ `StepNumber` + `Title` + `Description`.
- **Decision:** Include Title on each recipe step.
- **Confirmed by:** Đại diện nhóm (xác nhận chính thức trong hội thoại), 2026-09-23.
- **Status:** `DECIDED`

### CONFLICT-008: RecipeNutrition Fields
- **Mô tả:** Mâu thuẫn còn OPEN CHỈ LÀ số lượng trường dinh dưỡng (4 fields vs. 6 fields).
- **Evidence A:** SRS §3.3 FR-RCP-003 (trang 29) $\rightarrow$ Dùng 4 nutrition fields: Calories, Protein, Carbs, Fat.
- **Evidence B:** SRS §7.2.1 RecipeNutrition (trang 56) $\rightarrow$ Mô tả 6 fields: Calories, Protein, Carbohydrates, Fat, Fiber, Sodium.
- **Fact đã xác nhận:** §7.2.1 đã xác định rõ nutrition tính theo khẩu phần (**PER SERVING**): Calories (kcal/serving), Protein (g/serving), Carbohydrates (g/serving), Fat (g/serving), Fiber (g/serving), Sodium (mg/serving). Không coi phạm vi tính toán là mâu thuẫn mở nữa.
- **Decision:** Use six per-serving nutrition values: Calories, Protein, Carbohydrates, Fat, Fiber, and Sodium.
- **Confirmed by:** Đại diện nhóm (xác nhận chính thức trong hội thoại), 2026-09-23.
- **Status:** `DECIDED`

### CONFLICT-009: Auth User Name Fields
- **Mô tả:** Sự khác biệt giữa các trường tên định danh người dùng giữa phần Account và Profile.
- **Evidence A:** SRS §3.1 Auth / Account (trang 17) $\rightarrow$ Sử dụng `FullName` + `UserName`.
- **Evidence B:** SRS §3.1 User Profile (trang 23) & §8.1 API (trang 62) $\rightarrow$ Sử dụng `DisplayName` và có `Bio` trong profile.
- **Decision:** Use DisplayName and Bio for the user profile; keep Identity UserName as the authentication identifier.
- **Confirmed by:** Đại diện nhóm (xác nhận chính thức trong hội thoại), 2026-09-23.
- **Status:** `DECIDED`

### CONFLICT-010: Register Response
- **Mô tả:** Sự khác nhau về payload trả về của API đăng ký tài khoản (`POST /api/v1/auth/register`).
- **Evidence A:** SRS §3.1 FR-AUTH-001 (trang 18) $\rightarrow$ Trả về `accessToken` + `refreshToken` để người dùng đăng nhập ngay lập tức.
- **Evidence B:** SRS §8.1 Auth API (trang 61) $\rightarrow$ Chỉ trả về thông tin người dùng (`userId`, `email`, `displayName`), yêu cầu chuyển hướng sang màn hình đăng nhập.
- **Decision:** Registration returns userId, email, and displayName only; users obtain tokens through Login.
- **Confirmed by:** Đại diện nhóm (xác nhận chính thức trong hội thoại), 2026-09-23.
- **Status:** `DECIDED`

### CONFLICT-011: Validation HTTP Status
- **Mô tả:** Mã phản hồi HTTP khi dữ liệu gửi lên vi phạm quy tắc validation.
- **Evidence A:** Phụ lục A (trang 67) & RESTful guidelines $\rightarrow$ Quy định mã `422 Unprocessable Entity`.
- **Evidence B:** SRS §8.0 API Common Response status (trang 60) $\rightarrow$ Quy định mã `400 Bad Request`.
- **Decision:** Return HTTP 400 for validation errors using Problem Details.
- **Confirmed by:** Đại diện nhóm (xác nhận chính thức trong hội thoại), 2026-09-23.
- **Status:** `IMPLEMENTED`

### CONFLICT-012: Pagination Response Shape
- **Mô tả:** Hai cấu trúc JSON khác nhau được mô tả cho các endpoint danh sách phân trang.
- **Evidence A:** Phụ lục A (trang 66) $\rightarrow$ Flat shape: `{ items, totalCount, page, pageSize, totalPages, hasNextPage, hasPreviousPage }`.
- **Evidence B:** SRS §8.0 API Overview (trang 60) $\rightarrow$ Nested shape: `{ data: [...], meta: { page, pageSize, total, totalPages } }`.
- **Decision:** Use the flat pagination response: items, totalCount, page, pageSize, totalPages, hasNextPage, hasPreviousPage.
- **Confirmed by:** Đại diện nhóm (xác nhận chính thức trong hội thoại), 2026-09-23.
- **Status:** `DECIDED`

### CONFLICT-013: Google OAuth Contract
- **Mô tả:** Sự khác biệt về payload và luồng dữ liệu trao đổi trong chức năng đăng nhập Google OAuth.
- **Evidence A:** SRS §3.1 FR-AUTH-003 (trang 20) $\rightarrow$ Frontend gửi Google `ExternalLoginInfo` tới backend.
- **Evidence B:** SRS §5.3 Giao diện Dịch vụ Bên thứ ba (trang 47) $\rightarrow$ Giao thức OAuth 2.0 Authorization Code + PKCE, redirect URI `/api/v1/auth/google/callback`.
- **Evidence C:** SRS §8.1 Authentication Endpoints (trang 61) $\rightarrow$ Định nghĩa contract endpoint `POST /api/v1/auth/google` nhận `{ idToken: "string" }`.
- **Decision:** Use Google OAuth Authorization Code flow with PKCE and a backend callback.
- **Confirmed by:** Đại diện nhóm (xác nhận chính thức trong hội thoại), 2026-09-23.
- **Status:** `DECIDED`

### CONFLICT-014: Concurrency Conflict HTTP Status
- **Mô tả:** Mâu thuẫn mã phản hồi HTTP khi phát hiện xung đột dữ liệu đồng thời qua `RowVersion`.
- **Evidence A:** SRS §3.3 FR-RCP-004 (trang 31) & §8.3 Recipe API $\rightarrow$ Trả về `409 Conflict`.
- **Evidence B:** SRS Phụ lục A (trang 67) & Bảng mã lỗi Phụ lục B (trang 68) $\rightarrow$ `CONCURRENCY_CONFLICT` được ánh xạ thành `422 Unprocessable Entity`.
- **Decision:** Return HTTP 409 Conflict for optimistic concurrency failures.
- **Confirmed by:** Đại diện nhóm (xác nhận chính thức trong hội thoại), 2026-09-23.
- **Status:** `DECIDED`

### CONFLICT-015: RecipeStep StepNumber Generation
- **Mô tả:** Mâu thuẫn về trách nhiệm sinh chỉ số bước nấu ăn (`StepNumber`).
- **Evidence A:** SRS §3.3 FR-RCP-010 (trang 36) $\rightarrow$ Server tự động tính toán `StepNumber = Max(StepNumber) + 1`, client không cần truyền.
- **Evidence B:** SRS §8.5 Recipe Steps API (trang 65) $\rightarrow$ Body contract `POST /api/v1/recipes/{id}/steps` yêu cầu client truyền tường minh `stepNumber: int`.
- **Decision:** The server assigns StepNumber as Max(StepNumber) + 1; clients do not send it.
- **Confirmed by:** Đại diện nhóm (xác nhận chính thức trong hội thoại), 2026-09-23.
- **Status:** `DECIDED`

### CONFLICT-016: Search Query Cache TTL
- **Mô tả:** Mâu thuẫn về thời gian cache kết quả tìm kiếm giữa Functional Requirement và Non-Functional Requirement.
- **Evidence A:** SRS §3.4 FR-SRCH-001 (trang 37) $\rightarrow$ Đề xuất TTL = 5 phút hoặc không cache nếu từ khóa thay đổi liên tục.
- **Evidence B:** SRS §4.1 NFR-PERF-003 (trang 40) $\rightarrow$ Yêu cầu Redis cache kết quả tìm kiếm phổ biến với `TTL = 1 minute` (60 giây).
- **Decision:** Cache popular/common search queries in Redis for 60 seconds.
- **Confirmed by:** Đại diện nhóm (xác nhận chính thức trong hội thoại), 2026-09-23.
- **Status:** `DECIDED`

### CONFLICT-017: Category Update Allowed Fields
- **Mô tả:** Mâu thuẫn về danh sách trường được phép cập nhật khi thực hiện sửa danh mục.
- **Evidence A:** SRS §3.2 FR-CAT-004 (trang 26) $\rightarrow$ Chỉ cho phép sửa `Name` và `Description`.
- **Evidence B:** SRS §8.2 Category API (trang 63) $\rightarrow$ DTO cho phép sửa cả `Name`, `Description`, `ImageUrl`, `OrderIndex`.
- **Decision:** Category update accepts Name, Description, ImageUrl, and OrderIndex.
- **Confirmed by:** Đại diện nhóm (xác nhận chính thức trong hội thoại), 2026-09-23.
- **Status:** `DECIDED`

### CONFLICT-018: Refresh Token Entropy
- **Mô tả:** Mâu thuẫn về độ dài entropy của Refresh Token giữa yêu cầu chức năng và yêu cầu bảo mật.
- **Evidence A:** SRS §3.1 FR-AUTH-001 (trang 18) $\rightarrow$ Refresh Token = 512-bit, TTL 7 ngày.
- **Evidence B:** SRS §4.2 NFR-SEC-002 (trang 41) $\rightarrow$ Refresh Token = 128-bit cryptographically secure random bytes, hash SHA-256 trước khi lưu DB, TTL 7 ngày.
- **Decision:** Generate refresh tokens from 128-bit cryptographically secure randomness, store only SHA-256 hashes, and use a seven-day lifetime.
- **Confirmed by:** Đại diện nhóm (xác nhận chính thức trong hội thoại), 2026-09-23.
- **Status:** `DECIDED`

### CONFLICT-019: Recipe Detail Cache TTL / Cache Mechanism
- **Mô tả:** Mâu thuẫn về cơ chế cache và thời gian sống (TTL) của chi tiết công thức nấu ăn.
- **Evidence A:** SRS §3.3 FR-RCP-002 (trang 28) $\rightarrow$ Sử dụng ASP.NET Core Output Cache policy "RecipeDetail" với `TTL = 60 phút`, tag-based invalidation.
- **Evidence B:** SRS §4.1 NFR-PERF-003 (trang 40) $\rightarrow$ Sử dụng Redis distributed cache theo cache-aside pattern với `TTL = 5 phút`.
- **Decision:** Use Redis distributed cache-aside for recipe detail with a five-minute TTL.
- **Confirmed by:** Đại diện nhóm (xác nhận chính thức trong hội thoại), 2026-09-23.
- **Status:** `DECIDED`

### CONFLICT-020: Category Cache Technology / TTL
- **Mô tả:** Mâu thuẫn về công nghệ cache và TTL của danh mục công thức nấu ăn.
- **Evidence A:** SRS §3.2 FR-CAT-001/003 (trang 24, 25) $\rightarrow$ Sử dụng `IMemoryCache` (in-memory) với `TTL = 60 phút`, xóa cache qua `MemoryCache.Remove("categories:all")`.
- **Evidence B:** SRS §4.1 NFR-PERF-003 (trang 40) & §4.6 NFR-SCALE-001 (trang 44) $\rightarrow$ Sử dụng Redis distributed cache với `TTL = 30 phút` trong kiến trúc stateless backend (tham chiếu kiến trúc tổng quan §6.1).
- **Decision:** Use Redis distributed cache for Category with a 30-minute TTL.
- **Confirmed by:** Đại diện nhóm (xác nhận chính thức trong hội thoại), 2026-09-23.
- **Status:** `DECIDED`

### CONFLICT-021: Recipe List Author Visibility
- **Mô tả:** Mâu thuẫn về quyền xem bài viết của chính tác giả (Author) trong danh sách công thức tại `FR-RCP-001`.
- **Evidence A:** SRS §3.3 FR-RCP-001 (trang 28) phần Mô tả $
ightarrow$ Tác giả (Author) thấy thêm các công thức Draft và Archived của chính mình (`Status == Published OR (Status IN (Draft, Archived) AND AuthorId == currentUserId)`).
- **Evidence B:** SRS §3.3 FR-RCP-001 (trang 28) Luồng chính Happy Path bước 4 $
ightarrow$ Điều kiện filter cho Author chỉ gồm: `Status == Published OR (Status == Draft AND AuthorId == currentUserId)`. Công thức `Archived` của chính Author không xuất hiện trong filter này.
- **Decision:** Authors can see their own Draft and Archived recipes, as well as Published recipes visible to everyone.
- **Confirmed by:** Đại diện nhóm (xác nhận chính thức trong hội thoại), 2026-09-23.
- **Status:** `DECIDED`

### CONFLICT-022: Browser Version Support Matrix
- **Mô tả:** Mâu thuẫn về phiên bản trình duyệt tối thiểu được hỗ trợ giữa Chương 2 và Chương 5.
- **Evidence A:** SRS §2.4.3 (trang 14) quy định: Google Chrome 90+, Mozilla Firefox 88+, Microsoft Edge 90+, Safari 14+.
- **Evidence B:** SRS §5.4.2 (trang 49) quy định: Google Chrome 112+, Mozilla Firefox 113+, Microsoft Edge 112+, Safari 16+ (ES2020+).
- **Decision:** Support Chrome and Edge 112+, Firefox 113+, and Safari 16+.
- **Confirmed by:** Đại diện nhóm (xác nhận chính thức trong hội thoại), 2026-09-23.
- **Status:** `DECIDED`

### CONFLICT-023: Recipe Image Set-Primary Endpoint Contract
- **Mô tả:** Mâu thuẫn thiết kế endpoint đặt ảnh chính cho công thức nấu ăn giữa Chương 3 và Chương 8.
- **Evidence A:** SRS §3.3 FR-RCP-008 (trang 34) bước 9 quy định endpoint chuyên biệt: `PATCH /api/v1/recipes/{id}/images/{imageId}/primary`.
- **Evidence B:** SRS §8.4 Recipe Images API (trang 64) quy định cập nhật metadata ảnh chung qua `PATCH /api/v1/recipes/{id}/images/{imageId}` với body `{ altText?, isPrimary?, orderIndex? }`.
- **Decision:** Use the dedicated PATCH /api/v1/recipes/{id}/images/{imageId}/primary endpoint.
- **Confirmed by:** Đại diện nhóm (xác nhận chính thức trong hội thoại), 2026-09-23.
- **Status:** `DECIDED`

### CONFLICT-024: Recipe Image Upload Response Shape / URL Property Naming
- **Mô tả:** Mâu thuẫn giữa payload phản hồi upload ảnh trong FR-RCP-008 và Chapter 8 §8.4 / Data Model §7.5.
- **Evidence A:** SRS §3.3 FR-RCP-008 (trang 34) quy định upload ảnh trả về HTTP 201 với payload dạng: `{ url, isPrimary }`.
- **Evidence B:** SRS §8.4 Recipe Images API (trang 64) quy định upload ảnh trả về HTTP 201 với payload đầy đủ: `{ imageId, originalUrl, altText, isPrimary }`.
- **Evidence C:** SRS §7.5 RecipeImage Data Model (trang 58) định nghĩa trường lưu trữ CSDL là `OriginalUrl varchar(500) NOT NULL`.
- **Decision:** Upload returns imageId, originalUrl, altText, and isPrimary.
- **Confirmed by:** Đại diện nhóm (xác nhận chính thức trong hội thoại), 2026-09-23.
- **Status:** `DECIDED`

### CONFLICT-025: BaseEntity Inheritance vs ApplicationUser Identity Inheritance
- **Mô tả:** Mâu thuẫn mô hình kế thừa thực thể giữa quy tắc kiến trúc chung của hệ thống và cấu trúc bảng người dùng ASP.NET Core Identity.
- **Evidence A:** SRS §6.4 (trang 52) và §7.1 (trang 54) quy định: "Tất cả entities kế thừa BaseEntity". `BaseEntity` bắt buộc chứa các trường: `Id` (uuid), `CreatedAt` (timestamptz), `UpdatedAt` (timestamptz), `IsDeleted` (boolean soft delete áp dụng Global Query Filter), `RowVersion` (bytea optimistic concurrency token).
- **Evidence B:** SRS §7.7 (trang 58–59) đặc tả `ApplicationUser` kế thừa trực tiếp từ `IdentityUser<string>` (`AspNetUsers`), sử dụng các cột mặc định của Identity (`Id varchar(450)`, `Email`, `PasswordHash`, `ConcurrencyStamp`,...) cùng các cột tùy biến (`DisplayName`, `AvatarUrl`, `Bio`, `IsActive`, `CreatedAt`). Tài liệu không mô tả việc kế thừa các thuộc tính của `BaseEntity` (không có `RowVersion bytea`, không có `IsDeleted boolean` để EF Core áp dụng Global Query Filter đồng nhất).
- **Impact:** TV1 Auth entity / TV2 DbContext & Migrations.
- **Question:** ApplicationUser có thuộc quy tắc BaseEntity hay là ngoại lệ Identity riêng?
- **Decision:** ApplicationUser remains a direct IdentityUser<string> subtype and an explicit exception to BaseEntity; do not apply BaseEntity RowVersion or IsDeleted global filtering to Identity.
- **Confirmed by:** Đại diện nhóm (xác nhận chính thức trong hội thoại), 2026-09-23.
- **Status:** `DECIDED`
- **Affected:** TV1 Auth Entity / TV2 DbContext & Migrations.
- **Cross-Team Blocker:** TV1 ↔ TV2 (Group 4).
