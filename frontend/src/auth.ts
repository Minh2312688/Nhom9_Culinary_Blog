import { CredentialsSignin } from "@auth/core/errors";
import NextAuth, { type User } from "next-auth";
import Credentials from "next-auth/providers/credentials";
import {
  AuthBackendError,
  getProfile,
  loginWithGoogleIdToken,
  loginWithPassword,
  logoutFromBackend,
  refreshBackendTokensOnce,
  type BackendTokenPair,
} from "@/lib/server/auth-backend";

export const BACKEND_TOKEN_REFRESH_SKEW_MS = 30_000;

const isProduction = process.env.NODE_ENV === "production";
export const AUTH_SESSION_COOKIE_NAME = `${isProduction ? "__Secure-" : ""}authjs.session-token`;
export const AUTH_SESSION_MAX_AGE_SECONDS = 7 * 24 * 60 * 60;

type AuthenticatedUser = User & {
  roles: string[];
  accessToken: string;
  refreshToken: string;
  accessTokenExpiresAt: number;
};

class SafeCredentialsSignin extends CredentialsSignin {
  constructor(code: string) {
    super();
    this.code = code;
  }
}

function credentialsError(error: unknown): SafeCredentialsSignin {
  if (error instanceof AuthBackendError && error.status === 401) {
    return new SafeCredentialsSignin("invalid-credentials");
  }
  if (error instanceof AuthBackendError && error.status === 423) {
    return new SafeCredentialsSignin("account-locked");
  }
  return new SafeCredentialsSignin("authentication-failed");
}

async function authenticate(
  tokenPair: BackendTokenPair,
): Promise<AuthenticatedUser> {
  const profile = await getProfile(tokenPair.accessToken);
  return {
    id: profile.id,
    email: profile.email,
    name: profile.displayName,
    image: profile.avatarUrl,
    roles: profile.roles,
    accessToken: tokenPair.accessToken,
    refreshToken: tokenPair.refreshToken,
    accessTokenExpiresAt: Date.now() + tokenPair.expiresIn * 1000,
  };
}

export const { auth, handlers, signIn, signOut } = NextAuth({
  providers: [
    Credentials({
      id: "email-password",
      name: "Email and password",
      credentials: {
        email: { label: "Email", type: "email" },
        password: { label: "Password", type: "password" },
      },
      async authorize(credentials) {
        if (typeof credentials.email !== "string" || typeof credentials.password !== "string") {
          throw new SafeCredentialsSignin("invalid-credentials");
        }

        try {
          return await authenticate(
            await loginWithPassword(credentials.email, credentials.password),
          );
        } catch (error) {
          throw credentialsError(error);
        }
      },
    }),
    Credentials({
      id: "google-id-token",
      name: "Google Identity Services",
      credentials: {
        idToken: { label: "Google ID token", type: "text" },
      },
      async authorize(credentials) {
        if (typeof credentials.idToken !== "string") {
          throw new SafeCredentialsSignin("invalid-credentials");
        }

        try {
          return await authenticate(await loginWithGoogleIdToken(credentials.idToken));
        } catch (error) {
          throw credentialsError(error);
        }
      },
    }),
  ],
  secret: process.env.AUTH_SECRET,
  trustHost: true,
  session: {
    strategy: "jwt",
    maxAge: AUTH_SESSION_MAX_AGE_SECONDS,
  },
  cookies: {
    sessionToken: {
      name: AUTH_SESSION_COOKIE_NAME,
      options: {
        httpOnly: true,
        sameSite: "lax",
        path: "/",
        secure: isProduction,
      },
    },
  },
  callbacks: {
    async jwt({ token, user }) {
      if (user) {
        const authenticatedUser = user as AuthenticatedUser;
        token.sub = authenticatedUser.id;
        token.email = authenticatedUser.email;
        token.name = authenticatedUser.name;
        token.picture = authenticatedUser.image;
        token.roles = authenticatedUser.roles;
        token.accessToken = authenticatedUser.accessToken;
        token.refreshToken = authenticatedUser.refreshToken;
        token.accessTokenExpiresAt = authenticatedUser.accessTokenExpiresAt;
        delete token.error;
        return token;
      }

      if (
        token.error ||
        typeof token.accessToken !== "string" ||
        typeof token.refreshToken !== "string" ||
        typeof token.accessTokenExpiresAt !== "number" ||
        Date.now() < token.accessTokenExpiresAt - BACKEND_TOKEN_REFRESH_SKEW_MS
      ) {
        return token;
      }

      try {
        const rotated = await refreshBackendTokensOnce(token.refreshToken);
        return {
          ...token,
          accessToken: rotated.accessToken,
          refreshToken: rotated.refreshToken,
          accessTokenExpiresAt: Date.now() + rotated.expiresIn * 1000,
        };
      } catch {
        return {
          ...token,
          accessToken: undefined,
          refreshToken: undefined,
          accessTokenExpiresAt: 0,
          error: "RefreshTokenError",
        };
      }
    },
    async session({ session, token }) {
      session.user = {
        id: token.sub ?? "",
        email: typeof token.email === "string" ? token.email : "",
        name: typeof token.name === "string" ? token.name : null,
        image: typeof token.picture === "string" ? token.picture : null,
        roles: Array.isArray(token.roles) ? token.roles.filter((role): role is string => typeof role === "string") : [],
      } as typeof session.user;
      if (token.error === "RefreshTokenError") session.error = token.error;
      else delete session.error;
      return session;
    },
  },
  events: {
    async signOut(message) {
      if (!("token" in message)) return;
      const { accessToken, refreshToken } = message.token ?? {};
      if (typeof accessToken !== "string" || typeof refreshToken !== "string") return;

      try {
        await logoutFromBackend(accessToken, refreshToken);
      } catch (error) {
        const status = error instanceof AuthBackendError ? error.status : undefined;
        console.error("[auth] Backend token revocation failed", status ? { status } : undefined);
      }
    },
  },
});
