const TELEGRAM_API_ORIGIN = "https://api.telegram.org";
const UPSTREAM_TIMEOUT_MS = 30000;
const BOT_API_PATH = /^\/bot[0-9]+:[A-Za-z0-9_-]+\/[A-Za-z0-9_]+$/;

export default {
  async fetch(request) {
    const incomingUrl = new URL(request.url);

    if (incomingUrl.pathname === "/" || incomingUrl.pathname === "/health") {
      return json({ ok: true, service: "slowrig-telegram-worker" });
    }

    if (!BOT_API_PATH.test(incomingUrl.pathname)) {
      return json({ ok: false, error: "not_found" }, 404);
    }

    if (request.method !== "GET" && request.method !== "POST") {
      return json({ ok: false, error: "method_not_allowed" }, 405, { Allow: "GET, POST" });
    }

    const upstreamUrl = new URL(incomingUrl.pathname + incomingUrl.search, TELEGRAM_API_ORIGIN);
    const headers = new Headers(request.headers);
    for (const header of [
      "host",
      "cf-connecting-ip",
      "cf-ipcountry",
      "cf-ray",
      "cf-visitor",
      "connection",
      "x-forwarded-for",
      "x-real-ip",
    ]) {
      headers.delete(header);
    }
    headers.set("accept", "application/json");

    const init = {
      method: request.method,
      headers,
      redirect: "manual",
      signal: AbortSignal.timeout(UPSTREAM_TIMEOUT_MS),
    };

    if (request.method !== "GET" && request.method !== "HEAD") {
      init.body = request.body;
    }

    try {
      const response = await fetch(upstreamUrl.toString(), init);
      const responseHeaders = new Headers(response.headers);
      responseHeaders.set("cache-control", "no-store");

      return new Response(response.body, {
        status: response.status,
        statusText: response.statusText,
        headers: responseHeaders,
      });
    } catch (error) {
      return json(
        {
          ok: false,
          error: "upstream_fetch_failed",
          description: `${error.name}: ${error.message}`,
        },
        502,
      );
    }
  },
};

function json(body, status = 200, extraHeaders = {}) {
  return new Response(JSON.stringify(body), {
    status,
    headers: {
      "content-type": "application/json; charset=utf-8",
      "cache-control": "no-store",
      ...extraHeaders,
    },
  });
}
