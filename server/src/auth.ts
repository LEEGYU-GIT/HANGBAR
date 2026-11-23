import { createLocalJWKSet, createRemoteJWKSet, jwtVerify, type JWTVerifyGetKey } from "jose";

export interface AuthEnv {
  SUPABASE_URL: string;
  /** Test-only: inline JWKS instead of fetching Supabase's. */
  JWKS_JSON?: string;
}

export type VerifyResult =
  | { ok: true; userId: string; exp: number }
  /** invalid: the token itself is bad (answer 401). unavailable: keys could not be fetched (answer 503, client retries). */
  | { ok: false; reason: "invalid" | "unavailable" };

/** jose error codes that mean the token is at fault. Anything else (fetch failures, JWKS timeouts or bad JWKS) is ours. */
const TOKEN_FAULTS = new Set([
  "ERR_JWT_EXPIRED",
  "ERR_JWT_CLAIM_VALIDATION_FAILED",
  "ERR_JWT_INVALID",
  "ERR_JWS_INVALID",
  "ERR_JWS_SIGNATURE_VERIFICATION_FAILED",
  "ERR_JWKS_NO_MATCHING_KEY",
  "ERR_JWKS_MULTIPLE_MATCHING_KEYS",
  "ERR_JOSE_ALG_NOT_ALLOWED",
  "ERR_JOSE_NOT_SUPPORTED",
]);

let cachedKeys: { url: string; keys: JWTVerifyGetKey } | null = null;

function keysFor(env: AuthEnv): JWTVerifyGetKey {
  if (env.JWKS_JSON) return createLocalJWKSet(JSON.parse(env.JWKS_JSON));
  const url = `${env.SUPABASE_URL}/auth/v1/.well-known/jwks.json`;
  if (cachedKeys?.url !== url) cachedKeys = { url, keys: createRemoteJWKSet(new URL(url)) };
  return cachedKeys.keys;
}

/** Verifies a Supabase access token. `keys` is injectable for tests. */
export async function verifyToken(token: string, env: AuthEnv, keys: JWTVerifyGetKey = keysFor(env)): Promise<VerifyResult> {
  try {
    const { payload } = await jwtVerify(token, keys, {
      issuer: `${env.SUPABASE_URL}/auth/v1`,
      audience: "authenticated",
      requiredClaims: ["exp", "sub"],
    });
    return typeof payload.sub === "string" && payload.sub && typeof payload.exp === "number"
      ? { ok: true, userId: payload.sub, exp: payload.exp }
      : { ok: false, reason: "invalid" };
  } catch (e) {
    const code = (e as { code?: unknown })?.code;
    if (typeof code === "string" && TOKEN_FAULTS.has(code)) return { ok: false, reason: "invalid" };
    console.error(`[auth] key fetch failed: ${code ?? (e as Error)?.name ?? "unknown"}`);
    return { ok: false, reason: "unavailable" };
  }
}
