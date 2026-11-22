import { DurableObject } from "cloudflare:workers";

const MAX_MESSAGE_BYTES = 64 * 1024;

/**
 * One object per room. Relays events between members and answers pings.
 * Makes no judgments (hit decisions belong to the thrower's PC, design §7-2).
 */
export class RoomObject extends DurableObject {
  async fetch(request: Request): Promise<Response> {
    const userId = request.headers.get("X-Hangbar-User");
    if (!userId) return new Response("missing user", { status: 400 });
    const pair = new WebSocketPair();
    const [clientSide, serverSide] = Object.values(pair);
    this.ctx.acceptWebSocket(serverSide, [userId]);
    return new Response(null, { status: 101, webSocket: clientSide });
  }

  async webSocketMessage(ws: WebSocket, message: string | ArrayBuffer): Promise<void> {
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

    if (msg.t === "ping") {
      ws.send(JSON.stringify({ t: "pong", id: msg.id, sentAt: msg.sentAt }));
      return;
    }

    const from = this.ctx.getTags(ws)[0];
    const out = JSON.stringify({ ...msg, from });
    for (const other of this.ctx.getWebSockets()) {
      if (other !== ws) other.send(out);
    }
  }

  async webSocketClose(ws: WebSocket, code: number, reason: string): Promise<void> {
    ws.close(code, reason);
  }
}
