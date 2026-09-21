const API_BASE_URL = process.env.NEXT_PUBLIC_API_URL || "http://localhost:5000";

export interface RegisterPayload {
  email: string;
  password: string;
  displayName: string;
}

export interface RegisterResult {
  userId: string;
  email: string;
  displayName: string;
}

export interface LoginPayload {
  email: string;
  password: string;
}

export interface AuthResult {
  accessToken: string;
  refreshToken: string;
  expiresIn: number;
}

export interface ApiError {
  status: number;
  title: string;
  detail: string;
  errors?: Record<string, string[]>;
}

export async function registerApi(payload: RegisterPayload): Promise<RegisterResult> {
  const response = await fetch(`${API_BASE_URL}/api/v1/auth/register`, {
    method: "POST",
    headers: {
      "Content-Type": "application/json",
    },
    body: JSON.stringify(payload),
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
      title: errorData.title || (response.status === 409 ? "Conflict" : "Error"),
      detail:
        errorData.detail ||
        (response.status === 409
          ? "Email này đã được sử dụng. Vui lòng chọn email khác hoặc đăng nhập."
          : response.status >= 500
          ? "Đã có lỗi xảy ra từ hệ thống. Vui lòng thử lại sau."
          : "Thông tin đăng ký không hợp lệ."),
      errors: errorData.errors,
    };
    throw error;
  }

  return await response.json();
}

export async function loginApi(payload: LoginPayload): Promise<AuthResult> {
  const response = await fetch(`${API_BASE_URL}/api/v1/auth/login`, {
    method: "POST",
    headers: {
      "Content-Type": "application/json",
    },
    body: JSON.stringify(payload),
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
      title: errorData.title || "Authentication Error",
      detail:
        errorData.detail ||
        (response.status === 401
          ? "Email hoặc mật khẩu không chính xác."
          : response.status === 423
          ? "Tài khoản tạm thời bị khóa do nhiều lần đăng nhập không thành công. Vui lòng thử lại sau 15 phút."
          : response.status >= 500
          ? "Đã có lỗi xảy ra từ máy chủ. Vui lòng thử lại sau."
          : "Đăng nhập không thành công."),
    };
    throw error;
  }

  return await response.json();
}

export async function googleLoginApi(idToken: string): Promise<AuthResult> {
  const response = await fetch(`${API_BASE_URL}/api/v1/auth/google`, {
    method: "POST",
    headers: {
      "Content-Type": "application/json",
    },
    body: JSON.stringify({ idToken }),
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
      title: errorData.title || "Google Auth Error",
      detail:
        errorData.detail ||
        (response.status === 400 || response.status === 401
          ? "Google token không hợp lệ hoặc đã hết hạn."
          : "Xác thực với Google không thành công."),
    };
    throw error;
  }

  return await response.json();
}
