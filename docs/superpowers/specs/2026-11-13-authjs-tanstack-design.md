# Auth.js v5 and TanStack Query Design

**Status:** Approved design; implementation has not started.
**Date:** 2026-11-13

## Context

The Next.js 15 frontend currently calls the .NET API directly for authentication and stores backend access and refresh tokens in `sessionStorage` through `frontend/src/lib/auth-store.ts`. Authenticated Recipe, Category, and Profile calls attach a Bearer token in browser code. Most public listing and search screens load server state through component-level `useEffect`; Recipe detail and Author pages also have server-rendered fetches.

The backend remains the identity and authorization source. It accepts email/password at `/api/v1/auth/login`, Google Identity Services ID tokens at `/api/v1/auth/google`, issues 15-minute access tokens and one-use rotating refresh tokens, and protects private endpoints with Bearer authentication. Refresh tokens expire after seven days. The existing Google GIS button and .NET API contract are to be preserved.

## Goals

- Use Auth.js v5 for login, session management, refresh, and logout.
- Keep backend tokens inaccessible to browser JavaScript.
- Preserve email/password and the current Google GIS sign-in experience.
- Route authenticated frontend API calls through a same-origin Next.js Backend-for-Frontend (BFF).
- Use TanStack Query for client-managed server state on public pages, dashboards, and Profile.
- Preserve existing user-visible behavior, SEO metadata, Recipe JSON-LD, and .NET endpoint contracts.

## Non-goals

- Replacing Google GIS with Auth.js's Google OAuth redirect provider.
- Changing backend authentication endpoints, JWT semantics, or authorization rules.
- Migrating to Next.js 16 or adding an Auth.js database adapter.
- Replacing local form, file-preview, or modal state with TanStack Query.
- Replacing server-only sitemap, metadata, or other server-rendered fetches with client queries when they do not need client cache behavior.
- Supporting horizontally scaled Next.js instances in this change. The deployment target is one instance; scaling out requires revisiting refresh coordination.

## Authentication architecture

### Auth.js providers and session

Configure two Auth.js Credentials providers:

1. Email/password: `authorize` calls the backend `/api/v1/auth/login` using the server-only API URL.
2. Google GIS: the existing browser GIS callback supplies an `idToken` to the Credentials provider, whose `authorize` calls `/api/v1/auth/google`.

After successful authentication, the server retrieves `/api/v1/auth/me` using the newly issued backend access token and stores the minimum required identity/profile fields with the backend tokens in the Auth.js JWT. The Auth.js session callback returns only non-secret fields needed by the UI (user id, email, display name, avatar, and roles if needed for presentation). It must never return the backend access token or refresh token.

Use Auth.js's encrypted JWT session cookie with a seven-day maximum age to align with the backend refresh-token lifetime. The cookie remains `HttpOnly`, uses `SameSite=Lax`, and is `Secure` in production. `AUTH_SECRET` is required and must be provided outside source control. No database adapter or backend schema change is needed.

Sign-in remains on the existing custom page. The email form invokes Auth.js Credentials sign-in; the Google GIS callback invokes the Google-ID-token Credentials provider. Safe Auth.js credential error codes map to the current user-facing login messages, including invalid credentials and account lockout, without exposing tokens or server exceptions.

Registration, forgot-password, and reset-password remain public backend operations because they do not issue a session. Auth.js sign-out attempts the protected backend logout server-side using the access token as Bearer authorization and the refresh token in the request body, matching the existing API contract. If backend revocation fails, the user is still signed out, the failure is logged, and client query data is cleared.

### Refresh-token rotation

The backend access token lifetime is 900 seconds and its refresh token is one-use. Concurrent reuse of an old refresh token can revoke the token family. Auth.js documentation also identifies refresh-token rotation as a concurrency risk.

Refresh the backend token server-side from the Auth.js JWT callback before access-token expiration. Use a single-flight promise map keyed by a SHA-256 digest of the refresh token so concurrent requests in the single Next.js process share one rotation result. Do not log or expose the raw refresh token. On refresh failure, mark the Auth.js token/session as expired; authenticated BFF requests return 401 and the client signs out and clears private query data.

This coordination is process-local and is valid only for the agreed single-instance deployment. Before adding replicas, replace it with shared, atomic refresh coordination or a server-side token vault; do not rely on the in-process map across instances.

### BFF and authorization

Add a same-origin catch-all Route Handler under `frontend/src/app/api/backend/[...path]/route.ts`. It:

- Requires a valid Auth.js session on every request.
- Adds the backend `Authorization: Bearer ...` header only on the server.
- Forwards only allowlisted paths and methods for authenticated Recipe, Category, Profile, and password-change operations. Backend logout is called by the Auth.js server-side sign-out event, not exposed as a client proxy operation.
- Uses the fixed server-only `API_INTERNAL_URL`; it never accepts a destination host from the browser.
- Preserves request bodies, including multipart uploads, and forwards status and safe response headers/body without forwarding upstream cookies.
- Rejects unsupported paths/methods and checks the request Origin for state-changing methods.
- Does not cache authenticated responses.

Public API requests may continue to use the existing public API base URL. Private client API functions use the same-origin BFF and no longer accept or construct backend tokens.

Use Next.js 15 `middleware.ts` with Auth.js to redirect unauthenticated users from `/dashboard/*` and `/profile` to the login page while preserving a safe callback URL. The BFF and backend authorization checks remain mandatory; middleware is not the authorization boundary.

`API_INTERNAL_URL` is already configured for Docker Compose and Playwright. Add `AUTH_SECRET` to the documented local/Compose environment configuration. Keep `NEXT_PUBLIC_GOOGLE_CLIENT_ID` for the existing GIS button.

## TanStack Query architecture

### Provider and query policy

Add a client `Providers` component wrapping `SessionProvider` and `QueryClientProvider`, mounted from the root App Router layout. Use a stable browser QueryClient and a fresh server QueryClient if server prefetching is used. Set an explicit stale time for public queries, do not retry validation/authorization errors, and retry only a small number of transient network/5xx failures. Query functions consume TanStack's `AbortSignal`.

Centralize query keys by domain and include pagination, search/filter parameters, and authenticated user id where relevant. On authentication user change or sign-out, clear the QueryClient so data from one account cannot remain visible to another.

### Coverage

Use queries for client-managed server data on:

- Home recipes, public Recipe lists, search results, category lists, and category detail pages.
- Recipe detail client content, seeded from the existing server-rendered Recipe result so metadata and JSON-LD remain server-rendered.
- Author profile client content, seeded from the existing server-rendered result.
- Dashboard Recipe and Category lists, and Profile data.

Use mutations for Recipe create/update/delete/publish/archive, ingredient/step/image operations, Category create/update/delete/image upload, Profile update, and password change. Registration/forgot/reset may use mutation state for request/error handling but remain unauthenticated operations. Auth.js owns sign-in/sign-out rather than TanStack Query.

Keep local UI state (forms, selected files, image previews, editing flags, and modals) in React state or existing React Hook Form. Server-only metadata, sitemap, and other non-interactive server fetches remain server-side.

### Invalidation

- Recipe writes invalidate Recipe lists and the affected Recipe detail key(s); title/slug changes invalidate the detail namespace.
- Recipe image, step, and ingredient writes invalidate the affected detail and relevant lists.
- Category writes invalidate the category list/detail and recipe/search result keys that depend on category metadata.
- Profile writes update or invalidate the Profile key used by both Profile and the header.
- Sign-out or account change clears all query data, including private dashboard/Profile data.

Mutation errors continue to use the API Problem Details message where safe. Existing loading, error, empty, and success UX is preserved while query/mutation state replaces duplicated request state.

## Files and responsibilities

- `frontend/src/auth.ts`: Auth.js providers, callbacks, session shape, refresh coordination, and sign-out event.
- `frontend/src/app/api/auth/[...nextauth]/route.ts`: Auth.js handlers.
- `frontend/src/app/api/backend/[...path]/route.ts`: authenticated allowlisted BFF proxy.
- `frontend/src/middleware.ts`: UI route protection for Profile and Dashboard.
- `frontend/src/app/providers.tsx`: Auth.js and TanStack client providers and auth/query synchronization.
- `frontend/src/lib/auth-store.ts`: removed after all consumers migrate; no token state remains in browser storage.
- `frontend/src/lib/auth-api.ts` and `frontend/src/lib/recipes-api.ts`: separate public API calls from private same-origin BFF calls; private functions no longer accept tokens.
- `frontend/src/lib/queries/` (or equivalent existing lib structure): shared query keys and hooks for Recipe, Category, and Profile reads/mutations.
- Existing login, header, Profile, Dashboard, public list/search/category, and Recipe Editor components: consume Auth.js session and Query hooks without changing their intended UX.
- `frontend/e2e/support/mock-recipe-api.mjs` and affected Playwright specs: mock the server-side backend calls and verify cookies, BFF, refresh, and cache invalidation.
- `docs/recipe-core-implementation-spec.md`: document the resulting Auth.js/BFF and TanStack architecture after implementation.

## Error handling and security boundaries

- Backend validation and authorization errors retain their HTTP status and Problem Details payload through the BFF.
- Anonymous BFF requests return 401; backend resource authorization remains authoritative for owner/Admin decisions.
- Auth.js session errors do not include backend tokens. Failed refresh expires the UI session rather than silently using an invalid token.
- Logout removes the browser session even if backend revocation is unavailable; the failed revoke is logged.
- State-changing BFF requests require same-origin requests. Auth.js sign-in/sign-out use Auth.js's own CSRF protections.
- No token, password, Google ID token, or full credential object is logged.

## Verification plan

1. Unit-test credential authorization and token refresh/error mapping with mocked backend HTTP responses.
2. Test the BFF allowlist, anonymous 401 behavior, Origin checks, safe response forwarding, and server-side Bearer header.
3. E2E-test email login and mocked Google GIS login; verify the Auth.js cookie is HttpOnly and backend access/refresh tokens do not appear in `sessionStorage`, local storage, or the client-visible Session object.
4. E2E-test protected route redirects, refresh-and-retry, logout, and logout cleanup after a backend revoke failure.
5. E2E-test Recipe/Category/Profile mutations and query invalidation, including clearing private queries after account change.
6. Run frontend type-check/build/lint and the affected Playwright suites. No .NET contract change is expected; run existing backend tests as regression coverage.

## Dependencies and operational assumptions

Use `next-auth@5.0.0-beta.32` (Auth.js v5) and `@tanstack/react-query@5.107.1`, the published versions checked on 2026-11-13. No Google OAuth client secret is needed for the retained GIS ID-token flow. `AUTH_SECRET` is a deployment secret and must not be committed.

The BFF assumes the existing `API_INTERNAL_URL` runtime setting; local development outside Compose must point it at the API host. The approved deployment is one Next.js instance. Any future horizontal scaling requires revisiting refresh-token single-flight coordination before rollout.
