# Task 2 Report — Public author profile API

## Outcome

Implemented the anonymous `GET /api/v1/authors/{id}?page=1&pageSize=12` read API, allowlisted author and recipe DTOs, and published-date/public-author fields on recipe reads. Inactive and missing authors return 404. Only Published recipes belonging to the requested author are included; the recipe page sorts non-null publication dates newest-first, historical null dates last, and ties by stable recipe ID.

The author API serializes only `id`, `displayName`, `avatarUrl`, `bio`, and `recipes`; it does not serialize email or roles. The separate recipe detail author shape contains only `id`, `displayName`, and `avatarUrl`. Recipe-by-slug resolves current active public author fields for both cache misses and hits, and caches a recipe detail with no author identity data. Create/update request contracts and Task 1 publication lifecycle behavior were not changed.

## Files changed

- Added `backend/src/CulinaryBlog.Application/DTOs/Authors/AuthorProfilePageDto.cs`.
- Added `backend/src/CulinaryBlog.Application/Features/Authors/GetAuthorProfile.cs`.
- Added `backend/src/CulinaryBlog.API/Endpoints/AuthorEndpoints.cs`; registered the endpoint in `Program.cs`.
- Added nullable `PublishedAt` to recipe summary/detail projections and a small allowlisted `RecipeAuthorDto`.
- Updated recipe list and repository search summary projections so they populate `PublishedAt`.
- Updated recipe-by-slug reads to resolve active public author fields after cache retrieval and before returning the response.
- Added `GetAuthorProfileTests`, `AuthorEndpointsTests`, and focused recipe-by-slug cache/author tests.
- Did not modify README, Task 1 lifecycle/migration code, package versions, or global warning configuration.

## TDD evidence

1. Added the author query and endpoint tests before production changes.
2. The first pre-implementation test run caught an ambiguous identity-user type in the new integration test; corrected the test fixture type alias before evaluating behavior.
3. Re-ran the focused integration tests before mapping the route:
   `dotnet test tests/CulinaryBlog.Integration.Tests/CulinaryBlog.Integration.Tests.csproj --filter FullyQualifiedName~AuthorEndpointsTests --no-restore '-p:WarningsNotAsErrors=NU1902%3BNU1903'`
   Result: **4 failed, 2 passed**. The active author request expected 200 and received 404; invalid pagination expected 400 and received 404. This was the expected red state because `/api/v1/authors/{id}` was not mapped. Missing/inactive cases already returned 404.
4. Implemented the DTO, validated query, endpoint, and recipe read projections. Iterated on the focused tests until green.

## Validation

- Application focused tests:
  `dotnet test tests/CulinaryBlog.Application.Tests/CulinaryBlog.Application.Tests.csproj --filter 'FullyQualifiedName~GetAuthorProfileTests|FullyQualifiedName~RecipePublishTests|FullyQualifiedName~GetRecipeBySlugQueryHandlerTests' --no-restore '-p:WarningsNotAsErrors=NU1902%3BNU1903'`
  **Passed: 14, failed: 0, skipped: 0.**
- Integration focused tests:
  `dotnet test tests/CulinaryBlog.Integration.Tests/CulinaryBlog.Integration.Tests.csproj --filter 'FullyQualifiedName~AuthorEndpointsTests|FullyQualifiedName~RecipeSearchEndpointsTests' --no-restore '-p:WarningsNotAsErrors=NU1902%3BNU1903'`
  **Passed: 17, failed: 0, skipped: 0.**
- API build:
  `dotnet build src/CulinaryBlog.API/CulinaryBlog.API.csproj --no-restore '-p:WarningsNotAsErrors=NU1902%3BNU1903'`
  **Succeeded: 0 errors.**
- `git diff --check`: passed.

Restore/test/build output continued to show the repository's existing NU1902 and NU1903 advisories for `System.Security.Cryptography.Pkcs` 10.0.0 (GHSA-2fmv-rv2c-59x7 and GHSA-846f-f764-76mx). Validation used only the documented command-scoped `WarningsNotAsErrors` exception; warnings remained visible, and package/project configuration was not changed.

## Coverage notes

Tests verify public-property allowlisting, published-only and author-scoped filtering, newest-first publication sorting, stable ID tie-breaking, historical null-date ordering, pagination metadata, invalid pagination, missing/inactive author handling, recipe summary/detail publication timestamps, and fresh public-author lookup on recipe detail cache hits. The cache-miss test also verifies that the cached detail contains no author identity payload.
