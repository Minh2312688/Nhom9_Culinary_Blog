# Kế Hoạch Triển Khai: Bước Nấu Có Cấu Trúc, Cache Redis, Xóa Ảnh & E2E Testing

> **Phụ trách:** Dương Văn Minh (TV2 – MSSV: 2312688)  
> **Thời gian:** Tuần 8 (28/10/2026)  
> **Nhánh thực hiện:** `2312688_DuongVanMinh_feature/recipe-crud-enhancement-e2e`  
> **Trạng thái:** `[x]` Đã hoàn thành 100% (5/5 Giai đoạn hoàn tất)  
> **Tài liệu đặc tả:** [FR-RCP-010-recipe-steps-cache-e2e-spec.md](../../features/FR-RCP-010-recipe-steps-cache-e2e-spec.md)  
> **Checklist theo dõi:** [FR-RCP-010-recipe-steps-cache-e2e-checklist.md](../../features/FR-RCP-010-recipe-steps-cache-e2e-checklist.md)  
> **Ma trận truy xuất yêu cầu:** [TEAM-REQUIREMENT-TRACEABILITY.md](../../srs-audit/TEAM-REQUIREMENT-TRACEABILITY.md)

---

## 1. Mục Tiêu & Phạm Vi Triển Khai

| STT | Nhiệm vụ chính | Yêu cầu SRS / Rủi ro kỹ thuật | Kết quả đạt được |
|---:|---|---|---|
| 1 | **Cải thiện form bước nấu có cấu trúc** | `FR-RCP-010`, SRS §3.3 trang 35–36, §7.3 | Nâng cấp `StructuredStep` (tiêu đề, thời lượng, mô tả, ảnh), nút đổi thứ tự (↑/↓), xóa bước (×) |
| 2 | **Tối ưu hóa Cache Redis & Xóa ảnh MinIO** | `NFR-PERF-001`, `TECH-RISK-004`, `FR-RCP-007` | Chuẩn hóa key lowercase, generation key pattern `recipes:generation`, eager-load & xóa file MinIO khi xóa Recipe |
| 3 | **Sửa lỗi Foreign Key Testcontainers** | `CONS-006`, PostgreSQL Testcontainers | Thêm `ApplicationUser` trước khi tạo `Recipe` trong `PostgresMigrationTests` và `RecipeStepConcurrencyTests` |
| 4 | **Hiển thị thông tin dinh dưỡng & chi tiết bước** | `FR-RCP-010`, `FR-RCP-009` | Section dinh dưỡng 6 chỉ số (Calories, Protein, Carbs, Fat, Fiber, Sodium), huy hiệu thời gian `⏱ X phút`, ảnh bước nấu |
| 5 | **Bộ kiểm thử E2E Playwright hoàn chỉnh** | Kiểm thử luồng tạo/sửa, phân quyền, thứ tự bước, ảnh | 7/7 kịch bản E2E tự động hóa pass 100% trong 35.2s |

---

## 2. Chi Tiết Thực Hiện Theo Từng Giai Đoạn

### Giai đoạn 1: Lập Kế hoạch, Đặc tả Kỹ thuật & Cập nhật Ma trận Traceability
- [x] Phân tích hiện trạng dự án, mã nguồn backend, frontend và các file tài liệu SRS audit.
- [x] Soạn thảo file đặc tả kỹ thuật: `docs/features/FR-RCP-010-recipe-steps-cache-e2e-spec.md`.
- [x] Soạn thảo kế hoạch triển khai: `docs/superpowers/plans/2026-10-28-recipe-crud-enhancement-e2e-plan.md`.
- [x] Soạn thảo checklist theo dõi: `docs/features/FR-RCP-010-recipe-steps-cache-e2e-checklist.md`.
- [x] **Commit:** `70b4bac4` - `docs: create plan, spec, and checklist for recipe steps, cache, and e2e`

---

### Giai đoạn 2: Backend & Persistence: Fix Testcontainers, Cache Redis & Luồng Xóa Ảnh MinIO
- [x] **Sửa lỗi Foreign Key Testcontainers**:
  - `PostgresMigrationTests.cs`: Tạo `ApplicationUser` với `using ApplicationUser = CulinaryBlog.Infrastructure.Identity.ApplicationUser;` trước khi chèn `Recipe` để thỏa mãn ràng buộc `FK_Recipes_AspNetUsers_AuthorId`.
  - `RecipeStepConcurrencyTests.cs`: Tạo `ApplicationUser` trước khi khởi tạo `Recipe` và `RecipeStep`.
- [x] **Tối ưu hóa Cache Redis (`DistributedRecipeCache.cs`)**:
  - Chuẩn hóa key đọc/ghi/xóa luôn ở dạng lowercase: `recipes:slug:{slug.ToLowerInvariant()}`.
  - Tích hợp pattern generation key (`recipes:generation`) cho `RemoveByPrefixAsync` tương tự `DistributedCategoryCache` để vô hiệu hóa cache danh sách và tìm kiếm nhất quán mà không dùng lệnh KEYS chặn luồng.
  - Cập nhật chuẩn hóa key lowercase đồng bộ tại `RecipeImageAccess.cs`, `RecipeStepCommandHelpers.cs`, `RecipeIngredientCommandHelpers.cs`, `UpdateRecipeCommand.cs`.
- [x] **Cải thiện luồng xóa Recipe trong `RecipeLifecycleHandler.cs` (TECH-RISK-004)**:
  - Bổ sung phụ thuộc `IFileStorageService`.
  - Eager load danh sách `Images` của Recipe khi xử lý `DeleteRecipeCommand`.
  - Thực hiện xóa triệt để các file ảnh vật lý trên MinIO thông qua `IFileStorageService.DeleteAsync` (`OriginalUrl`, `MediumUrl`, `ThumbnailUrl`) trước khi xóa entity khỏi database.
- [x] **Kiểm thử tự động Backend**:
  - `CulinaryBlog.Application.Tests`: **725/725 passed (100%)**
  - `CulinaryBlog.Integration.Tests`: **138/138 passed (100%)** (0 failed, 9 skipped).
- [x] **Commit:** `eaf17043` - `feat(backend): optimize recipe redis cache invalidation, minio cleanup on delete, and fix testcontainers FK`

---

### Giai đoạn 3: Frontend Recipe Steps & Nutrition Display Enhancement
- [x] **Nâng cấp interface `StructuredStep` trong `recipe-editor.tsx`**:
  ```typescript
  export interface StructuredStep {
    id?: string;
    stepNumber?: number;
    title: string;
    description: string;
    durationMinutes: number | null;
    imageUrl: string | null;
  }
  ```
- [x] **Cập nhật giao diện nhập liệu bước nấu trong `recipe-editor.tsx`**:
  - Thẻ hiển thị từng bước với badge `Bước X`.
  - Input `Tiêu đề bước` (Title), input `Thời lượng (phút)` (DurationMinutes), textarea `Mô tả chi tiết` (Description, bắt buộc), input `URL ảnh minh họa` (ImageUrl, tùy chọn).
  - Nút di chuyển thứ tự bước: **Lên (↑)** (`data-testid="step-move-up-X"`), **Xuống (↓)** (`data-testid="step-move-down-X"`), **Xóa (×)** (`data-testid="step-delete-X"`).
  - Đồng bộ hai chiều giữa text nhanh `steps` và danh sách bước có cấu trúc.
  - Tự động map đầy đủ các trường của từng bước khi edit công thức hiện có và gửi đầy đủ dữ liệu khi gọi API `createRecipe` / `updateRecipe`.
- [x] **Cập nhật `RecipeDetailClient` (`frontend/src/app/recipes/[slug]/recipe-detail-client.tsx`)**:
  - Section `Giá trị dinh dưỡng (mỗi khẩu phần)` (`data-testid="recipe-nutrition"`) hiển thị 6 chỉ số: Calories, Protein, Carbohydrates, Fat, Fiber, Sodium.
  - Hiển thị bước nấu phong phú: Tiêu đề bước, huy hiệu thời lượng (`⏱ X phút`), ảnh minh họa bước nấu và mô tả thao tác.
- [x] **Kiểm tra Frontend Build**:
  - `npx tsc --noEmit`: 0 lỗi type.
  - `npm run build`: Next.js 15.5.25 production build thành công 16/16 pages.
- [x] **Commit:** `e7c7bdd2` - `feat(frontend): enhance recipe structured steps editor, reordering, and nutrition display`

---

### Giai đoạn 4: Bộ Kiểm thử E2E Hoàn Chỉnh với Playwright
- [x] **Mở rộng Mock API Server (`mock-recipe-api.mjs`)**:
  - Quản lý in-memory store các recipes và categories.
  - Hỗ trợ đầy đủ các routes:
    - `GET /api/v1/categories` & `GET /api/v1/categories/` (paginated result).
    - `GET /api/v1/recipes`, `POST /api/v1/recipes`, `GET /api/v1/recipes/:slug`, `PUT /api/v1/recipes/:id`, `DELETE /api/v1/recipes/:id`.
    - `POST /api/v1/recipes/:id/images/`, `PUT /api/v1/recipes/:id/images/:imageId/primary`, `DELETE /api/v1/recipes/:id/images/:imageId`.
    - Hỗ trợ kiểm tra phân quyền 403 Forbidden khi gặp token không hợp lệ (`forbidden-token`).
- [x] **Xây dựng bộ test E2E tự động hóa (`frontend/e2e/recipe-crud-flow.spec.ts`)**:
  - **Test 1**: *Access Control*: Chuyển hướng người dùng chưa đăng nhập về `/auth/login`.
  - **Test 2**: *Access Control*: Người dùng có token truy cập form tạo công thức thành công.
  - **Test 3**: *Recipe Creation*: Tạo công thức với nguyên liệu, các bước nấu có cấu trúc (tiêu đề, thời gian, mô tả, ảnh) và 6 trường dinh dưỡng.
  - **Test 4**: *Step Reordering*: Thêm 3 bước, đổi thứ tự bước xuống/lên, xóa bước và xác minh tính đúng đắn của danh sách.
  - **Test 5**: *Recipe Detail View*: Kiểm tra hiển thị đầy đủ thông tin dinh dưỡng, nguyên liệu và các bước kèm huy hiệu thời gian và ảnh minh họa.
  - **Test 6**: *Image Gallery*: Tải lên file ảnh, đặt làm ảnh chính (`⭐`), xóa ảnh (`🗑`) với dialog confirmation.
  - **Test 7**: *Access Control*: Người dùng không sở hữu công thức bị chặn cập nhật với mã lỗi 403 Forbidden và hiển thị thông báo lỗi thân thiện.
- [x] **Kết quả kiểm thử Playwright**:
  - `npx playwright test e2e/recipe-crud-flow.spec.ts`: **7/7 passed 100% (35.2s)**.
  - Regression test `e2e/author-profile.spec.ts`: **2/2 passed 100% (28.1s)**.
- [x] **Commit:** `68a7ee7f` - `test(e2e): add comprehensive recipe crud, structured steps, nutrition and image flow tests`

---

### Giai đoạn 5: Cập Nhật Tài Liệu Nghiệm Thu & Hoàn Tất
- [x] **`TEAM-REQUIREMENT-TRACEABILITY.md`**:
  - Chuyển trạng thái yêu cầu `FR-RCP-010` sang `DONE`.
  - Ghi nhận đầy đủ commit hashes và phạm vi hoàn thành.
- [x] **`README.md`**:
  - Cập nhật Mục 10.2 cho TV2 Dương Văn Minh: nghiệm thu các chức năng bước nấu có cấu trúc, cache Redis generation key, xóa ảnh MinIO tránh file mồ côi và 7/7 kịch bản test E2E.
- [x] **`FR-RCP-010-recipe-steps-cache-e2e-checklist.md`**:
  - Đánh dấu hoàn thành toàn bộ 5 nhóm nhiệm vụ kèm bằng chứng kiểm thử thực tế.
- [x] **Commit:** `17141cbe` - `docs: update traceability, readme, and implementation checklist for completed week 8 tasks`

---

## 3. Lịch Sử Commit Chi Tiết

| Commit Hash | Loại commit | Thông điệp commit | Nội dung chính |
|---|---|---|---|
| `70b4bac4` | `docs` | `docs: create plan, spec, and checklist for recipe steps, cache, and e2e` | Lập tài liệu đặc tả kỹ thuật, kế hoạch 5 pha và checklist theo dõi |
| `eaf17043` | `feat` | `feat(backend): optimize recipe redis cache invalidation, minio cleanup on delete, and fix testcontainers FK` | Fix FK PostgresMigrationTests, generation key Redis, eager load xóa file MinIO |
| `e7c7bdd2` | `feat` | `feat(frontend): enhance recipe structured steps editor, reordering, and nutrition display` | Form bước nấu có cấu trúc (title, duration, image, move up/down), bảng dinh dưỡng 6 chỉ số |
| `68a7ee7f` | `test` | `test(e2e): add comprehensive recipe crud, structured steps, nutrition and image flow tests` | Mock API CRUD, 7/7 kịch bản Playwright E2E pass 100% |
| `17141cbe` | `docs` | `docs: update traceability, readme, and implementation checklist for completed week 8 tasks` | Cập nhật README, Traceability Matrix, hoàn thiện checklist nghiệm thu |

---

## 4. Bằng Chứng Nghiệm Thu Kỹ Thuật (Evidence Matrix)

```text
============================== KIỂM THỬ BACKEND ==============================
CulinaryBlog.Application.Tests:
  Passed! - Failed: 0, Passed: 725, Skipped: 0, Total: 725, Duration: 4 s

CulinaryBlog.Integration.Tests:
  Passed! - Failed: 0, Passed: 138, Skipped: 9, Total: 147, Duration: 13 s
  (Đã sửa lỗi FK PostgresMigrationTests & RecipeStepConcurrencyTests)

============================== KIỂM THỬ FRONTEND ==============================
TypeScript Check:
  npx tsc --noEmit -> Exit code 0 (0 errors)

Next.js Production Build:
  Route (app)                                 Size  First Load JS
  ┌ ○ /                                    48.4 kB         154 kB
  ├ ○ /dashboard/recipes                    5.5 kB         112 kB
  ├ ƒ /dashboard/recipes/[id]/edit           128 B         116 kB
  ├ ○ /dashboard/recipes/new                 128 B         116 kB
  ├ ƒ /recipes/[slug]                         4 kB         110 kB
  ✓ Generating static pages (16/16)
  ✓ Compiled successfully in 42s

============================== KIỂM THỬ E2E (PLAYWRIGHT) =====================
Running 7 tests using 1 worker:
  ✓ 1. Access Control: redirects unauthenticated user away from recipe creator to /auth/login
  ✓ 2. Access Control: authenticated user can access recipe editor
  ✓ 3. Recipe Creation with Structured Steps, Ingredients, and Nutrition
  ✓ 4. Structured Steps Reordering (Move Up / Down / Delete)
  ✓ 5. Recipe Detail Page renders Structured Steps and Nutrition Info correctly
  ✓ 6. Recipe Image Gallery: Upload, Set Primary, and Delete Image
  ✓ 7. Access Control: Non-owner gets 403 Forbidden on update
  7 passed (35.2s)
```

---

## 5. Kết Luận

Nhiệm vụ Tuần 8 của TV2 Dương Văn Minh (MSSV: 2312688) đã được triển khai hoàn chỉnh, triệt để từ tầng Persistence/Backend đến Frontend UI và bộ kiểm thử tự động E2E, đáp ứng 100% các tiêu chuẩn kỹ thuật trong tài liệu đặc tả SRS của dự án Culinary Blog.
