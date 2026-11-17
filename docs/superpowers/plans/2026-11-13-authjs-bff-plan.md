# Auth.js v5 and Secure BFF Implementation Plan

> **Trạng thái hiện tại (17/11/2026):** Auth.js v5 session, Credentials/GIS
> sign-in flow, same-origin authenticated BFF, server-only `API_INTERNAL_URL`
> và server-side token lifecycle đã được triển khai theo design. Giới hạn
> single Next.js instance vẫn còn; dual-key API JWT rotation chưa được hỗ trợ.
> Các validation results trong plan là lịch sử, không chạy lại trong lần cập
> nhật này.

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace browser-held backend tokens with Auth.js v5 sessions and a protected same-origin BFF while preserving current login, Google GIS, and authenticated application flows.

**Architecture:** Auth.js Credentials providers call the existing .NET email/password and Google ID-token endpoints from the Next.js server. Backend access/refresh tokens stay in the encrypted Auth.js JWT cookie; all authenticated browser API operations use an allowlisted Next.js BFF that attaches the Bearer token server-side.

**Tech Stack:** Next.js 15 App Router, Auth.js v5 (`next-auth@5.0.0-beta.32`), TypeScript, Playwright, existing .NET API.

**Spec:** [../specs/2026-11-13-authjs-tanstack-design.md](../specs/2026-11-13-authjs-tanstack-design.md)

## Global Constraints

- Keep Next.js at version 15; do not upgrade to Next.js 16.
- Use `next-auth@5.0.0-beta.32`; keep the backend API contract unchanged.
- Keep backend tokens only in the encrypted Auth.js JWT cookie; never expose them through the Auth.js Session, browser storage, or browser-visible API responses.
- Use a seven-day Auth.js session maximum age; backend access tokens last 900 seconds and refresh tokens last seven days.
- Keep the Auth.js cookie `HttpOnly` and `SameSite=Lax`; mark it `Secure` in production.
- Keep Google Identity Services ID-token login; do not replace it with an OAuth redirect provider.
- Use server-only `API_INTERNAL_URL` and an explicit path/method allowlist for authenticated proxy calls.
- Require a valid session on every BFF request and same-origin Origin validation on state-changing BFF requests.
- The deployment target is one Next.js instance; refresh single-flight coordination is process-local.
- Keep the backend authoritative for authentication and resource authorization.
- Do not log passwords, Google ID tokens, access tokens, or refresh tokens.

## File map

- `frontend/src/auth.ts`: Auth.js providers, callbacks, session fields, refresh handling, and sign-out event.
- `frontend/src/lib/server/auth-backend.ts`: server-only backend auth requests and single-flight refresh coordination.
- `frontend/src/types/next-auth.d.ts`: typed non-secret Session fields and private JWT token fields.
- `frontend/src/app/api/auth/[...nextauth]/route.ts`: Auth.js GET/POST handlers.
- `frontend/src/app/api/backend/[...path]/route.ts`: authenticated allowlisted BFF proxy.
- `frontend/src/middleware.ts`: login redirects for Profile and Dashboard routes.
- `frontend/src/app/providers.tsx` and `frontend/src/app/layout.tsx`: Auth.js SessionProvider wiring; the later Query plan extends this provider.
- `frontend/src/lib/auth-store.ts`, `auth-api.ts`, and `recipes-api.ts`: remove browser-token APIs and route private requests through the BFF.
- Login, header, Profile, Dashboard, and Recipe Editor pages: use Auth.js session and BFF-backed API functions while retaining existing local form/file state.
- `frontend/e2e/support/mock-recipe-api.mjs` and `frontend/e2e/support/auth.ts`: emulate server-side auth/API calls and provide a shared browser sign-in helper.
- Existing authentication, Recipe CRUD, Category image, route, brand, and UI Playwright specs: replace sessionStorage seeding and direct-backend interception.
- `frontend/.env.example`, root `.env.example`, `docker-compose.yml`: document and pass `AUTH_SECRET`.
- `docs/recipe-core-implementation-spec.md`: document the resulting Auth.js and BFF flow.

---

### Task 1: Bootstrap Auth.js and runtime configuration

**Files:**
- Modify: `frontend/package.json`
- Modify: `frontend/package-lock.json`
- Create: `frontend/src/auth.ts`
- Create: `frontend/src/app/api/auth/[...nextauth]/route.ts`
- Create: `frontend/src/app/providers.tsx`
- Modify: `frontend/src/app/layout.tsx`
- Modify: `frontend/.env.example`
- Modify: `.env.example`
- Modify: `docker-compose.yml`
- Modify: `frontend/playwright.config.ts`
- Test: `frontend/e2e/auth-login.spec.ts`

**Interfaces:**
- Produces `auth`, `handlers`, `signIn`, and `signOut` from `frontend/src/auth.ts`.
- Produces a root `Providers` client component that currently wraps `SessionProvider`; the TanStack Query plan will extend it with `QueryClientProvider`.
- Requires `AUTH_SECRET` at runtime and leaves `API_INTERNAL_URL` server-only.

- [ ] **Step 1: Add a failing Auth.js handler smoke test**

Add a Playwright test that requests `/api/auth/session` and expects status 200 with no `accessToken` or `refreshToken` properties in the response JSON.

```ts
test("Auth.js session endpoint is mounted without exposing backend tokens", async ({ request }) => {
  const response = await request.get("/api/auth/session");
  expect(response.status()).toBe(200);
  const session = await response.json();
  expect(session).not.toHaveProperty("accessToken");
  expect(session).not.toHaveProperty("refreshToken");
});
```

- [ ] **Step 2: Run the test and confirm it fails before Auth.js is mounted**

Run from `frontend`: `npx playwright test e2e/auth-login.spec.ts --grep "Auth.js session endpoint"`

Expected: FAIL because `/api/auth/session` is not served by an Auth.js handler yet.

- [ ] **Step 3: Install and mount Auth.js**

Run from `frontend`: `npm install next-auth@5.0.0-beta.32`. Create the Auth.js route that exports `GET` and `POST` from `handlers`. Configure `session.strategy` as `"jwt"`, `session.maxAge` as `7 * 24 * 60 * 60`, and set the secret from `process.env.AUTH_SECRET`. Keep the Auth.js cookie `HttpOnly` and `SameSite=Lax`; verify it is `Secure` under `NODE_ENV=production`. Add `Providers` around the existing `FeedbackProvider` content in the root layout.

Add a non-secret example value to both environment examples, add `API_INTERNAL_URL=http://localhost:5000` to `frontend/.env.example`, and pass `AUTH_SECRET: ${AUTH_SECRET}` to the frontend service in Compose. Set a test-only `AUTH_SECRET` in the Playwright Next.js webServer environment. Document generating a local value with:

```powershell
node -e "console.log(require('crypto').randomBytes(32).toString('base64'))"
```

- [ ] **Step 4: Run the smoke test and frontend build**

Run: `npx playwright test e2e/auth-login.spec.ts --grep "Auth.js session endpoint"` and `npm run build`.

Expected: the session route returns 200, contains no backend token properties, and the production build exits with code 0.

---

### Task 2: Implement Credentials providers and server-side token lifecycle

**Files:**
- Modify: `frontend/src/auth.ts`
- Create: `frontend/src/lib/server/auth-backend.ts`
- Create: `frontend/src/types/next-auth.d.ts`
- Modify: `frontend/e2e/support/mock-recipe-api.mjs`
- Create: `frontend/e2e/support/auth.ts`
- Modify: `frontend/e2e/auth-login.spec.ts`

**Interfaces:**
- `auth-backend.ts` provides server-only login, Google login, profile lookup, refresh, and logout functions.
- Auth.js Credentials provider IDs are `email-password` and `google-id-token`.
- The Session exposes only `user.id`, `email`, `name`, `image`, `roles`, and a non-secret session error code when needed; JWT token fields for access token, refresh token, and expiry remain server-only.
- `frontend/e2e/support/auth.ts` exports `signInAs(page: Page, email = "chef@example.com"): Promise<void>`, which signs in through the UI and waits for the saved callback URL or `/`.

- [ ] **Step 1: Add failing email/GIS login and token-isolation tests**

Extend `auth-login.spec.ts` with successful email and Google GIS login tests, failed login cases for 401 and 423, and expired-access-token success/failure tests. Successful login sets an Auth.js session cookie marked `httpOnly` and `SameSite=Lax`; in both successful login tests assert that neither browser storage has `culinary_access_token` or `culinary_refresh_token`, and that `/api/auth/session` contains neither token string. For the expired-token account, make the mock login return `expiresIn: 0`, issue two concurrent `/api/auth/session` requests with the cookie jar, and assert the mock API records exactly one refresh request and returns rotated credentials only server-side. Configure another account's refresh to return 401 and assert its next private BFF call returns 401.

```ts
const cookie = (await page.context().cookies()).find((item) => item.name.includes("session-token"));
expect(cookie?.httpOnly).toBe(true);
expect(cookie?.sameSite).toBe("Lax");
expect(await page.evaluate(() => sessionStorage.getItem("culinary_access_token"))).toBeNull();
expect(await page.evaluate(() => sessionStorage.getItem("culinary_refresh_token"))).toBeNull();
expect(await page.evaluate(() => localStorage.getItem("culinary_access_token"))).toBeNull();
expect(await page.evaluate(() => localStorage.getItem("culinary_refresh_token"))).toBeNull();
```

- [ ] **Step 2: Run the auth flow tests and confirm they fail against the current client-token flow**

Run: `npx playwright test e2e/auth-login.spec.ts`.

Expected: the new cookie/session assertions fail because the current login writes tokens to sessionStorage and does not create an Auth.js session.

- [ ] **Step 3: Add server-only backend auth functions and typed session fields**

Implement email/password and Google ID-token POSTs to `/api/v1/auth/login` and `/api/v1/auth/google`, then fetch `/api/v1/auth/me` with the returned access token. Keep raw tokens out of errors and logs. In the JWT callback, store the tokens and `Date.now() + expiresIn * 1000` expiry with a 30-second refresh skew.

Before expiry, refresh using `/api/v1/auth/refresh`. Coordinate concurrent refreshes with one in-process promise per SHA-256 digest of the refresh token. Replace both tokens atomically from the refresh response. A refresh error marks the JWT/session expired. The Auth.js sign-out event calls `/api/v1/auth/logout` with the current access token as Bearer authorization and the refresh token in the JSON body; log a revoke failure without preventing local sign-out.

- [ ] **Step 4: Extend the mock API for login, Google login, profile, refresh, and logout**

Make the mock server return deterministic test-only access/refresh tokens and profile data for both login routes. Return 423 for a locked test account, return `expiresIn: 0` for `expired-chef@example.com`, and configure refresh to return either rotated tokens or 401 based on the test account. A logout response can be configured to return 204 or 503 for the revoke-failure test.

- [ ] **Step 5: Add the reusable Playwright sign-in helper**

Implement `signInAs` by navigating to `/auth/login`, filling the email and password labels, submitting the form, and waiting for the returned path. Use the same mock account by default and accept an email parameter so later query tests can switch users.

- [ ] **Step 6: Run the auth flows**

Run: `npx playwright test e2e/auth-login.spec.ts`.

Expected: email and Google GIS login both establish an HttpOnly/SameSite=Lax Auth.js cookie, expose only profile fields through `/api/auth/session`, and leave browser storage free of backend tokens; invalid/locked credentials retain their existing Vietnamese messages; concurrent expired-session requests cause one refresh; a rejected refresh expires the session.

---

### Task 3: Add the authenticated allowlisted BFF and route guard

**Files:**
- Create: `frontend/src/app/api/backend/[...path]/route.ts`
- Create: `frontend/src/middleware.ts`
- Modify: `frontend/src/lib/server/auth-backend.ts`
- Modify: `frontend/e2e/support/mock-recipe-api.mjs`
- Create: `frontend/e2e/backend-bff.spec.ts`

**Interfaces:**
- Browser private calls use `/api/backend/{resource}`; the proxy maps allowlisted resources to `/api/v1/{resource}` under the fixed `API_INTERNAL_URL`.
- Export `GET`, `POST`, `PUT`, `PATCH`, and `DELETE` handlers that share one proxy function and a path/method allowlist.
- Middleware redirects anonymous users from `/dashboard/:path*` and `/profile` to `/auth/login?callbackUrl=...`; the BFF still authenticates every request.

- [ ] **Step 1: Add failing BFF tests**

Add Playwright API tests that verify: an anonymous `GET /api/backend/recipes` returns 401; a signed-in request reaches the mock backend with an Authorization header; and a signed-in `POST` with `Origin: https://attacker.example.test` returns 403. The Playwright-only mock server records method, path, query string, and a boolean `authorizationPresent` value at `/__test__/requests`; `DELETE /__test__/requests` clears the records. Do not record token values.

```ts
const anonymous = await request.get("/api/backend/recipes");
expect(anonymous.status()).toBe(401);
```

- [ ] **Step 2: Run the BFF tests and confirm the proxy behavior is absent**

Run: `npx playwright test e2e/backend-bff.spec.ts`.

Expected: the tests fail because `/api/backend/*` has no route handler.

- [ ] **Step 3: Implement path/method validation and upstream forwarding**

Allow only the API operations used by `recipes-api.ts` and `auth-api.ts`: Recipe collection/detail CRUD, publish/archive, ingredient and image subroutes; Category collection/detail CRUD and image upload; Profile GET/PATCH and password change. Do not expose refresh or logout through the BFF. Reject unknown methods, unknown paths, empty path segments, dot segments, and encoded slashes. Preserve query parameters and multipart boundaries, attach the Bearer token server-side, send `cache: "no-store"`, and return only safe upstream response headers/body.

Reject state-changing requests unless the Origin exactly matches the request's Next.js origin. Never accept a destination URL from the request.

- [ ] **Step 4: Add middleware redirects and test callback safety**

Use the Auth.js `auth` wrapper for `/dashboard/:path*` and `/profile`. Preserve only a local relative callback URL; reject protocol-relative or external callback values.

- [ ] **Step 5: Run the BFF and route-guard tests**

Run: `npx playwright test e2e/backend-bff.spec.ts`.

Expected: anonymous BFF calls return 401, allowed signed-in calls reach the mock API with Bearer authorization, mismatched Origin and unsupported paths are rejected, and direct navigation to protected UI redirects to login.

---

### Task 4: Migrate existing UI and API helpers off browser token storage

**Files:**
- Modify: `frontend/src/app/auth/login/page.tsx`
- Modify: `frontend/src/app/site-ui.tsx`
- Modify: `frontend/src/app/profile/page.tsx`
- Modify: `frontend/src/app/dashboard/recipes/page.tsx`
- Modify: `frontend/src/app/dashboard/categories/page.tsx`
- Modify: `frontend/src/app/dashboard/recipe-editor.tsx`
- Modify: `frontend/src/lib/auth-api.ts`
- Modify: `frontend/src/lib/recipes-api.ts`
- Delete after migration: `frontend/src/lib/auth-store.ts`
- Modify: `frontend/e2e/support/mock-recipe-api.mjs`
- Modify: `frontend/e2e/support/auth.ts`
- Modify: `frontend/e2e/auth-login.spec.ts`
- Modify: `frontend/e2e/auth-profile.spec.ts`
- Modify: `frontend/e2e/auth-store.spec.ts`
- Modify: `frontend/e2e/brand-acceptance.spec.ts`
- Modify: `frontend/e2e/category-images.spec.ts`
- Modify: `frontend/e2e/recipe-crud-flow.spec.ts`
- Modify: `frontend/e2e/srs-routes.spec.ts`
- Modify: `frontend/e2e/ui-acceptance.spec.ts`

**Interfaces:**
- Private API functions no longer accept `accessToken` arguments and call the same-origin BFF.
- Public API functions continue to call the public API origin without credentials.
- `frontend/e2e/support/auth.ts` exports `signInAs(page: Page, email = "chef@example.com"): Promise<void>`, which opens `/auth/login`, submits the supplied credentials, and waits for navigation to the saved callback URL or `/`.

- [ ] **Step 1: Add failing protected-screen and logout-failure assertions**

Update Recipe CRUD and Profile E2E setup to sign in through the login UI instead of seeding `sessionStorage`. Add an assertion that the header shows the session user's name after sign-in and that a protected page remains accessible after a page reload. Configure mock logout to return 503, click sign out, and assert the browser reaches login and the Auth.js session cookie is cleared despite backend revoke failure.

- [ ] **Step 2: Migrate login, header, Profile, and private API helpers**

Use Auth.js `signIn` in the email submit and GIS callback. Keep React Hook Form and all current local UI state. Replace `useAuth` and `getProfileApi(accessToken)` in the header with `useSession`; use the Auth.js profile fields until the later Query plan adds a Profile query. Replace Profile token/refresh/logout calls with Auth.js sign-out and private BFF calls. Remove token parameters and `authHeaders` from private API helpers.

- [ ] **Step 3: Migrate Dashboard and Recipe Editor token reads**

Remove `sessionStorage` checks and the `token` state from Dashboard Recipe/Category lists and Recipe Editor. Keep redirects in middleware. Send all private reads/writes through the updated token-free API helpers; keep category form state, RHF/Zod state, image files/previews, nutrition state, and recipe conflict handling unchanged.

- [ ] **Step 4: Replace E2E token seeding and direct-backend interception**

Use `signInAs` in `auth-profile`, `brand-acceptance`, `category-images`, `recipe-crud-flow`, `srs-routes`, and `ui-acceptance`. Update the mock server to service API calls made from the Next.js server, because `page.route()` does not intercept server-side BFF requests. Replace `auth-store.spec.ts` with assertions that no backend token is present in `sessionStorage` or the Session response.

- [ ] **Step 5: Remove the legacy auth store and verify no token-bearing browser call sites remain**

Run from repository root:

```powershell
rg "culinary_access_token|culinary_refresh_token|auth-store" frontend\src
rg 'Authorization:\s*`Bearer' frontend\src\app frontend\src\lib\auth-api.ts frontend\src\lib\recipes-api.ts
```

Expected: both searches return no matches; server-only `frontend/src/lib/server/auth-backend.ts` is intentionally excluded because it must attach the Bearer token upstream.

- [ ] **Step 6: Run affected E2E suites**

Run from `frontend`: `npx playwright test e2e/auth-login.spec.ts e2e/auth-profile.spec.ts e2e/backend-bff.spec.ts e2e/recipe-crud-flow.spec.ts e2e/category-images.spec.ts e2e/brand-acceptance.spec.ts e2e/srs-routes.spec.ts e2e/ui-acceptance.spec.ts`.

Expected: the sign-in, Profile, Recipe, Category, and protected-route flows pass without sessionStorage seeding.

---

### Task 5: Document and validate the Auth.js/BFF deliverable

**Files:**
- Modify: `docs/recipe-core-implementation-spec.md`

- [ ] **Step 1: Update the implementation document**

Replace the existing statement that login/token storage is unchanged with the implemented Auth.js cookie, GIS Credentials flow, BFF boundary, refresh behavior, and single-instance limitation. Keep Recipe implementation details unchanged.

- [ ] **Step 2: Run type/build/lint and the complete frontend E2E suite**

Run from `frontend`: `npx tsc --noEmit`, `npm run build`, `npm run lint`, and `npx playwright test`.

Expected: type-check, build, and all E2E tests exit successfully. Report pre-existing lint warnings separately; do not hide them or expand this migration to unrelated cleanup.

- [ ] **Step 3: Review security invariants**

Confirm by E2E and `rg` that Session JSON, local/session storage, and browser API request headers contain no backend token values. Review server logging calls to verify they never include credentials. Confirm anonymous BFF calls fail and backend authorization remains enabled.

---
