// Dev-only second user for the M0 sync check: joins a room, prints what it receives,
// sends a jump and a chat, tries to forge keepalive replies (they must be dropped by the relay),
// and times its own plain-text "ping" round trips.
// Usage: node scripts/peer.mjs <ws-base> <room> <token> <seconds>
import WebSocket from "ws";

const [base, room, token, secondsArg] = process.argv.slice(2);
const seconds = Number(secondsArg ?? 10);
const ws = new WebSocket(`${base.replace(/\/$/, "")}/room/${room}`, { headers: { Authorization: `Bearer ${token}` } });
const rtts = [];
let pingAt = -1;
ws.on("open", () => {
  console.log("[peer] open");
  setTimeout(() => ws.send(JSON.stringify({ t: "jump" })), 1000);
  setTimeout(() => ws.send(JSON.stringify({ t: "chat", text: "안녕! 피어에서 보냄" })), 1500);
  setTimeout(() => { ws.send(JSON.stringify({ t: "pong" })); ws.send("pong"); console.log("[peer] sent forged pongs"); }, 2000);
  setInterval(() => { if (pingAt < 0) { pingAt = performance.now(); ws.send("ping"); } }, 500);
});
ws.on("unexpected-response", (_req, res) => { console.log(`[peer] http ${res.statusCode}`); process.exit(1); });
ws.on("message", (data) => {
  const text = String(data);
  if (text === "pong") { if (pingAt >= 0) rtts.push(performance.now() - pingAt); pingAt = -1; return; }
  const m = JSON.parse(text);
  console.log(`[peer] got ${m.t} from ${String(m.from).slice(0, 8)}`);
});
setTimeout(() => {
  rtts.sort((a, b) => a - b);
  const p = (q) => rtts.length ? rtts[Math.min(rtts.length - 1, Math.ceil(q * rtts.length) - 1)].toFixed(1) : "n/a";
  console.log(`[peer] rtt n=${rtts.length} p50=${p(0.5)}ms p95=${p(0.95)}ms`);
  ws.close(); process.exit(0);
}, seconds * 1000);
