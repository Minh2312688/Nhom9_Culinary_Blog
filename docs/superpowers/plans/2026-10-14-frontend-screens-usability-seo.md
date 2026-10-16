# Culinary Blog Author Profiles, UI and Recipe SEO Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build public author profiles and real recipe publication dates, expose them safely through the API and Recipe JSON-LD, and refresh the whole frontend brand while preserving existing recipe/auth behavior.

**Architecture:** Extend `Recipe` with a nullable UTC publication timestamp and add a paginated, public-only author query using existing Identity profile data. Keep the existing Next.js App Router, CSS Modules, Tailwind and Lucide; render recipe SEO from server-fetched API data and apply a shared Culinary Blog design system with small reduced-motion-aware Motion effects.

**Tech Stack:** .NET 10, EF Core 10, PostgreSQL, ASP.NET Core Minimal APIs, MediatR, Next.js 15, React 19, TypeScript, CSS Modules, Tailwind CSS, Motion, Playwright, xUnit.

**Spec:** `docs/superpowers/specs/2026-10-14-frontend-screens-usability-seo-design.md`

## Global Constraints

- Mobile 320–767 px one column; tablet 768–1199 px two columns; desktop from 1200 px; no horizontal overflow.
- Keep HTTP 400 Problem Details and existing write payloads, authorization, validation, row-version concurrency, recipe-state behavior and navigation.
- Public author responses contain only `id`, `displayName`, `avatarUrl`, `bio` and paged Published recipes; never expose email, roles, Draft or Archived recipes.
- `PublishedAt` is UTC and nullable. Do not infer historical publication times from `CreatedAt`.
- Archive preserves `PublishedAt`; a successful transition from Draft/Archived to Published sets the current UTC time. Repeating publish while already Published remains idempotent and does not move the date.
- JSON-LD omits author/date/nutrition properties when their source data is absent; never create ratings or placeholder values.
- Brand is **Culinary Blog** with vivid coral/orange, turmeric yellow, deep teal and cream; keep CSS Modules, Tailwind and `lucide-react`.
- Motion is limited to subtle UI transitions and must respect reduced-motion preferences.
- Add no feature beyond public author profiles and publication timestamps; do not change existing recipe write contracts.
- Commit each completed task using a scoped `git add` and a conventional commit message with the required Copilot co-author trailer.
- Preserve unrelated working-tree changes; do not stage or edit the existing unrelated `README.md` change.

---

### Task 1: Persist publication timestamps

**Files:**
- Modify: `backend/src/CulinaryBlog.Domain/Entities/Recipe.cs`
- Modify: `backend/src/CulinaryBlog.Infrastructure/Persistence/Configurations/RecipeConfiguration.cs`
- Modify: `backend/src/CulinaryBlog.Application/Features/Recipes/Commands/RecipeCommands.cs`
- Modify: `backend/src/CulinaryBlog.Infrastructure/Persistence/Migrations/ApplicationDbContextModelSnapshot.cs`
- Create: `backend/src/CulinaryBlog.Infrastructure/Persistence/Migrations/*_AddRecipePublishedAt.cs` and generated designer
- Test: `backend/tests/CulinaryBlog.Application.Tests/Domain/Entities/RecipePublishTests.cs`
- Create: `backend/tests/CulinaryBlog.Application.Tests/Features/Recipes/RecipeLifecycleTests.cs`

**Interfaces:**
- `Recipe.PublishedAt: DateTimeOffset?` represents the most recent successful transition to Published.
- `Recipe.Publish(DateTimeOffset publishedAt)` validates required ingredient/step collections, then sets `Status = Published` and `PublishedAt = publishedAt`.
- Archiving changes only `Status`; it does not clear the timestamp.

```csharp
public DateTimeOffset? PublishedAt { get; set; }

public void Publish(DateTimeOffset publishedAt)
{
    if (Ingredients.Count == 0 || Steps.Count == 0)
        throw new DomainRuleViolationException(
            "A recipe must have at least one step and one ingredient before publishing.");
    if (Status != RecipeStatus.Published)
        PublishedAt = publishedAt.ToUniversalTime();
    Status = RecipeStatus.Published;
}
```

- [x] **Step 1: Add deterministic domain tests**
  - Extend `RecipePublishTests.cs` with cases asserting publish sets the supplied UTC timestamp, archive preserves it, publish after archive overwrites it, and repeated publish without a state transition keeps the existing timestamp.
  - Retain existing missing-ingredient/step assertions.

```csharp
[Fact]
public void Publish_SetsTheLatestPublicationTimestamp()
{
    var publishedAt = new DateTimeOffset(2026, 10, 14, 11, 0, 0, TimeSpan.Zero);
    var recipe = new Recipe();
    recipe.Ingredients.Add(new RecipeIngredient());
    recipe.Steps.Add(new RecipeStep());

    recipe.Publish(publishedAt);

    Assert.Equal(publishedAt, recipe.PublishedAt);
    Assert.Equal(RecipeStatus.Published, recipe.Status);
}
```
- [x] **Step 2: Run focused publication lifecycle tests**
  - The pre-implementation failure was not retained; the focused post-implementation domain/lifecycle suite is the completion evidence.
  - Run from `backend`: `dotnet test tests/CulinaryBlog.Application.Tests/CulinaryBlog.Application.Tests.csproj --no-restore --filter "FullyQualifiedName~RecipePublishTests|FullyQualifiedName~RecipeLifecycleTests" '-p:WarningsNotAsErrors=NU1902%3BNU1903'`.
- [x] **Step 3: Implement the domain property and lifecycle transition**
  - Add nullable `DateTimeOffset PublishedAt` to `Recipe`.
  - Change `Publish` to accept a timestamp and set it only after existing recipe readiness checks pass.
  - In `RecipeLifecycleHandler.ChangeStatus`, call `recipe.Publish(DateTimeOffset.UtcNow)` only for publish; preserve the existing archive path and cache invalidation.
- [x] **Step 4: Map the nullable UTC column**
  - Configure `PublishedAt` as nullable PostgreSQL `timestamp with time zone`; leave existing rows null.
- [x] **Step 5: Generate and review the EF migration**
  - Run from `backend`: `dotnet ef migrations add AddRecipePublishedAt --project src/CulinaryBlog.Infrastructure --startup-project src/CulinaryBlog.API --context ApplicationDbContext --output-dir Persistence/Migrations`.
  - Confirm generated `Up` adds nullable `PublishedAt` and `Down` drops only that column; do not backfill from `CreatedAt`.
- [x] **Step 6: Run domain, application and migration compilation tests**
  - Focused domain/lifecycle tests: 7 passed, 0 failed.
  - API build succeeded; `dotnet ef migrations has-pending-model-changes` reported no pending model changes.
  - NU1902/NU1903 remain visible as warnings during validation; use only a command-scoped `WarningsNotAsErrors=NU1902;NU1903` override after restoring with the same override. The default repository warning policy remains strict.
- [x] **Step 7: Commit the timestamp persistence milestone**
  - Commits: `a716a714` implementation; `ef1fd550` removes the temporary broad audit-warning override. The temporary `ea5e6e76` config commit is superseded.

### Task 2: Add the public author profile API

**Files:**
- Create: `backend/src/CulinaryBlog.Application/DTOs/Authors/AuthorProfilePageDto.cs`
- Create: `backend/src/CulinaryBlog.Application/Features/Authors/GetAuthorProfile.cs`
- Create: `backend/src/CulinaryBlog.API/Endpoints/AuthorEndpoints.cs`
- Modify: `backend/src/CulinaryBlog.API/Program.cs`
- Modify: `backend/src/CulinaryBlog.Application/DTOs/Recipes/RecipeDtos.cs`
- Modify: `backend/src/CulinaryBlog.Application/Features/Recipes/RecipeMapping.cs`
- Modify: `backend/src/CulinaryBlog.Application/Features/Recipes/Queries/GetRecipes.cs`
- Modify: `backend/src/CulinaryBlog.Application/Features/Recipes/Queries/GetRecipeBySlug.cs`
- Test: `backend/tests/CulinaryBlog.Application.Tests/Features/Authors/GetAuthorProfileTests.cs`
- Test: `backend/tests/CulinaryBlog.Integration.Tests/Endpoints/AuthorEndpointsTests.cs`

**Interfaces:**
- `GET /api/v1/authors/{id}?page=1&pageSize=12` returns `{ id, displayName, avatarUrl, bio, recipes }`, where `recipes` is the existing `PaginatedResult<RecipeSummaryDto>` shape.
- `AuthorProfilePageDto` contains only public fields; `recipes.items` includes only Published rows for that author.
- `RecipeSummaryDto` adds nullable `PublishedAt`; `RecipeDetailDto` adds nullable `PublishedAt` and `Author: RecipeAuthorDto?` with `Id`, `DisplayName`, `AvatarUrl`.
- `GetAuthorProfileQuery(string AuthorId, int Page = 1, int PageSize = 12)` validates page >= 1 and pageSize in 1..50.

```csharp
public sealed record AuthorProfilePageDto(
    string Id,
    string DisplayName,
    string? AvatarUrl,
    string? Bio,
    PaginatedResult<RecipeSummaryDto> Recipes);
```

```csharp
using var response = await client.GetAsync($"/api/v1/authors/{authorId}?page=1&pageSize=12");
Assert.Equal(HttpStatusCode.OK, response.StatusCode);
using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
Assert.False(json.RootElement.TryGetProperty("email", out _));
Assert.False(json.RootElement.TryGetProperty("roles", out _));
```

- [x] **Step 1: Write API/query tests for public allowlisting**
  - Test that an active author response has only `id`, `displayName`, `avatarUrl`, `bio`, `recipes`; assert no `email` or `roles`.
  - Seed Published, Draft and Archived recipes for the same author and another author's Published recipe; assert only the first author's Published rows appear.
  - Assert page/pageSize and `hasNextPage`/`hasPreviousPage`; inactive or missing author returns 404.
  - Cover a very large valid page (such as `int.MaxValue`, `pageSize=50`) returning an empty page with correct pagination metadata without offset overflow.
- [x] **Step 2: Run the focused endpoint tests before implementation**
  - Run from `backend`: `dotnet test tests/CulinaryBlog.Integration.Tests/CulinaryBlog.Integration.Tests.csproj --filter FullyQualifiedName~AuthorEndpointsTests`.
  - Expected: endpoint is not mapped and tests fail with 404.
- [x] **Step 3: Add response DTOs and validated query**
  - Define the public response DTO without reusing `UserProfileResult` as the serialized API type.
  - Build the query from `IIdentityService.GetUserProfileByIdAsync` and `IRecipeRepository.Query`; reject absent/inactive authors and select only matching `RecipeStatus.Published`.
  - Sort non-null `PublishedAt` first, newest timestamp first, then by stable `Id` so historical null dates sort last; paginate before materializing; return the existing pagination shape.
- [x] **Step 4: Add an anonymous read-only endpoint**
  - Map `GET /api/v1/authors/{id}` with optional page/pageSize query values and send `GetAuthorProfileQuery`.
  - Return 404 for absent/inactive users; never add authorization that could reveal private recipe rows.
- [x] **Step 5: Extend recipe read DTOs without changing write contracts**
  - Add nullable `PublishedAt` to summary/detail projections.
  - Add a small allowlisted `RecipeAuthorDto` to detail mapping; resolve active author's public display fields on recipe-by-slug reads, including cache hits, without embedding Identity fields in the recipe cache entry.
  - Keep create/update command request DTOs and response status/semantics unchanged.
- [x] **Step 6: Run focused backend tests and API build**
  - Initial focused application tests: 14 passed; integration tests: 17 passed; API build succeeded using command-scoped audit-warning exceptions.
  - Review fix round: focused application tests 15 passed; integration tests 17 passed; API build succeeded with the same scoped exception and NU1902/NU1903 warnings visible.
  - Default strict application restore and API build were run and blocked: NU1902/NU1903 for `System.Security.Cryptography.Pkcs` 10.0.0 are warning-as-error findings. Override-scoped passes are functional validation only, not strict-policy passes. No dependency or global configuration change.
- [x] **Step 7: Commit the public API milestone**
  - Commits: `53393861 feat: expose public author profiles`; `26958bb1 fix: prevent author pagination offset overflow`.

### Task 3: Render author profiles and Recipe SEO from API data

**Files:**
- Modify: `frontend/src/lib/recipes-api.ts`
- Create: `frontend/src/lib/server/authors-api.ts`
- Create: `frontend/src/lib/server/recipes-api.ts`
- Create: `frontend/src/lib/recipe-json-ld.ts`
- Create: `frontend/src/lib/recipe-json-ld.test.ts`
- Create: `frontend/src/app/authors/[id]/page.tsx`
- Create: `frontend/src/app/authors/[id]/author-profile-client.tsx`
- Create: `frontend/src/app/recipes/[slug]/recipe-detail-client.tsx`
- Modify: `frontend/src/app/recipes/[slug]/page.tsx`
- Modify: `frontend/e2e/support/mock-recipe-api.mjs`
- Create: `frontend/e2e/recipe-json-ld.spec.ts`
- Create: `frontend/e2e/author-profile.spec.ts`
- Modify: `frontend/playwright.config.ts`, `docker-compose.yml`

**Interfaces:**
- `AuthorProfilePage` contains `id`, `displayName`, nullable `avatarUrl`/`bio`, and `PaginatedResult<RecipeSummary>`.
- `RecipeDetail` includes nullable `publishedAt` and nullable `{ id, displayName, avatarUrl }` author data.
- `buildRecipeJsonLd(recipe: RecipeDetail): RecipeJsonLd` includes `author: { "@type": "Person", name }` only when known and `datePublished` only when `publishedAt` is non-null.
- Server fetches use `API_INTERNAL_URL`, fallback `NEXT_PUBLIC_API_URL`, then `http://localhost:5000`; do not send user tokens to public endpoints.

```ts
return {
  "@context": "https://schema.org",
  "@type": "Recipe",
  name: recipe.title,
  ...(recipe.author ? {
    author: { "@type": "Person", name: recipe.author.displayName },
  } : {}),
  ...(recipe.publishedAt ? { datePublished: recipe.publishedAt } : {}),
};
```

- [x] **Step 1: Write builder tests for author/date and escaping**
  - Cover valid published recipe with author/date, historical recipe with null date, absent author, null nutrition, and hostile `<script>` text.
  - Assert generated JSON has correct author/date values and serialization escapes `<` as `\\u003c`.
- [x] **Step 2: Run unit tests to confirm missing builder**
  - Run from `frontend`: `node --experimental-strip-types --test src/lib/recipe-json-ld.test.ts`.
  - Expected: FAIL because the builder module is not yet present.
- [x] **Step 3: Implement typed API contracts and pure JSON-LD builder**
  - Extend frontend DTO types to match the additive backend fields exactly.
  - Sort images/ingredients/steps as current UI does; only emit optional properties with actual values.
- [x] **Step 4: Convert recipe detail to server data plus client presentation**
  - Resolve Next.js 15 `params: Promise<{ slug: string }>` in the server page.
  - Fetch the recipe once, render JSON-LD from that exact object, and pass the object to `RecipeDetailClient`; preserve current loading/error behavior on failure without rendering draft/archived markup.
  - Render the returned author name as a link to `/authors/{author.id}` and show the returned publication date only when non-null.
- [x] **Step 5: Implement the paginated author page**
  - Fetch `/api/v1/authors/{id}?page=&pageSize=12` server-side; render profile avatar fallback, display name, optional bio, recipe cards, pagination links and clear not-found/error/empty states.
  - Use only returned author fields; never render email/roles.
- [x] **Step 6: Configure local/container API routing and deterministic E2E fixtures**
  - Add server-only `API_INTERNAL_URL=http://api:8080` to the frontend Compose service.
  - Extend the Playwright mock server to return a published recipe and paged author profile; keep the test app on an isolated port.
  - Assert initial HTML contains JSON-LD author/date when present and omits date for historical data; assert profile pagination and no email text.
- [x] **Step 7: Run focused frontend tests**
  - JSON-LD unit tests: 5/5 passed; Playwright profile/SEO/loading E2E: 5/5 passed; production build passed.
  - Initial red and review-fix red/green evidence is recorded in the Task 3 report. Existing image lint and Node module-type warnings are documented.
- [x] **Step 8: Commit profile and SEO milestone**
  - Commits: `7fb592a5 feat: render public author profiles and recipe metadata`; `b6927053 fix: show loading states on public routes`.

### Task 4: Refresh the Culinary Blog design system and apply Motion

**Files:**
- Modify: `frontend/src/app/globals.css`
- Modify: `frontend/src/app/layout.tsx`
- Modify: `frontend/src/app/site-ui.tsx`, `frontend/src/app/site-ui.module.css`, `frontend/tailwind.config.ts`
- Create: `frontend/src/app/brand-motion.tsx`
- Modify: `frontend/src/app/search/search.module.css`
- Modify: shared UI consumers including home, recipe detail, author profile, categories, search and dashboard
- Modify: account pages `auth/login/page.tsx`, `auth/register/page.tsx`, `profile/page.tsx`
- Modify: dashboard page and category-administration UI through the shared design system
- Modify: `frontend/package.json`, `frontend/package-lock.json`
- Test: `frontend/e2e/brand-acceptance.spec.ts` (create)

**Interfaces:**
- Global CSS tokens define brand colors, type scale, spacing, borders, shadows and focus ring; components consume tokens rather than duplicating hex values.
- `BrandMotion` is a small client component using `motion/react`; it renders children without entrance animation when `useReducedMotion()` is true.
- Keep all layout and responsive styling in CSS Modules; keep `lucide-react`.

```tsx
"use client";
import { motion, useReducedMotion } from "motion/react";

export function BrandMotion({ children }: Readonly<{ children: React.ReactNode }>) {
  const reduceMotion = useReducedMotion();
  return (
    <motion.div
      initial={reduceMotion ? false : { opacity: 1, y: 8 }}
      animate={{ opacity: 1, y: 0 }}
      transition={{ duration: reduceMotion ? 0 : 0.2 }}
    >
      {children}
    </motion.div>
  );
}
```

- [x] **Step 1: Add failing brand assertions**
  - Create route checks that assert document title/wordmark “Culinary Blog” on public, author, authentication, profile and dashboard routes.
- [x] **Step 2: Run assertions and confirm the old brand is detected**
  - Run: `npx playwright test e2e/brand-acceptance.spec.ts`.
  - Expected: the stale “Culinary Table” wordmark fails; global metadata already uses “Culinary Blog”.
- [x] **Step 3: Install the approved animation dependency**
  - Run from `frontend`: `npm install motion`.
  - Keep `lucide-react`; do not install a UI component framework or alternate icon set.
- [x] **Step 4: Define shared design tokens and metadata**
  - Replace stale global values with the approved coral/orange, turmeric, deep teal and cream palette; set typography, content widths, focus styles and reduced-motion behavior.
  - Update document title and description to Culinary Blog.
- [x] **Step 5: Refresh the shared public shell and common primitives**
  - Update wordmark/logo mark, responsive header/navigation, footer, section headings, cards, buttons, notices, pagination, forms and tables.
  - Preserve existing Vietnamese labels and route destinations.
- [x] **Step 6: Apply the shared system to every existing app route**
  - Bring auth, personal profile, dashboard and category administration into the same brand system without changing handlers, authorization, request bodies or state transitions.
  - Keep public and dashboard navigation shells distinct.
- [x] **Step 7: Add restrained, accessible Motion**
  - Use Motion only for a short hero/card reveal; disable motion when reduced-motion is requested.
  - Do not animate status/error content in a way that delays screen-reader announcements or blocks interaction.
- [x] **Step 8: Check visual snapshots, brand routes and reduced motion**
  - Capture/update snapshots for home, recipe detail, author profile and dashboard at mobile and desktop sizes after review.
  - Run: `npx playwright test e2e/brand-acceptance.spec.ts` (7/7 passed).
  - Verify 375/768/1024/1280 px with no document horizontal overflow on representative public, author, auth, profile and dashboard routes; reduced-motion emulation disables card transitions and hero entrance motion.
  - Verify the profile's alpha-tinted gradient renders with its CSS-variable palette; measure CTA colors in the browser.
  - Measure key foreground/background pairs to ensure 4.5:1 text contrast and 3:1 controls: 12.79:1 ink/cream, 9.41:1 teal/cream, 5.52:1 on-coral/coral, 6.10:1 teal/turmeric.
- [x] **Step 9: Commit the design-system milestone**
  - Stage only dependency manifests/lock, shared styles/components, affected route styles and brand tests.
  - Commit: `403ebecd feat: refresh Culinary Blog visual identity`.

### Task 5: Complete shared layouts, usability feedback and route acceptance

**Files:**
- Create: `frontend/src/app/dashboard/layout.tsx`
- Create: `frontend/src/app/dashboard/dashboard-shell.tsx`
- Create: `frontend/src/app/feedback.tsx`
- Modify: `frontend/src/app/dashboard/dashboard.module.css`
- Modify: `frontend/src/app/site-ui.tsx`, `frontend/src/app/site-ui.module.css`, `frontend/src/app/layout.tsx`
- Modify: `frontend/src/app/search/page.tsx`, `frontend/src/app/search/search.module.css`
- Modify: `frontend/src/app/dashboard/page.tsx`, `dashboard/recipes/page.tsx`, `dashboard/recipe-editor.tsx`, `dashboard/categories/page.tsx`
- Modify: `frontend/src/lib/recipes-api.ts`
- Modify: `frontend/e2e/srs-routes.spec.ts`
- Modify: `frontend/e2e/support/mock-recipe-api.mjs`
- Create: `frontend/e2e/ui-acceptance.spec.ts`

**Interfaces:**
- `SitePage({ children, className?, footerMessage? })` keeps optional props backward-compatible and wraps every public route, including `/search`.
- `DashboardShell({ children }: Readonly<{ children: React.ReactNode }>)` wraps every `/dashboard/**` page without adding a `<main>`.
- `FeedbackApi` exposes `showToast(message: string, kind: "success" | "error")` and `dismissToast()`.
- `LoadingSkeleton({ variant }: { variant: "cards" | "detail" | "form" })` communicates busy state accessibly.

```ts
type FeedbackKind = "success" | "error";
type FeedbackApi = {
  showToast(message: string, kind: FeedbackKind): void;
  dismissToast(): void;
};
```

- [x] **Step 1: Extend route assertions before changing shells**
  - Test public routes `/`, `/recipes`, `/recipes/[slug]`, `/categories`, `/categories/[slug]`, `/search`, `/authors/[id]` for public header/footer and exactly one main landmark.
  - Test `/dashboard`, `/dashboard/recipes`, `/dashboard/recipes/new`, `/dashboard/recipes/[id]/edit`, `/dashboard/categories` for dashboard navigation and preserved public return link.
- [x] **Step 2: Add dashboard segment shell and remove nested public wrappers**
  - Create the layout/shell, add active route state, accessible nav name and focus styles.
  - Remove only the `SitePage` wrappers from dashboard child pages; preserve every handler/API/token/payload/navigation.
- [x] **Step 3: Move search into the shared public shell**
  - Keep the search tagline in page content, preserve its dotted background through optional `className`, and retain its existing footer text via `footerMessage`.
  - Remove duplicate search topbar/footer; keep query/debounce/filter behavior unchanged.
- [x] **Step 4: Add shared accessible loading and feedback**
  - Mount one feedback provider in root layout; toast success uses `role="status"` and errors use `role="alert"`.
  - Show skeletons only for regions loading data; keep persistent page errors visible.
- [x] **Step 5: Preserve structured Problem Details and field feedback**
  - Keep HTTP 400 handling; parse `title/detail/errors`, display unmatched messages in an error summary, and associate field errors with inputs via `aria-describedby`.
  - Ensure submit buttons disable while saving; publish rolls back if API fails; delete retains confirmation.
- [x] **Step 6: Add accessibility and viewport acceptance checks**
  - Check named controls, labels, keyboard focus, `aria-current`, image alt fallback, table containment and no overflow at 375/768/1024/1280 px.
  - Check contrast thresholds using computed colors; add no axe or other dependency.
- [x] **Step 7: Run route and UI regression tests**
  - Run: route, UI acceptance, author-profile and brand acceptance suites: **24/24 passed**.
  - `npm run build` passed; the existing `@next/next/no-img-element` warning remains in recipe detail.
- [x] **Step 8: Commit shared layouts and usability milestone**
  - Stage only shell/feedback/search/dashboard files and related tests.
  - Commit: `47f92cb3 feat: unify public and author dashboard layouts`.

### Task 6: Update project documentation and verify/run the application

**Files:**
- Modify: `docs/features/FR-UI-001-implementation-checklist.md`
- Modify: `docs/run-project.md`
- Modify: `docs/docker-guide.md`
- Modify only where requirements are referenced: `docs/srs-audit/SRS-AUDIT.md`, `docs/srs-audit/TEAM-REQUIREMENT-TRACEABILITY.md`
- Modify: `frontend/package-lock.json` if Docker `npm ci` exposes a lockfile inconsistency.
- Do not modify the unrelated working-tree change in `README.md`.

- [x] **Step 1: Synchronize checklist with implemented tasks**
  - Track migration/API privacy, author pagination, publication-date lifecycle, JSON-LD, brand across all routes, reduced motion, accessibility, tests and docs.
  - Leave a box unchecked unless a recorded command or test proves it.
- [x] **Step 2: Document database migration and runtime configuration**
  - Add exact migration/apply commands, `API_INTERNAL_URL` server-only Compose configuration, local API/frontend startup commands and author-profile route/API details.
  - Explain that historical rows remain without `PublishedAt` until genuinely republished.
- [x] **Step 3: Update requirement references**
  - Update the author-profile route and author/date JSON-LD acceptance text in `docs/srs-audit/SRS-AUDIT.md` and the matching existing rows in `docs/srs-audit/TEAM-REQUIREMENT-TRACEABILITY.md`; do not invent new requirement IDs.
- [x] **Step 4: Run all backend tests and build**
  - `dotnet test CulinaryBlog.sln --no-restore`: 762 passed, 9 skipped, 0 failed; API build passed.
- [x] **Step 5: Run frontend checks**
  - Run from `frontend`: `npx tsc --noEmit`, `npm run build`, and `npx playwright test`.
  - TypeScript check and production build passed; full Playwright suite passed 38/38. Docker `npm ci` initially exposed missing optional platform entries; regenerated the lockfile with Node 22/npm 10, then Docker `npm ci` and image build passed without changing package versions.
- [x] **Step 6: Apply migration and start services**
  - `docker compose up --build -d` started API, frontend, PostgreSQL, Redis, MinIO and MailHog. The Development API applied migrations on startup; PostgreSQL confirms `20261014123827_AddRecipePublishedAt` and `Recipes.PublishedAt`.
- [x] **Step 7: Smoke-test API and browser behavior**
  - Verify `GET /api/v1/authors/{id}?page=1&pageSize=12`, public recipe detail, `/authors/{id}`, `/recipes/{slug}`, dashboard and auth/profile routes.
  - Inspect initial recipe HTML for escaped JSON-LD author/date; verify no email, Draft/Archived recipe or fabricated historical date is present.
  - Author API returned 100 published recipes in pages of 12, with no email/roles fields. Fourteen representative public, author, dashboard, auth and profile routes returned HTTP 200. Recipe SSR included the author in JSON-LD and omitted historical `datePublished`; existing JSON-LD tests cover escaping and populated publication dates.
- [x] **Step 8: Commit docs and verified checklist**
  - Commit only Task 6 docs, the frontend lockfile repair, and directly related TypeScript/E2E assertion fixes; leave the pre-existing README change unstaged.
  - Commit: `chore: complete UI rollout validation and docs`.

## Self-review / coverage

- Data lifecycle and null-safe migration: Task 1.
- Author DTO, pagination, active-user filtering and privacy: Task 2.
- Recipe author/date, public author route, JSON-LD and SSR: Task 3.
- New brand, Motion package, reduced motion and contrast: Task 4.
- Public/dashboard shells, `/search`, NFR-USE-001..004 and interaction feedback: Task 5.
- SRS/checklist/run instructions, full tests, database migration and startup: Task 6.
- Each task has a focused test gate and scoped commit; no API write contract or unrelated README change is staged.
