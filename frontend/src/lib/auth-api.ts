import { ApiError, AuthResult } from "./api";

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

export async function refreshTokenApi(refreshToken: string): Promise<AuthResult> {
  const response = await fetch(`${API_BASE_URL}/api/v1/auth/refresh`, {
    method: "POST",
    headers: {
      "Content-Type": "application/json",
    },
    body: JSON.stringify({ refreshToken }),
  });

  if (!response.ok) {
    let errorData: any = {};
    try {
      errorData = await response.json();
    } catch {
      // Non-JSON response
    }

    const error: ApiError = {
      status: response.status,
      title: errorData.title || "Refresh Token Error",
      detail:
        errorData.detail ||
        (response.status === 401
          ? "Phiên làm việc đã hết hạn. Vui lòng đăng nhập lại."
          : "Không thể làm mới phiên đăng nhập."),
      errors: errorData.errors,
    };
    throw error;
  }

  return await response.json();
}

export async function logoutApi(accessToken: string, refreshToken: string): Promise<void> {
  const response = await fetch(`${API_BASE_URL}/api/v1/auth/logout`, {
    method: "POST",
    headers: {
      "Content-Type": "application/json",
      Authorization: `Bearer ${accessToken}`,
    },
    body: JSON.stringify({ refreshToken }),
  });

  if (!response.ok && response.status !== 204) {
    let errorData: any = {};
    try {
      errorData = await response.json();
    } catch {
      // Non-JSON response
    }

    const error: ApiError = {
      status: response.status,
      title: errorData.title || "Logout Error",
      detail: errorData.detail || "Đăng xuất không thành công.",
    };
    throw error;
  }
}

export async function getProfileApi(accessToken: string): Promise<UserProfile> {
  const response = await fetch(`${API_BASE_URL}/api/v1/auth/me`, {
    method: "GET",
    headers: {
      Authorization: `Bearer ${accessToken}`,
    },
  });

  if (!response.ok) {
    let errorData: any = {};
    try {
      errorData = await response.json();
    } catch {
      // Non-JSON response
    }

    const error: ApiError = {
      status: response.status,
      title: errorData.title || "Profile Error",
      detail:
        errorData.detail ||
        (response.status === 401
          ? "Phiên làm việc đã hết hạn hoặc không hợp lệ."
          : response.status === 404
          ? "Không tìm thấy thông tin người dùng."
          : "Không thể tải thông tin trang cá nhân."),
    };
    throw error;
  }

  return await response.json();
}

export interface UpdateProfileRequest {
  displayName?: string | null;
  avatarUrl?: string | null;
  bio?: string | null;
}

export async function updateProfileApi(
  accessToken: string,
  request: UpdateProfileRequest
): Promise<UserProfile> {
  const response = await fetch(`${API_BASE_URL}/api/v1/auth/me`, {
    method: "PATCH",
    headers: {
      "Content-Type": "application/json",
      Authorization: `Bearer ${accessToken}`,
    },
    body: JSON.stringify(request),
  });

  if (!response.ok) {
    let errorData: any = {};
    try {
      errorData = await response.json();
    } catch {
      // Non-JSON response
    }

    const error: ApiError = {
      status: response.status,
      title: errorData.title || "Profile Update Error",
      detail:
        errorData.detail ||
        (response.status === 401
          ? "Phiên làm việc đã hết hạn. Vui lòng đăng nhập lại."
          : response.status === 404
          ? "Không tìm thấy thông tin người dùng."
          : "Không thể cập nhật thông tin hồ sơ."),
      errors: errorData.errors,
    };
    throw error;
  }

  return await response.json();
}
