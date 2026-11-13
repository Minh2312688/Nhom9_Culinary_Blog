import { encode, getToken, type JWT } from "next-auth/jwt";
import {
  BACKEND_TOKEN_REFRESH_SKEW_MS,
  AUTH_SESSION_COOKIE_NAME,
  AUTH_SESSION_MAX_AGE_SECONDS,
} from "@/auth";
import {
  internalApiEndpoint,
  refreshBackendTokensOnce,
} from "@/lib/server/auth-backend";

const COOKIE_CHUNK_SIZE = 3936;
const UPSTREAM_RESPONSE_HEADERS = [
  "content-type",
  "content-disposition",
  "etag",
  "last-modified",
  "retry-after",
] as const;

type HttpMethod = "GET" | "POST" | "PUT" | "PATCH" | "DELETE";

function isIdentifier(segment: string): boolean {
  return /^[A-Za-z0-9_-]+$/.test(segment) && segment !== "." && segment !== "..";
}

function allowedMethods(segments: string[]): HttpMethod[] | null {
  const [resource, first, second, third] = segments;

  if (segments.length === 1 && resource === "recipes") return ["GET", "POST"];
  if (segments.length === 1 && resource === "categories") return ["GET", "POST"];

  if (resource === "recipes" && segments.length === 2 && isIdentifier(first)) {
    return ["GET", "PUT", "DELETE"];
  }
  if (
    resource === "recipes" &&
    segments.length === 3 &&
    isIdentifier(first) &&
    (second === "publish" || second === "archive")
  ) {
    return ["PATCH"];
  }
  if (
    resource === "recipes" &&
    segments.length === 3 &&
    isIdentifier(first) &&
    second === "ingredients"
  ) {
    return ["POST"];
  }
  if (
    resource === "recipes" &&
    segments.length === 4 &&
    isIdentifier(first) &&
    second === "ingredients" &&
    isIdentifier(third)
  ) {
    return ["PUT", "DELETE"];
  }
  if (
    resource === "recipes" &&
    segments.length === 3 &&
    isIdentifier(first) &&
    second === "images"
  ) {
    return ["POST"];
  }
  if (
    resource === "recipes" &&
    segments.length === 4 &&
    isIdentifier(first) &&
    second === "images" &&
    isIdentifier(third)
  ) {
    return ["DELETE"];
  }
  if (
    resource === "recipes" &&
    segments.length === 5 &&
    isIdentifier(first) &&
    second === "images" &&
    isIdentifier(third) &&
    segments[4] === "primary"
  ) {
    return ["PATCH"];
  }

  if (resource === "categories" && segments.length === 2 && isIdentifier(first)) {
    return ["GET", "PUT", "DELETE"];
  }
  if (
    resource === "categories" &&
    segments.length === 3 &&
    isIdentifier(first) &&
    second === "image"
  ) {
    return ["POST"];
  }
  if (segments.length === 2 && resource === "auth" && first === "me") {
    return ["GET", "PATCH"];
  }
  if (segments.length === 2 && resource === "auth" && first === "change-password") {
    return ["POST"];
  }

  return null;
}

function pathSegments(request: Request): string[] | null {
  const pathname = new URL(request.url).pathname;
  const prefix = "/api/backend/";
  if (!pathname.startsWith(prefix)) return null;

  const rawPath = pathname.slice(prefix.length);
  if (
    !rawPath ||
    rawPath.includes("\\") ||
    /%(?:2f|5c|2e)/i.test(rawPath) ||
    rawPath.includes("%")
  ) {
    return null;
  }

  const segments = rawPath.split("/");
  if (segments.some((segment) => !segment || segment === "." || segment === "..")) {
    return null;
  }
  return segments;
}

function serializedSessionCookie(name: string, value: string, secure: boolean): string {
  return [
    `${name}=${value}`,
    "Path=/",
    `Max-Age=${AUTH_SESSION_MAX_AGE_SECONDS}`,
    "HttpOnly",
    "SameSite=Lax",
    secure ? "Secure" : "",
  ].filter(Boolean).join("; ");
}

function sessionCookieHeaders(request: Request, value: string): string[] {
  const secure = AUTH_SESSION_COOKIE_NAME.startsWith("__Secure-");
  const chunks: string[] = [];
  for (let offset = 0; offset < value.length; offset += COOKIE_CHUNK_SIZE) {
    chunks.push(value.slice(offset, offset + COOKIE_CHUNK_SIZE));
  }

  const nextCookieNames = new Set(
    chunks.map((_, index) =>
      chunks.length === 1 ? AUTH_SESSION_COOKIE_NAME : `${AUTH_SESSION_COOKIE_NAME}.${index}`,
    ),
  );
  const existingCookieNames = (request.headers.get("cookie") ?? "")
    .split(";")
    .map((item) => item.trim().split("=", 1)[0])
    .filter((name) =>
      name === AUTH_SESSION_COOKIE_NAME ||
      new RegExp(`^${AUTH_SESSION_COOKIE_NAME.replace(/[.*+?^${}()|[\]\\]/g, "\\$&")}\\.\\d+$`).test(name),
    );

  const headers = existingCookieNames
    .filter((name) => !nextCookieNames.has(name))
    .map((name) => `${name}=; Path=/; Max-Age=0; HttpOnly; SameSite=Lax${secure ? "; Secure" : ""}`);
  headers.push(...chunks.map((chunk, index) =>
    serializedSessionCookie(
      chunks.length === 1 ? AUTH_SESSION_COOKIE_NAME : `${AUTH_SESSION_COOKIE_NAME}.${index}`,
      chunk,
      secure,
    ),
  ));
  return headers;
}

async function encodeSessionToken(token: JWT): Promise<string | null> {
  const secret = process.env.AUTH_SECRET;
  if (!secret) return null;
  return encode({
    token,
    secret,
    salt: AUTH_SESSION_COOKIE_NAME,
    maxAge: AUTH_SESSION_MAX_AGE_SECONDS,
  });
}

function jsonResponse(status: number, message: string, cookies: string[] = []): Response {
  const headers = new Headers({
    "Content-Type": "application/json; charset=utf-8",
    "Cache-Control": "no-store",
  });
  for (const cookie of cookies) headers.append("Set-Cookie", cookie);
  return new Response(JSON.stringify({ error: message }), { status, headers });
}

async function getValidAccessToken(
  request: Request,
): Promise<{ accessToken: string; cookies: string[] } | null> {
  const secret = process.env.AUTH_SECRET;
  if (!secret) return null;

  const token = await getToken({
    req: request,
    secret,
    cookieName: AUTH_SESSION_COOKIE_NAME,
    salt: AUTH_SESSION_COOKIE_NAME,
  });
  if (!token || !token.sub || token.error === "RefreshTokenError") return null;

  const accessToken = token.accessToken;
  const refreshToken = token.refreshToken;
  const expiresAt = token.accessTokenExpiresAt;
  if (
    typeof accessToken !== "string" ||
    typeof refreshToken !== "string" ||
    typeof expiresAt !== "number"
  ) {
    return null;
  }
  if (Date.now() < expiresAt - BACKEND_TOKEN_REFRESH_SKEW_MS) {
    return { accessToken, cookies: [] };
  }

  try {
    const rotated = await refreshBackendTokensOnce(refreshToken);
    const updatedToken: JWT = {
      ...token,
      accessToken: rotated.accessToken,
      refreshToken: rotated.refreshToken,
      accessTokenExpiresAt: Date.now() + rotated.expiresIn * 1000,
    };
    const encoded = await encodeSessionToken(updatedToken);
    if (!encoded) return null;
    return {
      accessToken: rotated.accessToken,
      cookies: sessionCookieHeaders(request, encoded),
    };
  } catch {
    const failedToken: JWT = {
      ...token,
      accessToken: undefined,
      refreshToken: undefined,
      accessTokenExpiresAt: 0,
      error: "RefreshTokenError",
    };
    const encoded = await encodeSessionToken(failedToken);
    return {
      accessToken: "",
      cookies: encoded ? sessionCookieHeaders(request, encoded) : [],
    };
  }
}

async function proxy(request: Request, method: HttpMethod): Promise<Response> {
  const session = await getValidAccessToken(request);
  if (!session || !session.accessToken) {
    return jsonResponse(401, "Unauthorized", session?.cookies);
  }

  const segments = pathSegments(request);
  if (!segments) return jsonResponse(404, "Not found");

  const methods = allowedMethods(segments);
  if (!methods) return jsonResponse(404, "Not found");
  if (!methods.includes(method)) {
    const response = jsonResponse(405, "Method not allowed");
    response.headers.set("Allow", methods.join(", "));
    return response;
  }

  if (method !== "GET") {
    const origin = request.headers.get("origin");
    if (!origin || origin !== new URL(request.url).origin) {
      return jsonResponse(403, "Forbidden");
    }
  }

  const upstreamPath = `/api/v1/${segments.join("/")}`;
  let upstreamUrl: URL;
  try {
    upstreamUrl = internalApiEndpoint(upstreamPath);
  } catch {
    return jsonResponse(502, "Backend unavailable", session.cookies);
  }
  upstreamUrl.search = new URL(request.url).search;

  const headers = new Headers({
    Authorization: `Bearer ${session.accessToken}`,
  });
  const contentType = request.headers.get("content-type");
  if (contentType) headers.set("Content-Type", contentType);
  const accept = request.headers.get("accept");
  if (accept) headers.set("Accept", accept);

  try {
    const body = method === "GET" ? undefined : await request.arrayBuffer();
    const upstream = await fetch(upstreamUrl, {
      method,
      headers,
      body,
      cache: "no-store",
      redirect: "manual",
    });
    const responseHeaders = new Headers({ "Cache-Control": "no-store" });
    for (const name of UPSTREAM_RESPONSE_HEADERS) {
      const value = upstream.headers.get(name);
      if (value) responseHeaders.set(name, value);
    }
    for (const cookie of session.cookies) responseHeaders.append("Set-Cookie", cookie);

    return new Response(upstream.body, {
      status: upstream.status,
      headers: responseHeaders,
    });
  } catch {
    return jsonResponse(502, "Backend unavailable", session.cookies);
  }
}

export async function GET(request: Request): Promise<Response> {
  return proxy(request, "GET");
}

export async function POST(request: Request): Promise<Response> {
  return proxy(request, "POST");
}

export async function PUT(request: Request): Promise<Response> {
  return proxy(request, "PUT");
}

export async function PATCH(request: Request): Promise<Response> {
  return proxy(request, "PATCH");
}

export async function DELETE(request: Request): Promise<Response> {
  return proxy(request, "DELETE");
}

export async function HEAD(): Promise<Response> {
  return new Response(null, {
    status: 405,
    headers: { Allow: "GET, POST, PUT, PATCH, DELETE", "Cache-Control": "no-store" },
  });
}

export async function OPTIONS(): Promise<Response> {
  return new Response(null, {
    status: 405,
    headers: { Allow: "GET, POST, PUT, PATCH, DELETE", "Cache-Control": "no-store" },
  });
}
