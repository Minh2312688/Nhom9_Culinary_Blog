# Checklist triển khai Culinary Blog, hồ sơ tác giả và SEO

> **Trạng thái hiện tại (17/11/2026):** Các route, author profile, Recipe SEO,
> brand/responsive changes được ghi nhận là đã triển khai. Manual WCAG review
> với screen reader vẫn chưa thực hiện; lượt full E2E 133 tests trước đó timeout.
> Các kết quả historical trong checklist không được chạy lại cho lần cập nhật
> tài liệu này, ngoại trừ migration regression liên quan `PublishedAt` (2/2
> PostgreSQL migration tests đạt ngày 2026-11-17).

**Trạng thái:** Hoàn thành
**Đặc tả:** `../superpowers/specs/2026-10-14-frontend-screens-usability-seo-design.md`
**Kế hoạch:** `../superpowers/plans/2026-10-14-frontend-screens-usability-seo.md`

> Chỉ đánh dấu hoàn thành khi có bằng chứng từ lệnh/test. Không gán ngày xuất bản giả cho recipe cũ.

## 1. PublishedAt và database

- [x] `Recipe.PublishedAt` nullable UTC; publish sets, archive preserves, republish refreshes.
- [x] Migration adds nullable column without backfill from `CreatedAt`.
- [x] PostgreSQL Testcontainers upgrade test verifies a pre-existing Published recipe retains `PublishedAt=NULL` after all migrations; không tạo ngày lịch sử giả.
- [x] Domain/lifecycle tests pass; API builds with migration and EF reports no pending model changes.
- [x] Commit riêng cho mốc persistence.

> NU1902/NU1903 advisories for `System.Security.Cryptography.Pkcs` 10.0.0 remain visible. Default strict restore/build treats them as errors; Task 1 and Task 2 focused commands used a command-scoped `WarningsNotAsErrors=NU1902;NU1903` exception, so those results are functional validation with visible warnings, not strict-policy validation. Package versions and global configuration are unchanged.

## 2. Public author API

- [x] `GET /api/v1/authors/{id}?page=&pageSize=` trả profile allowlist và trang recipe.
- [x] Chỉ account active và recipe Published; không có email/roles/Draft/Archived.
- [x] Missing/inactive author trả 404; pagination và thứ tự recipe có test.
- [x] Recipe read DTO bổ sung `PublishedAt` và author display data; write contracts không đổi.
- [x] Focused application/integration tests và API build pass with command-scoped audit-warning exceptions; commit `53393861`.

> Review fix round 1: very large valid pagination is covered and returns an empty page without overflowing. Override-scoped focused tests/build pass with NU1902/NU1903 visible. Default strict application restore and API build are blocked by those existing advisories as warning-as-error; strict validation is not claimed. See `task-2-report.md` for commands and outputs.

## 3. Public author UI và Recipe SEO

- [x] `/authors/[id]` hiển thị avatar fallback, tên, bio, recipe list, pagination, loading/error/empty.
- [x] Recipe detail SSR dùng cùng dữ liệu cho UI và JSON-LD; `author`/`datePublished` chỉ khi có dữ liệu.
- [x] Chi tiết recipe liên kết đúng hồ sơ tác giả; ngày cũ không có nguồn thì không hiển thị.
- [x] JSON-LD escape `<`; không phát schema cho Draft/Archived; recipe lịch sử không có ngày giả.
- [x] Compose dùng `API_INTERNAL_URL` server-only; author API không gửi token.
- [x] JSON-LD unit tests 5/5, Playwright profile/SEO/loading tests 5/5, frontend build pass; commits `7fb592a5`, `b6927053`.

> Task 3 passed independent review and scoped re-review. Existing build warnings are documented in the Task 3 report.

## 4. Nhận diện mới và Motion

- [x] Tên “Culinary Blog”, logo mark, palette coral/orange–turmeric–teal–cream, typography và shared tokens áp dụng nhất quán toàn app.
- [x] Public, auth, profile, dashboard và administration giữ nguyên hành vi, labels/route destinations.
- [x] Giữ CSS Modules/Tailwind và `lucide-react`; Motion chỉ dùng cho chuyển động nhỏ, SSR giữ hero hiển thị và reduced motion tắt chuyển động.
- [x] Contrast đạt 4.5:1 văn bản, 3:1 controls; brand E2E xác nhận viewport 375/768/1024/1280 không tràn ngang.
- [x] Brand/reduced-motion tests 7/7 pass, including rendered alpha gradient, hero reduced motion, and responsive overflow.

> Task 4 production build and Next.js type/lint checks pass; only the pre-existing `<img>` optimization warning remains. `npx tsc --noEmit` alone reports TS5097 in the Task 3 JSON-LD test import; the complete TypeScript check passes with `--allowImportingTsExtensions`. npm audit reports 13 advisories (3 moderate, 10 high) in existing dependencies; none of the newly added Motion packages are listed. Dependency upgrades outside the approved Motion addition were left out of this task.

## 5. Layout, usability và accessibility

- [x] Public routes và `/authors/[id]` dùng public shell; `/search` giữ tagline, dotted background và footer message mà không lặp header/footer.
- [x] `/dashboard/**` có shell quản trị riêng, active route/return link; auth token, payload và handlers giữ nguyên.
- [x] Loading skeletons và feedback status/alert; HTTP Problem Details được map vào field hoặc error summary.
- [x] Submit khóa khi lưu; publish lỗi giữ nguyên bản nháp; delete vẫn yêu cầu xác nhận.
- [x] Responsive E2E: 12 public/dashboard routes × 5 viewport boundaries (320, 767, 768, 1199, 1200 px), no horizontal overflow; 60/60 pass on Chromium.
- [x] Keyboard navigation smoke test trên trang chủ; axe scans dùng tags WCAG 2.1 A/AA.
- [x] Axe/keyboard automation 13/13 pass trên 12 route; đã thêm label cho `#imageFile` ở create/edit và sửa màu chữ các bước Wizard để đạt tương phản.
- [ ] Manual WCAG 2.1 AA review với keyboard/screen reader chưa thực hiện; không kết luận toàn hệ thống đạt chứng nhận WCAG AA.
- [x] Route/UI regression 24/24 pass (SRS, UI acceptance, author profile và brand; kết quả baseline trước lần rà soát này).

> Build còn warning `<img>` đã có trong recipe detail. Recipe-detail SRS fixture được bổ sung vào mock API server để khớp với SSR data fetching.

> **Kiểm chứng bổ sung 15/11/2026:** Responsive E2E — 60/60 pass sau khi ổn định chờ điều hướng và locator landmark. Axe/keyboard — 13/13 pass sau khi sửa label và contrast cho Recipe Editor. Lượt chạy toàn bộ 133 E2E chưa hoàn tất (timeout ở các suite ngoài phạm vi); không dùng kết quả đó để xác nhận pass.

## 6. Tài liệu, regression và chạy ứng dụng

- [x] Run/Docker docs khớp migration, server-only API URL, profile route và lệnh chạy.
- [x] SRS/traceability được cập nhật tại chỗ cần thiết; README có thay đổi sẵn được giữ nguyên.
- [x] Backend solution tests/build và frontend TypeScript/build/Playwright pass; skipped/failure được giải thích.
- [x] Migration áp dụng được trên PostgreSQL; API, frontend, `/authors/[id]`, recipe SEO và dashboard smoke-test thành công.
- [x] Initial HTML có JSON-LD author; ngày lịch sử NULL không được bịa; API/HTML không lộ email, roles hay recipe chưa Published.
- [x] Commit tài liệu, kiểm thử liên quan và lockfile đã đồng bộ sau khi toàn bộ bằng chứng hoàn tất.

> Docker frontend build ban đầu phát hiện `package-lock.json` thiếu/có phiên bản sai của các gói optional platform-specific. Đã đồng bộ lockfile bằng npm 10 của image Node 22; `npm ci` và production image build sau đó thành công, không đổi package version. Backend tests: 765 passed, 9 skipped, 0 failed. Frontend Playwright: 38/38 passed. Compose chạy đủ 6 dịch vụ; migration có mặt trong `__EFMigrationsHistory`. Smoke test: API 200, 14 route 200, profile pagination 100 recipes, không có email/roles, JSON-LD có author và bỏ qua ngày xuất bản lịch sử NULL.
