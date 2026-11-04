# Đặc Tả Kỹ Thuật: Cải Thiện Bước Nấu Có Cấu Trúc, Cache Redis, Xóa Ảnh & Kiểm Thử E2E

> **Tính năng:** FR-RCP-010 (Quản lý các bước nấu), FR-RCP-007 / FR-RCP-008 (Xóa công thức & quản lý ảnh), NFR-PERF-001 (Redis Cache)  
> **Người thực hiện:** Dương Văn Minh (TV2 – MSSV: 2312688)  
> **Ngày lập:** 28/10/2026  
> **Trạng thái:** [x] Đã hoàn thành (Completed - Verified with 7/7 E2E tests passing, 138/138 integration tests passing)

---

## 1. Bối Cảnh & Mục Tiêu

### 1.1. Bối cảnh
Theo SRS (§3.3 trang 35–36, §7.3 trang 57, §8.5 trang 65 và SRS-AUDIT), tính năng Quản lý các bước nấu ăn (`FR-RCP-010`) đòi hỏi dữ liệu có cấu trúc bao gồm: `StepNumber`, `Title`, `Description`, `DurationMinutes`, `ImageUrl`. Tuy nhiên, trước đây giao diện `recipe-editor.tsx` chỉ cho phép nhập chuỗi thô sơ `description` qua textarea phân cách dòng, làm mất dữ liệu `Title`, `DurationMinutes` và `ImageUrl` khi biên tập công thức.

Đồng thời:
- Cơ chế cache Redis cho Recipe (`DistributedRecipeCache`) cần chuẩn hóa định dạng key (chữ thường - lowercase) và đồng bộ invalidation khi công thức, bước, nguyên liệu hoặc ảnh thay đổi.
- Luồng xóa ảnh (`DeleteRecipeImageCommand`) và xóa công thức (`DeleteRecipeCommand`) cần phối hợp xóa sạch tài nguyên object storage (MinIO) tránh rò rỉ file mồ côi (`TECH-RISK-004`).
- Cần có bộ kiểm thử E2E hoàn chỉnh kiểm tra toàn bộ vòng đời tạo/sửa công thức (ảnh, nguyên liệu, bước nấu có cấu trúc, dinh dưỡng), kiểm tra phân quyền tác giả, thứ tự bước và cache/xóa ảnh.

### 1.2. Mục tiêu kỹ thuật
1. **Dữ liệu Bước nấu có Cấu trúc (FR-RCP-010)**:
   - Cung cấp form nhập liệu động cho các bước nấu trong `recipe-editor.tsx` gồm: `Title` (tiêu đề bước), `Description` (nội dung chi tiết), `DurationMinutes` (thời lượng), `ImageUrl` (ảnh minh họa).
   - Cho phép thêm, sửa, xóa, và điều chỉnh thứ tự (lên/xuống) các bước trực quan.
   - Hiển thị đầy đủ thông tin bước nấu có cấu trúc và thông tin dinh dưỡng trên trang chi tiết công thức `recipe-detail-client.tsx`.
2. **Chuẩn hóa Cache Redis & Dọn dẹp Tài nguyên Ảnh (FR-RCP-007, FR-RCP-008, NFR-PERF-001)**:
   - Chuẩn hóa toàn bộ key cache `recipes:slug:{slug.ToLowerInvariant()}` nhằm loại trừ lỗi lệch case-sensitivity trên Redis.
   - Mở rộng cơ chế invalidation của `DistributedRecipeCache` với cache versioning/generation key để vô hiệu hóa cache danh sách/tìm kiếm khi có thay đổi dữ liệu công thức.
   - Bổ sung xóa file ảnh vật lý trên MinIO khi xóa công thức (`DeleteRecipeCommand` trong `RecipeLifecycleHandler`).
   - Sửa lỗi foreign key `FK_Recipes_AspNetUsers_AuthorId` trong bộ testcontainers integration tests (`PostgresMigrationTests` và `RecipeStepConcurrencyTests`).
3. **Kiểm thử E2E Hoàn chỉnh với Playwright**:
   - Xây dựng file test E2E `frontend/e2e/recipe-crud.spec.ts` kiểm thử toàn diện:
     - Tạo công thức kèm ảnh, nguyên liệu, bước có cấu trúc, dinh dưỡng.
     - Hiển thị đúng dữ liệu trên giao diện chi tiết.
     - Chỉnh sửa công thức, cập nhật dinh dưỡng, thêm/bớt/đổi thứ tự bước.
     - Upload ảnh, đặt ảnh chính, xóa ảnh.
     - Kiểm tra quyền truy cập (Guest/Non-owner bị chặn, Owner/Admin được phép).
     - Kiểm tra dọn dẹp ảnh và xóa công thức.

---

## 2. Thiết Kế Chi Tiết

### 2.1. Cấu trúc Dữ liệu Bước nấu (FR-RCP-010)
```typescript
export interface RecipeStep {
  id?: string;
  stepNumber?: number;
  title: string | null;
  description: string;
  durationMinutes: number | null;
  imageUrl: string | null;
}
```

Trên giao diện `recipe-editor.tsx`:
- Quản lý state danh sách `structuredSteps: StructuredStep[]`.
- Cho phép nhập trực tiếp từng thuộc tính qua các trường tương ứng:
  - `Title`: input text ngắn gọn (tối đa 200 ký tự).
  - `Description`: textarea mô tả các thao tác của bước.
  - `DurationMinutes`: input number (phút >= 0).
  - `ImageUrl`: input text URL ảnh minh họa (nếu có).
  - Nút điều hướng thứ tự: Lên (▲), Xuống (▼), Xóa (×).

### 2.2. Hiển thị Dinh dưỡng & Bước nấu trên Recipe Detail (`recipe-detail-client.tsx`)
- Thêm Section `Giá trị dinh dưỡng (Mỗi khẩu phần)`:
  - Hiển thị trực quan 6 chỉ số: Calories (`kcal`), Protein (`g`), Carbohydrates (`g`), Fat (`g`), Fiber (`g`), Sodium (`mg`).
- Trong Section `Cách thực hiện`:
  - Hiển thị thứ tự bước `Bước {stepNumber}: {title}`.
  - Hiển thị thời gian thực hiện nếu có (`⏱ {durationMinutes} phút`).
  - Hiển thị ảnh minh họa bước nếu có `imageUrl`.
  - Hiển thị `description`.

### 2.3. Redis Cache & Dọn dẹp File Ảnh
- **Redis Cache Key Convention**:
  - Chi tiết công thức: `recipes:slug:{slug.ToLowerInvariant()}:g{generation}`
  - Generation Key: `recipes:generation` (khi invalidate sẽ cập nhật token mới).
- **Luồng Xóa Công Thức Kèm Xóa File MinIO (`DeleteRecipeCommand`)**:
  1. Tải Recipe kèm danh sách `Images`.
  2. Xóa các file vật lý trên MinIO thông qua `IFileStorageService.DeleteAsync` (`OriginalUrl`, `MediumUrl`, `ThumbnailUrl`).
  3. Xóa Recipe khỏi DbContext (cascade xóa các bản ghi liên quan trong DB).
  4. Lưu thay đổi DB (`SaveChangesAsync`).
  5. Invalidate cache Redis cho slug tương ứng và tăng cache generation.

### 2.4. Kịch bản Kiểm thử E2E (Playwright)
1. **Test Case 1: Tạo công thức đầy đủ (Create Recipe Flow)**:
   - Nhập thông tin cơ bản + nguyên liệu + các bước có cấu trúc (có title, duration) + bật dinh dưỡng (nhập 6 chỉ số).
   - Kiểm tra request gửi đi đúng contract.
   - Kiểm tra thông báo lưu thành công và điều hướng.
2. **Test Case 2: Kiểm tra lưu & hiển thị chi tiết (Recipe Detail Display)**:
   - Kiểm tra ảnh đại diện chính, danh sách ảnh.
   - Kiểm tra danh sách nguyên liệu và ghi chú.
   - Kiểm tra danh sách các bước nấu có cấu trúc (số thứ tự, tiêu đề, thời lượng, nội dung).
   - Kiểm tra bảng dinh dưỡng 6 chỉ số.
3. **Test Case 3: Sửa công thức & quản lý thứ tự bước (Edit & Reorder Steps)**:
   - Load dữ liệu công thức vào form chỉnh sửa.
   - Thêm bước mới, di chuyển bước lên/xuống, xóa bước.
   - Cập nhật thông số dinh dưỡng.
   - Lưu công thức và xác nhận dữ liệu cập nhật thành công.
4. **Test Case 4: Quản lý ảnh công thức (Image Gallery)**:
   - Upload ảnh mới -> kiểm tra preview.
   - Đặt ảnh làm ảnh đại diện chính (`Set Primary`).
   - Xóa ảnh -> xác nhận dialog và ảnh bị xóa khỏi danh sách.
5. **Test Case 5: Phân quyền truy cập (Access Control)**:
   - Chưa đăng nhập truy cập `/dashboard/recipes/new` -> redirect về login hoặc chặn truy cập.
   - Không phải chủ sở hữu hoặc admin -> nhận HTTP 403 / thông báo không có quyền chỉnh sửa.

