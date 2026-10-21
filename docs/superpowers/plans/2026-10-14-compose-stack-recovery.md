# CulinaryBlog Compose Stack Recovery Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Run the CulinaryBlog frontend, API, PostgreSQL, Redis, and MinIO together in Docker Compose with verified API/data connectivity.

**Architecture:** Keep the existing Clean Architecture and Compose service DNS. Confirm `Program.cs` middleware order and mappings, correct only reproducible defects, reset only this Compose project's PostgreSQL and MinIO data, then apply the existing migration and seed path.

**Tech Stack:** .NET 10 Minimal API, EF Core, PostgreSQL 16, Next.js 15, Docker Compose, MinIO, Redis, Playwright.

**Spec:** `docs/superpowers/specs/2026-10-14-compose-stack-recovery-design.md`

## Global Constraints

- Docker Compose is the sole runtime path for this recovery.
- Keep exactly one authentication/authorization middleware pair after rate limiting and before endpoint execution; keep `MapRecipeImageEndpoints()`.
- Frontend API URL is `http://localhost:5000`; in Compose the API uses service DNS names `postgres`, `minio`, and `redis`.
- PostgreSQL remains on container port `5432` and publishes to host port `5433`.
- Delete only verified CulinaryBlog PostgreSQL and MinIO data volumes; preserve Redis and all unrelated resources.
- Do not expose `.env` values or overwrite unrelated worktree changes.
- Change code only to fix a reproduced build, test, or runtime failure.
- NuGet currently reports NU1902/NU1903 for `System.Security.Cryptography.Pkcs` 10.0.0. Keep the repository's default warnings-as-errors policy; for validation only, restore and build in the same PowerShell session with `'-p:WarningsNotAsErrors=NU1902%3BNU1903'` on each command. The audit advisories must remain visible; do not disable NuGet audit or change package versions as part of this recovery.

---

### Task 1: Verify API setup

**Files:**
- Inspect/modify only if a reproducible defect requires it: `backend/src/CulinaryBlog.API/Program.cs`
- Existing tests: `backend/tests/CulinaryBlog.Application.Tests/`, `backend/tests/CulinaryBlog.Integration.Tests/`, `backend/tests/CulinaryBlog.ArchitectureTests/`

- [ ] Check `git status --short`; preserve existing edits.
- [ ] Confirm one `UseAuthentication()` and one `UseAuthorization()`, ordered after `UseRateLimiter()` and before mapped endpoints; confirm recipe image endpoint is mapped.
- [ ] Restore and build API with the scoped audit-warning exception above; build: `dotnet build backend/src/CulinaryBlog.API/CulinaryBlog.API.csproj --no-restore '-p:WarningsNotAsErrors=NU1902%3BNU1903'`.
- [ ] If build fails, diagnose the exact compiler error and add/fix only the regression test and code needed for its root cause.
- [ ] Verify API build succeeds; do not make speculative middleware changes.

### Task 2: Verify frontend-to-service configuration

**Files:**
- Inspect/modify only if a mismatch is reproduced: `docker-compose.yml`, `frontend/src/lib/api.ts`, `frontend/src/lib/auth-api.ts`, `frontend/src/lib/recipes-api.ts`, `docs/run-project.md`

- [ ] Run `docker compose config --quiet` from the repository root; do not print resolved secrets.
- [ ] Confirm frontend build argument resolves to `http://localhost:5000`; confirm API Compose settings use `postgres:5432`, `minio:9000`, and `redis:6379`.
- [ ] Correct only a demonstrated endpoint, origin, or environment-variable mismatch; keep all three frontend API clients consistent.
- [ ] Keep the PostgreSQL container port at `5432` while publishing host port `5433` to avoid the verified host port conflict; update the local welcome-email script and host connection instructions accordingly.
- [ ] Verify with `docker compose config --quiet` and `Set-Location frontend; npm run build`.

### Task 3: Reset only approved data

**Files/resources:**
- Docker Compose project containers and volumes only; no source files.

- [ ] Record the Compose project name using `docker compose ls` and inspect service/container Compose labels.
- [ ] Inspect only PostgreSQL and MinIO mounts with `docker inspect`; resolve their exact named volumes.
- [ ] Verify both volume labels identify this repository's Compose project and their service ownership is PostgreSQL/MinIO.
- [ ] If a container/volume is not unambiguously owned by this project, stop and ask; never substitute global prune or remove Redis.
- [ ] Stop this Compose stack without removing volumes: `docker compose down`.
- [ ] Remove only the two verified PostgreSQL and MinIO volumes by their exact names with `docker volume rm <verified-postgres-volume> <verified-minio-volume>`.
- [ ] Verify only those exact volumes are absent.

### Task 4: Recreate and run the integrated stack

**Files/resources:**
- Docker Compose services: `postgres`, `redis`, `minio`, `mailhog`, `api`, `frontend`

- [ ] Start/rebuild the complete stack: `docker compose up --build -d`.
- [ ] Check `docker compose ps` and logs; wait for PostgreSQL health, MinIO readiness, API migration/seed completion, and frontend readiness.
- [ ] If startup fails, capture the relevant service logs and fix only the diagnosed cause; do not clear any other volume.
- [ ] Verify `curl.exe --fail http://localhost:5000/` and `curl.exe --fail http://localhost:3000/`.
- [ ] Verify a recipe list/search API request reaches PostgreSQL and returns the documented paginated response; verify MinIO endpoint/bucket initialization from API logs.

### Task 5: Run tests and document the result

**Files:**
- Modify `docs/run-project.md` only if the verified commands or wiring changed.

- [ ] Run backend tests with `dotnet test backend/CulinaryBlog.sln --no-restore --maxcpucount:1`; ensure output reports executed tests, not merely a successful build.
- [ ] If the test runner is not restored, check available disk space, restore only declared solution dependencies with `dotnet restore backend/CulinaryBlog.sln`, then rerun tests. Do not delete caches automatically.
- [ ] Run frontend Playwright suite against the development server: `Set-Location frontend; npx playwright test e2e/srs-routes.spec.ts`.
- [ ] Record passed, failed, skipped, or environment-blocked checks distinctly; run `git diff --check`.
- [ ] Leave Compose running if all services start; report URLs and `docker compose down` as the stop command. Do not commit unless explicitly requested.
