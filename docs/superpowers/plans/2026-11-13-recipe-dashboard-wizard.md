# Recipe Dashboard Permissions and Editor Wizard Implementation Plan

> **Trạng thái hiện tại (17/11/2026):** Dashboard recipe wizard và Admin-only
> category access đã có trong frontend; dùng `FR-UI-001` và mã hiện tại làm
> bằng chứng. Recipe update/publish/archive và nested resource mutations là
> owner-only; Admin chỉ xem Published và có thể xóa Published recipe. Các E2E
> results trong plan là historical, không chạy lại trong lần cập nhật này.

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Restrict Category management to Admin users and convert Recipe create/edit into a validated four-step wizard with a final review screen.

**Architecture:** Keep backend authorization and current API contracts unchanged. Use Auth.js session roles to guard Category navigation and dashboard routes, and keep the Recipe editor on one route with one React Hook Form/component state so entered data, selected images, and existing save/concurrency behavior survive step navigation.

**Tech Stack:** Next.js 15 App Router, Auth.js v5, React Hook Form, Zod, Playwright, ASP.NET Core integration tests.

**Spec:** `docs/superpowers/specs/2026-11-13-recipe-dashboard-wizard-design.md`

## Global Constraints

- Use one route and one mounted form/state instance for the Recipe wizard.
- Category create, update, delete, and image upload remain Admin-only in the backend.
- Non-Admin direct navigation to `/dashboard/categories` redirects to `/dashboard`.
- The existing “my recipes” list remains scoped to the current account, including for Admin; do not add an Admin-wide Recipe list.
- Admin retains the existing backend ability to manage any Recipe; non-Admin Recipe mutations remain author-scoped.
- Recipe creation continues to create a Draft; editing preserves the existing status and submits the current `rowVersion`.
- A newly selected image is uploaded only after the Recipe is created; preserve the retry path for upload failure.
- Preserve the existing 409/concurrency reload flow and entered values on API errors.
- Do not change the database schema, API contracts, authorization policies, cache behavior, or dependencies.
- Work only in a non-main isolated branch/worktree during implementation; preserve pre-existing dirty changes and stage only files belonging to a plan task.

---

### Task 1: Aligning Dashboard Recipe Test Mocks With the Owner-Only Route

**Files:**
- Modify: `frontend/e2e/srs-routes.spec.ts`
- Modify: `frontend/e2e/ui-acceptance.spec.ts`

**Interfaces:**
- Consumes: `getMyRecipes()` sends dashboard list requests to `/recipes/mine`; the BFF forwards these to `/api/v1/recipes/mine`; the mock response queue matches exact method and path.
- Produces: Dashboard Recipe tests queue responses for `/api/v1/recipes/mine` and assert that exact recorded path. No application behavior changes.

- [ ] **Step 1: Record the failing baseline**

The pre-change baseline already reproduced three failures: pagination in `srs-routes.spec.ts`, and the Draft/pending states in `ui-acceptance.spec.ts`. The failing tests queue GET responses for `/api/v1/recipes`, while the dashboard calls `/api/v1/recipes/mine`.

- [ ] **Step 2: Correct the exact mock paths**

Change only the dashboard-list mock configurations in those tests from:

```ts
path: "/api/v1/recipes",
```

to:

```ts
path: "/api/v1/recipes/mine",
```

Change the recorded backend request expectation in the dashboard pagination assertion to `/api/v1/recipes/mine`. Keep public Recipe-list mocks at `/api/v1/recipes` unchanged.

- [ ] **Step 3: Confirm the remaining pagination timeout**

From `frontend`, run:

```powershell
npx playwright test e2e/srs-routes.spec.ts e2e/ui-acceptance.spec.ts -g "recipe and category listings paginate|keeps a recipe draft unchanged|shows accessible loading feedback"
```

Expected: the Draft and loading cases pass. If pagination still exceeds its existing 30-second test timeout, capture the BFF response and mock request to confirm `/api/backend/recipes/mine` returns 200 and the mock records `/api/v1/recipes/mine`; do not change production code to address a test timeout.

- [ ] **Step 4: Give the measured multi-route pagination test a specific timeout**

The pagination case visits and paginates public Recipe and Category pages before testing the authenticated dashboard list. The observed case exceeds 30 seconds but passes when given a 90-second test budget. Set a test-specific timeout only for this test:

```ts
test("recipe and category listings paginate through the API", async ({ page }) => {
  test.setTimeout(90_000);
  // Keep the existing test body and its API/request assertions.
});
```

Do not change Playwright's global timeout or the behavior of the application.

- [ ] **Step 5: Verify all three baseline tests pass**

From `frontend`, run:

```powershell
npx playwright test e2e/srs-routes.spec.ts e2e/ui-acceptance.spec.ts -g "recipe and category listings paginate|keeps a recipe draft unchanged|shows accessible loading feedback"
```

Expected: all three tests pass with the pagination test's justified, test-local budget.

### Task 2: Enforcing Admin-Only Category Dashboard Access

**Files:**
- Modify: `frontend/e2e/support/mock-recipe-api.mjs`
- Modify: `frontend/e2e/category-images.spec.ts`
- Modify: `frontend/e2e/srs-routes.spec.ts`
- Modify: `frontend/e2e/ui-acceptance.spec.ts`
- Modify: `frontend/src/middleware.ts`
- Modify: `frontend/src/app/dashboard/dashboard-shell.tsx`
- Modify: `frontend/src/app/dashboard/page.tsx`

**Interfaces:**
- Consumes: Auth.js `session.user.roles: string[]`, already exposed to the UI.
- Produces: An `admin@example.com` test account with role `Admin`; Category dashboard access requires that role. Non-Admin users are redirected to `/dashboard`.

- [ ] **Step 1: Add a dedicated Admin account to the Playwright mock**

Add a test identity in `frontend/e2e/support/mock-recipe-api.mjs` without changing the existing Author or Member fixtures:

```js
createAuthAccount("admin@example.com", {
  suffix: "admin",
  id: "user-admin",
  displayName: "Admin Example",
  roles: ["Admin"],
});
```

- [ ] **Step 2: Write failing Category role-gating tests**

In `frontend/e2e/category-images.spec.ts`, add an Author test that signs in with the existing default `chef@example.com`, verifies the navigation and overview card do not expose Category management, and verifies direct navigation returns to `/dashboard`:

```ts
test("Author cannot access Category management", async ({ page }) => {
  await signInAs(page);
  await page.goto("/dashboard");
  await expect(page.getByRole("link", { name: "Danh mục" })).toHaveCount(0);
  await page.goto("/dashboard/categories");
  await expect(page).toHaveURL(/\/dashboard$/);
});
```

Add an Admin counterpart that signs in with `admin@example.com`, opens `/dashboard/categories`, and sees the Category heading and navigation link. Change the existing Category create/edit/delete/image tests to sign in as Admin because those operations are Admin-only.

- [ ] **Step 3: Run the focused tests and verify the expected failure**

From `frontend`, run:

```powershell
npx playwright test e2e/category-images.spec.ts
```

Expected: the Author visibility/redirect assertions fail because Category is currently visible and the route is not role-gated. Admin sign-in should succeed with the new fixture.

- [ ] **Step 4: Add the Category role check to middleware**

Keep the current anonymous redirect behavior. Insert this role check after its redirect branch and before returning `NextResponse.next()`:

```ts
if (
  request.nextUrl.pathname.startsWith("/dashboard/categories") &&
  !request.auth?.user?.roles?.includes("Admin")
) {
  const dashboardUrl = request.nextUrl.clone();
  dashboardUrl.pathname = "/dashboard";
  dashboardUrl.search = "";
  return NextResponse.redirect(dashboardUrl);
}
return NextResponse.next();
```

Retain the existing matcher for all dashboard and Profile routes.

- [ ] **Step 5: Hide Category navigation and overview entry for non-Admins**

Use the existing Auth.js `SessionProvider`/`useSession` pattern in both client components. Filter the Category item from `dashboard-shell.tsx` unless `session?.user.roles.includes("Admin")`. Apply the same role check to the Category overview card in `dashboard/page.tsx`; leave Recipe links visible to all authenticated dashboard users.

- [ ] **Step 6: Update existing route and acceptance tests for the role boundary**

Ensure tests that intentionally open Category management use `admin@example.com`, including the `/dashboard/categories` route assertions in `srs-routes.spec.ts` and Category CRUD assertions in `ui-acceptance.spec.ts`. Where a test is already signed in as Author, use the existing `signInAsByApi(page, "admin@example.com")` helper to switch accounts for the Category-only assertion. Keep Author-based Recipe dashboard tests unchanged.

- [ ] **Step 7: Run focused frontend tests and type-check**

From `frontend`, run:

```powershell
npx playwright test e2e/category-images.spec.ts e2e/srs-routes.spec.ts e2e/ui-acceptance.spec.ts
npx tsc --noEmit
```

Expected: Admin can access and use Category management; Author cannot see its entry or open its route; all focused tests and type-check pass.

- [ ] **Step 8: Commit the Category access task**

```powershell
git add frontend/e2e/support/mock-recipe-api.mjs frontend/e2e/category-images.spec.ts frontend/e2e/srs-routes.spec.ts frontend/e2e/ui-acceptance.spec.ts frontend/src/middleware.ts frontend/src/app/dashboard/dashboard-shell.tsx frontend/src/app/dashboard/page.tsx
git commit -m "feat: restrict category dashboard to admins"
```

### Task 3: Implementing the Recipe Wizard and Review Save Flow

**Files:**
- Modify: `frontend/e2e/recipe-crud-flow.spec.ts`
- Modify: `frontend/e2e/ui-acceptance.spec.ts`
- Modify: `frontend/src/app/dashboard/recipe-editor.tsx`

**Interfaces:**
- Consumes: The editor's existing React Hook Form schema, `useFieldArray`, component state for nutrition/structured steps/images, and existing save handlers.
- Produces: Four sequential steps—general information, ingredients, cooking steps, and review—with Previous/Next navigation, step-aware validation, and the existing create/update API behavior.

- [ ] **Step 1: Write failing tests for the complete wizard flow**

Add focused Playwright cases in `frontend/e2e/recipe-crud-flow.spec.ts` before changing the editor:

1. **Step validation and retained values:** invalid title blocks Next; a blank ingredient row blocks Next; valid values advance; Previous returns with ingredient values intact; an added step with no description blocks transition to Review.
2. **Review and creation:** enter title/category, ingredient, ordered steps, nutrition, and an image; verify Review displays these values and the selected image preview; submit once and assert exactly one create request with typed ingredient values, ordered steps, nutrition, and no status override.
3. **Image retry:** queue a failed upload response, create through Review, retry the image upload, and assert there was only one Recipe create request.
4. **Edit and concurrency:** open an existing Recipe for edit, move through the wizard, update from Review, and assert the PUT body includes its existing `rowVersion`; queue a 409 and verify the editor retains the entered title and exposes the existing reload action.

- [ ] **Step 2: Run the tests and verify they fail for the missing wizard**

From `frontend`, run the new cases by their descriptive titles:

```powershell
npx playwright test e2e/recipe-crud-flow.spec.ts -g "wizard"
```

Expected: FAIL because the current editor has no Next/Previous or Review panels. Confirm the failure is caused by the absent wizard, not by mock authentication or a locator error.

- [ ] **Step 3: Add wizard state and step-specific form validation**

In `recipe-editor.tsx`, define the four step identifiers and use component state for the active step. Destructure `trigger` and `setError` from `useForm`, and define field groups that match `RecipeEditorFormValues`:

```ts
const generalFields = [
  "title", "description", "categoryId", "prepTime", "cookTime", "servings", "difficulty",
] as const;
const ingredientFields = ["ingredients"] as const;
```

Use `trigger(generalFields, { shouldFocus: true })` before advancing from General Information and `trigger(ingredientFields, { shouldFocus: true })` before advancing from Ingredients. Keep the existing `useForm`, resolver, image state, nutrition state, structured step state, recipe state, and RowVersion handling mounted for the whole wizard.

- [ ] **Step 4: Group existing fields into the three input panels**

Move the existing JSX into conditional panels without deleting or changing field names/IDs used by API submission:

1. General Information: title, description, category, difficulty, time, servings, nutrition, and selected-image input/preview.
2. Ingredients: existing `useFieldArray` list and add/remove controls.
3. Cooking Steps: existing structured-step controls and text fallback.

Keep image selection in component state so the `File` and preview URL remain intact when changing steps. Do not mount/unmount the form or move the selected file into browser storage.

- [ ] **Step 5: Add cooking-step validation matching the backend**

Before leaving Cooking Steps, validate each structured step's description when a step row exists; backend create/update validators require every submitted step description to be non-empty. For text fallback, validate the non-empty lines produced by the existing parser. Do not require the user to add at least one ingredient or step, because backend validators allow empty collections. On invalid input, show an accessible error and focus the first invalid description.

- [ ] **Step 6: Add accessible navigation and step state**

Render Previous on steps 2–4 and Next on steps 1–2; step 3 advances with a Review action. All navigation buttons use `type="button"` and Previous does not validate. Identify the active step for assistive technology and put the controls after the current panel content so they remain at the bottom.

- [ ] **Step 7: Implement Review and submit only from the final step**

Render a read-only summary from `watch()`, `structuredSteps`, `includeNutrition`, `nutrition`, and image state. Show the selected/current image preview and the ordered Recipe Steps. The Review panel owns the final action, retaining the existing labels `Tạo bản nháp` and `Cập nhật`. Keep Next/Previous controls out of form submission.

- [ ] **Step 8: Preserve create, edit, image, and conflict semantics**

Keep existing payload construction and API methods intact. New Recipe creation must continue using the backend's default Draft status. Update must continue sending `recipe.rowVersion` without changing status. Pending image upload occurs only after successful creation; retry uploads to `createdRecipeId` without creating another Recipe. A 409 still displays the existing conflict recovery/reload action without clearing entered values. When backend field errors identify a known field, activate its panel: general fields to step 1, ingredients to step 2, and steps to step 3.

- [ ] **Step 9: Update existing Recipe E2E tests to navigate between steps**

In `recipe-crud-flow.spec.ts` and `ui-acceptance.spec.ts`, move existing ingredient, structured-step, nutrition, image-selection, validation, and submit actions to their corresponding steps. Advance through Review before final submission. Keep tests for Recipe detail rendering and direct image management unchanged unless they interact with hidden wizard panels.

- [ ] **Step 10: Run focused frontend tests and type-check**

From `frontend`, run:

```powershell
npx playwright test e2e/recipe-crud-flow.spec.ts e2e/ui-acceptance.spec.ts
npx tsc --noEmit
```

Expected: PASS for step navigation, validation, Review, Draft creation, update `rowVersion`, image upload/retry, structured steps, nutrition, and conflict behavior.

- [ ] **Step 11: Run backend authorization regression tests**

From the repository root, run:

```powershell
dotnet test backend\tests\CulinaryBlog.Integration.Tests\CulinaryBlog.Integration.Tests.csproj --filter "FullyQualifiedName~CategoryMutationEndpointsTests"
```

Expected: existing Admin-only Category mutation tests pass without backend policy changes.

- [ ] **Step 12: Run the complete affected E2E set and whitespace check**

From `frontend`, run:

```powershell
npx playwright test e2e/category-images.spec.ts e2e/recipe-crud-flow.spec.ts e2e/ui-acceptance.spec.ts e2e/srs-routes.spec.ts
```

From the repository root, run:

```powershell
git diff --check
```

Expected: all affected browser tests pass and `git diff --check` reports no whitespace errors.

- [ ] **Step 13: Commit the Recipe wizard task**

```powershell
git add frontend/e2e/recipe-crud-flow.spec.ts frontend/e2e/ui-acceptance.spec.ts frontend/src/app/dashboard/recipe-editor.tsx
git commit -m "feat: add recipe editor wizard and review"
```
