# Task 3 Report: Author Profiles and Recipe SEO

## Outcome

Implemented public author profiles and server-rendered Recipe JSON-LD using the committed public API read DTOs. Recipe details now fetch once on the server and pass that same recipe object to the client presentation and JSON-LD builder. Historical recipes keep their null `publishedAt`; no inferred date is emitted or shown. Non-Published API responses are rejected before recipe content or schema is rendered.

Author pages request `/api/v1/authors/{id}?page={page}&pageSize=12`, render only the public profile fields and paged recipes, and provide avatar fallback, pagination, empty, not-found, and error states. No authentication token is sent to either public endpoint. Compose configures the frontend's server-only API base URL as `http://api:8080`.

## TDD Evidence

### JSON-LD tests (before implementation)

Command, run from `frontend`:

```text
node --experimental-strip-types --test src/lib/recipe-json-ld.test.ts
```

Observed red: `ERR_MODULE_NOT_FOUND` for `src/lib/recipe-json-ld.ts`, with 1 test file failed and 0 tests passed. This was the expected missing production module.

### Page E2E tests (before page implementation)

Command, run from `frontend` with `PLAYWRIGHT_CHROMIUM_EXECUTABLE_PATH` set to the installed Chrome executable:

```text
npx playwright test e2e/recipe-json-ld.spec.ts e2e/author-profile.spec.ts
```

Observed red: 3 tests failed against the missing profile/SSR implementation. The author test reported `getByRole('heading', { name: 'Linh Nguyen' })` was not found. The recipe tests had no expected Recipe JSON-LD or rendered public author/date behavior.

The first Playwright launch attempt could not find Playwright's bundled Chromium. The focused tests were rerun using the already-installed Chrome executable; no browser download was needed.

## Review Fix Round 1: Route Loading Feedback

### TDD red

Added focused E2E checks that intentionally delay the mock API response for `/recipes/slow-recipe` and `/authors/slow-author`, then assert a visible status message before the server page completes.

Command, run from `frontend` with `PLAYWRIGHT_CHROMIUM_EXECUTABLE_PATH` set to the installed Chrome executable:

```text
npx playwright test e2e/recipe-json-ld.spec.ts e2e/author-profile.spec.ts --grep "shows loading feedback"
```

Before adding route loading boundaries, **2 tests failed** as expected: `getByRole('status')` was not found while the API response was delayed (one recipe case and one author case).

### Fix

Added App Router `loading.tsx` boundaries under both `/recipes/[slug]` and `/authors/[id]`, each rendering the existing site shell and a route-specific accessible `role="status"` feedback message.

## Final Verification

| Command | Result |
| --- | --- |
| `node --experimental-strip-types --test src/lib/recipe-json-ld.test.ts` | **Passed:** 5 tests, 0 failures. Covers author/date, null historical date, absent author, null nutrition, and hostile `<script>` content serialized with `<` escaped as `\\u003c`. Node emitted the existing typeless-package module warning. |
| `npx playwright test e2e/recipe-json-ld.spec.ts e2e/author-profile.spec.ts` | **Passed:** 5 tests, 0 failures. Verifies initial-HTML Recipe JSON-LD, matching author-profile navigation, historical date omission, profile pagination/no email exposure, and visible loading feedback while each server-side API request is pending. Used installed Chrome via `PLAYWRIGHT_CHROMIUM_EXECUTABLE_PATH`. |
| `npm run build` | **Passed:** final sequential production build, lint, and TypeScript checks completed. One existing `@next/next/no-img-element` warning remains for the recipe image element. A first build attempt run concurrently with Playwright failed when concurrent Next processes contended for `.next` output (`PageNotFoundError: Cannot find module for page: /_document`); rerunning the build after E2E completed passed. |
| `git diff --check` | **Passed.** |

## Changed Files

- `docker-compose.yml`
- `frontend/e2e/author-profile.spec.ts`
- `frontend/e2e/recipe-json-ld.spec.ts`
- `frontend/e2e/support/mock-recipe-api.mjs`
- `frontend/playwright.config.ts`
- `frontend/src/app/authors/[id]/author-profile-client.tsx`
- `frontend/src/app/authors/[id]/loading.tsx`
- `frontend/src/app/authors/[id]/page.tsx`
- `frontend/src/app/recipes/[slug]/loading.tsx`
- `frontend/src/app/recipes/[slug]/page.tsx`
- `frontend/src/app/recipes/[slug]/recipe-detail-client.tsx`
- `frontend/src/lib/recipe-json-ld.test.ts`
- `frontend/src/lib/recipe-json-ld.ts`
- `frontend/src/lib/recipes-api.ts`
- `frontend/src/lib/server/authors-api.ts`
- `frontend/src/lib/server/recipes-api.ts`

The pre-existing dirty `README.md` was not modified or staged. No backend contracts, dashboard behavior, global brand styling, or unrelated documents were changed.

## Commit

Committed with subject `feat: render public author profiles and recipe metadata` and the required Copilot co-author trailer.

Review fix round 1 is tracked in a separate commit with the same required co-author trailer.
