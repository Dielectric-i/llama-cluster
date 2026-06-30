using Slowrig.IdeProxy.Config;
using Slowrig.IdeProxy.Handlers;
using Slowrig.IdeProxy.Models;

using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

var builder = WebApplication.CreateBuilder(args);

var config = new ProxyConfig(builder.Configuration);
builder.Services.AddSingleton(config);
builder.Services.AddSingleton<ActiveRequestRegistry>();

builder.Services.AddHttpClient("upstream", client =>
{
    client.Timeout = Timeout.InfiniteTimeSpan;
}).ConfigurePrimaryHttpMessageHandler(() =>
{
    return new SocketsHttpHandler
    {
        ConnectTimeout = config.UpstreamConnectTimeout,
        PooledConnectionLifetime = TimeSpan.FromMinutes(5),
    };
});

builder.Services.AddEndpointsApiExplorer();

var app = builder.Build();

// ── GET /health ──────────────────────────────────────────────────────
app.MapGet("/health", ([FromServices] ProxyConfig cfg) =>
{
    return Results.Json(new
    {
        status = "ok",
        service = "slowrig-ide-proxy",
        upstreamBaseUrl = cfg.UpstreamBaseUrl,
        heartbeatIntervalSeconds = cfg.HeartbeatIntervalSeconds
    });
});

// ── GET /debug/active ────────────────────────────────────────────────
app.MapGet("/debug/active", ([FromServices] ActiveRequestRegistry registry, [FromServices] ProxyConfig cfg) =>
{
    var now = DateTime.UtcNow;
    var requests = registry.All.Select(r => new
    {
        r.Id,
        r.Model,
        startedAtUtc = r.StartedAtUtc.ToString("O"),
        elapsedSeconds = Math.Round((now - r.StartedAtUtc).TotalSeconds, 1),
        r.HeartbeatsSent,
        r.FirstUpstreamByteAfterSeconds,
        state = r.State.ToString()
    }).ToList();

    var architectRequests = registry.CountActive(cfg.ArchitectModelName);
    var activeRequests = registry.CountActive();

    return Results.Json(new
    {
        activeRequests,
        activeArchitectRequests = architectRequests,
        requests
    });
});

// ── GET /v1/models (proxy pass-through) ──────────────────────────────
app.MapGet("/v1/models", async (
    HttpContext httpContext,
    [FromServices] IHttpClientFactory factory,
    [FromServices] ProxyConfig cfg,
    [FromServices] ILoggerFactory loggerFactory) =>
{
    await ModelsHandler.HandleAsync(httpContext, factory, cfg, loggerFactory);
});

// ── POST /v1/chat/completions ────────────────────────────────────────
app.MapPost("/v1/chat/completions", async (
    HttpContext httpContext,
    [FromServices] IHttpClientFactory factory,
    [FromServices] ProxyConfig cfg,
    [FromServices] ActiveRequestRegistry registry,
    [FromServices] ILoggerFactory loggerFactory) =>
{
    await ChatHandler.HandleAsync(httpContext, factory, cfg, registry, loggerFactory);
});

app.Run();
