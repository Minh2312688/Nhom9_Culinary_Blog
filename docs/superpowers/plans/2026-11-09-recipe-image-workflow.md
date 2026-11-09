# Recipe Image Workflow Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Let authors choose a recipe image during creation and display the primary and secondary images in the requested order.

**Architecture:** Stage a selected file until recipe creation returns an ID, then reuse the existing recipe image upload endpoint. Retain the created ID and file on upload failure so retry cannot duplicate the recipe; use the existing `isPrimary` data to render one hero image and a later secondary gallery.

**Tech Stack:** Next.js 15, React 19, TypeScript, existing ASP.NET Core recipe image API, Playwright.

**Spec:** [2026-11-09-recipe-category-media-and-nutrition-design.md](../specs/2026-11-09-recipe-category-media-and-nutrition-design.md)

## Global Constraints

- Reuse the current recipe image upload endpoint and MinIO storage path.
- The create form stages the selected file and sends it after recipe creation.
- Keep the selected file and report partial completion if recipe creation succeeded but image upload failed; provide a retry path without creating a duplicate recipe.
- Preserve existing image count, file size, primary image, alt-text, and authorization rules.
- On the recipe detail page, show the primary image prominently near the title before the recipe information. Show secondary images after the nutrition section rather than before the recipe content.
- Do not remove PostgreSQL, Redis, MinIO, or Seq volumes during container rebuild/recreation.

---

### Task 1: Stage and upload the creation image with retry

**Files:**
- Modify: `frontend/src/app/dashboard/recipe-editor.tsx`
- Modify: `frontend/src/lib/recipes-api.ts` only if a typed upload signature needs adjustment
- Test: `frontend/e2e/recipe-crud-flow.spec.ts`
- Modify: `frontend/e2e/support/mock-recipe-api.mjs`

**Interfaces:**
- Recipe editor state owns `pendingImageFile: File | null`, `createdRecipeId: string | null`, image alt text, and primary selection.
- Existing `uploadRecipeImage(token, recipeId, file, isPrimary, altText)` remains the single upload API.

- [ ] **Step 1: Add failing create-with-image and retry tests**

In the recipe creation E2E flow, select a file before submit, mock recipe creation to return a stable ID, and assert one multipart upload with `isPrimary=true`. Add a failure case: first upload returns an error, the form explains that the recipe was saved, retry makes a second upload request but does not issue another recipe `POST`.

```ts
await page.locator("#imageFile").setInputFiles({
  name: "main.webp",
  mimeType: "image/webp",
  buffer: Buffer.from("sample-image"),
});
await page.getByRole("button", { name: "Lưu bản nháp" }).click();
await expect(page.getByText(/đã lưu.*ảnh chưa tải lên/i)).toBeVisible();
```

- [ ] **Step 2: Run the relevant E2E cases and verify failure**

Run from `frontend`: `.\node_modules\.bin\playwright.cmd test e2e/recipe-crud-flow.spec.ts --grep "image|retry"`

Expected: the new create-time selection/retry assertions fail before implementation.

- [ ] **Step 3: Add file staging to the create form**

Expose the existing image file picker when creating or editing. In create mode, only store the selected `File`, alt text, and primary choice; in edit mode, retain immediate upload. Validate the existing 5 MB limit before submit and surface invalid file errors in the established form feedback.

- [ ] **Step 4: Make creation and upload a retryable sequence**

After create succeeds, store `response.id` in `createdRecipeId`; upload the staged file using that ID. Do not call `createRecipe` again when `createdRecipeId` is set. On upload success clear the staged file and navigate to the recipe list; on upload failure keep both values and show an explicit retry action.

```ts
if (createdRecipeId) {
  await uploadPendingImage(createdRecipeId);
  return;
}
const created = await createRecipe(token, payload);
setCreatedRecipeId(created.id);
if (pendingImageFile) await uploadPendingImage(created.id);
```

- [ ] **Step 5: Run creation and existing image gallery tests**

Run:
- `.\node_modules\.bin\playwright.cmd test e2e/recipe-crud-flow.spec.ts --grep "Recipe Creation|Image Gallery|image|retry"`
- `npm run build`

Expected: both failure/retry and existing image management paths pass.

### Task 2: Put primary image first and secondary images after nutrition

**Files:**
- Modify: `frontend/src/app/recipes/[slug]/recipe-detail-client.tsx`
- Modify: `frontend/src/app/site-ui.module.css`
- Test: `frontend/e2e/recipe-crud-flow.spec.ts`
- Modify: `frontend/e2e/support/mock-recipe-api.mjs`

**Interfaces:**
- No API changes. `RecipeDetail.images` already contains `isPrimary`, URLs, alt text, and order index.

- [ ] **Step 1: Add a detail-ordering regression assertion**

Give the mock recipe two images, one primary and one secondary. Assert the primary image appears before the recipe metadata/content and the secondary gallery image appears after the nutrition section.

```ts
const primary = page.locator('[data-testid="recipe-primary-image"]');
const nutrition = page.locator('[data-testid="recipe-nutrition"]');
const gallery = page.locator('[data-testid="recipe-secondary-images"]');
expect(await primary.evaluate((node) => node.compareDocumentPosition(
  document.querySelector('[data-testid="recipe-nutrition"]')!,
) & Node.DOCUMENT_POSITION_FOLLOWING)).toBeTruthy();
expect(await nutrition.evaluate((node) => node.compareDocumentPosition(
  document.querySelector('[data-testid="recipe-secondary-images"]')!,
) & Node.DOCUMENT_POSITION_FOLLOWING)).toBeTruthy();
```

- [ ] **Step 2: Run the focused detail test and verify failure**

Run from `frontend`: `.\node_modules\.bin\playwright.cmd test e2e/recipe-crud-flow.spec.ts --grep "image ordering"`

Expected: current page places all recipe images before nutrition, so the ordering assertion fails.

- [ ] **Step 3: Render primary and secondary groups separately**

Sort images as today, find the `isPrimary` image, render it near the title, and render remaining images in a secondary gallery after the nutrition section. Preserve the existing URL preference (`mediumUrl ?? thumbnailUrl ?? originalUrl`) and alt fallback.

```tsx
const primaryImage = images.find((image) => image.isPrimary) ?? images[0];
const secondaryImages = images.filter((image) => image.id !== primaryImage?.id);
```

Use stable `data-testid` values for the hero and secondary gallery, and add responsive sizing for the hero image.

- [ ] **Step 4: Verify image ordering and detail regressions**

Run:
- `.\node_modules\.bin\playwright.cmd test e2e/recipe-crud-flow.spec.ts --grep "image ordering|Recipe Detail|Image Gallery"`
- `npm run build`

Expected: primary image appears first, secondary images appear after nutrition, and the detail page still renders recipes with no images.

## Integration and documentation

- Update `README.md` only if author-facing create/edit image behavior needs a new instruction.
- After all feature plans are complete and Docker is running, follow [the Docker rollout plan](2026-11-09-docker-rollout.md).
