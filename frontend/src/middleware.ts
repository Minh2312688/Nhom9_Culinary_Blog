import { auth, AUTH_SESSION_COOKIE_NAME } from "@/auth";
import type { NextRequest } from "next/server";
import { NextResponse } from "next/server";

const CALLBACK_ORIGIN = "http://next.internal";

function localCallback(value: string): string {
  if (
    !value.startsWith("/") ||
    value.startsWith("//") ||
    value.includes("\\") ||
    /[\u0000-\u001f]/.test(value)
  ) {
    return "/";
  }

  try {
    const url = new URL(value, CALLBACK_ORIGIN);
    if (url.origin !== CALLBACK_ORIGIN) return "/";
    return `${url.pathname}${url.search}${url.hash}`;
  } catch {
    return "/";
  }
}

const authMiddleware = auth((request) => {
  const sessionExpired = request.auth?.error === "RefreshTokenError";
  if (!request.auth?.user || sessionExpired) {
    const requestedPath = `${request.nextUrl.pathname}${request.nextUrl.search}`;
    const loginUrl = request.nextUrl.clone();
    loginUrl.pathname = "/auth/login";
    loginUrl.search = "";
    loginUrl.searchParams.set("callbackUrl", localCallback(requestedPath));
    if (sessionExpired) loginUrl.searchParams.set("reason", "session-expired");
    return NextResponse.redirect(loginUrl);
  }

  if (
    (request.nextUrl.pathname.startsWith("/dashboard/categories") ||
      request.nextUrl.pathname.startsWith("/dashboard/users")) &&
    !request.auth.user.roles?.includes("Admin")
  ) {
    const dashboardUrl = request.nextUrl.clone();
    dashboardUrl.pathname = "/dashboard";
    dashboardUrl.search = "";
    return NextResponse.redirect(dashboardUrl);
  }

  return NextResponse.next();
});

export default async function middleware(request: NextRequest) {
  const response = await authMiddleware(request, { params: Promise.resolve({}) }) ?? NextResponse.next();
  const redirect = response.headers.get("location");
  if (redirect && new URL(redirect).searchParams.get("reason") === "session-expired") {
    const secure = AUTH_SESSION_COOKIE_NAME.startsWith("__Secure-");
    const sessionCookieNames = request.cookies
      .getAll()
      .map(({ name }) => name)
      .filter((name) =>
        name === AUTH_SESSION_COOKIE_NAME || name.startsWith(`${AUTH_SESSION_COOKIE_NAME}.`),
      );
    for (const name of sessionCookieNames.length ? sessionCookieNames : [AUTH_SESSION_COOKIE_NAME]) {
      response.headers.append(
        "Set-Cookie",
        `${name}=; Path=/; Max-Age=0; HttpOnly; SameSite=Lax${secure ? "; Secure" : ""}`,
      );
    }
  }
  return response;
}

export const config = {
  matcher: ["/dashboard/:path*", "/profile"],
};
