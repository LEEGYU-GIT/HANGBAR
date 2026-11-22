import { verifyToken, type AuthEnv } from "./auth";

export { RoomObject } from "./room";

export interface Env extends AuthEnv {
  ROOM: DurableObjectNamespace;
}

const ROOM_PATH = /^\/room\/([^/]+)$/;
const ROOM_ID = /^[A-Za-z0-9_-]{1,64}$/;

export default {
  async fetch(request: Request, env: Env): Promise<Response> {
    const url = new URL(request.url);
    const match = ROOM_PATH.exec(url.pathname);
    if (!match) return new Response("not found", { status: 404 });

    const roomId = decodeURIComponent(match[1]);
    if (!ROOM_ID.test(roomId)) return new Response("bad room id", { status: 400 });
    if (request.headers.get("Upgrade")?.toLowerCase() !== "websocket") {
      return new Response("expected websocket", { status: 426 });
    }

    const auth = request.headers.get("Authorization") ?? "";
    const bearer = auth.startsWith("Bearer ") ? auth.slice(7) : "";
    const user = bearer ? await verifyToken(bearer, env) : null;
    if (!user) return new Response("unauthorized", { status: 401 });

    // Membership check against Supabase rooms comes after M0 (rooms table not built yet).
    const stub = env.ROOM.get(env.ROOM.idFromName(roomId));
    const forwarded = new Request(request);
    forwarded.headers.set("X-Hangbar-User", user.userId);
    forwarded.headers.set("X-Hangbar-Exp", String(user.exp));
    return stub.fetch(forwarded);
  },
};
