# Checklist triển khai Culinary Blog, hồ sơ tác giả và SEO

**Trạng thái:** Chưa triển khai
**Đặc tả:** `../superpowers/specs/2026-10-14-frontend-screens-usability-seo-design.md`
**Kế hoạch:** `../superpowers/plans/2026-10-14-frontend-screens-usability-seo.md`

> Chỉ đánh dấu hoàn thành khi có bằng chứng từ lệnh/test. Không gán ngày xuất bản giả cho recipe cũ.

## 1. PublishedAt và database

- [ ] `Recipe.PublishedAt` nullable UTC; publish sets, archive preserves, republish refreshes.
- [ ] Migration adds nullable column without backfill from `CreatedAt`.
- [ ] Domain/lifecycle tests pass; API builds with migration.
- [ ] Commit riêng cho mốc persistence.

## 2. Public author API

- [ ] `GET /api/v1/authors/{id}?page=&pageSize=` trả profile allowlist và trang recipe.
- [ ] Chỉ account active và recipe Published; không có email/roles/Draft/Archived.
- [ ] Missing/inactive author trả 404; pagination và thứ tự recipe có test.
- [ ] Recipe read DTO bổ sung `PublishedAt` và author display data; write contracts không đổi.
- [ ] Application/integration tests và API build pass; commit riêng.

## 3. Public author UI và Recipe SEO

- [ ] `/authors/[id]` hiển thị avatar fallback, tên, bio, recipe list, pagination, loading/error/empty.
- [ ] Recipe detail SSR dùng cùng dữ liệu cho UI và JSON-LD; `author`/`datePublished` chỉ khi có dữ liệu.
- [ ] Chi tiết recipe liên kết đúng hồ sơ tác giả; ngày cũ không có nguồn thì không hiển thị.
- [ ] JSON-LD escape `<`; không phát schema cho Draft/Archived; recipe lịch sử không có ngày giả.
- [ ] Compose dùng `API_INTERNAL_URL` server-only; author API không gửi token.
- [ ] Unit và Playwright author/SEO tests pass; commit riêng.

## 4. Nhận diện mới và Motion

- [ ] Tên “Culinary Blog”, logo mark, palette coral/orange–turmeric–teal–cream, typography và tokens áp dụng nhất quán toàn app.
- [ ] Public, auth, profile, dashboard và administration giữ nguyên hành vi, labels/route destinations.
- [ ] Giữ CSS Modules/Tailwind và `lucide-react`; Motion chỉ dùng cho chuyển động nhỏ, tắt khi reduced motion.
- [ ] Contrast đạt 4.5:1 văn bản, 3:1 controls; test viewport 375/768/1024/1280 không tràn ngang.
- [ ] Brand/reduced-motion tests pass; commit riêng.

## 5. Layout, usability và accessibility

- [ ] Sáu public routes và `/authors/[id]` dùng public shell; `/search` không lặp header/footer.
- [ ] `/dashboard/**` có shell quản trị riêng; token checks, form payload/API và semantics không đổi.
- [ ] Loading/error/empty/success feedback phù hợp; HTTP 400 Problem Details và field errors gắn đúng input.
- [ ] Submit khóa khi lưu; publish rollback khi lỗi; delete vẫn confirm.
- [ ] Keyboard, focus, accessible names, labels, alt text, landmarks và responsive UI được kiểm chứng.
- [ ] Route/UI Playwright tests pass; commit riêng.

## 6. Tài liệu, regression và chạy ứng dụng

- [ ] Run/Docker docs khớp migration, server-only API URL, profile route và lệnh chạy.
- [ ] SRS/traceability được cập nhật tại chỗ cần thiết; README có thay đổi sẵn được giữ nguyên.
- [ ] Backend solution tests/build và frontend TypeScript/build/Playwright pass; skipped/failure được giải thích.
- [ ] Migration áp dụng được trên PostgreSQL; API, frontend, `/authors/[id]`, recipe SEO và dashboard smoke-test thành công.
- [ ] Initial HTML có JSON-LD escape đúng; không rò email/role hay recipe private.
- [ ] Commit tài liệu và checklist sau khi toàn bộ bằng chứng hoàn tất.
