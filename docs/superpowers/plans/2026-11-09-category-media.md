# Category Media Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Seed matching category images into MinIO, let administrators upload category images, and show them on public category cards.

**Architecture:** Match local seed files by exact category slug, upload them through `IFileStorageService`, and store URLs in the existing `Category.ImageUrl` field. Add an admin-only category image upload command/endpoint and reuse the category DTO for public UI display.

**Tech Stack:** .NET 10, EF Core, ASP.NET Core Minimal APIs, MinIO, Next.js 15, TypeScript, Playwright.

**Spec:** [2026-11-09-recipe-category-media-and-nutrition-design.md](../specs/2026-11-09-recipe-category-media-and-nutrition-design.md)

## Global Constraints

- Match a category asset by exact slug filename stem, allowing the existing supported image extensions (`.webp`, `.jpg`, `.jpeg`, `.png`).
- Seed existing categories only when `ImageUrl` is empty. Do not replace an image set by an administrator.
- A category without a matching asset remains without an image; the public UI displays its normal placeholder.
- Add an authenticated, admin-only multipart upload endpoint for category images.
- Invalidate category list/detail cache after a successful image update.
- Do not remove PostgreSQL, Redis, MinIO, or Seq volumes during container rebuild/recreation.

---

### Task 1: Load and seed category assets

**Files:**
- Create: `backend/src/CulinaryBlog.Infrastructure/Persistence/Seed/SampleCategoryImageLoader.cs`
- Test: `backend/tests/CulinaryBlog.Application.Tests/Infrastructure/Persistence/SampleCategoryImageLoaderTests.cs`
- Modify: `backend/src/CulinaryBlog.Infrastructure/Persistence/Seed/Lab02DataSeeder.cs`
- Test: `backend/tests/CulinaryBlog.Integration.Tests/Persistence/SampleRecipeSeederTests.cs`

**Interfaces:**
- Produces `SampleCategoryImageLoader.Load(string imagesDirectory)`, returning a case-insensitive map from exact slug filename stem to full file path.
- Uses the existing `StorageFolders.Categories`, `FileUploadRequest`, `IFileStorageService`, and `Category.ImageUrl`.

- [ ] **Step 1: Add focused loader tests**

Test a temporary `danhmuc` directory containing matching and unrelated files:

```csharp
File.WriteAllBytes(Path.Combine(directory, "mon-canh.jpg"), [1]);
File.WriteAllBytes(Path.Combine(directory, "mon-viet.webp"), [1]);
File.WriteAllBytes(Path.Combine(directory, "unrelated.txt"), [1]);

var result = SampleCategoryImageLoader.Load(directory);

Assert.Equal(Path.GetFullPath(Path.Combine(directory, "mon-canh.jpg")), result["mon-canh"]);
Assert.Equal(Path.GetFullPath(Path.Combine(directory, "mon-viet.webp")), result["mon-viet"]);
Assert.False(result.ContainsKey("unrelated"));
```

- [ ] **Step 2: Run the loader test and verify the missing behavior**

Run: `dotnet test backend\tests\CulinaryBlog.Application.Tests\CulinaryBlog.Application.Tests.csproj --filter FullyQualifiedName~SampleCategoryImageLoaderTests`

Expected: the test initially fails because the loader does not yet exist.

- [ ] **Step 3: Implement safe, deterministic asset lookup**

Enumerate only files directly under `images/danhmuc`; index supported extensions by `Path.GetFileNameWithoutExtension`; reject duplicate slug stems with an `InvalidDataException` rather than selecting an arbitrary asset. Return full paths only for recognized image extensions.

```csharp
private static readonly HashSet<string> SupportedExtensions =
    new(StringComparer.OrdinalIgnoreCase) { ".webp", ".jpg", ".jpeg", ".png" };
```

- [ ] **Step 4: Seed category URLs without overwriting existing values**

In `Lab02DataSeeder.SeedAsync`, resolve matching image files after category creation. For a category where `ImageUrl` is null/blank, upload through `storage.UploadAsync` with `StorageFolders.Categories` and the file's actual content type; set `ImageUrl` to the returned URL and persist. Do not upload if the category has no exact match or already has an image.

Extend the sample seeder regression test to assert that a matching category gets the mocked storage URL and a pre-populated category URL is unchanged. Keep image upload cleanup consistent with the existing recipe seed path.

- [ ] **Step 5: Run loader and seeder tests**

Run:
- `dotnet test backend\tests\CulinaryBlog.Application.Tests\CulinaryBlog.Application.Tests.csproj --filter FullyQualifiedName~SampleCategoryImageLoaderTests`
- `dotnet test backend\tests\CulinaryBlog.Integration.Tests\CulinaryBlog.Integration.Tests.csproj --filter FullyQualifiedName~SampleRecipeSeederTests`

Expected: loader tests pass; the PostgreSQL test passes when Docker is available or reports the repository's Docker-unavailable skip.

### Task 2: Add admin category image upload API

**Files:**
- Create: `backend/src/CulinaryBlog.Application/Features/Categories/Commands/UploadCategoryImage/UploadCategoryImageCommand.cs`
- Modify: `backend/src/CulinaryBlog.API/Endpoints/CategoryEndpoints.cs`
- Test: `backend/tests/CulinaryBlog.Application.Tests/Features/Categories/Commands/UploadCategoryImageCommandHandlerTests.cs`
- Test: `backend/tests/CulinaryBlog.Integration.Tests/Endpoints/CategoryMutationEndpointsTests.cs`

**Interfaces:**
- Produces `UploadCategoryImageCommand(Guid CategoryId, Stream Content, string FileName, string? ContentType, long Length) : IRequest<CategoryDto>`.
- Adds `POST /api/v1/categories/{id:guid}/image`, accepting multipart field `file`, requiring the existing Admin role.

- [ ] **Step 1: Write handler and endpoint regression tests**

Cover successful URL persistence/cache invalidation, missing category, storage failure, and denial for a non-admin. The success test must assert the returned `CategoryDto.ImageUrl` is the storage URL.

```csharp
Assert.Equal("https://media.example.test/categories/mon-canh.webp", result.ImageUrl);
cache.Verify(value => value.InvalidateAsync(It.IsAny<CancellationToken>()), Times.Once);
```

- [ ] **Step 2: Run focused tests and verify failure**

Run: `dotnet test backend\tests\CulinaryBlog.Application.Tests\CulinaryBlog.Application.Tests.csproj --filter FullyQualifiedName~UploadCategoryImageCommandHandlerTests`

Expected: the test fails because the command/handler has not been implemented.

- [ ] **Step 3: Implement upload, persistence, cleanup, and cache invalidation**

Validate category existence and file request through the existing file validation pattern; upload to `StorageFolders.Categories`; set and save `Category.ImageUrl`; invalidate category cache. If saving fails, delete the newly uploaded object and rethrow the original error, logging cleanup failures rather than hiding them.

```csharp
var uploaded = await storage.UploadAsync(uploadRequest, cancellationToken);
category.ImageUrl = uploaded.Url;
await context.SaveChangesAsync(cancellationToken);
await cache.InvalidateAsync(cancellationToken);
```

- [ ] **Step 4: Map the multipart endpoint**

Read `file` from `ReadFormAsync(ct)`, return a validation problem if the request is not multipart or the file is absent, dispatch the command, and annotate the endpoint with `.RequireAuthorization(policy => policy.RequireRole(AppRoles.Admin))`.

- [ ] **Step 5: Verify handler and endpoint tests**

Run both focused test filters. Expected: upload and authorization cases pass, and existing category mutation tests remain green.

### Task 3: Add category image controls and public cards

**Files:**
- Modify: `frontend/src/lib/recipes-api.ts`
- Modify: `frontend/src/app/dashboard/categories/page.tsx`
- Modify: `frontend/src/app/categories/page.tsx`
- Modify: `frontend/src/app/site-ui.module.css`
- Test: `frontend/e2e/category-images.spec.ts`
- Modify: `frontend/e2e/support/mock-recipe-api.mjs`

**Interfaces:**
- Add `uploadCategoryImage(token: string, categoryId: string, file: File): Promise<RecipeCategory>`.
- `RecipeCategory.imageUrl` remains optional/null; public pages use it when present.

- [ ] **Step 1: Add browser tests for category preview and upload**

Use the existing test auth setup. Verify public category card image and alt text, then verify the admin form previews a chosen file and submits multipart field `file` to `/api/v1/categories/{id}/image`.

```ts
await page.locator("#categoryImageFile").setInputFiles({
  name: "mon-canh.jpg",
  mimeType: "image/jpeg",
  buffer: Buffer.from("image"),
});
await expect(page.getByRole("img", { name: "Món canh" })).toBeVisible();
```

- [ ] **Step 2: Run the focused browser test and verify it fails**

Run from `frontend`: `.\node_modules\.bin\playwright.cmd test e2e/category-images.spec.ts`

Expected: missing upload/preview behavior causes the relevant assertions to fail.

- [ ] **Step 3: Implement typed multipart client and admin image form**

Use `FormData` and do not set `Content-Type` manually, so the browser supplies the multipart boundary:

```ts
const form = new FormData();
form.append("file", file);
return request(`/api/v1/categories/${categoryId}/image`, {
  method: "POST",
  headers: authHeaders(token),
  body: form,
});
```

Add image selection, preview, upload progress, and explicit error/success feedback to create/edit administration. On create, create the category first, then upload the selected image; on upload failure retain the created category ID and file for a retry without issuing a second create request.

- [ ] **Step 4: Render category images with a missing-image fallback**

Render `category.imageUrl` as the card's representative image; when absent, render a semantic placeholder using the category name as accessible context. Keep the existing card title and count.

- [ ] **Step 5: Run build and focused tests**

Run:
- `npm run build`
- `.\node_modules\.bin\playwright.cmd test e2e/category-images.spec.ts`

Expected: build and category image tests pass.

## Integration and documentation

- Update `README.md` and `docs/docker-guide.md` only where seed behavior or category image administration instructions changed.
- After all feature plans are complete and Docker is running, follow [the Docker rollout plan](2026-11-09-docker-rollout.md).
