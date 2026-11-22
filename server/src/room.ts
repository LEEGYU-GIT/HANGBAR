import { DurableObject } from "cloudflare:workers";

const MAX_MESSAGE_BYTES = 64 * 1024;
/** Message types only the relay itself may produce; clients sending them are ignored. */
const RESERVED = new Set(["ping", "pong"]);

interface Attachment {
  userId: string;
  /** Token expiry, seconds since epoch. */
  exp: number;
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

    const out = JSON.stringify({ ...msg, from: me.userId });
    const now = Date.now();
    for (const other of this.ctx.getWebSockets()) {
      if (other === ws) continue;
      const them = other.deserializeAttachment() as Attachment | null;
      if (!them || now >= them.exp * 1000) {
        other.close(4001, "token expired");
        continue;
      }
      other.send(out);
    }
  }

  async webSocketClose(ws: WebSocket, code: number, reason: string): Promise<void> {
    ws.close(code, reason);
  }
}
