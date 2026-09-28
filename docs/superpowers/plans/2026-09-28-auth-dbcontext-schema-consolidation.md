# AuthDbContext Schema Consolidation Plan

> **For agentic workers:** Execute inline in the approved current branch. Preserve existing database rows and avoid destructive schema operations.

**Goal:** Make `AuthDbContext` the only runtime and migration owner for Identity and recipe/category data.

**Architecture:** `AuthDbContext` will implement `IApplicationDbContext`; runtime recipe handlers, advisory locks, and data seeders will use that context. Keep the old `ApplicationDbContext` temporarily only for existing tests/history, remove its production registration and design-time migration entry point, and clearly mark its migration chain as legacy. Add/repair the active `AuthDbContext` migration snapshot to match current entity configuration using forward, data-preserving operations.

**Tech Stack:** .NET 10, EF Core, Npgsql, ASP.NET Core Identity, PostgreSQL.

## Global Constraints

- Do not drop existing tables or columns as part of the consolidation.
- Do not apply migrations to PostgreSQL until credentials work and the target schema has been inspected.
- Keep existing unrelated FR-RCP-009/010 source changes intact.
- Do not run tests unless requested; verify compilation and EF model/migration state.

### Task 1: Unify production DbContext registration

**Files:** `AuthDbContext.cs`, `DependencyInjection.cs`, `PostgresRecipeMutationLock.cs`.

- [ ] Make AuthDbContext implement IApplicationDbContext and expose all recipe/category DbSets.
- [ ] Move shared BaseEntity timestamp/soft-delete/RowVersion save behavior into AuthDbContext.
- [ ] Register IApplicationDbContext to the scoped AuthDbContext; remove ApplicationDbContext production registration.
- [ ] Point recipe advisory locking at AuthDbContext so transaction and writes share the same context.

### Task 2: Align production data tooling

**Files:** `RandomDataSeeder.cs`, `Program.cs`, `ApplicationDbContextFactory.cs`, `AuthDbContextFactory.cs`.

- [ ] Move report seeding to AuthDbContext and IdentityUser/role semantics without a legacy `ApplicationUser.Role` column.
- [ ] Preserve current development startup migration ownership through AuthDbContext.
- [ ] Remove the ApplicationDbContext design-time factory so new migrations target AuthDbContext.

### Task 3: Reconcile AuthDbContext migrations

**Files:** `AuthDbContextModelSnapshot.cs`, new Auth migration, existing ApplicationDbContext migration files.

- [ ] Scaffold and review current AuthDbContext model drift.
- [ ] Keep additive table/index changes and safe renames; reject generated drops or data-truncating changes.
- [ ] Update the Auth snapshot to current model.
- [ ] Mark the old ApplicationDbContext migration chain as legacy and ensure documented commands target AuthDbContext.

### Task 4: Update documentation and verify

**Files:** `implementation_plan.md`, `docs/recipe-core-implementation-spec.md`, `README.md`.

- [ ] Document AuthDbContext as canonical and explain the legacy migration chain.
- [ ] Build the solution.
- [ ] Confirm `dotnet ef migrations has-pending-model-changes --context AuthDbContext` reports no model drift.
- [ ] Attempt no database migration until connection authentication works and the live schema is inspected.
