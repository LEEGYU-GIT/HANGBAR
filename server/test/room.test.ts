import { SELF } from "cloudflare:test";
import { SignJWT, generateKeyPair, importJWK } from "jose";
import { describe, expect, it } from "vitest";
import { TEST_PRIVATE_JWK } from "./keys";

const SUPABASE_URL = "https://test.supabase.co";

async function token(sub: string, opts: { expiredSecondsAgo?: number; expiresInSeconds?: number; key?: CryptoKey } = {}) {
  const key = opts.key ?? ((await importJWK(TEST_PRIVATE_JWK, "ES256")) as CryptoKey);
  const now = Math.floor(Date.now() / 1000);
  return new SignJWT({ role: "authenticated" })
    .setProtectedHeader({ alg: "ES256", kid: "test" })
    .setSubject(sub)
    .setIssuer(`${SUPABASE_URL}/auth/v1`)
    .setAudience("authenticated")
    .setIssuedAt(now - 120)
    .setExpirationTime(opts.expiredSecondsAgo ? now - opts.expiredSecondsAgo : now + (opts.expiresInSeconds ?? 3600))
    .sign(key);
}

function open(path: string, bearer?: string) {
  const headers: Record<string, string> = { Upgrade: "websocket" };
  if (bearer) headers.Authorization = `Bearer ${bearer}`;
  return SELF.fetch(`http://relay.test${path}`, { headers });
}

/** Accepts the client side of the socket and queues every message it receives. */
function client(res: Response) {
  expect(res.status).toBe(101);
  const ws = res.webSocket!;
  const queue: unknown[] = [];
  const waiters: ((m: unknown) => void)[] = [];
  let closeCode: number | null = null;
  ws.addEventListener("message", (e) => {
    const raw = e.data as string;
    let m: unknown;
    try { m = JSON.parse(raw); } catch { m = raw; } // "pong" is plain text
    const w = waiters.shift();
    w ? w(m) : queue.push(m);
  });
  ws.addEventListener("close", (e) => { closeCode = e.code; });
  ws.accept();
  return {
    ws,
    closeCode: () => closeCode,
    send: (m: unknown) => ws.send(JSON.stringify(m)),
    sendRaw: (s: string) => ws.send(s),
    /** Resolves with the next message, or null if none arrives within `ms`. */
    next: (ms = 1000) =>
      new Promise<unknown>((resolve) => {
        if (queue.length) return resolve(queue.shift());
        const t = setTimeout(() => {
          const i = waiters.indexOf(done);
          if (i >= 0) waiters.splice(i, 1);
          resolve(null);
        }, ms);
        const done = (m: unknown) => {
          clearTimeout(t);
          resolve(m);
        };
        waiters.push(done);
      }),
  };
}

describe("room relay", () => {
  it("rejects missing token with 401", async () => {
    const res = await open("/room/r1");
    expect(res.status).toBe(401);
  });

  it("rejects token signed by another key with 401", async () => {
    const { privateKey } = await generateKeyPair("ES256");
    const res = await open("/room/r1", await token("u1", { key: privateKey }));
    expect(res.status).toBe(401);
  });

  it("rejects expired token with 401", async () => {
    const res = await open("/room/r1", await token("u1", { expiredSecondsAgo: 60 }));
    expect(res.status).toBe(401);
  });

  it("rejects bad room id with 400", async () => {
    const res = await open("/room/bad%20id!", await token("u1"));
    expect(res.status).toBe(400);
  });

  it("relays a message to the other member but not the sender", async () => {
    const a = client(await open("/room/relay1", await token("u1")));
    const b = client(await open("/room/relay1", await token("u2")));
    a.send({ t: "throw", target: "u2" });
    expect(await b.next()).toEqual({ t: "throw", target: "u2", from: "u1" });
    expect(await a.next(500)).toBeNull();
  });

  // Review I3: keepalive must not wake the Durable Object, so "ping" is answered by the runtime auto-response.
  it("answers a bare ping with pong, only to the sender", async () => {
    const a = client(await open("/room/ping1", await token("u1")));
    const b = client(await open("/room/ping1", await token("u2")));
    a.sendRaw("ping");
    expect(await a.next()).toBe("pong");
    expect(await b.next(500)).toBeNull();
  });

  // Review I5: a member must not be able to forge keepalive replies for others.
  it("drops client-sent pong instead of relaying it", async () => {
    const a = client(await open("/room/forge1", await token("u1")));
    const b = client(await open("/room/forge1", await token("u2")));
    a.send({ t: "pong", id: 1, sentAt: 0 });
    a.sendRaw("pong");
    expect(await b.next(500)).toBeNull();
  });

  // Review I4: an open socket whose token has expired is closed with 4001 and its messages are not relayed.
  it("closes a socket whose token expired and stops relaying it", async () => {
    const a = client(await open("/room/exp1", await token("u1", { expiresInSeconds: 2 })));
    const b = client(await open("/room/exp1", await token("u2")));
    await new Promise((r) => setTimeout(r, 2600));
    a.send({ t: "jump" });
    expect(await b.next(800)).toBeNull();
    await new Promise((r) => setTimeout(r, 200));
    expect(a.closeCode()).toBe(4001);
  }, 10_000);

  // Review I4 (receive side): a silent socket with an expired token must not keep receiving the room.
  it("does not deliver to a socket whose token expired", async () => {
    const a = client(await open("/room/exp2", await token("u1", { expiresInSeconds: 2 })));
    const b = client(await open("/room/exp2", await token("u2")));
    await new Promise((r) => setTimeout(r, 2600));
    b.send({ t: "chat", text: "secret" });
    expect(await a.next(800)).toBeNull();
    await new Promise((r) => setTimeout(r, 200));
    expect(a.closeCode()).toBe(4001);
  }, 10_000);
});
