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

export interface RecipeIngredient {
  id: string;
  name: string;
  quantity: number | null;
  unit: string | null;
  notes: string | null;
  orderIndex: number;
}

export interface RecipeStep {
  id: string;
  stepNumber: number;
  title: string | null;
  description: string;
  durationMinutes: number | null;
  imageUrl: string | null;
}

export interface RecipeDetail {
  id: string;
  title: string;
  slug: string;
  ingredients: RecipeIngredient[];
  steps: RecipeStep[];
}

export interface RecipeIngredientPayload {
  name: string;
  quantity: number | null;
  unit: string | null;
  notes: string | null;
  orderIndex: number;
}

export interface RecipeStepPayload {
  title: string;
  description: string;
  durationMinutes: number | null;
  imageUrl: string | null;
}

async function recipeRequest<T>(path: string, token: string, init: RequestInit = {}): Promise<T> {
  const response = await fetch(`${API_BASE_URL}${path}`, {
    ...init,
    headers: {
      Accept: "application/json",
      ...(init.body ? { "Content-Type": "application/json" } : {}),
      Authorization: `Bearer ${token}`,
      ...init.headers,
    },
  });

  if (!response.ok) {
    let body: any = {};
    try {
      body = await response.json();
    } catch {
      // Use the status fallback for empty or non-JSON responses.
    }
    const error: ApiError = {
      status: response.status,
      title: body.title || "Recipe request failed",
      detail: body.detail || body.title || `Yêu cầu thất bại (HTTP ${response.status}).`,
      errors: body.errors,
    };
    throw error;
  }

  if (response.status === 204) return undefined as T;
  return await response.json() as T;
}

export async function getRecipeDetail(slug: string, token: string): Promise<RecipeDetail> {
  return recipeRequest<RecipeDetail>(`/api/v1/recipes/${encodeURIComponent(slug)}`, token);
}

export async function addRecipeIngredient(recipeId: string, payload: RecipeIngredientPayload, token: string): Promise<RecipeIngredient> {
  return recipeRequest<RecipeIngredient>(`/api/v1/recipes/${recipeId}/ingredients`, token, { method: "POST", body: JSON.stringify(payload) });
}

export async function updateRecipeIngredient(recipeId: string, ingredientId: string, payload: RecipeIngredientPayload, token: string): Promise<RecipeIngredient> {
  return recipeRequest<RecipeIngredient>(`/api/v1/recipes/${recipeId}/ingredients/${ingredientId}`, token, { method: "PUT", body: JSON.stringify(payload) });
}

export async function deleteRecipeIngredient(recipeId: string, ingredientId: string, token: string): Promise<void> {
  return recipeRequest<void>(`/api/v1/recipes/${recipeId}/ingredients/${ingredientId}`, token, { method: "DELETE" });
}

export async function addRecipeStep(recipeId: string, payload: RecipeStepPayload, token: string): Promise<RecipeStep> {
  return recipeRequest<RecipeStep>(`/api/v1/recipes/${recipeId}/steps`, token, { method: "POST", body: JSON.stringify(payload) });
}

export async function updateRecipeStep(recipeId: string, stepId: string, payload: RecipeStepPayload, token: string): Promise<RecipeStep> {
  return recipeRequest<RecipeStep>(`/api/v1/recipes/${recipeId}/steps/${stepId}`, token, { method: "PUT", body: JSON.stringify(payload) });
}

export async function deleteRecipeStep(recipeId: string, stepId: string, token: string): Promise<void> {
  return recipeRequest<void>(`/api/v1/recipes/${recipeId}/steps/${stepId}`, token, { method: "DELETE" });
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
