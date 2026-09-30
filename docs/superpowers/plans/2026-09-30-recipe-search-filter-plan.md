# Recipe Search and Filtering Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Complete FR-SRCH-001 full-text recipe search and FR-SRCH-002 multi-criteria recipe filtering using the existing Recipe Core CQRS, EF Core, PostgreSQL and API patterns.

**Architecture:** Keep request validation and query orchestration in Application CQRS handlers, persistence-specific full-text operations in Infrastructure, and HTTP contracts in the existing Minimal API endpoints. Reuse the existing recipe listing query/result for filtering, add a dedicated search query path, and integrate search terms with filters only after settling the endpoint and pagination contract questions listed below.

**Tech Stack:** .NET 10, ASP.NET Core Minimal APIs, MediatR, FluentValidation, EF Core, PostgreSQL full-text search (`tsvector`, `unaccent`, GIN), Redis distributed cache, xUnit.

**Spec:** `docs/srs-audit/SRS-AUDIT.md` (§3.4 FR-SRCH-001 and FR-SRCH-002, §7.2, §8.3); `docs/srs-audit/SRS-CONFLICTS-AND-DECISIONS.md` (CONFLICT-016); `docs/srs-audit/TEAM-REQUIREMENT-TRACEABILITY.md`; `docs/recipe-core-implementation-spec.md`; `docs/architecture/PHASE-1-BOUNDARIES.md`.

## Scope and assumptions

- The request repeats `FR-SRCH-001`. This plan interprets the second occurrence as **FR-SRCH-002: Lọc Công thức (Filter Recipes)**, because the SRS defines it immediately after FR-SRCH-001. Confirm this interpretation before implementation if a different requirement was intended.
- FR-SRCH-001 is presently marked `CONFLICT` in the SRS audit, but the conflict register records an approved decision: cache popular/common search queries in Redis for **60 seconds** (`CONFLICT-016`, `DECIDED`, 2026-09-23). Use that decision unless the decision record is superseded.
- FR-SRCH-002 is `CLEAR`: anonymous users may filter; filters combine with AND; `maxCookTime > 0`, `minServings > 0`, and difficulty is `Easy`, `Medium`, or `Hard`; dynamic filters are not cached.
- Existing `GetRecipesQuery` already supports category, difficulty, max cooking time, minimum servings, sorting, paging and public/owner visibility. Preserve and extend it rather than building a second listing implementation.
- The specification has inconsistent endpoint descriptions: FR-SRCH-001 lists `GET /api/v1/recipes?search={q}`, while §8.3 lists `GET /recipes/search?q=...`; existing code maps `GET /api/v1/recipes/`. Pagination response shape also has open CONFLICT-012. Resolve these API contract decisions with TV2/TV3 before locking public routes or response JSON. Until then, keep search and filter handlers internally independent of the final route spelling and envelope.
- SRS's full-text requirements mention an unaccented Vietnamese query, PostgreSQL `tsvector`, `to_tsquery('simple', ...)`, prefix matching (`:*`), GIN index and `ts_rank_cd`. Validate the actual PostgreSQL `unaccent` extension/configuration and Vietnamese behavior in the deployed PostgreSQL version; do not claim Vietnamese language stemming from the `simple` configuration.

## Global constraints

- Anonymous visitors can search/filter published recipes; authenticated authors can also see their own non-published recipes, following the existing `GetRecipesQueryHandler` visibility rule. Admin visibility follows existing Recipe Core behavior.
- Use existing `PaginatedResult<RecipeSummaryDto>` unless CONFLICT-012 resolves to another response envelope; do not create a competing DTO by assumption.
- Search term maximum length is 100 characters per FR-SRCH-001. Empty search input returns the default newest-first recipe list (200) per the FR alternate path.
- Apply all selected filters conjunctively (AND). Do not cache dynamic FR-SRCH-002 filters.
- Keep database-specific FTS translation out of the Domain project and preserve Clean Architecture dependencies.
- Do not create duplicate entities/repositories or modify another member's module outside the agreed Recipe Core boundary. TV2 owns Recipe Core/search; coordinate route/UI contracts with TV3.

## File map

### Existing files to extend

- `backend/src/CulinaryBlog.Application/Features/Recipes/Queries/GetRecipes.cs` — current list query, validation, visibility, filters, sorting and paging; extend validation for FR-SRCH-002 and optional search integration only after contract decision.
- `backend/src/CulinaryBlog.Application/Contracts/Persistence/IRecipeRepository.cs` — persistence boundary for recipe queries; add a narrowly scoped search operation if provider translation cannot remain in an Application query.
- `backend/src/CulinaryBlog.Infrastructure/Repositories/RecipeRepository.cs` — implementation of repository query/search behavior.
- `backend/src/CulinaryBlog.Infrastructure/Configurations/RecipeConfiguration.cs` — provider-supported search column/index mapping where applicable.
- `backend/src/CulinaryBlog.API/Endpoints/RecipeEndpoints.cs` — final route and query parameter binding after API contract decision.
- `backend/src/CulinaryBlog.Infrastructure/DependencyInjection.cs` (or current Infrastructure registration file) — register a search cache implementation if a new abstraction is introduced.
- `backend/src/CulinaryBlog.Infrastructure/Persistence/ApplicationDbContext.cs` and migration files — PostgreSQL extension, generated/maintained search vector and GIN index; inspect existing migration conventions before creating another migration.

### New files expected

- `backend/src/CulinaryBlog.Application/Features/Recipes/Queries/SearchRecipes.cs` — `SearchRecipesQuery`, validator and handler if search remains a distinct use case.
- `backend/src/CulinaryBlog.Application/Contracts/IRecipeSearchCache.cs` — optional abstraction for 60-second popular-query cache; create only if existing `IRecipeCache` cannot safely express search cache operations.
- `backend/src/CulinaryBlog.Infrastructure/Persistence/DistributedRecipeSearchCache.cs` — Redis implementation if the cache abstraction is added.
- `backend/tests/CulinaryBlog.Application.Tests/Features/Recipes/Queries/GetRecipesQueryValidatorTests.cs` — FR-SRCH-002 boundary and enum validation coverage.
- `backend/tests/CulinaryBlog.Application.Tests/Features/Recipes/Queries/SearchRecipesQueryTests.cs` — search validation, empty-query behavior, visibility and ranking behavior where unit-testable.
- `backend/tests/CulinaryBlog.Integration.Tests/Endpoints/RecipeSearchEndpointsTests.cs` — API contract, PostgreSQL FTS semantics and combined filters; use the repository's established database fixture or add PostgreSQL Testcontainers support if none exists.

## Contract decisions required before implementation

- [ ] Agree one canonical search route and parameter names with TV2/TV3: SRS §3.4 `GET /api/v1/recipes?search={q}` vs §8.3 `GET /api/v1/recipes/search?q={keyword}`. The existing `GET /api/v1/recipes/` route already serves list/filter queries; avoid ambiguous route matching.
- [ ] Confirm page parameter names/defaults and response envelope against CONFLICT-012. Current implementation uses `page`, `pageSize` and flat `PaginatedResult`.
- [ ] Decide whether search can be combined with FR-SRCH-002 filters on one request. Recommended behavior is to combine them with AND and apply sorting/pagination once, while retaining empty-search default list behavior.
- [ ] Confirm difficulty enum compatibility. The SRS only allows `Easy|Medium|Hard`, while Recipe Core's `Difficulty` is a string and the data model also lists `Expert`. Enforce the FR-SRCH-002 allowed set for this filter unless the team updates the SRS.
- [ ] Confirm production migration ownership and safe rollout for `unaccent` extension and search vector. The migration must not assume extension creation privileges that deployment PostgreSQL does not grant.

## Implementation tasks

### Task 1: Lock and document the API contract

**Files:**
- Modify: `docs/srs-audit/SRS-CONFLICTS-AND-DECISIONS.md` only if the team formally records a new decision.
- Modify: `docs/srs-audit/SRS-AUDIT.md` only if an authorized SRS reconciliation is made.
- Review: `backend/src/CulinaryBlog.API/Endpoints/RecipeEndpoints.cs`
- Review: `backend/src/CulinaryBlog.Application/Common/Models/PaginatedResult.cs`

- [ ] Confirm route, query parameter names, pagination envelope and search/filter combination with TV2/TV3.
- [ ] Record the decision in the canonical decision log before changing the endpoint contract; do not silently rewrite an open SRS conflict.
- [ ] Set the selected contract in endpoint tests and this plan's task interfaces.

**Deliverable:** approved route/query/response contract with no unresolved assumption in the implementation tasks.

### Task 2: Complete FR-SRCH-002 filter validation and list behavior

**Files:**
- Modify: `backend/src/CulinaryBlog.Application/Features/Recipes/Queries/GetRecipes.cs`
- Test: `backend/tests/CulinaryBlog.Application.Tests/Features/Recipes/Queries/GetRecipesQueryValidatorTests.cs`
- Test: `backend/tests/CulinaryBlog.Application.Tests/Features/Recipes/Queries/GetRecipesQueryHandlerTests.cs` (create only if no suitable handler tests exist)

**Interface:** Keep `GetRecipesQuery(int Page, int PageSize, Guid? CategoryId, string? Difficulty, int? MaxCookTime, int? MinServings, string SortBy, string SortOrder)` and return `PaginatedResult<RecipeSummaryDto>`.

- [ ] Add tests that accept omitted filters and valid category/difficulty/maxCookTime/minServings combinations.
- [ ] Add validation cases rejecting `maxCookTime` equal to 0 and negative values, `minServings` equal to 0 and negative values, and difficulty outside `Easy`, `Medium`, `Hard` (case behavior should be explicitly defined and tested; accept case-insensitive values if consistent with API conventions).
- [ ] Verify multiple supplied filters are conjunctive: a recipe must satisfy every supplied predicate.
- [ ] Verify a public request only sees Published recipes and an author request retains current owner/admin visibility rules.
- [ ] Run focused Application tests and verify the new boundary cases fail against current code before implementation.
- [ ] Implement the validator rules and only change query predicates if a test exposes mismatch with FR-SRCH-002; retain sort and pagination behavior.
- [ ] Run focused tests and the Application test project; inspect generated SQL/provider translation when adding predicates.

**Deliverable:** list query meets FR-SRCH-002 validation and AND semantics without regressing current Recipe Core list behavior.

### Task 3: Provision PostgreSQL full-text search storage

**Files:**
- Modify: `backend/src/CulinaryBlog.Infrastructure/Configurations/RecipeConfiguration.cs`
- Modify: `backend/src/CulinaryBlog.Infrastructure/Persistence/ApplicationDbContext.cs` only if the existing EF model requires it.
- Create: a new timestamped migration under `backend/src/CulinaryBlog.Infrastructure/Persistence/Migrations/`.
- Test: `backend/tests/CulinaryBlog.Integration.Tests/` PostgreSQL-backed recipe search fixture/tests.

- [ ] Inspect existing migrations and model for `SearchVector`, `unaccent`, trigger/function, and GIN index before adding schema objects; the audit describes these objects, but current code/migrations may not yet implement them.
- [ ] Add a repeatable deployment migration that enables `unaccent` where permitted and creates/maintains a `tsvector` for the searchable recipe text agreed from the SRS/data model. Include insert/update behavior so changes to searchable fields cannot leave stale vectors.
- [ ] Create the GIN index for the vector using the established migration pattern; use a non-blocking/concurrent index strategy only if compatible with this project's migration transaction/deployment rules.
- [ ] Ensure soft-deleted/non-public recipe visibility remains enforced by query logic, not by exposing raw vector data.
- [ ] Verify migration up/down behavior on the supported PostgreSQL version and verify recipes created or edited after migration have populated vectors.

**Deliverable:** migrated PostgreSQL schema can index and maintain the agreed search document.

### Task 4: Implement FR-SRCH-001 query construction, ranking and validation

**Files:**
- Create: `backend/src/CulinaryBlog.Application/Features/Recipes/Queries/SearchRecipes.cs`
- Modify: `backend/src/CulinaryBlog.Application/Contracts/Persistence/IRecipeRepository.cs` if a provider-specific search boundary is needed.
- Modify: `backend/src/CulinaryBlog.Infrastructure/Repositories/RecipeRepository.cs`
- Modify: `backend/src/CulinaryBlog.Application/DTOs/Recipes/RecipeDtos.cs` only if the approved API requires an explicit rank field; do not expose it unless contract requires it.
- Test: `backend/tests/CulinaryBlog.Application.Tests/Features/Recipes/Queries/SearchRecipesQueryTests.cs`
- Test: PostgreSQL-backed search integration tests.

**Interface:** `SearchRecipesQuery(string? Search, int Page = 1, int PageSize = 12, Guid? CategoryId = null, string? Difficulty = null, int? MaxCookTime = null, int? MinServings = null)` returns `PaginatedResult<RecipeSummaryDto>`; adjust names only to match Task 1's approved API contract.

- [ ] Test validation: query length up to 100 is accepted; over 100 is rejected with 400; null/empty/whitespace takes the default newest-first list path.
- [ ] Test input safety: punctuation, quote characters, repeated spaces and Unicode input cannot inject raw tsquery syntax or raise a database error.
- [ ] Normalize Vietnamese accents consistently with the configured PostgreSQL `unaccent` implementation, then construct a safe `simple` tsquery with prefix matching for each supported term. Define whether terms are ANDed before implementing and encode the chosen semantics in tests.
- [ ] Execute against the `SearchVector` GIN index and rank matching recipes using `ts_rank_cd`; order by descending rank with a deterministic secondary sort (for example `PublishedAt` or `CreatedAt`) so pagination is stable for ties.
- [ ] Apply Recipe visibility and any selected FR-SRCH-002 filters before count/page projection; preserve filters' AND semantics.
- [ ] Return the agreed empty result shape when no recipes match; use 200 rather than 404.
- [ ] Verify query plans on representative data to ensure the search expression can use the GIN index.

**Deliverable:** valid, safe, ranked full-text results satisfying FR-SRCH-001 and compatible filters.

### Task 5: Add the decided 60-second cache for popular searches

**Files:**
- Review: `backend/src/CulinaryBlog.Application/Contracts/IRecipeCache.cs`
- Create if needed: `backend/src/CulinaryBlog.Application/Contracts/IRecipeSearchCache.cs`
- Create if needed: `backend/src/CulinaryBlog.Infrastructure/Persistence/DistributedRecipeSearchCache.cs`
- Modify: `backend/src/CulinaryBlog.Infrastructure/DependencyInjection.cs` (or current registration file)
- Test: `backend/tests/CulinaryBlog.Application.Tests/Infrastructure/Caching/RecipeSearchCacheTests.cs`

- [ ] Reuse `IRecipeCache` only if it supports an unambiguous search-result key and TTL without weakening existing detail cache semantics; otherwise define a focused cache interface.
- [ ] Canonicalize query and all filter/paging inputs before constructing the cache key; include every result-affecting parameter and a version prefix.
- [ ] Cache only popular/common queries per NFR/CONFLICT-016 at TTL 60 seconds; define the popularity threshold/mechanism from the existing NFR or leave cache bypassed until agreed rather than caching every dynamic filter combination.
- [ ] Confirm how result invalidation occurs after recipe create/update/publish/archive/delete; if safe targeted invalidation is unavailable, rely on the 60-second TTL and document that bounded staleness.
- [ ] Verify Redis unavailable behavior follows the existing cache fallback and search continues against PostgreSQL.

**Deliverable:** only eligible common search results use Redis with the decided 60-second TTL; FR-SRCH-002 dynamic filter requests remain uncached.

### Task 6: Expose the agreed endpoint contract

**Files:**
- Modify: `backend/src/CulinaryBlog.API/Endpoints/RecipeEndpoints.cs`
- Modify: `backend/src/CulinaryBlog.API/Program.cs` only if endpoint/validation registration requires it.
- Test: `backend/tests/CulinaryBlog.Integration.Tests/Endpoints/RecipeSearchEndpointsTests.cs`

- [ ] Bind route/query parameters from the approved Task 1 contract and dispatch to `SearchRecipesQuery` or `GetRecipesQuery` as appropriate.
- [ ] Map FluentValidation failures to the project's standard 400 Problem Details response; do not leak SQL or internal error details.
- [ ] Keep search/filter endpoints anonymous as required by SRS while preserving handler visibility rules.
- [ ] Cover 200 with results, 200 empty results, empty query default list, invalid filter 400, overlong query 400, and anonymous access.
- [ ] Verify pagination response and OpenAPI metadata match the decided contract and conflict log.

**Deliverable:** API exposes the approved search/filter contract and error mapping.

### Task 7: Frontend integration and acceptance

**Files:**
- Inspect/coordinate: `frontend/src/` search UI, search page and recipe API client; identify exact paths during implementation because frontend layout may change.
- Owner coordination: TV3 (Search input bar, debounce, filter sidebar UI), TV2 (query/API).

- [ ] Confirm TV3's current input, debounce, filter control and route patterns before editing frontend files.
- [ ] Connect search input and filter controls to the canonical endpoint; serialize only active filters and reset page to 1 when query or filters change.
- [ ] Display loading, empty and API validation states without changing recipe card behavior outside this feature.
- [ ] Verify keyboard submit, URL refresh/deep-link behavior, and preservation of selected filters if the approved frontend route uses query parameters.
- [ ] Run frontend lint/build only when frontend files are changed, and complete API/UI smoke tests against PostgreSQL and Redis.

**Deliverable:** users can submit search and combine category, difficulty, maximum cook time and minimum servings from the recipe search UI.

## Acceptance checklist

- [ ] FR-SRCH-001 supports Vietnamese accent-insensitive full-text matching, safe prefix query construction, `ts_rank_cd` ordering, max 100 character validation, default list for blank input, and 200 for zero matches.
- [ ] Full-text search uses PostgreSQL vector + GIN index and handles inserts/updates consistently.
- [ ] Common search cache TTL is 60 seconds according to decided CONFLICT-016; filter-only combinations are not cached.
- [ ] FR-SRCH-002 supports `categoryId`, `difficulty`, `maxCookTime`, `minServings`; validates the stated ranges/enumeration and combines simultaneous filters with AND.
- [ ] Public/author/admin recipe visibility matches existing Recipe Core behavior.
- [ ] API route, query names, pagination contract and error envelope match recorded decisions.
- [ ] Application and PostgreSQL-backed integration coverage passes; migration applies against the supported PostgreSQL version; no unrelated Recipe Core behavior regresses.

## Risks and unresolved details

- FR-SRCH-001 API contract is inconsistent between SRS §3.4, SRS §8.3 and current code; Task 1 is a hard prerequisite.
- Search cache decision is clear in the decision log, while the SRS audit still labels FR-SRCH-001 `CONFLICT`; the decision log is the current resolution and audit status may need authorized reconciliation.
- Searchable fields are not enumerated in the FR block. Agree title/description and whether ingredient names participate before defining the vector and trigger.
- The data model says `Difficulty` includes `Expert`, while FR-SRCH-002 only allows three values. Filtering should follow the FR set unless product requirements explicitly revise it.
- Query minimum length appears as 2 characters in the §8.3 endpoint table but is absent from FR-SRCH-001, which only sets a maximum length. Do not enforce a minimum until this discrepancy is resolved.
- The current SRS says `maxCookTime > 0`, while current list validation allows zero. This plan follows the FR requirement; the team should confirm whether `0` (no-cook) should be a valid upper bound.

