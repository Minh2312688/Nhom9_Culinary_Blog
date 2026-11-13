import NextAuth from "next-auth";

const isProduction = process.env.NODE_ENV === "production";

export const { auth, handlers, signIn, signOut } = NextAuth({
  providers: [],
  secret: process.env.AUTH_SECRET,
  trustHost: true,
  session: {
    strategy: "jwt",
    maxAge: 7 * 24 * 60 * 60,
  },
  cookies: {
    sessionToken: {
      name: `${isProduction ? "__Secure-" : ""}authjs.session-token`,
      options: {
        httpOnly: true,
        sameSite: "lax",
        path: "/",
        secure: isProduction,
      },
    },
  },
});
