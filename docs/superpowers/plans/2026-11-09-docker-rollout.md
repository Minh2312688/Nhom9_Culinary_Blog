# Culinary Blog Docker Rollout Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Rebuild and run the completed category media, recipe media, and nutrition changes in Docker, then remove only obsolete Culinary Blog images.

**Architecture:** Reuse the existing API and frontend Dockerfiles and Compose sample-data mount. Build the two application images, start the stack without deleting volumes, verify seeded data and media, and remove only verified stale project-owned image IDs.

**Tech Stack:** Docker Compose, .NET 10 API, Next.js 15 frontend, PostgreSQL, MinIO.

**Spec:** [2026-11-09-recipe-category-media-and-nutrition-design.md](../specs/2026-11-09-recipe-category-media-and-nutrition-design.md)

## Global Constraints

- Do not remove PostgreSQL, Redis, MinIO, or Seq volumes during container rebuild/recreation.
- Once Docker is available, rebuild API and frontend images, start Compose without `down -v`, and verify service health, sample records, and image delivery from MinIO.
- Remove only stale/dangling Culinary Blog project images after the rebuilt stack is healthy.
- Keep active container images, third-party images, and all volumes; do not run a global prune.

---

### Task 1: Wait for Docker and rebuild application containers safely

**Files:**
- No source file changes; use the existing `docker-compose.yml`, `backend/Dockerfile`, and `frontend/Dockerfile`.

**Interfaces:**
- Uses current Compose service names `api`, `frontend`, `postgres`, `redis`, `minio`, `mailhog`, and `seq`.
- Public ports remain frontend `3000`, API `5000`, PostgreSQL `5433`, and MinIO `9000/9001`.

- [ ] **Step 1: Confirm Docker daemon availability**

Run: `docker info`

Expected: Docker Engine returns successfully. If the daemon is still stopped, stop rollout here and ask the user to start Docker Desktop; do not attempt destructive cleanup.

- [ ] **Step 2: Check Compose configuration before mutation**

Run: `docker compose config --quiet`

Expected: zero exit code with no configuration errors.

- [ ] **Step 3: Build and start without removing data**

Run: `docker compose up -d --build`

Do not run `docker compose down -v`, `docker volume rm`, `docker system prune`, or `docker image prune -a`.

- [ ] **Step 4: Wait for health and verify public ports**

Run: `docker compose ps`

Expected: API, PostgreSQL, Redis, and MinIO are healthy; frontend is running on port `3000`. If API is not healthy, inspect `docker compose logs --tail 100 api`; for other services use the exact service name shown by `docker compose ps`. Resolve health failures before image cleanup.

### Task 2: Verify data and image delivery, then remove stale project images

**Files:**
- No source file changes.

**Interfaces:**
- API list and detail responses contain category `imageUrl`, recipe `nutrition`, and recipe `images`.
- Category and recipe image URLs resolve through the Compose MinIO public URL.

- [ ] **Step 1: Verify seeded recipe and category records**

Run:

```powershell
$recipes = Invoke-RestMethod -Uri "http://localhost:5000/api/v1/recipes/?page=1&pageSize=50"
$categories = Invoke-RestMethod -Uri "http://localhost:5000/api/v1/categories/?page=1&pageSize=100"
if ($recipes.totalCount -ne 20) { throw "Expected 20 sample recipes; found $($recipes.totalCount)." }
foreach ($item in $recipes.items) {
  $detail = Invoke-RestMethod -Uri "http://localhost:5000/api/v1/recipes/$($item.slug)"
  if ($null -eq $detail.nutrition) { throw "Recipe $($item.slug) has no nutrition." }
  foreach ($field in @("calories", "protein", "carbohydrates", "fat", "fiber", "sodium")) {
    if ($null -eq $detail.nutrition.$field) { throw "Recipe $($item.slug) is missing $field." }
  }
}
$categories.items | Where-Object { $_.imageUrl } | Select-Object name, slug, imageUrl
```

Do not print or expose `.env` values.

- [ ] **Step 2: Verify a real image response**

From the detail response, select a primary recipe image URL; from the categories response, select a non-empty `imageUrl`. Request both URLs and confirm HTTP 200 with an image content type. Open `http://localhost:3000/recipes` and a recipe detail page to confirm the hero image is first and secondary images follow nutrition.

- [ ] **Step 3: Identify image IDs in use**

Record image IDs for running containers:

```powershell
$activeImageIds = docker ps --format '{{.ID}}' | ForEach-Object {
  docker inspect --format '{{.Image}}' $_
} | Sort-Object -Unique
```

List project-tagged image candidates and inspect their Compose project labels. Do not infer project ownership from an untagged/dangling image alone.

- [ ] **Step 4: Remove only verified obsolete project-owned image IDs**

Initialize an empty list and add an ID only after verifying it belongs to this project and is not used by any running or stopped container:

```powershell
$staleProjectImageIds = @()
# Add only individually verified image IDs to this list.
foreach ($imageId in $staleProjectImageIds) {
  if ($activeImageIds -contains $imageId) { throw "Refusing to remove an active image: $imageId" }
  docker image rm $imageId
  if ($LASTEXITCODE -ne 0) { throw "Failed to remove verified stale image: $imageId" }
}
```

Leave active images, images with uncertain ownership, third-party images, containers, and volumes untouched.

- [ ] **Step 5: Confirm final runtime and data persistence**

Run `docker compose ps` and confirm API/frontend remain running. Confirm the named Compose volumes still exist with `docker volume ls`; do not recreate or delete them.
