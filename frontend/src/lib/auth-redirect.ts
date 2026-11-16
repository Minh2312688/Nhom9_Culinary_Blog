export function getLoginCallbackUrl(): string {
  if (typeof window === "undefined") return "/";

  const search = new URLSearchParams(window.location.search);
  if (search.get("reason") !== "session-expired") return "/";

  const value = search.get("callbackUrl");
  if (
    !value ||
    !value.startsWith("/") ||
    value.startsWith("//") ||
    value.includes("\\") ||
    /[\u0000-\u001f]/.test(value)
  ) {
    return "/";
  }

  try {
    const url = new URL(value, window.location.origin);
    if (url.origin !== window.location.origin) return "/";
    return `${url.pathname}${url.search}${url.hash}`;
  } catch {
    return "/";
  }
}

export function redirectToLoginForCurrentPage(): void {
  if (typeof window === "undefined" || window.location.pathname === "/auth/login") return;

  const callbackUrl = `${window.location.pathname}${window.location.search}${window.location.hash}`;
  window.location.replace(
    `/auth/login?reason=session-expired&callbackUrl=${encodeURIComponent(callbackUrl)}`,
  );
}
