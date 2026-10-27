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

export interface ChangePasswordRequest {
  currentPassword: string;
  newPassword: string;
  confirmPassword: string;
}

export async function changePasswordApi(
  accessToken: string,
  request: ChangePasswordRequest
): Promise<{ message: string }> {
  const response = await fetch(`${API_BASE_URL}/api/v1/auth/change-password`, {
    method: "POST",
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
      title: errorData.title || "Change Password Error",
      detail:
        errorData.detail ||
        (response.status === 401
          ? "Phiên làm việc đã hết hạn hoặc mật khẩu hiện tại không đúng."
          : response.status === 400
          ? "Dữ liệu mật khẩu không hợp lệ."
          : "Không thể đổi mật khẩu. Vui lòng thử lại."),
      errors: errorData.errors,
    };
    throw error;
  }

  return await response.json();
}

export interface ForgotPasswordRequest {
  email: string;
}

export async function forgotPasswordApi(
  request: ForgotPasswordRequest
): Promise<{ message: string }> {
  const response = await fetch(`${API_BASE_URL}/api/v1/auth/forgot-password`, {
    method: "POST",
    headers: {
      "Content-Type": "application/json",
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
      title: errorData.title || "Forgot Password Error",
      detail:
        errorData.detail ||
        (response.status === 400
          ? "Địa chỉ email không hợp lệ."
          : "Không thể xử lý yêu cầu quên mật khẩu."),
      errors: errorData.errors,
    };
    throw error;
  }

  return await response.json();
}

export interface ResetPasswordRequest {
  email: string;
  token: string;
  newPassword: string;
  confirmPassword: string;
}

export async function resetPasswordApi(
  request: ResetPasswordRequest
): Promise<{ message: string }> {
  const response = await fetch(`${API_BASE_URL}/api/v1/auth/reset-password`, {
    method: "POST",
    headers: {
      "Content-Type": "application/json",
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
      title: errorData.title || "Reset Password Error",
      detail:
        errorData.detail ||
        (response.status === 400
          ? "Mã xác thực không hợp lệ hoặc mật khẩu mới không đúng quy định."
          : "Không thể đặt lại mật khẩu. Vui lòng thử lại."),
      errors: errorData.errors,
    };
    throw error;
  }

  return await response.json();
}
