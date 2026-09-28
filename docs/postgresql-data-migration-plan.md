# PostgreSQL data migration plan: Application schema to AuthDbContext

**Status:** AuthDbContext migrations and source data import completed on 2026-09-28. PostgreSQL checks for Identity password validation/role membership, RowVersion concurrency, Category/Recipe soft delete, sequential step mutation and concurrent add/add plus add/delete passed. The imported account's password, backup restore and application cutover remain to be confirmed.
**Source:** existing PostgreSQL database `culinary_blog` using the legacy ApplicationDbContext schema.
**Target:** new PostgreSQL database `culinary_blog_auth`.
**Canonical context:** `AuthDbContext` and its existing migration chain.

## Goal and safety rules

Keep `culinary_blog` unchanged as the source and create a separate target using the current AuthDbContext model. Apply AuthDbContext migrations only to the empty target, then copy business data in a transaction after preflight checks pass. Do not copy `__EFMigrationsHistory`; the target records its own AuthDbContext migrations and the source retains its ApplicationDbContext history.

Do not start the import until a verified source backup exists, source/target connection strings point to different database names on the intended server, the target schema matches the AuthDbContext migration head, and every mapping preflight succeeds. Use environment variables or a local secret store for credentials; do not commit them or put them in command output.

## Schema and data mapping

| Source | Target | Transformation / check |
|---|---|---|
| `ApplicationUser` | `AspNetUsers` | Preserve user IDs and compatible Identity columns (`UserName`, normalized fields, email, password/security stamps, lockout and confirmation fields). Preserve `DisplayName`, `AvatarUrl`, `Bio`, `IsActive`, `CreatedAt`. Validate that every recipe `AuthorId` resolves to a source user. |
| `ApplicationUser.Role` | `AspNetRoles`, `AspNetUserRoles` | Create each distinct supported role once and associate users by ID. Normalize role names using ASP.NET Identity conventions. Abort on null/unknown role values; never silently promote a user. |
| `Categories` | `Categories` | Preserve ID, Name, Slug, Description, CreatedAt and UpdatedAt. Initialize target-only `ImageUrl` to null and `OrderIndex` to 0 when absent in source. Preserve IsDeleted if present; otherwise initialize false. Initialize RowVersion to the target default because this is a new database. |
| `Recipes` | `Recipes` | Preserve IDs, content, category/author IDs, times, servings, status, audit fields and soft-delete flag. Initialize RowVersion to the target default. Map Difficulty explicitly: Easy=1, Medium=2, Hard=3, Expert=4; abort on any other value. Omit the legacy nullable `ApplicationUserId` column; `AuthorId` is the canonical FK. |
| `RecipeIngredients` | `RecipeIngredients` | Preserve IDs, RecipeId, name, nullable quantity, unit, notes, order, audit/deletion fields. Initialize RowVersion to the target default. Validate recipe references and target length/precision constraints. |
| `RecipeSteps` | `RecipeSteps` | Preserve IDs, RecipeId, StepNumber, Title, Description, `DurationMinutes`, ImageUrl and audit/deletion fields. Initialize RowVersion to the target default. Validate unique `(RecipeId, StepNumber)` among active rows before insert. |
| `RecipeImages` | `RecipeImages` | Preserve rows and fields if present. Validate recipe references and target string lengths. |
| `RecipeNutritions` | `RecipeNutritions` | Preserve rows and fields if present. Validate recipe references, one nutrition row per recipe, and numeric precision. |
| `RefreshTokens` | `RefreshTokens` | Do not migrate by default. Existing refresh tokens are security credentials and should be revoked at cutover; users sign in again on the new target. |

Import order: roles, users, categories, recipes, then ingredients, steps, images and nutrition. The importer must preserve UUIDs and user IDs so foreign keys and recipe ownership remain stable.

## Execution steps

1. Record source counts, distinct role/difficulty values, FK orphans, max string lengths, numeric ranges, soft-deleted row counts, and active duplicate step numbers. Stop on any orphan, unsupported role/difficulty, invalid target length/precision, or active order collision.
2. Create and verify a PostgreSQL backup of `culinary_blog`; record the backup path and verify it can be listed/read.
3. Create an empty target database (suggested `culinary_blog_auth`) on the intended PostgreSQL instance. Verify the source and target database names differ.
4. Apply all pending migrations using `--context AuthDbContext` against the target only. Verify target migration history and required tables/indexes before importing.
5. Import in dependency order in one transaction (or a resumable, idempotent batch strategy with explicit checkpoints). Keep the source connection read-only. Do not copy migration history, test/demo seed data, or refresh tokens.
6. Validate target row counts by table, ID-set equality, FK integrity, category/recipe ownership, active step ordering, role memberships and mapped difficulty values. Verify all copied timestamps and soft-delete states.
7. Run application integration checks against the target for login, recipe reads, RowVersion concurrency, soft delete and concurrent step add/delete. Confirm existing password hashes authenticate before cutover; otherwise require password reset rather than copying/changing hashes blindly.
8. Point the application to the target only after validation. Keep the source unchanged and available for rollback until the new target is accepted.

## Required tooling before execution

Prepare a dedicated import command that accepts separate source and target connection strings from environment/secret storage. It must:

- reject identical source and target database names;
- default to read-only dry-run/preflight and require explicit `--apply` before writes;
- open the source read-only and target with writes enabled only after all preflight validations pass;
- print counts and mapping summaries without printing connection strings or secrets;
- fail closed on unsupported values, data truncation, missing references or target migration/schema mismatch;
- import in FK order with a transaction and provide a clear rollback on error;
- produce a post-import count/reference validation report.

The importer now exists at `backend/tools/CulinaryBlog.DataMigration`. By default it performs preflight only; `--apply` additionally requires `CULINARY_BLOG_SOURCE_BACKUP_VERIFIED=yes`. It reads source data in a read-only repeatable-read transaction and writes into a locked target transaction. Before committing, it checks counts, IDs and projected row values (including timestamps, soft-delete state, mapped difficulty/roles and target-only Category defaults). RowVersion columns are omitted on insert so the new database defaults initialize them.

### Operator commands

First create/verify the source backup in pgAdmin (or with an approved `pg_dump` setup), create an empty target database, then point `ConnectionStrings__Postgres` to the target and run:

```powershell
dotnet ef database update `
  --project backend/src/CulinaryBlog.Infrastructure `
  --startup-project backend/src/CulinaryBlog.API `
  --context AuthDbContext
```

Set separate source and target connection strings in the current PowerShell session using `ConnectionStrings__PostgresSource` and `ConnectionStrings__PostgresTarget`. Run preflight first:

```powershell
dotnet run --project backend/tools/CulinaryBlog.DataMigration
```

Only after confirming the backup and reviewing a successful preflight, run the import:

```powershell
$env:CULINARY_BLOG_SOURCE_BACKUP_VERIFIED = "yes"
dotnet run --project backend/tools/CulinaryBlog.DataMigration -- --apply
```

The utility never prints either connection string. Clear the temporary backup confirmation and connection string variables after the operation.

Implementation verification: `dotnet build backend/CulinaryBlog.sln --no-restore -m:1 -p:UseSharedCompilation=false` succeeded with 0 warnings/errors. The full solution test run passed 102 tests (Application 53, Integration 22, Architecture 3, DataMigration 24). Live PostgreSQL migration/import and targeted behavior results are recorded below.

Run the live PostgreSQL behavior verifier after pointing `CULINARY_BLOG_POSTGRES_VERIFY` at `culinary_blog_auth`:

```powershell
dotnet run --project backend/tools/CulinaryBlog.PostgresVerification
```

It refuses any database other than `culinary_blog_auth`, checks migration history and behavior using uniquely identified temporary Category/Recipe rows, then removes those rows.

Initial environment readiness on 2026-09-28: separate source/target connection variables were not configured, and `pg_dump`/`psql` were not available in PATH or the checked PostgreSQL install locations. The source/target credentials were therefore loaded from the legacy user setting for this run.

Follow-up diagnostic on 2026-09-28: the legacy user-scope connection was loaded into the current process as the source connection and used for a read-only preflight. PostgreSQL returned SQLSTATE `28P01` (invalid password authentication), so source validation stopped before any target access or writes. Update the stored credential and retry the preflight; the importer now reports SQLSTATE without exposing connection details.

Latest run on 2026-09-28: using the pgAdmin-confirmed IPv6 loopback endpoint (`::1:5432`), source validation completed. The user confirmed the source backup was created and `culinary_blog_auth` was created. EF applied the initial two AuthDbContext migrations (`20260921191147_Lab02PersonalInitialDatabase`, `20260928050453_AlignAuthDbContextRecipeSchema`). Importer preflight passed with 1 user, 20 categories, 100 recipes, 1,000 ingredients, 500 steps, 100 nutrition rows and 0 images; target was migrated and empty. The transactional import committed after count, ID and mapped-value comparisons passed. The source was read-only and unchanged. The backup artifact `dulieu.backup` was found in the project root and its `PGDMP` signature confirms PostgreSQL custom format. The user restored it with pgAdmin to `culinary_blog_backup_verify`. A read-only connection confirmed the restored schema contains `ApplicationUser`, `Categories`, `Recipes`, `RecipeIngredients`, `RecipeSteps`, `RecipeNutritions` and `RecipeImages`; counts were 1 user, 20 categories, 100 recipes, 1,000 ingredients, 500 steps, 100 nutrition rows and 0 images, matching the source preflight/import counts.

Post-import PostgreSQL checks found the mapped bytea `RowVersion` default did not change on update, so stale updates were accepted. Added and applied migration `20260928142200_AddPostgresRowVersionTriggers`, which refreshes RowVersion on updates to Categories, Recipes, RecipeIngredients, RecipeSteps, RecipeImages and RecipeNutritions. `backend/tools/CulinaryBlog.PostgresVerification` then passed against `culinary_blog_auth`: all three AuthDbContext migrations present, a temporary user registered and authenticated with its Author role, stale Category update rejected, Category and Recipe soft delete hidden by query filters and retained with `IsDeleted`, sequential recipe step add/delete mutations assign/compact numbers, two concurrent step additions receive distinct consecutive numbers, and a concurrent add/delete leaves active steps consecutive. The verifier removes all uniquely identified temporary rows. A Production API smoke test returned the paginated recipe list. It exposed a case-sensitive `sortBy` validator bug; the validator was fixed and regression tests added. Backup restore and expected row counts are verified. The imported user's actual password and application cutover remain follow-up steps.

Cutover run on 2026-09-28: the User-scope `ConnectionStrings__Postgres` now selects `culinary_blog_auth`, preserving the existing host and credentials. Since no `Jwt__Key` was configured, a random 512-bit signing key was generated into User-scope environment configuration (not written to the repository). The API is running in Production on `http://localhost:5000`, with memory cache for this local run and local frontend CORS origins. Smoke checks returned root status `running` and 100 total recipes (3 returned for page size 3). Production mode prevents startup migration or development seeding. The frontend dev server could not start because `next` is not available in the installed `frontend/node_modules`; test the imported account through the API login endpoint until frontend dependencies are restored. A read-only query confirmed the imported user's email, but this app has no forgot/reset-password endpoint; actual password login remains unverified. The source `culinary_blog` remains available for rollback. To roll back the connection setting, change only its `Database` value back to `culinary_blog` and restart the API.

## Acceptance criteria

- The original `culinary_blog` schema, migration history and row counts remain unchanged.
- The new database has all AuthDbContext migrations applied and matches `AuthDbContextModelSnapshot`.
- All in-scope source business rows are accounted for in the target, with documented mappings and no orphaned references.
- RowVersion concurrency, Category/Recipe soft delete, Identity role membership, sequential step order, concurrent adds and concurrent add/delete passed the targeted verifier on PostgreSQL. Backup restore and expected row counts are verified; imported-user password login remains unverified.
- Backup restore, target data checks and Production API smoke checks passed. The imported account's actual password still needs a user sign-in check; keep the source available through the rollback window.

## Current live-source facts

Read-only inspection on 2026-09-28 found 20 categories, 100 recipes, 500 recipe steps and one user. The source migration history contains `InitialRecipeSchema` and `AddRowVersionDefaults`; the user table is `ApplicationUser` with a `Role` column, while the target Identity model uses `AspNetUsers` and `AspNetRoles`. Source recipe difficulty is text and step duration is `DurationMinutes`. There were no duplicate active `(RecipeId, StepNumber)` groups. Other table counts and all per-value mapping checks must be collected by the importer preflight before any write.
