# PostgreSQL Data Import Implementation Plan

> **For agentic workers:** Execute this plan task by task and update the checkboxes as work is verified.

**Goal:** Add a safe one-shot importer that copies the populated ApplicationDbContext database into a separately migrated AuthDbContext database.

**Architecture:** A small .NET console tool reads the source through a read-only Npgsql connection, validates the source values and the empty migrated target, then writes all in-scope tables in FK order in one target transaction. Pure role/difficulty mapping rules have unit tests; database execution requires separate source and target connection strings and never alters the source.

**Tech Stack:** .NET 10, Npgsql, xUnit, PostgreSQL, existing EF Core AuthDbContext migration chain.

**Spec:** `docs/postgresql-data-migration-plan.md`

## Global Constraints

- `AuthDbContext` is the production schema and migration context.
- Keep the source `culinary_blog` read-only and unchanged.
- Never log connection strings, passwords, password hashes, tokens, or personal data.
- Refuse identical source/target database names and refuse a non-empty target.
- Do not copy `__EFMigrationsHistory` or refresh tokens.

---

### Task 1: Add and test deterministic value mappings

**Files:**
- Create: `backend/tools/CulinaryBlog.DataMigration/CulinaryBlog.DataMigration.csproj`
- Create: `backend/tools/CulinaryBlog.DataMigration/ImportMappings.cs`
- Create: `backend/tests/CulinaryBlog.DataMigration.Tests/CulinaryBlog.DataMigration.Tests.csproj`
- Create: `backend/tests/CulinaryBlog.DataMigration.Tests/ImportMappingsTests.cs`
- Modify: `backend/CulinaryBlog.sln`

**Interfaces:**
- Produces public `ImportMappings.ParseDifficulty(string?) -> int` mapping Easy/Medium/Hard/Expert to 1/2/3/4, throwing on null or unsupported values.
- Produces public `ImportMappings.NormalizeRole(string?) -> string` accepting only `Author` and `Admin`, returning canonical casing and throwing on null/unsupported values.

- [x] Write xUnit tests for each supported difficulty and for null/unknown difficulty rejection.
- [x] Write xUnit tests for case-insensitive supported role names and null/unknown role rejection.
- [x] Run the new test project and confirm expected compilation/test failures before implementation.
- [x] Implement only the explicit mappings and useful error messages that omit source row personal data.
- [x] Run the test project and confirm all mapping tests pass (22/22).

### Task 2: Implement read-only preflight and safe target guard

**Files:**
- Create: `backend/tools/CulinaryBlog.DataMigration/Program.cs`
- Create: `backend/tools/CulinaryBlog.DataMigration/DatabasePreflight.cs`
- Modify: `backend/CulinaryBlog.sln`

**Interfaces:**
- Consume environment variables `ConnectionStrings__PostgresSource` and `ConnectionStrings__PostgresTarget`; default to dry-run and require `--apply` before target writes.
- `DatabasePreflight.ValidateAsync(source, target)` rejects identical database names, reads source in a PostgreSQL read-only repeatable-read transaction, confirms required source columns/tables, checks role/difficulty values, FK orphans, length/precision constraints, and active step uniqueness, then confirms the target has the expected AuthDbContext migration IDs and no business rows.

- [x] Write focused tests for database-name equality, backup confirmation, category defaults and unsupported mapping values using pure guard functions.
- [x] Run those tests and confirm the expected failures.
- [x] Implement connection string presence checks without echoing values.
- [x] Make dry-run the default; require explicit `--apply` and a verified backup before opening a target write transaction.
- [x] Implement schema/data preflight; stop before writes on unsupported values, missing FKs, duplicate active step order, invalid target lengths/precision, or a non-empty target.
- [x] Run the importer unit tests (24/24); live preflight awaits a migrated target database.

### Task 3: Import business data transactionally

**Files:**
- Create: `backend/tools/CulinaryBlog.DataMigration/DataImporter.cs`
- Modify: `backend/tools/CulinaryBlog.DataMigration/Program.cs`
- Modify: `backend/tests/CulinaryBlog.DataMigration.Tests/ImportMappingsTests.cs`

**Interfaces:**
- `DataImporter.ImportAsync(source, target)` assumes successful preflight, opens a source read-only snapshot and a target transaction, inserts roles/users/categories/recipes/ingredients/steps/images/nutrition in FK order, and leaves refresh tokens and migration history out.
- It preserves IDs and timestamps, maps user roles and recipe difficulty, initializes target-only Category columns and RowVersion defaults, and rolls back the target transaction on any failure.

- [x] Add tests for Category defaults and explicit difficulty/role mapping; RowVersion values are omitted so database defaults are applied.
- [x] Implement Npgsql binary COPY with fixed table/column names; no source values are interpolated into SQL.
- [x] Preserve source rows; source connection uses a read-only transaction and no source write statement exists.
- [x] Add target-side count and ID reconciliation before committing; PostgreSQL FKs and unique indexes enforce relational constraints.
- [x] Run importer unit tests (24/24) and full solution tests (96/96); build solution with single-node compilation (0 warnings, 0 errors).

### Task 4: Document operations and verify against PostgreSQL when target is available

**Files:**
- Modify: `docs/postgresql-data-migration-plan.md`
- Modify: `implementation_plan.md`
- Modify: `docs/recipe-core-implementation-spec.md`
- Modify: `docs/recipe-management-fr009-fr010-spec.md`

- [x] Document backup/migration/import ordering, required environment variables, dry-run/preflight and apply command without credentials.
- [ ] If both connection strings are configured and target backup/migration preconditions are met, run the importer and reconcile table counts/IDs/values.
- [ ] Verify login/role mapping, RowVersion behavior, soft delete and step ordering on the new PostgreSQL target.
- [ ] Mark only evidenced steps complete; keep target creation/import/database verification pending if connection settings or backup are unavailable.
