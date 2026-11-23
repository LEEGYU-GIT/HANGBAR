import { describe, expect, it } from "vitest";
import { relay, type RelaySocket } from "../src/room";

function fake(userId: string, opts: { exp?: number; sendThrows?: boolean } = {}) {
  const sent: string[] = [];
  const closed: number[] = [];
  const s: RelaySocket = {
    deserializeAttachment: () => ({ userId, exp: opts.exp ?? Number.MAX_SAFE_INTEGER }),
    send: (m: string) => {
      if (opts.sendThrows) throw new TypeError("Can't call WebSocket send() after close()");
      sent.push(m);
    },
    close: (code?: number) => {
      closed.push(code ?? 0);
    },
  };
  return { s, sent, closed };
}

describe("relay", () => {
  it("skips the sender and delivers to everyone else", () => {
    const a = fake("a"), b = fake("b"), c = fake("c");
    relay([a.s, b.s, c.s], a.s, "m", Date.now());
    expect(a.sent).toEqual([]);
    expect(b.sent).toEqual(["m"]);
    expect(c.sent).toEqual(["m"]);
  });

  it("keeps delivering when one member's socket throws", () => {
    const a = fake("a"), dying = fake("b", { sendThrows: true }), c = fake("c");
    expect(() => relay([a.s, dying.s, c.s], a.s, "m", Date.now())).not.toThrow();
    expect(c.sent).toEqual(["m"]);
  });

  it("closes members whose token expired instead of sending", () => {
    const a = fake("a"), old = fake("b", { exp: 1 }), c = fake("c");
    relay([a.s, old.s, c.s], a.s, "m", Date.now());
    expect(old.sent).toEqual([]);
    expect(old.closed).toEqual([4001]);
    expect(c.sent).toEqual(["m"]);
  });
});
