# Recipe Dashboard Permissions and Editor Wizard

**Status:** Approved design; implementation has not started.
**Date:** 2026-11-13

## Context

The application already has authenticated Recipe dashboard routes and a
multi-purpose editor in `frontend/src/app/dashboard/recipe-editor.tsx`. The
editor uses React Hook Form and Zod for core fields, while nutrition, structured
steps, and image selection are held in component state. It submits a Recipe
with its ingredients, steps, and nutrition; a selected image is uploaded after
the Recipe is created. Recipe updates send `rowVersion` and handle concurrency
conflicts.

The backend already protects Category create, update, delete, and image upload
with the Admin role. Recipe operations use author-based authorization with an
existing Admin override. The dashboard shell currently shows the Category link
to every signed-in user, and the Category page has no UI-level role guard.

## Goals

- Let a signed-in user find and manage their own Recipes through the dashboard.
- Keep Recipe authorization enforced by the backend; non-Admin users can only
  mutate their own Recipes.
- Retain the current Admin ability to manage any Recipe at the backend.
- Restrict Category management navigation and page access to Admin users.
- Change Recipe create/edit into a guided wizard with a final review step.
- Preserve existing Recipe, image-upload, and optimistic-concurrency behavior.

## Non-goals

- Add a new Admin-wide Recipe listing; the existing “my recipes” list remains
  scoped to the signed-in account.
- Change backend role or author authorization policies.
- Change the Recipe database schema, status lifecycle, endpoint contracts, or
  cache behavior.
- Redesign Category CRUD or add new Category operations.
- Move form state to a different form or state-management library.

## Permissions and navigation

The backend remains the authorization boundary:

- Category read operations remain public as currently configured.
- Category create, update, delete, and image upload remain Admin-only.
- Recipe create and author-scoped reads/mutations retain their existing policy.
- Admin retains the existing ability to manage Recipes regardless of author.

The dashboard UI will align with those policies. The shared dashboard
navigation will show the Category link only to Admin users. Direct navigation
to `/dashboard/categories` by a signed-in non-Admin will redirect to
`/dashboard`. The route guard is a presentation/access-control layer only; the
API must continue returning 403 for non-Admin mutation requests.

The Recipe dashboard list remains the current user's own Recipes, including for
Admin accounts. This work does not introduce an Admin-wide Recipe list.

## Recipe editor wizard

Use one route and one mounted form/state instance rather than separate routes
for each step. This reuses the existing React Hook Form/Zod setup and avoids
serializing transient state such as a selected `File` between pages.

The wizard has four steps:

1. **General information:** title, description, category, preparation/cooking
   time, servings, difficulty, nutrition, and image selection.
2. **Ingredients:** the existing add/remove controls and ingredient fields.
3. **Cooking steps:** the existing structured step editor and ordering
   controls.
4. **Review:** read-only summary of all values, nutrition, ingredients, steps,
   and image preview; the final action creates or updates the Recipe.

Place Previous and Next controls at the bottom of the current step. Previous
returns without validation. Next validates the fields in the current step and
does not advance while they are invalid. Values and selected image state remain
intact when moving between steps. Validation must follow existing client and
backend constraints; do not silently introduce stricter product rules.

The Review step allows Previous to return to editing. Server validation errors
should identify the relevant step when possible so the user can correct the
input without losing other entered data. Loading and saving states prevent
duplicate submissions.

### Save and media behavior

- Creating a Recipe continues to create a Draft through the existing API
  workflow; this change does not add a status selector or alter lifecycle
  transitions.
- Editing continues to preserve the existing Recipe status and submit its
  current `rowVersion`.
- A newly selected image is previewed in the wizard and uploaded only after the
  Recipe has been created, as today. If upload fails after creation, retain the
  existing retry path and make clear that the Draft exists but its image upload
  is incomplete.
- Existing image management while editing remains available and retains its
  current API behavior.
- A 409/concurrency conflict retains the current reload-latest-data recovery
  flow. Other API errors remain visible, and entered values are not cleared.

## Components and responsibilities

- `frontend/src/app/dashboard/recipe-editor.tsx`: step state, per-step
  validation/navigation, review, and existing submit/image/concurrency flows.
- `frontend/src/app/dashboard/dashboard-shell.tsx`: render Category navigation
  only for Admin.
- `frontend/src/app/dashboard/layout.tsx` and/or
  `frontend/src/middleware.ts`: provide role-aware access to the Category
  dashboard route while retaining authentication protection for all dashboard
  pages.
- `backend/src/CulinaryBlog.API/Endpoints/CategoryEndpoints.cs`: no policy
  weakening; existing Admin-only endpoint authorization remains authoritative.

No dependency or API change is expected.

## Error handling and accessibility

- Preserve existing API error messages and field-level errors.
- Do not clear form values on failed submit, upload retry, or concurrency
  conflict.
- Clearly identify the active wizard step and provide keyboard-accessible
  Previous/Next/Review actions.
- When validation blocks Next, focus or announce the first invalid field using
  existing form/accessibility conventions.
- A denied Category route must not expose mutation controls; backend
  authorization remains mandatory even if UI state is stale.

## Verification plan

1. Frontend tests confirm non-Admin users do not see the Category navigation
   link and cannot open the Category management route; Admin can access it.
2. Backend authorization tests confirm Category mutations remain Admin-only
   and Recipe author/Admin rules are unchanged.
3. Wizard tests confirm step-specific validation, Previous/Next behavior,
   preservation of values and selected image, and accurate Review content.
4. Recipe create/edit tests confirm successful submission, Draft creation,
   edit `rowVersion`, image upload retry, and conflict recovery.
5. Run the focused frontend type-check and relevant Playwright tests, plus
   backend authorization tests.
