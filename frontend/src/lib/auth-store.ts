"use client";

import { useSyncExternalStore } from "react";

/**
 * SRS FR-AUTH-002, FR-AUTH-005, FR-AUTH-006:
 * Client-Side Authentication State Store (Lab 6 - Yêu cầu 3)
 * Phụ trách (Owner): TV1 - Nguyễn Phạm Phú Nam (MSSV: 2312695)
 *
 * Lưu trữ phiên làm việc trong sessionStorage:
 * - Giới hạn vòng đời token theo phiên tab trình duyệt (tự hủy khi đóng tab),
 *   tránh lưu trữ vĩnh viễn trên thiết bị công cộng như localStorage.
 * - PHÂN TÁCH PHIÊN THEO TAB (Per-tab Isolation theo chuẩn MDN):
 *   sessionStorage được cô lập theo từng tab trình duyệt (top-level browsing context).
 *   Các tab độc lập KHÔNG tự chia sẻ hay đồng bộ token với nhau như localStorage.
 *   Hàm emitChange() thực hiện thông báo cho các component subscriber trong cùng tab hiện tại.
 * - LƯU Ý BẢO MẬT (theo OWASP Session Management Cheat Sheet): sessionStorage
 *   KHÔNG ngăn chặn được tấn công XSS (Cross-Site Scripting). Nếu ứng dụng bị dính XSS,
 *   mã độc chạy trên cùng origin vẫn có thể đọc toàn bộ sessionStorage.
 * - Sử dụng mô hình Observer Pub-Sub + React useSyncExternalStore tương thích SSR Next.js 15 / React 19.
 */

const ACCESS_TOKEN_KEY = "culinary_access_token";
const REFRESH_TOKEN_KEY = "culinary_refresh_token";
const USER_PROFILE_KEY = "culinary_user_profile";

export interface StoredUserProfile {
  displayName?: string;
  email?: string;
  avatarUrl?: string | null;
}

export interface AuthTokens {
  accessToken: string | null;
  refreshToken: string | null;
}

export interface AuthState extends AuthTokens {
  isAuthenticated: boolean;
  user: StoredUserProfile | null;
}

const listeners = new Set<() => void>();

function emitChange() {
  for (const listener of listeners) {
    try {
      listener();
    } catch (err) {
      console.error("Error in auth listener callback", err);
    }
  }
}

/**
 * Lấy Access Token hiện tại từ sessionStorage (SSR-safe).
 */
export function getAccessToken(): string | null {
  if (typeof window === "undefined") return null;
  try {
    return sessionStorage.getItem(ACCESS_TOKEN_KEY);
  } catch {
    return null;
  }
}

/**
 * Lấy Refresh Token hiện tại từ sessionStorage (SSR-safe).
 */
export function getRefreshToken(): string | null {
  if (typeof window === "undefined") return null;
  try {
    return sessionStorage.getItem(REFRESH_TOKEN_KEY);
  } catch {
    return null;
  }
}

/**
 * Lấy thông tin user cache từ sessionStorage (SSR-safe).
 */
export function getStoredUser(): StoredUserProfile | null {
  if (typeof window === "undefined") return null;
  try {
    const raw = sessionStorage.getItem(USER_PROFILE_KEY);
    return raw ? JSON.parse(raw) : null;
  } catch {
    return null;
  }
}

/**
 * Lưu hoặc cập nhật thông tin user trong sessionStorage.
 */
export function setStoredUser(user: StoredUserProfile | null): void {
  if (typeof window === "undefined") return;
  try {
    if (user) {
      sessionStorage.setItem(USER_PROFILE_KEY, JSON.stringify(user));
    } else {
      sessionStorage.removeItem(USER_PROFILE_KEY);
    }
    emitChange();
  } catch (err) {
    console.error("Failed to save user profile to sessionStorage", err);
  }
}

/**
 * Giải mã nhanh email / displayName cơ bản từ JWT payload (SSR-safe, không dùng external lib).
 */
function parseJwtUser(token: string | null): StoredUserProfile | null {
  if (!token) return null;
  try {
    const parts = token.split(".");
    if (parts.length !== 3) return null;
    const base64 = parts[1].replace(/-/g, "+").replace(/_/g, "/");
    const json = decodeURIComponent(
      atob(base64)
        .split("")
        .map((c) => "%" + ("00" + c.charCodeAt(0).toString(16)).slice(-2))
        .join("")
    );
    const payload = JSON.parse(json);
    const email = payload.email || payload["http://schemas.xmlsoap.org/ws/2005/05/identity/claims/emailaddress"];
    const name = payload.displayName || payload.name || (email ? email.split("@")[0] : null);
    if (!email && !name) return null;
    return {
      email: email ?? undefined,
      displayName: name ?? undefined,
    };
  } catch {
    return null;
  }
}

/**
 * Lưu cặp Token vào sessionStorage sau khi đăng nhập hoặc refresh thành công.
 * Cơ chế Rollback Atomic (All-or-Nothing).
 */
export function setAuthTokens(accessToken: string, refreshToken: string): boolean {
  if (typeof window === "undefined") return false;
  try {
    sessionStorage.setItem(ACCESS_TOKEN_KEY, accessToken);
    sessionStorage.setItem(REFRESH_TOKEN_KEY, refreshToken);

    // Trích xuất thông tin cơ bản từ JWT nếu chưa có profile cache
    if (!sessionStorage.getItem(USER_PROFILE_KEY)) {
      const jwtUser = parseJwtUser(accessToken);
      if (jwtUser) {
        sessionStorage.setItem(USER_PROFILE_KEY, JSON.stringify(jwtUser));
      }
    }

    emitChange();
    return true;
  } catch (error) {
    console.error("Failed to save auth tokens to sessionStorage, rolling back", error);
    try {
      sessionStorage.removeItem(ACCESS_TOKEN_KEY);
      sessionStorage.removeItem(REFRESH_TOKEN_KEY);
      sessionStorage.removeItem(USER_PROFILE_KEY);
    } catch {
      // Ignore cleanup error
    }
    emitChange();
    return false;
  }
}

/**
 * Xóa sạch phiên đăng nhập khi Logout hoặc Token hết hạn/bị hủy.
 */
export function clearAuthTokens(): boolean {
  if (typeof window === "undefined") return false;
  let success = true;
  try {
    sessionStorage.removeItem(ACCESS_TOKEN_KEY);
  } catch (error) {
    console.error("Failed to remove access token from sessionStorage", error);
    success = false;
  }
  try {
    sessionStorage.removeItem(REFRESH_TOKEN_KEY);
  } catch (error) {
    console.error("Failed to remove refresh token from sessionStorage", error);
    success = false;
  }
  try {
    sessionStorage.removeItem(USER_PROFILE_KEY);
  } catch (error) {
    console.error("Failed to remove user profile from sessionStorage", error);
  }
  emitChange();
  return success;
}

/**
 * Kiểm tra trạng thái đã đăng nhập hay chưa.
 */
export function isAuthenticated(): boolean {
  return Boolean(getAccessToken());
}

let cachedState: AuthState = {
  accessToken: null,
  refreshToken: null,
  isAuthenticated: false,
  user: null,
};

function getSnapshot(): AuthState {
  const accessToken = getAccessToken();
  const refreshToken = getRefreshToken();
  const isAuth = Boolean(accessToken);
  const user = getStoredUser() || parseJwtUser(accessToken);

  const userChanged = JSON.stringify(cachedState.user) !== JSON.stringify(user);

  if (
    cachedState.accessToken !== accessToken ||
    cachedState.refreshToken !== refreshToken ||
    cachedState.isAuthenticated !== isAuth ||
    userChanged
  ) {
    cachedState = {
      accessToken,
      refreshToken,
      isAuthenticated: isAuth,
      user,
    };
  }

  return cachedState;
}

const SERVER_SNAPSHOT: AuthState = {
  accessToken: null,
  refreshToken: null,
  isAuthenticated: false,
  user: null,
};

function getServerSnapshot(): AuthState {
  return SERVER_SNAPSHOT;
}

function subscribe(callback: () => void): () => void {
  listeners.add(callback);

  const handleStorage = (e: StorageEvent) => {
    if (
      e.key === ACCESS_TOKEN_KEY ||
      e.key === REFRESH_TOKEN_KEY ||
      e.key === USER_PROFILE_KEY
    ) {
      callback();
    }
  };

  if (typeof window !== "undefined") {
    window.addEventListener("storage", handleStorage);
  }

  return () => {
    listeners.delete(callback);
    if (typeof window !== "undefined") {
      window.removeEventListener("storage", handleStorage);
    }
  };
}

/**
 * Hook useAuth: Đăng ký lắng nghe trạng thái đăng nhập trong các Client Component.
 */
export function useAuth(): AuthState {
  return useSyncExternalStore(subscribe, getSnapshot, getServerSnapshot);
}
