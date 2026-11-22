import { createLocalJWKSet, createRemoteJWKSet, jwtVerify, type JWTVerifyGetKey } from "jose";

export interface AuthEnv {
  SUPABASE_URL: string;
  /** Test-only: inline JWKS instead of fetching Supabase's. */
  JWKS_JSON?: string;
}

let cachedKeys: { url: string; keys: JWTVerifyGetKey } | null = null;

function keysFor(env: AuthEnv): JWTVerifyGetKey {
  if (env.JWKS_JSON) return createLocalJWKSet(JSON.parse(env.JWKS_JSON));
  const url = `${env.SUPABASE_URL}/auth/v1/.well-known/jwks.json`;
  if (cachedKeys?.url !== url) cachedKeys = { url, keys: createRemoteJWKSet(new URL(url)) };
  return cachedKeys.keys;
}

/** Verifies a Supabase access token. Returns null for any invalid, expired or foreign token. */
export async function verifyToken(token: string, env: AuthEnv): Promise<{ userId: string } | null> {
  try {
    const { payload } = await jwtVerify(token, keysFor(env), {
      issuer: `${env.SUPABASE_URL}/auth/v1`,
      audience: "authenticated",
    });
    return typeof payload.sub === "string" && payload.sub ? { userId: payload.sub } : null;
  } catch {
    return null;
  }
}
