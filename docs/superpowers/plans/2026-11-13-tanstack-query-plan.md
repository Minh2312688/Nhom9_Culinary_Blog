# TanStack Query Server-State Migration Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace duplicated component-level server-data loading with TanStack Query for public pages, Dashboard, Profile, and supported auth form requests while preserving SSR and existing UI behavior.

**Architecture:** A shared QueryClient and centralized domain query keys back query/mutation hooks. Public pages continue using public API helpers; authenticated mutations and reads use the Auth.js-protected BFF from the prerequisite Auth.js plan. Recipe metadata, JSON-LD, and Author SSR remain server-rendered and seed their client queries.

**Tech Stack:** Next.js 15 App Router, React 19, TanStack Query (`@tanstack/react-query@5.107.1`), TypeScript, Playwright.

**Spec:** [../specs/2026-11-13-authjs-tanstack-design.md](../specs/2026-11-13-authjs-tanstack-design.md)

## Global Constraints

- Complete [the Auth.js and BFF plan](./2026-11-13-authjs-bff-plan.md) first; private Query operations depend on its BFF and Auth.js session.
- Use `@tanstack/react-query@5.107.1`; do not upgrade Next.js 15.
- Use Query for client-managed server data; keep form values, selected files, previews, modal state, and other local UI state in React/React Hook Form.
- Preserve Recipe metadata, Recipe JSON-LD, Author SSR, current route behavior, and current loading/error/empty/success UX.
- Private browser requests use the same-origin BFF and never receive backend tokens.
- Include query filters/pagination and user identity in the appropriate query keys.
- Clear private cached data on logout and authenticated-user change.
- Do not retry 4xx responses; use `AbortSignal` in query functions.
- Do not add a database adapter, Next.js 16, or unrelated state-management libraries.

## File map

- `frontend/package.json`, `frontend/package-lock.json`: install the exact Query dependency.
- `frontend/src/app/providers.tsx` and `frontend/src/app/layout.tsx`: compose `SessionProvider` and `QueryClientProvider`.
- `frontend/src/lib/queries/query-client.ts`, `query-keys.ts`, and domain query modules: shared QueryClient defaults, key factories, and hooks.
- `frontend/src/lib/recipes-api.ts` and `frontend/src/lib/auth-api.ts`: public/private request helpers that accept AbortSignals and no token parameters.
- Public pages: `src/app/page.tsx`, `recipes/page.tsx`, `search/page.tsx`, `categories/page.tsx`, `categories/[slug]/page.tsx`.
- SSR-seeded pages: `src/app/recipes/[slug]/page.tsx`, `recipe-detail-client.tsx`, `src/app/authors/[id]/page.tsx`, and `author-profile-client.tsx`.
- Private pages: Dashboard Recipe/Category lists and `dashboard/recipe-editor.tsx`, plus `profile/page.tsx` and `site-ui.tsx`.
- `frontend/e2e/support/mock-recipe-api.mjs` and `frontend/e2e/query-cache.spec.ts`: deterministic server responses, request counters, mutation invalidation, and user-switch coverage.
- `docs/recipe-core-implementation-spec.md`: document Query keys, usage boundaries, and invalidation behavior after migration.

---

### Task 1: Add QueryClient infrastructure and migrate Home/Recipe list

**Files:**
- Modify: `frontend/package.json`
- Modify: `frontend/package-lock.json`
- Create: `frontend/src/lib/queries/query-client.ts`
- Create: `frontend/src/lib/queries/query-keys.ts`
- Create: `frontend/src/lib/queries/recipes.ts`
- Modify: `frontend/src/app/providers.tsx`
- Modify: `frontend/src/app/page.tsx`
- Modify: `frontend/src/app/recipes/page.tsx`
- Modify: `frontend/e2e/support/mock-recipe-api.mjs`
- Create: `frontend/e2e/query-cache.spec.ts`

**Interfaces:**
- Export `createQueryClient(): QueryClient` with a 30-second default `staleTime`, mutation retries disabled, and this query retry predicate:

```ts
retry: (failureCount, error) =>
  failureCount < 1 &&
  (error instanceof TypeError || (error instanceof ApiRequestError && error.status >= 500))
```
- Export `useRecipes(params: { page: number; pageSize: number; categoryId?: string })`; pass TanStack's signal to `getRecipes(signal, params.pageSize, undefined, params.page)`.
- Export key factories:

```ts
export const queryKeys = {
  recipes: {
    all: ["recipes"] as const,
    lists: () => [...queryKeys.recipes.all, "list"] as const,
    list: (params: { page: number; pageSize: number; categoryId?: string; search?: string }) =>
      [...queryKeys.recipes.lists(), params] as const,
    details: () => [...queryKeys.recipes.all, "detail"] as const,
    detail: (slug: string) => [...queryKeys.recipes.details(), slug] as const,
  },
  categories: {
    all: ["categories"] as const,
    list: () => [...queryKeys.categories.all, "list"] as const,
    detail: (slug: string) => [...queryKeys.categories.all, "detail", slug] as const,
  },
  search: {
    all: ["search"] as const,
    recipes: (params: string) => [...queryKeys.search.all, "recipes", params] as const,
  },
  dashboard: {
    recipeLists: (userId: string) => ["dashboard", userId, "recipes"] as const,
    recipes: (userId: string, page: number) => ["dashboard", userId, "recipes", page] as const,
    recipeDetails: (userId: string) => ["dashboard", userId, "recipe-detail"] as const,
    recipeDetail: (userId: string, id: string) =>
      [...queryKeys.dashboard.recipeDetails(userId), id] as const,
    categories: (userId: string) => ["dashboard", userId, "categories"] as const,
  },
  profile: (userId: string) => ["profile", userId] as const,
  authors: {
    profile: (id: string, page: number) => ["authors", id, page] as const,
  },
};
```

- Query functions accept TanStack's `AbortSignal`; public reads call the public Recipe API with no token.

- [ ] **Step 1: Add a failing Recipe-list cache reuse test**

Add request recording to the Playwright-only mock API server. It exposes `GET /__test__/requests` as JSON records containing `method`, `path`, `search`, and a boolean `authorizationPresent`, and `DELETE /__test__/requests` to clear the records. In `query-cache.spec.ts`, clear the records, visit `/recipes`, navigate to Home with the brand link, then return using the header's `Công thức` link (not `page.goto`) to preserve the QueryClient. Assert that the page-size-12 Recipe list response is reused while fresh.

```ts
test("Recipe list cache reuse", async ({ page, request }) => {
  await request.delete("http://127.0.0.1:5055/__test__/requests");
  await page.goto("/recipes");
  await expect(page.getByRole("link", { name: /Roasted tomato soup/ })).toBeVisible();
  await page.getByRole("link", { name: "Culinary Blog, trang chủ" }).click();
  await page.getByRole("link", { name: "Công thức" }).click();
  await expect(page).toHaveURL(/\/recipes$/);
  await expect(page.getByRole("link", { name: /Roasted tomato soup/ })).toBeVisible();
  const response = await request.get("http://127.0.0.1:5055/__test__/requests");
  const requests = await response.json();
  expect(requests.filter((entry: { method: string; path: string; search: string }) =>
    entry.method === "GET" &&
    entry.path.replace(/\/$/, "") === "/api/v1/recipes" &&
    new URLSearchParams(entry.search).get("pageSize") === "12",
  )).toHaveLength(1);
});
```

- [ ] **Step 2: Run the test and confirm the current per-page fetching duplicates the request**

Run from `frontend`: `npx playwright test e2e/query-cache.spec.ts --grep "Recipe list cache reuse"`.

Expected: FAIL because Home and Recipe list currently fetch independently in component effects.

- [ ] **Step 3: Install Query and add shared providers/query modules**

Run from `frontend`: `npm install @tanstack/react-query@5.107.1`. Extend `providers.tsx` to wrap existing `SessionProvider` and `QueryClientProvider`. Use one QueryClient per browser provider mount. Put the key factories in `query-keys.ts` and implement `useRecipes(params)` in `queries/recipes.ts` with the signal passed through to `getRecipes`.

- [ ] **Step 4: Replace Home and Recipe list effect state with `useRecipes`**

Use `query.data`, `query.isPending`, and `query.error` to render the existing cards, skeleton, and error message. Keep the current pagination behavior. Do not keep a second copy of query results in `useState`.

- [ ] **Step 5: Run the query cache test and build**

Run: `npx playwright test e2e/query-cache.spec.ts --grep "Recipe list cache reuse"` and `npm run build`.

Expected: the mock reports one identical fresh Recipe list request and the production build succeeds.

---

### Task 2: Migrate Search and Category public data

**Files:**
- Create: `frontend/src/lib/queries/categories.ts`
- Modify: `frontend/src/app/search/page.tsx`
- Modify: `frontend/src/app/categories/page.tsx`
- Modify: `frontend/src/app/categories/[slug]/page.tsx`
- Modify: `frontend/src/lib/recipes-api.ts`
- Modify: `frontend/e2e/support/mock-recipe-api.mjs`
- Modify: `frontend/e2e/query-cache.spec.ts`

**Interfaces:**
- `useCategories()` returns public categories with query key `queryKeys.categories.list()`.
- `useSearchRecipes(params: URLSearchParams)` uses a sorted canonical parameter string in the query key, including page, pageSize, and every active filter.
- `useCategory(slug)` and `useCategoryRecipes(slug, page)` use separate category-detail and category-recipe list keys.
- The mock server returns deterministic results for `GET /api/v1/recipes/search` and records its full query string.

- [ ] **Step 1: Add failing query-param isolation tests**

Add a test named `"search query keys isolate filters"` to `query-cache.spec.ts`. Set text, category, difficulty, max cook time, minimum servings, sort order, and page through the existing Search controls; inspect the mock server's `GET /__test__/requests` response and assert the resulting `/api/v1/recipes/search` record contains the corresponding `q`, `categoryId`, `difficulty`, `maxCookTime`, `minServings`, `sortBy`, `sortOrder`, and `page` parameters. Reopen the same URL with client navigation and assert no duplicate search request is made while the result is fresh.

- [ ] **Step 2: Run the Search/Category tests before migration**

Run: `npx playwright test e2e/query-cache.spec.ts --grep "search query keys"`.

Expected: FAIL because the current effect-driven pages do not use Query cache keys and reuse.

- [ ] **Step 3: Add public query hooks and preserve URL-driven state**

Keep the existing URL parsing and filter controls. Build query keys from the same normalized values used for the API request. Pass the query signal to `getRecipeCategories`, `searchRecipes`, and `getCategory`.

- [ ] **Step 4: Replace effect-managed network state in Search and Category pages**

Replace duplicate `loading`, `error`, and result states with query state. Keep local form/filter state and page navigation. Render the existing empty, error, and loading messages.

- [ ] **Step 5: Run public browse tests**

Run: `npx playwright test e2e/query-cache.spec.ts e2e/srs-routes.spec.ts`.

Expected: query parameter changes request the matching result, category detail combines the selected category with its recipe query, and current routes render correctly.

---

### Task 3: Seed TanStack queries from Recipe and Author SSR

**Files:**
- Create: `frontend/src/lib/queries/authors.ts`
- Modify: `frontend/src/app/recipes/[slug]/page.tsx`
- Modify: `frontend/src/app/recipes/[slug]/recipe-detail-client.tsx`
- Modify: `frontend/src/app/authors/[id]/page.tsx`
- Modify: `frontend/src/app/authors/[id]/author-profile-client.tsx`
- Modify: `frontend/src/lib/recipes-api.ts`
- Modify: `frontend/e2e/recipe-metadata.spec.ts`
- Modify: `frontend/e2e/recipe-json-ld.spec.ts`
- Modify: `frontend/e2e/author-profile.spec.ts`

**Interfaces:**
- `useRecipe(slug, initialData)` uses `queryKeys.recipes.detail(slug)` and seeds from the existing server result.
- `useAuthorProfile(id, page, initialData)` uses an author key containing both ID and page.
- `getAuthorProfile(id: string, page: number, signal: AbortSignal): Promise<AuthorProfilePage>` is the public client helper used by the Author query.
- `generateMetadata`, Recipe JSON-LD generation, and server-side 404 handling remain in the Server Component.

- [ ] **Step 1: Add regression assertions for SSR output**

Assert Recipe `<title>`, Recipe JSON-LD, and Author name/recipe cards are present in the initial response HTML without requiring a browser-side API request.

- [ ] **Step 2: Run SEO/SSR tests before adding Query**

Run: `npx playwright test e2e/recipe-metadata.spec.ts e2e/recipe-json-ld.spec.ts e2e/author-profile.spec.ts`.

Expected: existing SSR assertions pass; record that output as the baseline for this migration.

- [ ] **Step 3: Add Recipe and Author query hooks**

Implement query functions that call public API helpers and accept AbortSignals. In the client components, use `initialData` from the server-provided props and the corresponding stable query key. Set the same explicit 30-second stale time to avoid an immediate duplicate fetch.

- [ ] **Step 4: Preserve server-rendered metadata and content**

Keep `generateMetadata`, `buildRecipeJsonLd`, `getPublicRecipe`, and `getPublicAuthorProfile` server-side. Feed the exact server result into the client query; do not move metadata, JSON-LD, or error/404 decisions into `useEffect`.

- [ ] **Step 5: Re-run SSR/SEO and Recipe detail tests**

Run the three Playwright specs above and `npx playwright test e2e/recipe-thumbnails.spec.ts`.

Expected: Recipe title/metadata/JSON-LD and Author content remain in initial HTML and all existing detail behavior remains unchanged.

---

### Task 4: Migrate Dashboard Recipe/Category and Recipe Editor server state

**Files:**
- Modify: `frontend/src/lib/queries/recipes.ts`
- Modify: `frontend/src/lib/queries/categories.ts`
- Create: `frontend/src/lib/queries/recipe-mutations.ts`
- Create: `frontend/src/lib/queries/category-mutations.ts`
- Modify: `frontend/src/lib/recipes-api.ts`
- Modify: `frontend/src/app/dashboard/recipes/page.tsx`
- Modify: `frontend/src/app/dashboard/categories/page.tsx`
- Modify: `frontend/src/app/dashboard/recipe-editor.tsx`
- Modify: `frontend/e2e/query-cache.spec.ts`
- Modify: `frontend/e2e/recipe-crud-flow.spec.ts`
- Modify: `frontend/e2e/category-images.spec.ts`

**Interfaces:**
- Private query/mutation functions call the Auth.js BFF; they accept no token argument.
- Private edit reads use `getRecipeForEdit(id: string, signal: AbortSignal): Promise<RecipeDetail>`; public detail reads continue using `getRecipe(slug, signal)`.
- `useRecipeForEdit(userId: string, id: string)` uses `queryKeys.dashboard.recipeDetail(userId, id)` so private drafts cannot collide with public slug keys.
- Dashboard list keys include authenticated `userId`, pagination, and filters.
- Recipe mutations invalidate `queryKeys.recipes.lists()`, `queryKeys.dashboard.recipeLists(userId)`, the affected private edit key, and the public slug detail key when the recipe is public; Category mutations invalidate Category list/detail and dependent Recipe/Search keys.

- [ ] **Step 1: Add failing mutation-invalidation and user-isolation E2E tests**

Add a test named `"dashboard mutation invalidation and user isolation"`. Sign in as `chef@example.com`, publish a Draft, and assert the Dashboard list changes status after the mutation. Then sign out and sign in as `second-chef@example.com`; make the mock API return a distinct second-user row based on the test access-token identity and assert the first user's Dashboard row is not shown from cache.

- [ ] **Step 2: Run tests to confirm old manual reloads/user-independent state do not satisfy them**

Run: `npx playwright test e2e/query-cache.spec.ts --grep "dashboard mutation invalidation and user isolation"`.

Expected: the new mutation/cache assertions fail before Query mutations and user-scoped keys are implemented.

- [ ] **Step 3: Add Dashboard queries and mutation hooks**

Add `useMyRecipes(userId: string, page: number)`, `useDashboardCategories(userId: string)`, `useCreateRecipe`, `useUpdateRecipe`, `useDeleteRecipe`, `usePublishRecipe`, `useArchiveRecipe`, `useUploadRecipeImage`, `useSetPrimaryRecipeImage`, `useDeleteRecipeImage`, and Category create/update/delete/image mutations. Ingredients, steps, and nutrition remain part of the existing Recipe create/update payload instead of gaining new API operations. Query keys use the user ID; mutation `onSuccess` invalidates public Recipe lists, the relevant Dashboard list, the affected Recipe detail, Category keys, and dependent Search keys as applicable.

- [ ] **Step 4: Migrate Dashboard pages and Recipe Editor**

Replace list `useEffect` loads with queries and imperative refetches with mutation hooks plus invalidation. In Recipe Editor, query categories and the existing edit recipe; keep RHF/Zod fields, image File/preview state, nutrition, steps, retryable upload flow, and RowVersion conflict UX local and unchanged.

- [ ] **Step 5: Verify Recipe and Category behavior**

Run: `npx playwright test e2e/recipe-crud-flow.spec.ts e2e/category-images.spec.ts e2e/query-cache.spec.ts`.

Expected: CRUD, publish/delete, upload retry, optimistic-concurrency messaging, and query invalidation pass, including after switching accounts.

---

### Task 5: Migrate Profile/header and public auth form requests

**Files:**
- Create: `frontend/src/lib/queries/profile.ts`
- Modify: `frontend/src/lib/queries/query-keys.ts`
- Modify: `frontend/src/lib/queries/query-client.ts`
- Modify: `frontend/src/app/profile/page.tsx`
- Modify: `frontend/src/app/site-ui.tsx`
- Modify: `frontend/src/app/auth/register/page.tsx`
- Modify: `frontend/src/app/auth/forgot-password/page.tsx`
- Modify: `frontend/src/app/auth/reset-password/page.tsx`
- Modify: `frontend/src/lib/auth-api.ts`
- Modify: `frontend/e2e/auth-profile.spec.ts`
- Modify: `frontend/e2e/query-cache.spec.ts`

**Interfaces:**
- `useProfile(userId)` reads Profile through the BFF and is shared by Profile and header.
- Profile update mutation updates/invalidate the Profile query key.
- Change-password uses a mutation but keeps passwords in local form state.
- Auth.js owns sign-in/sign-out; public register/forgot/reset operations may use `useMutation` for pending/error/success state.

- [ ] **Step 1: Add failing profile cache/logout cleanup tests**

Add a test named `"profile cache updates and clears on logout"` that updates the display name and observes the same name in the header, then signs out, signs in as a second user, and asserts the first user's Profile data is absent.

- [ ] **Step 2: Implement shared Profile query and mutation hooks**

Use a user-scoped Profile key. On successful update, place the response in that key or invalidate it; use that same key from the header. In the provider, compare the current user ID with a ref holding the previously authenticated user ID; when an established identity changes or signs out, call `queryClient.cancelQueries()` and `queryClient.clear()` before private data for another account can render. Do not clear the cache for the initial `null`-to-user transition.

- [ ] **Step 3: Replace Profile effects with query/mutation state**

Keep edit-mode values, password fields, confirmation dialogs, and toast UI local. Remove refresh-token retry logic from the Profile page; Auth.js owns refresh. Use mutation errors from the existing Problem Details parser and preserve current Vietnamese messages.

- [ ] **Step 4: Use mutation state for public register/forgot/reset**

Replace manual submit pending/error/success toggles only where `useMutation` directly models the request. Keep password visibility, reset-token parsing, and form state local. These public endpoints remain direct unauthenticated API calls.

- [ ] **Step 5: Run Profile/auth and cache cleanup tests**

Run: `npx playwright test e2e/auth-profile.spec.ts e2e/auth-register.spec.ts e2e/auth-reset-password.spec.ts e2e/query-cache.spec.ts`.

Expected: Profile updates render consistently in the header, password change and logout work, public auth forms retain their behavior, and private data does not cross user sessions.

---

### Task 6: Document Query boundaries and complete validation

**Files:**
- Modify: `docs/recipe-core-implementation-spec.md`

- [ ] **Step 1: Document the Query architecture**

Record QueryClient defaults, query-key domains, pages using queries, SSR-seeded Recipe/Author data, mutation invalidation rules, and the rule that local forms/files remain outside Query. Keep the Auth.js/BFF explanation from the prerequisite plan.

- [ ] **Step 2: Search for duplicated client server-state loaders**

Run from repository root:

```powershell
rg "useEffect|setLoading|setError|setResult|setCategories" frontend\src\app\page.tsx frontend\src\app\recipes frontend\src\app\search frontend\src\app\categories frontend\src\app\dashboard frontend\src\app\profile frontend\src\app\authors
```

Inspect each remaining match and retain only effects/state needed for local UI, browser URL synchronization, or resource cleanup; remove duplicated API request state.

- [ ] **Step 3: Run frontend type-check/build/lint and all Playwright tests**

Run from `frontend`: `npx tsc --noEmit`, `npm run build`, `npm run lint`, and `npx playwright test`.

Expected: type-check, build, and E2E suite exit successfully. Report pre-existing lint warnings separately without unrelated cleanup.

- [ ] **Step 4: Verify the approved spec point by point**

Confirm public page data, Dashboard/Profile data, mutations, AbortSignals, 4xx retry policy, user-scoped keys, logout cache clearing, Recipe metadata/JSON-LD, and Author SSR all have corresponding passing tests or direct implementation evidence. List any unmet item instead of marking the migration complete.

---
