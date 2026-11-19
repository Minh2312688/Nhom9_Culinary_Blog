import assert from "node:assert/strict";
import test from "node:test";
import { queryKeys } from "./query-keys.ts";

test("private recipe details use separate cache keys for Admin and owner access", () => {
  const adminKey = queryKeys.dashboard.recipeDetail("user-1", "recipe-1", "admin");
  const ownerKey = queryKeys.dashboard.recipeDetail("user-1", "recipe-1", "owner");

  assert.notDeepEqual(adminKey, ownerKey);
});
