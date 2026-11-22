// Dev-only: signs a token with the TEST key so a local `wrangler dev` relay (with .dev.vars JWKS_JSON) accepts it.
// Usage: node scripts/dev-token.ts <userId> [expiresInSeconds=3600]
import { SignJWT, importJWK } from "jose";
import { TEST_PRIVATE_JWK } from "../test/keys.ts";

const sub = process.argv[2] ?? "dev-user";
const ttl = Number(process.argv[3] ?? 3600);
const key = await importJWK(TEST_PRIVATE_JWK, "ES256");
const now = Math.floor(Date.now() / 1000);
const jwt = await new SignJWT({ role: "authenticated" })
  .setProtectedHeader({ alg: "ES256", kid: "test" })
  .setSubject(sub)
  .setIssuer("https://test.supabase.co/auth/v1")
  .setAudience("authenticated")
  .setIssuedAt(now)
  .setExpirationTime(now + ttl)
  .sign(key);
process.stdout.write(jwt);
