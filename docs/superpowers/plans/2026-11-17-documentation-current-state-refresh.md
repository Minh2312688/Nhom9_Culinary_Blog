# Project Documentation Current-State Refresh Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Reconcile all tracked root and `docs/` Markdown files with the repository's current implementation while preserving historical context.

**Architecture:** Treat source code, tests, and runtime configuration as evidence; update present-tense guidance and status claims in place. Preserve dated requirements and decisions, adding dated status notes rather than rewriting the historical intent.

**Tech Stack:** Markdown, Git, .NET 10, Next.js/TypeScript, Docker Compose.

**Spec:** [2026-11-17-documentation-current-state-design.md](../specs/2026-11-17-documentation-current-state-design.md)

## Global Constraints

- Scope is tracked Markdown files in the repository root and `docs/`.
- Do not modify application code, tests, deployment configuration, or dependency manifests.
- Preserve the untracked `Bao_cao_Culinary_Blog.docx`.
- Do not claim a behavior is verified unless current test or execution evidence supports it.
- Do not rerun application build or tests; the user has confirmed they already ran successfully.
- Keep original SRS requirements and dated decisions traceable; update present-day status without backdating.

---

### Task 1: Establish Current-State Evidence

**Files:**
- Read: `backend/src/CulinaryBlog.Application/Features/Recipes/Queries/GetRecipes.cs`
- Read: `backend/src/CulinaryBlog.Application/Features/Recipes/Queries/GetRecipeBySlug.cs`
- Read: `backend/src/CulinaryBlog.Application/Features/Recipes/Queries/GetPrivateRecipeById.cs`
- Read: `backend/src/CulinaryBlog.API/Program.cs`
- Read: `backend/src/CulinaryBlog.Infrastructure/Persistence/Seed/Lab02DataSeeder.cs`
- Read: `backend/src/CulinaryBlog.Infrastructure/Persistence/RandomDataSeeder.cs`
- Read: `frontend/src/app/recipes/[slug]/recipe-detail-client.tsx`
- Read: `frontend/src/app/dashboard/recipes/page.tsx`
- Read: `frontend/src/app/dashboard/recipes/[id]/page.tsx`
- Read: `frontend/src/app/dashboard/recipe-editor.tsx`
- Read: `docker-compose.yml`, `docker-compose.e2e.yml`, `frontend/package.json`
- Read: relevant backend tests under `backend/tests/` and frontend E2E tests under `frontend/e2e/`

**Interfaces:**
- Consumes: Current repository source and configuration.
- Produces: An evidence checklist used by subsequent documentation tasks; do not create a separate artifact.

- [x] **Step 1: Verify recipe visibility and detail behavior**
  - Confirm the exact admin, owner, and anonymous list/detail rules from query handlers, endpoint registration, and tests.
  - Confirm recipe detail fields and empty states from the tagged client component.
- [x] **Step 2: Verify seeding, runtime, and test commands**
  - Distinguish the active `Lab02DataSeeder` from `RandomDataSeeder`; confirm which path is called by `Program.cs` and under what configuration.
  - Confirm Compose ports, environment variables, and runnable test/build scripts from manifests and guides.
- [x] **Step 3: Record only evidence-backed statements for document updates**
  - Separate implemented, tested, environment-dependent, pending, and superseded items.
  - Do not run builds or tests.

**Verification:** Each fact used in later tasks must map to a source/config/test path listed above.

---

### Task 2: Refresh Project Overview and Operating Guides

**Files:**
- Modify: `README.md`
- Modify: `docs/run-project.md`
- Modify: `docs/docker-guide.md`
- Modify: `docs/postgresql-setup.md`

**Interfaces:**
- Consumes: Evidence checklist from Task 1.
- Produces: Consistent entry point and local run/deploy/test guidance.

- [x] **Step 1: Update the README overview**
  - Align listed capabilities, frontend dashboard routes, recipe detail features, authentication flow, seeding behavior, and test entry points with current code.
  - Preserve confirmed team information; avoid inferring authorship.
- [x] **Step 2: Reconcile startup and data instructions**
  - Align Compose services/ports, `.env` requirements, migration behavior, and seed toggles with current configuration.
  - Keep destructive volume-reset instructions explicitly warned and scoped.
- [x] **Step 3: Align test and troubleshooting instructions**
  - Retain only commands that are valid for the current toolchain and explain required services/secrets without embedding secret values.
  - Ensure README links to the detailed guide instead of duplicating conflicting procedures.
- [x] **Step 4: Verify paths and commands**
  - Check each referenced script, solution, project, and test file exists.
  - Run `git diff --check`.

---

### Task 3: Correct Recipe Implementation Status

**Files:**
- Modify: `implementation_plan.md`
- Modify: `docs/recipe-core-implementation-spec.md`

**Interfaces:**
- Consumes: Evidence checklist from Task 1 and operating guidance from Task 2.
- Produces: One consistent technical status account for recipe backend and UI.

- [x] **Step 1: Reconcile recipe lifecycle and visibility claims**
  - Update list visibility, private detail access, admin behavior, mutation authorization, soft delete, and concurrency claims against current handlers/tests.
- [x] **Step 2: Reconcile recipe UI and data sections**
  - Describe the public recipe detail's primary/secondary images, publication date/author, ingredients, ordered steps, optional step duration/image, nutrition, and empty states.
  - Describe the current dashboard/editor flow only to the extent supported by the frontend implementation.
- [x] **Step 3: Correct seed and testing status**
  - Distinguish the sample-data seeder from the report-oriented random seeder and accurately state whether each is called or available.
  - Update pending/complete labels only when code and test evidence support them; preserve environment-dependent verification as such.
- [x] **Step 4: Remove internal contradictions**
  - Ensure the scope, FR status matrix, open-work list, and dated implementation notes agree without erasing historical results.
- [x] **Step 5: Verify the two documents**
  - Check referenced code paths and commands exist; run `git diff --check`.

---

### Task 4: Reconcile Feature Specs and Checklists

**Files:**
- Modify: `docs/features/FR-RCP-009-recipe-editor-testcontainers-checklist.md`
- Modify: `docs/features/FR-RCP-009-recipe-editor-testcontainers-plan.md`
- Modify: `docs/features/FR-RCP-010-recipe-steps-cache-e2e-checklist.md`
- Modify: `docs/features/FR-RCP-010-recipe-steps-cache-e2e-spec.md`
- Modify: `docs/features/FR-SRCH-001-implementation-checklist.md`
- Modify: `docs/features/FR-SRCH-001-implementation-spec.md`
- Modify: `docs/features/FR-SRCH-003-004-implementation-checklist.md`
- Modify: `docs/features/FR-SRCH-003-004-implementation-spec.md`
- Modify: `docs/features/FR-UI-001-implementation-checklist.md`

**Interfaces:**
- Consumes: Current-state evidence and corrected recipe status from Tasks 1 and 3.
- Produces: Feature-level records whose status is current but whose original acceptance intent remains intact.

- [x] **Step 1: Update recipe editor and nested-data records**
  - Reconcile FR-RCP-009/010 checkboxes, implementation notes, and test evidence with current API and UI behavior.
  - Distinguish code completion from database/Testcontainers or live-service verification.
- [x] **Step 2: Update search and UI records**
  - Verify each claimed route, filter, sorting/pagination behavior, dashboard flow, accessibility/SEO behavior, and E2E check against implementation and tests.
  - Mark unsupported or unverified outcomes as pending rather than assumed complete.
- [x] **Step 3: Cross-check requirement links**
  - Verify spec/checklist links point to the matching current file and identifiers remain stable.
- [x] **Step 4: Verify**
  - Run `git diff --check` and inspect all changed status rows for supported evidence.

---

### Task 5: Refresh SRS, Architecture, and Security Status Notes

**Files:**
- Modify: `docs/architecture/PHASE-1-BOUNDARIES.md`
- Modify: `docs/srs-audit/SRS-71-PAGE-MASTER-CHECKLIST.md`
- Modify: `docs/srs-audit/SRS-AUDIT.md`
- Modify: `docs/srs-audit/SRS-CONFLICTS-AND-DECISIONS.md`
- Modify: `docs/srs-audit/TEAM-REQUIREMENT-TRACEABILITY.md`
- Modify: `docs/security/jwt-key-rotation-procedure.md`
- Modify: `docs/security/tv2-recipe-authorization-proposal.md`
- Modify: `docs/security/tv4-audit-trail-logging-proposal.md`

**Interfaces:**
- Consumes: Current implementation facts from Task 1 and feature status from Tasks 3–4.
- Produces: Accurate status annotations while preserving original requirements, boundaries, and proposals.

- [x] **Step 1: Preserve requirement history**
  - Do not rewrite original SRS wording or conflict decisions; add dated implementation/status notes where current behavior resolves or supersedes an item.
- [x] **Step 2: Reconcile ownership and boundaries**
  - Align current module boundaries and confirmed team assignments; do not derive ownership from commit authorship.
- [x] **Step 3: Reconcile security proposal status**
  - Confirm whether each proposal is implemented, partially implemented, pending, or superseded before changing its status.
  - Keep procedural security guidance intact unless current implementation makes a step materially false.
- [x] **Step 4: Verify evidence links and dates**
  - Ensure added notes identify the current-state date and point to existing evidence paths.
  - Run `git diff --check`.

---

### Task 6: Annotate Historical Plans and Designs — Group A

**Files:**
- Modify: `docs/superpowers/plans/2026-10-14-compose-stack-recovery.md`
- Modify: `docs/superpowers/plans/2026-10-14-frontend-screens-usability-seo.md`
- Modify: `docs/superpowers/plans/2026-11-13-authjs-bff-plan.md`
- Modify: `docs/superpowers/plans/2026-11-13-tanstack-query-plan.md`
- Modify: `docs/superpowers/specs/2026-10-14-compose-stack-recovery-design.md`
- Modify: `docs/superpowers/specs/2026-10-14-frontend-screens-usability-seo-design.md`
- Modify: `docs/superpowers/specs/2026-11-13-authjs-tanstack-design.md`

**Interfaces:**
- Consumes: Operating-guide facts and current UI/auth/runtime evidence.
- Produces: Historical records with clearly labeled current status and preserved original proposals.

- [x] **Step 1: Add a concise dated status note to each document**
  - State what is implemented, what remains, and whether the document is still an implementation reference.
- [x] **Step 2: Preserve dated intent**
  - Do not replace original plans, options, or rejected alternatives with current-state wording.
- [x] **Step 3: Validate internal links and factual status**
  - Check cited paths exist and labels agree with Tasks 2–5.
  - Run `git diff --check`.

---

### Task 7: Annotate Historical Plans and Designs — Group B

**Files:**
- Modify: `docs/superpowers/plans/2026-09-30-recipe-search-filter-plan.md`
- Modify: `docs/superpowers/plans/2026-10-02-fr-rcp-008-recipe-images-plan.md`
- Modify: `docs/superpowers/plans/2026-10-02-fr-srch-001-plan.md`
- Modify: `docs/superpowers/plans/2026-10-02-fr-srch-002-plan.md`
- Modify: `docs/superpowers/plans/2026-10-07-fr-srch-003-004-plan.md`
- Modify: `docs/superpowers/plans/2026-10-21-fr-rcp-009-recipe-editor-testcontainers-plan.md`
- Modify: `docs/superpowers/plans/2026-10-28-recipe-crud-enhancement-e2e-plan.md`
- Modify: `docs/superpowers/plans/2026-11-09-category-media.md`
- Modify: `docs/superpowers/plans/2026-11-09-recipe-image-workflow.md`
- Modify: `docs/superpowers/plans/2026-11-09-sample-nutrition.md`
- Modify: `docs/superpowers/plans/2026-11-13-recipe-dashboard-wizard.md`
- Modify: `docs/superpowers/specs/2026-11-09-recipe-category-media-and-nutrition-design.md`
- Modify: `docs/superpowers/specs/2026-11-13-recipe-dashboard-wizard-design.md`

**Interfaces:**
- Consumes: Recipe, search, editor, seed, media, and nutrition evidence from Tasks 1–4.
- Produces: Historical feature plans/designs annotated with present status and verification limits.

- [x] **Step 1: Reconcile recipe/search/media outcomes**
  - For each plan, label planned items as implemented, partial, pending, or superseded from current code/tests.
- [x] **Step 2: Reconcile editor, sample data, and nutrition outcomes**
  - Verify the editor flow, sample-data sources, and nutrition display/seeding statements before updating.
- [x] **Step 3: Preserve original plan history**
  - Add a dated current-status block; do not rewrite original task sequence as though it were authored now.
- [x] **Step 4: Validate links and terminology**
  - Cross-check with README and `docs/recipe-core-implementation-spec.md`.
  - Run `git diff --check`.

---

### Task 8: Annotate Remaining Historical Infrastructure Plan

**Files:**
- Modify: `docs/superpowers/plans/2026-11-09-docker-rollout.md`

**Interfaces:**
- Consumes: Current Compose, Docker, and run-guide evidence from Tasks 1–2.
- Produces: A historically accurate rollout plan with an explicit current outcome.

- [x] **Step 1: Add current status without replacing the rollout record**
  - Distinguish implemented Compose changes from any remaining rollout work.
- [x] **Step 2: Validate referenced commands and paths**
  - Confirm instructions agree with `docs/docker-guide.md` and `docs/run-project.md`.
  - Run `git diff --check`.

---

### Task 9: Perform Final Documentation Consistency Review

**Files:**
- Review: All root and `docs/` Markdown files changed in Tasks 2–8.

**Interfaces:**
- Consumes: All updated documents.
- Produces: A verified documentation-only diff.

- [x] **Step 1: Check status vocabulary**
  - Ensure “implemented”, “tested”, “environment-dependent”, “pending”, and “superseded” are not used interchangeably.
- [x] **Step 2: Check internal links and referenced paths**
  - Verify every newly edited Markdown link target exists; resolve any stale links found in touched content.
- [x] **Step 3: Check consistency across entry points**
  - Confirm README, run guides, implementation plan, Recipe Core spec, feature records, and historical status notes do not contradict one another.
- [x] **Step 4: Review final diff**
  - Run `git diff --check`; confirm no source/config/test files changed and `Bao_cao_Culinary_Blog.docx` remains untouched.
  - Do not run application build or tests.
