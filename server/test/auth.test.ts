import { SignJWT, importJWK, type JWTVerifyGetKey } from "jose";
import { describe, expect, it } from "vitest";
import { verifyToken } from "../src/auth";
import { TEST_JWKS, TEST_PRIVATE_JWK } from "./keys";

const env = { SUPABASE_URL: "https://test.supabase.co", JWKS_JSON: JSON.stringify(TEST_JWKS) };

async function token(expiresIn: number) {
  const key = (await importJWK(TEST_PRIVATE_JWK, "ES256")) as CryptoKey;
  const now = Math.floor(Date.now() / 1000);
  return new SignJWT({})
    .setProtectedHeader({ alg: "ES256", kid: "test" })
    .setSubject("u1")
    .setIssuer(`${env.SUPABASE_URL}/auth/v1`)
    .setAudience("authenticated")
    .setIssuedAt(now - 120)
    .setExpirationTime(now + expiresIn)
    .sign(key);
}

describe("verifyToken", () => {
  it("accepts a valid token", async () => {
    const r = await verifyToken(await token(3600), env);
    expect(r).toMatchObject({ ok: true, userId: "u1" });
  });

  it("calls an expired or malformed token invalid", async () => {
    expect(await verifyToken(await token(-60), env)).toEqual({ ok: false, reason: "invalid" });
    expect(await verifyToken("not-a-jwt", env)).toEqual({ ok: false, reason: "invalid" });
  });

  it("calls a key-fetch failure unavailable, not invalid", async () => {
    // A Supabase JWKS outage must not look like a bad token (the client would give up on 401).
    const failingKeys: JWTVerifyGetKey = async () => {
      throw new TypeError("network connection lost");
    };
    expect(await verifyToken(await token(3600), env, failingKeys)).toEqual({ ok: false, reason: "unavailable" });
  });
});
