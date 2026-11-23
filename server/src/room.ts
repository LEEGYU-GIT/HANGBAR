import { DurableObject } from "cloudflare:workers";

const MAX_MESSAGE_BYTES = 64 * 1024;
/** Message types only the relay itself may produce; clients sending them are ignored. */
const RESERVED = new Set(["ping", "pong"]);

interface Attachment {
  userId: string;
  /** Token expiry, seconds since epoch. */
  exp: number;
}

/** The part of a hibernatable WebSocket the relay loop uses (lets tests pass fakes). */
export type RelaySocket = Pick<WebSocket, "deserializeAttachment" | "send" | "close">;

/** Sends `out` to every member but the sender; members whose token expired are closed with 4001. */
export function relay(sockets: Iterable<RelaySocket>, sender: RelaySocket, out: string, now: number): void {
  for (const other of sockets) {
    if (other === sender) continue;
    // One member's socket may already be closing; that must not cost everyone after it the event.
    try {
      const them = other.deserializeAttachment() as Attachment | null;
      if (!them || now >= them.exp * 1000) other.close(4001, "token expired");
      else other.send(out);
    } catch {
      /* socket went away mid-relay; the runtime delivers its close event separately */
    }
  }
}

/**
 * One object per room. Relays events between members.
 * Makes no judgments (hit decisions belong to the thrower's PC, design §7-2).
 * Keepalive "ping" is answered by the runtime auto-response so it never wakes the object (hibernation).
 */
export class RoomObject extends DurableObject {
  constructor(ctx: DurableObjectState, env: unknown) {
    super(ctx, env as never);
    this.ctx.setWebSocketAutoResponse(new WebSocketRequestResponsePair("ping", "pong"));
  }

  async fetch(request: Request): Promise<Response> {
    const userId = request.headers.get("X-Hangbar-User");
    const exp = Number(request.headers.get("X-Hangbar-Exp"));
    if (!userId || !Number.isFinite(exp)) return new Response("missing user", { status: 400 });
    const pair = new WebSocketPair();
    const [clientSide, serverSide] = Object.values(pair);
    this.ctx.acceptWebSocket(serverSide, [userId]);
    serverSide.serializeAttachment({ userId, exp } satisfies Attachment);
    return new Response(null, { status: 101, webSocket: clientSide });
  }

  async webSocketMessage(ws: WebSocket, message: string | ArrayBuffer): Promise<void> {
    const me = ws.deserializeAttachment() as Attachment | null;
    if (!me) return;
    if (Date.now() >= me.exp * 1000) {
      ws.close(4001, "token expired");
      return;
    }

    const text = typeof message === "string" ? message : new TextDecoder().decode(message);
    if (new TextEncoder().encode(text).byteLength > MAX_MESSAGE_BYTES) return;

    let msg: Record<string, unknown>;
    try {
      const parsed = JSON.parse(text);
      if (!parsed || typeof parsed !== "object" || Array.isArray(parsed) || typeof parsed.t !== "string") return;
      msg = parsed;
    } catch {
      return;
    }
    if (RESERVED.has(msg.t as string)) return;

    relay(this.ctx.getWebSockets(), ws, JSON.stringify({ ...msg, from: me.userId }), Date.now());
  }

  async webSocketClose(ws: WebSocket, code: number, reason: string): Promise<void> {
    ws.close(code, reason);
  }
}
