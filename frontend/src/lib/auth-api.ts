import type { ApiError } from "./api";
import { redirectToLoginForCurrentPage } from "./auth-redirect";

const API_BASE_URL = process.env.NEXT_PUBLIC_API_URL || "http://localhost:5000";

export interface UserProfile {
  id: string;
  email: string;
  displayName: string;
  avatarUrl?: string | null;
  bio?: string | null;
  emailConfirmed: boolean;
  createdAt: string;
  roles: string[];
}

async function request<T>(
  url: string,
  init: RequestInit,
  fallbackMessage: string,
  isPrivate = false,
): Promise<T> {
  const response = await fetch(url, init);
  if (!response.ok) {
    if (isPrivate && response.status === 401) redirectToLoginForCurrentPage();
    let errorData: Record<string, unknown> = {};
    try {
      const body: unknown = await response.json();
      if (body && typeof body === "object" && !Array.isArray(body)) {
        errorData = body as Record<string, unknown>;
      }
    } catch {
      // Non-JSON response.
    }

    const errors = errorData.errors;
    const apiError: ApiError = {
      status: response.status,
      title: typeof errorData.title === "string" ? errorData.title : "Request Error",
      detail:
        typeof errorData.detail === "string"
          ? errorData.detail
          : fallbackMessage,
      ...(errors && typeof errors === "object" && !Array.isArray(errors)
        ? { errors: errors as Record<string, string[]> }
        : {}),
    };
    throw apiError;
  }

  if (response.status === 204) return undefined as T;
  return (await response.json()) as T;
}

function privateRequest<T>(
  path: string,
  init: RequestInit,
  fallbackMessage: string,
): Promise<T> {
  return request(`/api/backend${path}`, {
    ...init,
    cache: "no-store",
  }, fallbackMessage, true);
}

function publicRequest<T>(
  path: string,
  init: RequestInit,
  fallbackMessage: string,
): Promise<T> {
  return request(`${API_BASE_URL}/api/v1/${path}`, {
    ...init,
    credentials: "omit",
  }, fallbackMessage);
}

export function getProfileApi(): Promise<UserProfile> {
  return privateRequest(
    "/auth/me",
    { method: "GET" },
    "Không thể tải thông tin trang cá nhân.",
  );
}

export interface UpdateProfileRequest {
  displayName?: string | null;
  avatarUrl?: string | null;
  bio?: string | null;
}

export function updateProfileApi(requestBody: UpdateProfileRequest): Promise<UserProfile> {
  return privateRequest(
    "/auth/me",
    {
      method: "PATCH",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify(requestBody),
    },
    "Không thể cập nhật thông tin hồ sơ.",
  );
}

export interface ChangePasswordRequest {
  currentPassword: string;
  newPassword: string;
  confirmPassword: string;
}

export function changePasswordApi(
  requestBody: ChangePasswordRequest,
): Promise<{ message: string }> {
  return privateRequest(
    "/auth/change-password",
    {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify(requestBody),
    },
    "Không thể đổi mật khẩu. Vui lòng thử lại.",
  );
}

export interface ForgotPasswordRequest {
  email: string;
}

export function forgotPasswordApi(
  requestBody: ForgotPasswordRequest,
): Promise<{ message: string }> {
  return publicRequest(
    "auth/forgot-password",
    {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify(requestBody),
    },
    "Không thể xử lý yêu cầu quên mật khẩu.",
  );
}

export interface ResetPasswordRequest {
  email: string;
  token: string;
  newPassword: string;
  confirmPassword: string;
}

export function resetPasswordApi(
  requestBody: ResetPasswordRequest,
): Promise<{ message: string }> {
  return publicRequest(
    "auth/reset-password",
    {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify(requestBody),
    },
    "Không thể đặt lại mật khẩu. Vui lòng thử lại.",
  );
}
