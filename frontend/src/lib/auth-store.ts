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
 *   Lưu ý: HttpOnly Cookie bảo vệ cookie khỏi bị JavaScript đọc trực tiếp nhưng không loại bỏ
 *   hoàn toàn mọi tác động của XSS (kẻ tấn công vẫn có thể mượn quyền gửi request).
 * - Sử dụng mô hình Observer Pub-Sub + React useSyncExternalStore tương thích SSR Next.js 15 / React 19.
 */

const ACCESS_TOKEN_KEY = "culinary_access_token";
const REFRESH_TOKEN_KEY = "culinary_refresh_token";

export interface AuthTokens {
  accessToken: string | null;
  refreshToken: string | null;
}

export interface AuthState extends AuthTokens {
  isAuthenticated: boolean;
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
 * Lưu cặp Token vào sessionStorage sau khi đăng nhập hoặc refresh thành công.
 * Cơ chế Rollback Atomic (All-or-Nothing):
 * Nếu một trong hai token ghi thất bại (ví dụ: QuotaExceededError hoặc SecurityError),
 * hệ thống sẽ xóa sạch cả hai token để tránh trạng thái nửa vời (Access Token mới đi cùng Refresh Token cũ),
 * đồng thời thông báo tới các subscriber và trả về false.
 */
export function setAuthTokens(accessToken: string, refreshToken: string): boolean {
  if (typeof window === "undefined") return false;
  try {
    sessionStorage.setItem(ACCESS_TOKEN_KEY, accessToken);
    sessionStorage.setItem(REFRESH_TOKEN_KEY, refreshToken);
    emitChange();
    return true;
  } catch (error) {
    console.error("Failed to save auth tokens to sessionStorage, rolling back", error);
    try {
      sessionStorage.removeItem(ACCESS_TOKEN_KEY);
      sessionStorage.removeItem(REFRESH_TOKEN_KEY);
    } catch {
      // Ignore cleanup error
    }
    emitChange();
    return false;
  }
}

/**
 * Xóa sạch phiên đăng nhập khi Logout hoặc Token hết hạn/bị hủy.
 * Cố gắng xóa cả hai token ngay cả khi một thao tác gặp lỗi.
 * Trả về true nếu cả hai được xóa thành công, false nếu có lỗi xảy ra.
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
};

function getSnapshot(): AuthState {
  const accessToken = getAccessToken();
  const refreshToken = getRefreshToken();
  const isAuth = Boolean(accessToken);

  if (
    cachedState.accessToken !== accessToken ||
    cachedState.refreshToken !== refreshToken ||
    cachedState.isAuthenticated !== isAuth
  ) {
    cachedState = {
      accessToken,
      refreshToken,
      isAuthenticated: isAuth,
    };
  }

  return cachedState;
}

const SERVER_SNAPSHOT: AuthState = {
  accessToken: null,
  refreshToken: null,
  isAuthenticated: false,
};

function getServerSnapshot(): AuthState {
  return SERVER_SNAPSHOT;
}

function subscribe(callback: () => void): () => void {
  listeners.add(callback);

  // Lắng nghe sự kiện storage trên window nếu có đồng bộ tab
  const handleStorage = (e: StorageEvent) => {
    if (e.key === ACCESS_TOKEN_KEY || e.key === REFRESH_TOKEN_KEY) {
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
