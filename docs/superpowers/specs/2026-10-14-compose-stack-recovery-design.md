# CulinaryBlog Compose stack recovery

## Goal

Make `Program.cs`, the frontend, API, PostgreSQL, and MinIO work as one
consistent local Docker Compose stack, then verify the running application.

## Decisions

- Docker Compose is the sole runtime path for this recovery.
- `Program.cs` has one authentication/authorization middleware pair, after rate
  limiting and before endpoint execution. Keep the existing image endpoint
  mapping. Change code only to fix a reproduced build, test, or runtime failure.
- Frontend requests the API at `http://localhost:5000`. Within Compose, the API
  uses service DNS names `postgres`, `minio`, and `redis`, not host loopback.
- PostgreSQL remains on container port `5432` and publishes as host port
  `5433` to avoid stopping the unrelated local PostgreSQL using host port 5432.
- Recreate PostgreSQL and MinIO with clean data as explicitly requested. Before
  deletion, verify Compose project labels and resolved volume names; remove only
  this project's PostgreSQL and MinIO data volumes. Preserve Redis and all
  unrelated containers, databases, and volumes.
- Start the stack with Compose; API Development startup applies EF migrations
  and configured seed data. Do not introduce schema or business-logic changes
  unless testing demonstrates they are needed.

## Execution and acceptance

1. Confirm repository changes, active Compose project, exact target containers
   and volumes, port conflicts, and `.env` presence without displaying secrets.
2. Validate Compose configuration. If required settings are invalid, stop and
   report the specific missing setting rather than inventing credentials.
3. Recreate only CulinaryBlog PostgreSQL and MinIO data; start the full stack.
4. Verify PostgreSQL and MinIO readiness, API root/health response, frontend
   HTTP response, migration/seed logs, and recipe-image service wiring.
5. Run relevant backend and frontend tests/builds that the machine can support;
   distinguish passed, failed, skipped, and blocked tests.
6. Leave the full Compose stack running only if all startup prerequisites pass;
   report URLs, remaining issues, and exact stop command.

## Safety boundaries

- PostgreSQL and MinIO data loss is intentional and approved for this project.
- Never run global prune, delete unrelated Docker resources, remove Redis data,
  expose `.env` values, or overwrite unrelated worktree changes.
- If service labels or volume ownership do not unambiguously identify
  CulinaryBlog resources, stop before deletion and ask.
