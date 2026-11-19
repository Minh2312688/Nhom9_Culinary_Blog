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
type RequestMethod = HttpMethod | "HEAD" | "OPTIONS";

function isIdentifier(segment: string): boolean {
  return /^[A-Za-z0-9_-]+$/.test(segment) && segment !== "." && segment !== "..";
}

function allowedMethods(segments: string[]): HttpMethod[] | null {
  const [resource, first, second, third] = segments;

  if (segments.length === 1 && resource === "recipes") return ["GET", "POST"];
  if (segments.length === 1 && resource === "categories") return ["GET", "POST"];

  if (resource === "recipes" && segments.length === 2 && first === "mine") {
    return ["GET"];
  }
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
    second === "steps" &&
    isIdentifier(third) &&
    segments[4] === "image"
  ) {
    return ["POST"];
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
  if (segments.length === 2 && resource === "admin" && first === "users") {
    return ["GET"];
  }
  if (segments.length === 2 && resource === "admin" && first === "recipes") {
    return ["GET"];
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

function hasSameOrigin(request: Request, origin: string | null): boolean {
  if (!origin) return false;

  const host = request.headers.get("host");
  if (!host) return false;

  const requestUrl = new URL(request.url);
  const forwardedProtocol = request.headers.get("x-forwarded-proto")?.split(",")[0]?.trim();
  const protocol = forwardedProtocol || requestUrl.protocol.slice(0, -1);
  if (protocol !== "http" && protocol !== "https") return false;

  try {
    const originUrl = new URL(origin);
    const requestOrigin = new URL(`${protocol}://${host}`).origin;
    return originUrl.origin === origin && originUrl.origin === requestOrigin;
  } catch {
    return false;
  }
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

function existingSessionCookieNames(request: Request): string[] {
  return (request.headers.get("cookie") ?? "")
    .split(";")
    .map((item) => item.trim().split("=", 1)[0])
    .filter((name) =>
      name === AUTH_SESSION_COOKIE_NAME ||
      new RegExp(`^${AUTH_SESSION_COOKIE_NAME.replace(/[.*+?^${}()|[\]\\]/g, "\\$&")}\\.\\d+$`).test(name),
    );
}

function expiredSessionCookieHeaders(request: Request): string[] {
  const secure = AUTH_SESSION_COOKIE_NAME.startsWith("__Secure-");
  const names = existingSessionCookieNames(request);
  return (names.length ? names : [AUTH_SESSION_COOKIE_NAME]).map(
    (name) => `${name}=; Path=/; Max-Age=0; HttpOnly; SameSite=Lax${secure ? "; Secure" : ""}`,
  );
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

  const headers = existingSessionCookieNames(request)
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
  forceRefresh = false,
  currentToken?: JWT,
): Promise<{ accessToken: string; cookies: string[]; token: JWT } | null> {
  const secret = process.env.AUTH_SECRET;
  if (!secret) return null;

  const token = currentToken ?? await getToken({
    req: request,
    secret,
    cookieName: AUTH_SESSION_COOKIE_NAME,
    salt: AUTH_SESSION_COOKIE_NAME,
  });
  if (!token) return null;
  if (!token.sub || token.error === "RefreshTokenError") {
    return { accessToken: "", cookies: expiredSessionCookieHeaders(request), token };
  }

  const accessToken = token.accessToken;
  const refreshToken = token.refreshToken;
  const expiresAt = token.accessTokenExpiresAt;
  if (
    typeof accessToken !== "string" ||
    typeof refreshToken !== "string" ||
    typeof expiresAt !== "number"
  ) {
    return { accessToken: "", cookies: expiredSessionCookieHeaders(request), token };
  }
  if (!forceRefresh && Date.now() < expiresAt - BACKEND_TOKEN_REFRESH_SKEW_MS) {
    return { accessToken, cookies: [], token };
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
    if (!encoded) {
      return { accessToken: "", cookies: expiredSessionCookieHeaders(request), token: updatedToken };
    }
    return {
      accessToken: rotated.accessToken,
      cookies: sessionCookieHeaders(request, encoded),
      token: updatedToken,
    };
  } catch {
    return {
      accessToken: "",
      cookies: expiredSessionCookieHeaders(request),
      token,
    };
  }
}

async function proxy(request: Request, method: RequestMethod): Promise<Response> {
  const session = await getValidAccessToken(request);
  if (!session || !session.accessToken) {
    return jsonResponse(401, "Unauthorized", session?.cookies);
  }

  const segments = pathSegments(request);
  if (!segments) return jsonResponse(404, "Not found", session.cookies);

  const methods = allowedMethods(segments);
  if (!methods) return jsonResponse(404, "Not found", session.cookies);
  if (!methods.includes(method as HttpMethod)) {
    const response = jsonResponse(405, "Method not allowed", session.cookies);
    response.headers.set("Allow", methods.join(", "));
    return response;
  }

  if (method !== "GET") {
    const origin = request.headers.get("origin");
    if (!hasSameOrigin(request, origin)) {
      return jsonResponse(403, "Forbidden", session.cookies);
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

  let responseCookies = session.cookies;
  try {
    const body = method === "GET" ? undefined : await request.arrayBuffer();
    const sendToBackend = (accessToken: string) => {
      const headers = new Headers({ Authorization: `Bearer ${accessToken}` });
      const contentType = request.headers.get("content-type");
      if (contentType) headers.set("Content-Type", contentType);
      const accept = request.headers.get("accept");
      if (accept) headers.set("Accept", accept);

      return fetch(upstreamUrl, {
        method,
        headers,
        body,
        cache: "no-store",
        redirect: "manual",
      });
    };

    let upstream = await sendToBackend(session.accessToken);
    if (upstream.status === 401) {
      const refreshed = await getValidAccessToken(request, true, session.token);
      responseCookies = [...responseCookies, ...(refreshed?.cookies ?? [])];
      if (!refreshed?.accessToken) {
        if (!refreshed) responseCookies.push(...expiredSessionCookieHeaders(request));
        return jsonResponse(401, "Unauthorized", responseCookies);
      }

      upstream = await sendToBackend(refreshed.accessToken);
      if (upstream.status === 401) {
        responseCookies.push(...expiredSessionCookieHeaders(request));
      }
    }

    const responseHeaders = new Headers({ "Cache-Control": "no-store" });
    for (const name of UPSTREAM_RESPONSE_HEADERS) {
      const value = upstream.headers.get(name);
      if (value) responseHeaders.set(name, value);
    }
    for (const cookie of responseCookies) responseHeaders.append("Set-Cookie", cookie);

    return new Response(upstream.body, {
      status: upstream.status,
      headers: responseHeaders,
    });
  } catch {
    return jsonResponse(502, "Backend unavailable", responseCookies);
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

export async function HEAD(request: Request): Promise<Response> {
  return proxy(request, "HEAD");
}

export async function OPTIONS(request: Request): Promise<Response> {
  return proxy(request, "OPTIONS");
}
