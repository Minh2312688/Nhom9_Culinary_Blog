import { createServer } from "node:http";

const author = {
  id: "author-1",
  displayName: "Linh Nguyen",
  avatarUrl: null,
  bio: "A home cook sharing practical recipes.",
  email: "linh@example.com",
  roles: ["Admin"],
};

const categories = [
  { id: "category-1", name: "Món chính", slug: "mon-chinh" },
  { id: "category-2", name: "Món khai vị", slug: "mon-khai-vi" },
  { id: "category-3", name: "Tráng miệng", slug: "trang-mieng" },
];
const initialCategories = categories.map((category) => ({ ...category }));

const recipesDb = new Map();
const authAccounts = new Map();
const accountsByAccessToken = new Map();
const accountsByRefreshToken = new Map();
const forcedResponses = [];
let refreshRequestCount = 0;
let logoutRequestCount = 0;
let logoutRequestAuthorized = false;
let createdRecipeCount = 0;
const apiRequests = [];

function createAuthAccount(email, options = {}) {
  const suffix = options.suffix ?? "chef";
  const account = {
    profile: {
      id: options.id ?? "user-chef",
      email,
      displayName: options.displayName ?? "Chef Example",
      avatarUrl: null,
      bio: null,
      emailConfirmed: true,
      createdAt: "2026-10-01T00:00:00Z",
      roles: options.roles ?? ["Author"],
    },
    accessToken: `backend-access-${suffix}`,
    refreshToken: `backend-refresh-${suffix}`,
    expiresIn: options.expiresIn ?? 900,
    refreshFails: options.refreshFails ?? false,
    logoutFails: options.logoutFails ?? false,
  };
  authAccounts.set(email, account);
  accountsByAccessToken.set(account.accessToken, account);
  accountsByRefreshToken.set(account.refreshToken, account);
  return account;
}

createAuthAccount("chef@example.com");
createAuthAccount("admin@example.com", {
  suffix: "admin",
  id: "user-admin",
  displayName: "Admin Example",
  roles: ["Admin"],
});
createAuthAccount("expired-chef@example.com", {
  suffix: "expired",
  expiresIn: 0,
});
createAuthAccount("near-expiry-chef@example.com", {
  suffix: "near-expiry",
  expiresIn: 15,
});
createAuthAccount("refresh-failure-chef@example.com", {
  suffix: "refresh-failure",
  expiresIn: 0,
  refreshFails: true,
});
createAuthAccount("logout-failure-chef@example.com", {
  suffix: "logout-failure",
  logoutFails: true,
});
createAuthAccount("google@example.com", {
  suffix: "google",
  id: "user-google",
  displayName: "Google Chef",
  roles: ["Member"],
});

const initialAccounts = new Map(
  Array.from(authAccounts, ([email, account]) => [
    email,
    {
      profile: JSON.parse(JSON.stringify(account.profile)),
      accessToken: account.accessToken,
      refreshToken: account.refreshToken,
      expiresIn: account.expiresIn,
    },
  ]),
);

function createInitialRecipe(overrides = {}) {
  const r = {
    id: overrides.id || "recipe-1",
    title: overrides.title || "Roasted tomato soup",
    slug: overrides.slug || "recipe-with-author",
    description: overrides.description || "A simple soup.",
    prepTimeMinutes: overrides.prepTimeMinutes ?? 10,
    cookTimeMinutes: overrides.cookTimeMinutes ?? 30,
    servings: overrides.servings ?? 4,
    difficulty: overrides.difficulty || "Easy",
    status: overrides.status || "Published",
    categoryId: overrides.categoryId || "category-1",
    authorId: overrides.authorId || "author-1",
    publishedAt: overrides.publishedAt !== undefined ? overrides.publishedAt : "2026-10-01T09:30:00Z",
    rowVersion: overrides.rowVersion || "v1",
    ingredients: overrides.ingredients || [
      { id: "ingredient-1", name: "Tomatoes", quantity: 4, unit: null, notes: null, orderIndex: 0 },
    ],
    steps: overrides.steps || [
      { id: "step-1", stepNumber: 1, title: null, description: "Roast the tomatoes.", durationMinutes: null, imageUrl: null },
    ],
    images: overrides.images || [],
    nutrition: overrides.nutrition || null,
    author: overrides.author !== undefined ? overrides.author : { id: author.id, displayName: author.displayName, avatarUrl: author.avatarUrl },
  };
  return r;
}

// Seed initial recipes
const initialRecipes = [
  createInitialRecipe(),
  createInitialRecipe({
    id: "00000000-0000-0000-0000-000000000001",
    title: "Phở bò",
    slug: "pho-bo",
    description: "Nước dùng đậm đà",
    prepTimeMinutes: 20,
    cookTimeMinutes: 90,
    servings: 4,
    difficulty: "Medium",
    ingredients: [{
      id: "ingredient-1",
      name: "Thịt bò",
      quantity: 300,
      unit: "g",
      notes: null,
      orderIndex: 0,
    }],
    steps: [{
      id: "step-1",
      stepNumber: 1,
      title: null,
      description: "Nấu nước dùng.",
      durationMinutes: 10,
      imageUrl: null,
    }],
    images: [{
      id: "image-1",
      originalUrl: "https://storage.example.test/original.avif",
      mediumUrl: null,
      thumbnailUrl: null,
      altText: "Phở bò",
      isPrimary: true,
      orderIndex: 0,
    }],
    author: null,
  }),
  createInitialRecipe({ id: "recipe-slow", title: "Slow recipe", slug: "slow-recipe" }),
  createInitialRecipe({
    id: "recipe-2",
    title: "Historical tomato soup",
    slug: "historical-recipe",
    publishedAt: null,
  }),
  createInitialRecipe({
    id: "recipe-canh-chua",
    title: "Canh Chua Cá Lóc Nam Bộ",
    slug: "canh-chua-ca-loc",
    description: "Món canh chua cá lóc đậm đà vị miền Tây sông nước.",
    prepTimeMinutes: 15,
    cookTimeMinutes: 25,
    servings: 4,
    difficulty: "Medium",
    status: "Published",
    categoryId: "category-1",
    authorId: "author-1",
    publishedAt: "2026-10-28T00:00:00Z",
    rowVersion: "v1",
    ingredients: [
      { id: "ing-1", name: "Cá lóc", quantity: 500, unit: "g", notes: "cắt khúc", orderIndex: 0 },
      { id: "ing-2", name: "Cà chua", quantity: 2, unit: "quả", notes: null, orderIndex: 1 },
    ],
    steps: [
      {
        id: "st-1",
        stepNumber: 1,
        title: "Sơ chế cá lóc",
        description: "Làm sạch vảy cá, rửa sạch với chanh khử tanh.",
        durationMinutes: 10,
        imageUrl: "https://storage.example.test/step1.jpg",
      },
      {
        id: "st-2",
        stepNumber: 2,
        title: "Nấu nước dùng canh",
        description: "Đun sôi nước me lọc và cho cà chua vào.",
        durationMinutes: 15,
        imageUrl: null,
      },
    ],
    images: [],
    nutrition: {
      calories: 285,
      protein: 34.5,
      carbohydrates: 12,
      fat: 7.5,
      fiber: 3.2,
      sodium: 620,
    },
    author: { id: "author-1", displayName: "Chef Minh", avatarUrl: null },
  }),
  createInitialRecipe({
    id: "recipe-image-ordering",
    title: "Món có nhiều ảnh",
    slug: "image-ordering",
    description: "Mô tả món ăn.",
    publishedAt: null,
    images: [
      {
        id: "secondary-image",
        originalUrl: "https://storage.example.test/secondary.webp",
        mediumUrl: null,
        thumbnailUrl: null,
        altText: "Ảnh phụ",
        isPrimary: false,
        orderIndex: 1,
      },
      {
        id: "primary-image",
        originalUrl: "https://storage.example.test/primary.webp",
        mediumUrl: null,
        thumbnailUrl: null,
        altText: "Ảnh chính",
        isPrimary: true,
        orderIndex: 0,
      },
    ],
    nutrition: {
      calories: 310,
      protein: 18.5,
      carbohydrates: 36,
      fat: 10.2,
      fiber: 4.1,
      sodium: 680,
    },
  }),
  createInitialRecipe({
    id: "recipe-step-image-gallery",
    title: "Công thức có ảnh từng bước",
    slug: "step-image-gallery",
    steps: [
      {
        id: "step-gallery-1",
        stepNumber: 1,
        title: "Sơ chế",
        description: "Sơ chế nguyên liệu.",
        durationMinutes: null,
        imageUrl: "https://storage.example.test/recipes/a71f.webp",
      },
      {
        id: "step-gallery-2",
        stepNumber: 2,
        title: "Nấu",
        description: "Nấu chín.",
        durationMinutes: null,
        imageUrl: null,
      },
    ],
    images: [
      {
        id: "step-image-1-1",
        originalUrl: "https://storage.example.test/recipes/a71f.webp",
        mediumUrl: null,
        thumbnailUrl: null,
        altText: "Sơ chế lần 1",
        isPrimary: false,
        orderIndex: 1,
        stepNumber: 1,
      },
      {
        id: "step-image-1-2",
        originalUrl: "https://storage.example.test/recipes/b82a.webp",
        mediumUrl: null,
        thumbnailUrl: null,
        altText: "Sơ chế lần 2",
        isPrimary: false,
        orderIndex: 2,
        stepNumber: 1,
      },
      {
        id: "step-image-2",
        originalUrl: "https://storage.example.test/recipes/c93b.webp",
        mediumUrl: null,
        thumbnailUrl: null,
        altText: "Nấu",
        isPrimary: false,
        orderIndex: 3,
        stepNumber: 2,
      },
    ],
  }),
];

for (const r of initialRecipes) {
  recipesDb.set(r.id, r);
}

function paginated(items, page, pageSize = 12, totalCountOverride) {
  const totalCount = totalCountOverride !== undefined ? totalCountOverride : items.length;
  const totalPages = Math.ceil(totalCount / pageSize) || 1;
  return {
    items,
    totalCount,
    page,
    pageSize,
    totalPages,
    hasNextPage: page < totalPages,
    hasPreviousPage: page > 1,
  };
}

function sendJson(response, statusCode, payload) {
  response.writeHead(statusCode, { "Content-Type": "application/json; charset=utf-8" });
  response.end(JSON.stringify(payload));
}

function readBody(request) {
  return new Promise((resolve, reject) => {
    const chunks = [];
    request.on("data", (chunk) => chunks.push(chunk));
    request.on("end", () => resolve(Buffer.concat(chunks)));
    request.on("error", reject);
  });
}

function recordJsonBody(record, raw) {
  if (!record || !raw?.length) return;
  try {
    record.body = JSON.parse(raw.toString("utf-8"));
  } catch {
    // Keep multipart and malformed payload contents out of test request records.
  }
}

function accountForRequest(request, response) {
  const accessToken = request.headers.authorization?.replace(/^Bearer\s+/i, "");
  const account = accessToken ? accountsByAccessToken.get(accessToken) : undefined;
  if (!account) {
    sendJson(response, 401, { status: 401, title: "Unauthorized" });
    return null;
  }
  return account;
}

function slugify(text) {
  return text
    .toLowerCase()
    .normalize("NFD")
    .replace(/[\u0300-\u036f]/g, "")
    .replace(/[đĐ]/g, "d")
    .replace(/[^a-z0-9]+/g, "-")
    .replace(/^-+|-+$/g, "");
}

const server = createServer(async (request, response) => {
  response.setHeader("Access-Control-Allow-Origin", "*");
  response.setHeader("Access-Control-Allow-Methods", "GET, POST, PUT, DELETE, OPTIONS");
  response.setHeader("Access-Control-Allow-Headers", "Content-Type, Authorization");

  if (request.method === "OPTIONS") {
    response.writeHead(204);
    response.end();
    return;
  }

  const url = new URL(request.url ?? "/", "http://127.0.0.1");

  if (url.pathname === "/health") {
    response.writeHead(200);
    response.end("ok");
    return;
  }

  if (request.method === "POST" && url.pathname === "/__test/auth/reset") {
    refreshRequestCount = 0;
    logoutRequestCount = 0;
    logoutRequestAuthorized = false;
    createdRecipeCount = 0;
    forcedResponses.length = 0;
    apiRequests.length = 0;
    categories.splice(0, categories.length, ...initialCategories.map((category) => ({ ...category })));
    recipesDb.clear();
    for (const recipe of initialRecipes) {
      recipesDb.set(recipe.id, JSON.parse(JSON.stringify(recipe)));
    }
    accountsByAccessToken.clear();
    accountsByRefreshToken.clear();
    for (const [email, account] of authAccounts) {
      const initial = initialAccounts.get(email);
      account.profile = JSON.parse(JSON.stringify(initial.profile));
      account.accessToken = initial.accessToken;
      account.refreshToken = initial.refreshToken;
      account.expiresIn = initial.expiresIn;
      accountsByAccessToken.set(account.accessToken, account);
      accountsByRefreshToken.set(account.refreshToken, account);
    }
    response.writeHead(204);
    response.end();
    return;
  }

  if (request.method === "POST" && url.pathname === "/__test/auth/profile") {
    let configuration;
    try {
      configuration = JSON.parse((await readBody(request)).toString("utf-8"));
    } catch {
      sendJson(response, 400, { title: "Bad Request" });
      return;
    }
    const account = authAccounts.get(configuration.email);
    if (!account || !configuration.profile || typeof configuration.profile !== "object") {
      sendJson(response, 400, { title: "Invalid profile setup" });
      return;
    }
    account.profile = configuration.profile;
    response.writeHead(204);
    response.end();
    return;
  }

  if (request.method === "POST" && url.pathname === "/__test/responses") {
    let configuration;
    try {
      configuration = JSON.parse((await readBody(request)).toString("utf-8"));
    } catch {
      sendJson(response, 400, { title: "Bad Request" });
      return;
    }
    if (
      !["GET", "POST", "PUT", "PATCH", "DELETE"].includes(configuration.method) ||
      typeof configuration.path !== "string" ||
      !configuration.path.startsWith("/api/v1/") ||
      !Number.isInteger(configuration.status) ||
      configuration.status < 100 ||
      configuration.status > 599
    ) {
      sendJson(response, 400, { title: "Invalid response setup" });
      return;
    }
    forcedResponses.push({
      ...configuration,
      remaining: Number.isInteger(configuration.times) && configuration.times > 0
        ? configuration.times
        : 1,
    });
    response.writeHead(201);
    response.end();
    return;
  }

  if (url.pathname === "/__test__/requests" && request.method === "GET") {
    sendJson(response, 200, apiRequests);
    return;
  }

  if (url.pathname === "/__test__/requests" && request.method === "DELETE") {
    apiRequests.length = 0;
    response.writeHead(204);
    response.end();
    return;
  }

  if (request.method === "GET" && url.pathname === "/__test/auth/refresh-count") {
    sendJson(response, 200, { count: refreshRequestCount });
    return;
  }

  if (request.method === "GET" && url.pathname === "/__test/auth/logout-count") {
    sendJson(response, 200, {
      count: logoutRequestCount,
      authorized: logoutRequestAuthorized,
    });
    return;
  }

  let requestRecord;
  if (url.pathname.startsWith("/api/v1/")) {
    requestRecord = {
      method: request.method,
      path: url.pathname,
      query: url.search,
      authorizationPresent: Boolean(request.headers.authorization),
      ...(request.headers["content-type"] ? { contentType: request.headers["content-type"] } : {}),
    };
    apiRequests.push(requestRecord);
    const responseIndex = forcedResponses.findIndex((forced) =>
      forced.method === request.method && forced.path === url.pathname,
    );
    if (responseIndex >= 0) {
      const [forced] = forcedResponses.splice(responseIndex, 1);
      forced.remaining -= 1;
      if (forced.remaining > 0) forcedResponses.unshift(forced);
      if (forced.delayMs > 0) {
        await new Promise((resolve) => setTimeout(resolve, forced.delayMs));
      }
      if (request.method !== "GET") {
        const raw = await readBody(request);
        if (url.pathname.startsWith("/api/v1/recipes") || url.pathname.startsWith("/api/v1/categories")) {
          recordJsonBody(requestRecord, raw);
        }
      }
      if (forced.status === 204 || forced.status === 304) {
        response.writeHead(forced.status);
        response.end();
      } else {
        sendJson(response, forced.status, forced.body ?? {});
      }
      return;
    }
  }

  if (request.method === "POST" && url.pathname === "/api/v1/auth/login") {
    let credentials;
    try {
      credentials = JSON.parse((await readBody(request)).toString("utf-8"));
    } catch {
      sendJson(response, 400, { title: "Bad Request" });
      return;
    }

    if (credentials.email === "locked-chef@example.com") {
      sendJson(response, 423, {
        status: 423,
        title: "Locked",
        detail: "The test account is locked.",
      });
      return;
    }

    const account = authAccounts.get(credentials.email);
    if (!account || credentials.password !== "Password123!") {
      sendJson(response, 401, {
        status: 401,
        title: "Unauthorized",
        detail: "Invalid test credentials.",
      });
      return;
    }

    sendJson(response, 200, {
      accessToken: account.accessToken,
      refreshToken: account.refreshToken,
      expiresIn: account.expiresIn,
    });
    return;
  }

  if (request.method === "POST" && url.pathname === "/api/v1/auth/google") {
    let body;
    try {
      body = JSON.parse((await readBody(request)).toString("utf-8"));
    } catch {
      sendJson(response, 400, { title: "Bad Request" });
      return;
    }

    if (body.idToken !== "test-google-id-token") {
      sendJson(response, 401, {
        status: 401,
        title: "Unauthorized",
        detail: "Invalid Google test token.",
      });
      return;
    }

    const account = authAccounts.get("google@example.com");
    sendJson(response, 200, {
      accessToken: account.accessToken,
      refreshToken: account.refreshToken,
      expiresIn: account.expiresIn,
    });
    return;
  }

  if (
    (request.method === "GET" || request.method === "PATCH") &&
    url.pathname === "/api/v1/auth/me"
  ) {
    const account = accountForRequest(request, response);
    if (!account) return;
    if (request.method === "PATCH") {
      const raw = await readBody(request);
      recordJsonBody(requestRecord, raw);
      let payload;
      try {
        payload = JSON.parse(raw.toString("utf-8"));
      } catch {
        sendJson(response, 400, { title: "Bad Request" });
        return;
      }
      account.profile = { ...account.profile, ...payload };
    }

    sendJson(response, 200, account.profile);
    return;
  }

  if (request.method === "POST" && url.pathname === "/api/v1/auth/change-password") {
    const account = accountForRequest(request, response);
    if (!account) return;
    await readBody(request);
    sendJson(response, 200, { message: "Password changed." });
    return;
  }

  if (request.method === "POST" && url.pathname === "/api/v1/auth/refresh") {
    refreshRequestCount++;
    let body;
    try {
      body = JSON.parse((await readBody(request)).toString("utf-8"));
    } catch {
      sendJson(response, 400, { title: "Bad Request" });
      return;
    }

    await new Promise((resolve) => setTimeout(resolve, 100));
    const account = accountsByRefreshToken.get(body.refreshToken);
    if (!account || account.refreshFails) {
      sendJson(response, 401, { status: 401, title: "Unauthorized" });
      return;
    }

    accountsByAccessToken.delete(account.accessToken);
    accountsByRefreshToken.delete(account.refreshToken);
    account.accessToken = `${account.accessToken}-rotated`;
    account.refreshToken = `${account.refreshToken}-rotated`;
    accountsByAccessToken.set(account.accessToken, account);
    accountsByRefreshToken.set(account.refreshToken, account);

    sendJson(response, 200, {
      accessToken: account.accessToken,
      refreshToken: account.refreshToken,
      expiresIn: 900,
    });
    return;
  }

  if (request.method === "POST" && url.pathname === "/api/v1/auth/logout") {
    logoutRequestCount++;
    let body;
    try {
      body = JSON.parse((await readBody(request)).toString("utf-8"));
    } catch {
      body = {};
    }

    const accessToken = request.headers.authorization?.replace(/^Bearer\s+/i, "");
    const account = accessToken ? accountsByAccessToken.get(accessToken) : undefined;
    logoutRequestAuthorized =
      Boolean(account) &&
      typeof body.refreshToken === "string" &&
      body.refreshToken === account.refreshToken;
    if (!logoutRequestAuthorized) {
      sendJson(response, 401, { status: 401, title: "Unauthorized" });
      return;
    }
    if (account.logoutFails) {
      sendJson(response, 503, { status: 503, title: "Unavailable" });
      return;
    }

    response.writeHead(204);
    response.end();
    return;
  }

  // Public category reads.
  if (
    request.method === "GET" &&
    (
      url.pathname === "/api/v1/categories" ||
      url.pathname === "/api/v1/categories/" ||
      url.pathname === "/api/v1/recipes/categories"
    )
  ) {
    sendJson(response, 200, paginated(categories, 1, 100));
    return;
  }

  if (
    request.method === "POST" &&
    (url.pathname === "/api/v1/categories" || url.pathname === "/api/v1/categories/")
  ) {
    if (!accountForRequest(request, response)) return;
    const raw = await readBody(request);
    recordJsonBody(requestRecord, raw);
    let payload;
    try {
      payload = JSON.parse(raw.toString("utf-8"));
    } catch {
      sendJson(response, 400, { title: "Bad Request" });
      return;
    }
    const created = {
      id: "category-created",
      name: payload.name,
      slug: slugify(payload.name),
      description: payload.description ?? null,
      imageUrl: null,
      orderIndex: categories.length,
      recipeCount: 0,
    };
    categories.push(created);
    sendJson(response, 201, created);
    return;
  }

  const categoryImageMatch = url.pathname.match(/^\/api\/v1\/categories\/([^/]+)\/image\/?$/);
  if (request.method === "POST" && categoryImageMatch) {
    if (!accountForRequest(request, response)) return;
    const category = categories.find((item) => item.id === categoryImageMatch[1]);
    if (!category) {
      sendJson(response, 404, { title: "Not Found", detail: "Category not found." });
      return;
    }
    await readBody(request);
    category.imageUrl = "https://storage.example.test/category.webp";
    sendJson(response, 200, category);
    return;
  }

  const categoryMatch = url.pathname.match(/^\/api\/v1\/categories\/([^/]+)\/?$/);
  if (categoryMatch && request.method === "GET") {
    const category = categories.find((item) =>
      item.id === categoryMatch[1] || item.slug === categoryMatch[1],
    );
    if (category) sendJson(response, 200, category);
    else sendJson(response, 404, { title: "Not Found", detail: "Category not found." });
    return;
  }
  if (categoryMatch && request.method === "PUT") {
    if (!accountForRequest(request, response)) return;
    const category = categories.find((item) => item.id === categoryMatch[1]);
    if (!category) {
      sendJson(response, 404, { title: "Not Found", detail: "Category not found." });
      return;
    }
    const raw = await readBody(request);
    recordJsonBody(requestRecord, raw);
    let payload;
    try {
      payload = JSON.parse(raw.toString("utf-8"));
    } catch {
      sendJson(response, 400, { title: "Bad Request" });
      return;
    }
    Object.assign(category, payload, { slug: slugify(payload.name ?? category.name) });
    sendJson(response, 200, category);
    return;
  }
  if (categoryMatch && request.method === "DELETE") {
    if (!accountForRequest(request, response)) return;
    const categoryIndex = categories.findIndex((item) => item.id === categoryMatch[1]);
    if (categoryIndex < 0) {
      sendJson(response, 404, { title: "Not Found", detail: "Category not found." });
      return;
    }
    categories.splice(categoryIndex, 1);
    response.writeHead(204);
    response.end();
    return;
  }

  // Authors endpoint
  if (request.method === "GET" &&
      (url.pathname === "/api/v1/authors/author-1" || url.pathname === "/api/v1/authors/slow-author")) {
    if (url.pathname.endsWith("/slow-author")) {
      await new Promise((resolve) => setTimeout(resolve, 1200));
    }
    const page = Number(url.searchParams.get("page") ?? "1");
    const items = page === 1
      ? [{
          id: "recipe-1",
          title: "Roasted tomato soup",
          slug: "recipe-with-author",
          description: "A simple soup.",
          difficulty: "Easy",
          cookTimeMinutes: 30,
          servings: 4,
          status: "Published",
          categoryId: "category-1",
          authorId: "author-1",
          publishedAt: "2026-10-01T09:30:00Z",
          primaryImageUrl: "https://storage.example.test/main.webp",
          primaryImageAltText: "Roasted tomato soup",
          rowVersion: "v1",
        }]
      : [{
          id: "recipe-3",
          title: "Citrus lentil salad",
          slug: "citrus-lentil-salad",
          description: "Bright and filling.",
          difficulty: "Easy",
          cookTimeMinutes: 25,
          servings: 3,
          status: "Published",
          categoryId: "category-1",
          authorId: "author-1",
          publishedAt: "2026-09-20T12:00:00Z",
          primaryImageUrl: null,
          primaryImageAltText: null,
          rowVersion: "v1",
        }];

    sendJson(response, 200, {
      id: author.id,
      displayName: author.displayName,
      avatarUrl: author.avatarUrl,
      bio: author.bio,
      recipes: paginated(items, page, 12, 13),
    });
    return;
  }

  // List recipes
  if (request.method === "GET" && (
    url.pathname === "/api/v1/recipes" ||
    url.pathname === "/api/v1/recipes/" ||
    url.pathname === "/api/v1/recipes/mine"
  )) {
    const mineOnly = url.pathname === "/api/v1/recipes/mine";
    const bearer = request.headers.authorization?.replace(/^Bearer\s+/i, "");
    const account = bearer ? accountsByAccessToken.get(bearer) : undefined;
    if ((bearer && !account) || (mineOnly && !account)) {
      sendJson(response, 401, { status: 401, title: "Unauthorized" });
      return;
    }

    const recipeValues = Array.from(recipesDb.values()).filter((recipe) =>
      mineOnly || account || recipe.status === "Published" || recipe.status === 1,
    );
    const all = recipeValues.map((r) => ({
      id: r.id,
      title: r.title,
      slug: r.slug,
      description: r.description,
      difficulty: r.difficulty,
      cookTimeMinutes: r.cookTimeMinutes,
      servings: r.servings,
      status: r.status,
      categoryId: r.categoryId,
      authorId: r.authorId,
      publishedAt: r.publishedAt,
      primaryImageUrl: r.images.find((image) => image.isPrimary)?.originalUrl ?? null,
      primaryImageAltText: r.images.find((image) => image.isPrimary)?.altText ?? null,
      rowVersion: r.rowVersion,
    }));
    sendJson(response, 200, paginated(all, 1, 50));
    return;
  }

  // Create recipe
  if (request.method === "POST" && (url.pathname === "/api/v1/recipes" || url.pathname === "/api/v1/recipes/")) {
    if (!accountForRequest(request, response)) return;
    const raw = await readBody(request);
    recordJsonBody(requestRecord, raw);
    let body;
    try {
      body = JSON.parse(raw.toString("utf-8"));
    } catch {
      sendJson(response, 400, { title: "Bad Request" });
      return;
    }
    const id = `created-recipe-${++createdRecipeCount}`;
    const slug = slugify(body.title || "new-recipe") + "-" + createdRecipeCount;
    const newRecipe = {
      id,
      slug,
      title: body.title,
      description: body.description ?? null,
      categoryId: body.categoryId,
      prepTimeMinutes: body.prepTimeMinutes ?? 0,
      cookTimeMinutes: body.cookTimeMinutes ?? 0,
      servings: body.servings ?? 1,
      difficulty: body.difficulty || "Easy",
      status: "Draft",
      authorId: "author-1",
      publishedAt: null,
      rowVersion: "v1",
      ingredients: (body.ingredients || []).map((ing, i) => ({
        id: `ing-${Date.now()}-${i}`,
        ...ing,
      })),
      steps: (body.steps || []).map((st, i) => ({
        id: `step-${Date.now()}-${i}`,
        stepNumber: i + 1,
        ...st,
      })),
      images: [],
      nutrition: body.nutrition ?? null,
      author: { id: author.id, displayName: author.displayName, avatarUrl: author.avatarUrl },
    };
    recipesDb.set(id, newRecipe);
    sendJson(response, 201, newRecipe);
    return;
  }

  // Image upload: /api/v1/recipes/:id/images or /api/v1/recipes/:id/images/
  const uploadMatch = url.pathname.match(/^\/api\/v1\/recipes\/([^/]+)\/images\/?$/);
  if (request.method === "POST" && uploadMatch) {
    if (!accountForRequest(request, response)) return;
    const recipeId = uploadMatch[1];
    const r = recipesDb.get(recipeId);
    if (!r) {
      sendJson(response, 404, { title: "Not Found", detail: "Recipe not found." });
      return;
    }
    const body = await readBody(request);
    const primaryField = body.toString("utf8").match(/name="isPrimary"\r\n\r\n(true|false)\r\n/i);
    const stepNumberField = body.toString("utf8").match(/name="stepNumber"\r\n\r\n(\d+)\r\n/i);
    const stepNumber = stepNumberField ? Number(stepNumberField[1]) : null;
    const isPrimary = primaryField?.[1].toLowerCase() === "true"
      || (stepNumber === null && r.images.length === 0);
    const imageId = `img-${Date.now()}-${r.images.length}`;
    const newImage = {
      id: imageId,
      originalUrl: `https://storage.example.test/${imageId}.avif`,
      mediumUrl: `https://storage.example.test/${imageId}-med.webp`,
      thumbnailUrl: `https://storage.example.test/${imageId}-thumb.webp`,
      altText: "Ảnh công thức",
      isPrimary,
      orderIndex: r.images.length,
      stepNumber,
    };
    if (isPrimary) r.images.forEach((img) => (img.isPrimary = false));
    r.images.push(newImage);
    sendJson(response, 201, {
      imageId: newImage.id,
      originalUrl: newImage.originalUrl,
      thumbnailUrl: newImage.thumbnailUrl,
      mediumUrl: newImage.mediumUrl,
      altText: newImage.altText,
      isPrimary: newImage.isPrimary,
      stepNumber: newImage.stepNumber,
    });
    return;
  }

  const stepImageMatch = url.pathname.match(/^\/api\/v1\/recipes\/([^/]+)\/steps\/([^/]+)\/image\/?$/);
  if (request.method === "POST" && stepImageMatch) {
    if (!accountForRequest(request, response)) return;
    const [, recipeId, stepId] = stepImageMatch;
    const recipe = recipesDb.get(recipeId);
    const step = recipe?.steps.find((item) => item.id === stepId);
    if (!recipe || !step) {
      sendJson(response, 404, { title: "Not Found", detail: "Recipe step not found." });
      return;
    }
    await readBody(request);
    step.imageUrl = `https://storage.example.test/${stepId}.webp`;
    sendJson(response, 200, step);
    return;
  }

  // Set primary image: /api/v1/recipes/:id/images/:imageId/primary
  const primaryMatch = url.pathname.match(/^\/api\/v1\/recipes\/([^/]+)\/images\/([^/]+)\/primary\/?$/);
  if ((request.method === "PUT" || request.method === "PATCH") && primaryMatch) {
    if (!accountForRequest(request, response)) return;
    const [, recipeId, imageId] = primaryMatch;
    const r = recipesDb.get(recipeId);
    if (!r) {
      sendJson(response, 404, { title: "Not Found", detail: "Recipe not found." });
      return;
    }
    r.images.forEach((img) => {
      img.isPrimary = img.id === imageId;
    });
    sendJson(response, 200, { success: true });
    return;
  }

  // Delete image: /api/v1/recipes/:id/images/:imageId
  const deleteImageMatch = url.pathname.match(/^\/api\/v1\/recipes\/([^/]+)\/images\/([^/]+)\/?$/);
  if (request.method === "DELETE" && deleteImageMatch) {
    if (!accountForRequest(request, response)) return;
    const [, recipeId, imageId] = deleteImageMatch;
    const r = recipesDb.get(recipeId);
    if (!r) {
      sendJson(response, 404, { title: "Not Found", detail: "Recipe not found." });
      return;
    }
    const deletedImage = r.images.find((img) => img.id === imageId);
    r.images = r.images.filter((img) => img.id !== imageId);
    if (deletedImage?.isPrimary && r.images.length > 0) {
      r.images.forEach((img, index) => (img.isPrimary = index === 0));
    }
    sendJson(response, 204, null);
    return;
  }

  // Update recipe: /api/v1/recipes/:id
  const updateMatch = url.pathname.match(/^\/api\/v1\/recipes\/([^/]+)\/?$/);
  if (request.method === "PUT" && updateMatch) {
    if (!accountForRequest(request, response)) return;
    const recipeId = updateMatch[1];
    const r = recipesDb.get(recipeId);
    if (!r) {
      sendJson(response, 404, { title: "Not Found", detail: "Recipe not found." });
      return;
    }
    const raw = await readBody(request);
    recordJsonBody(requestRecord, raw);
    let body;
    try {
      body = JSON.parse(raw.toString("utf-8"));
    } catch {
      sendJson(response, 400, { title: "Bad Request" });
      return;
    }
    r.title = body.title ?? r.title;
    r.description = body.description !== undefined ? body.description : r.description;
    r.categoryId = body.categoryId ?? r.categoryId;
    r.prepTimeMinutes = body.prepTimeMinutes ?? r.prepTimeMinutes;
    r.cookTimeMinutes = body.cookTimeMinutes ?? r.cookTimeMinutes;
    r.servings = body.servings ?? r.servings;
    r.difficulty = body.difficulty ?? r.difficulty;
    if (body.ingredients) {
      r.ingredients = body.ingredients.map((ing, i) => ({
        id: ing.id || `ing-${Date.now()}-${i}`,
        ...ing,
      }));
    }
    if (body.steps) {
      r.steps = body.steps.map((st, i) => ({
        id: st.id || `step-${Date.now()}-${i}`,
        stepNumber: i + 1,
        ...st,
      }));
    }
    if (body.nutrition !== undefined) {
      r.nutrition = body.nutrition;
    }
    r.rowVersion = `v-${Date.now()}`;
    sendJson(response, 200, r);
    return;
  }

  // Delete recipe
  if (request.method === "DELETE" && updateMatch) {
    if (!accountForRequest(request, response)) return;
    const recipeId = updateMatch[1];
    recipesDb.delete(recipeId);
    sendJson(response, 204, null);
    return;
  }

  // Get recipe by slug or ID
  const getMatch = url.pathname.match(/^\/api\/v1\/recipes\/([^/]+)\/?$/);
  if (request.method === "GET" && getMatch) {
    const param = getMatch[1];
    if (param === "slow-recipe") {
      await new Promise((resolve) => setTimeout(resolve, 1200));
    }
    const found = Array.from(recipesDb.values()).find((r) => r.slug === param || r.id === param);
    const bearer = request.headers.authorization?.replace(/^Bearer\s+/i, "");
    if (bearer && !accountsByAccessToken.has(bearer)) {
      sendJson(response, 401, { status: 401, title: "Unauthorized" });
      return;
    }
    if (found && (bearer || found.status === "Published" || found.status === 1)) {
      sendJson(response, 200, found);
      return;
    }
  }

  const publishMatch = url.pathname.match(/^\/api\/v1\/recipes\/([^/]+)\/publish\/?$/);
  if (request.method === "PATCH" && publishMatch) {
    if (!accountForRequest(request, response)) return;
    const recipe = recipesDb.get(publishMatch[1]);
    if (!recipe) {
      sendJson(response, 404, { title: "Not Found", detail: "Recipe not found." });
      return;
    }
    recipe.status = "Published";
    recipe.publishedAt = new Date().toISOString();
    sendJson(response, 200, { success: true });
    return;
  }

  sendJson(response, 404, { title: "Not Found", detail: "Mock API endpoint not found." });
});

server.listen(Number(process.env.MOCK_API_PORT ?? "5055"), "127.0.0.1");
