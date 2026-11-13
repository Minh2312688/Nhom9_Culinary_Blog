import type { DefaultSession } from "next-auth";

declare module "next-auth" {
  interface Session {
    user: {
      id: string;
      email: string | null;
      name: string | null;
      image: string | null;
      roles: string[];
    } & DefaultSession["user"];
    error?: "RefreshTokenError";
  }

}

declare module "@auth/core/jwt" {
  interface JWT {
    roles?: string[];
    accessToken?: string;
    refreshToken?: string;
    accessTokenExpiresAt?: number;
    error?: "RefreshTokenError";
  }
}
