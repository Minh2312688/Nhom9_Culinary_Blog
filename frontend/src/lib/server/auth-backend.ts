import "server-only";

const API_INTERNAL_URL = process.env.API_INTERNAL_URL;

export interface BackendTokenPair {
  accessToken: string;
  refreshToken: string;
  expiresIn: number;
}

export interface BackendProfile {
  id: string;
  email: string;
  displayName: string;
  avatarUrl: string | null;
  roles: string[];
}

export class AuthBackendError extends Error {
  constructor(readonly status?: number) {
    super("Authentication service request failed");
    this.name = "AuthBackendError";
  }
}

function endpoint(path: string): string {
  if (!API_INTERNAL_URL) throw new AuthBackendError();
  return `${API_INTERNAL_URL.replace(/\/+$/, "")}${path}`;
}

async function postJson<T>(path: string, body: unknown): Promise<T> {
  let response: Response;
  try {
    response = await fetch(endpoint(path), {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify(body),
      cache: "no-store",
    });
  } catch {
    throw new AuthBackendError();
  }

  if (!response.ok) throw new AuthBackendError(response.status);
  try {
    return (await response.json()) as T;
  } catch {
    throw new AuthBackendError(response.status);
  }
}

function tokenPair(value: BackendTokenPair): BackendTokenPair {
  if (
    !value ||
    typeof value.accessToken !== "string" ||
    typeof value.refreshToken !== "string" ||
    typeof value.expiresIn !== "number" ||
    !Number.isFinite(value.expiresIn) ||
    value.expiresIn < 0
  ) {
    throw new AuthBackendError();
  }
  return value;
}

export async function loginWithPassword(email: string, password: string): Promise<BackendTokenPair> {
  const result = await postJson<BackendTokenPair>("/api/v1/auth/login", { email, password });
  return tokenPair(result);
}

export async function loginWithGoogleIdToken(idToken: string): Promise<BackendTokenPair> {
  const result = await postJson<BackendTokenPair>("/api/v1/auth/google", { idToken });
  return tokenPair(result);
}

export async function getProfile(accessToken: string): Promise<BackendProfile> {
  let response: Response;
  try {
    response = await fetch(endpoint("/api/v1/auth/me"), {
      headers: { Authorization: `Bearer ${accessToken}` },
      cache: "no-store",
    });
  } catch {
    throw new AuthBackendError();
  }

  if (!response.ok) throw new AuthBackendError(response.status);
  let profile: BackendProfile;
  try {
    profile = (await response.json()) as BackendProfile;
  } catch {
    throw new AuthBackendError(response.status);
  }

  if (
    !profile ||
    typeof profile.id !== "string" ||
    typeof profile.email !== "string" ||
    typeof profile.displayName !== "string" ||
    (profile.avatarUrl !== null && typeof profile.avatarUrl !== "string") ||
    !Array.isArray(profile.roles) ||
    !profile.roles.every((role) => typeof role === "string")
  ) {
    throw new AuthBackendError();
  }
  return profile;
}

export async function refreshBackendTokens(refreshToken: string): Promise<BackendTokenPair> {
  return tokenPair(await postJson<BackendTokenPair>("/api/v1/auth/refresh", { refreshToken }));
}

export async function logoutFromBackend(accessToken: string, refreshToken: string): Promise<void> {
  let response: Response;
  try {
    response = await fetch(endpoint("/api/v1/auth/logout"), {
      method: "POST",
      headers: {
        "Content-Type": "application/json",
        Authorization: `Bearer ${accessToken}`,
      },
      body: JSON.stringify({ refreshToken }),
      cache: "no-store",
    });
  } catch {
    throw new AuthBackendError();
  }

  if (!response.ok && response.status !== 204) throw new AuthBackendError(response.status);
}
