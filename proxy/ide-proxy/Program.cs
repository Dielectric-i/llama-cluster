using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace Slowrig.IdeProxy;

// ─── Config ───────────────────────────────────────────────────────────────────

public sealed class ProxyConfig
{
    public string UpstreamBaseUrl { get; }
    public int HeartbeatIntervalSeconds { get; }
    public TimeSpan UpstreamConnectTimeout { get; }
    public TimeSpan? UpstreamTotalTimeout { get; }
    public int MaxActiveArchitectRequests { get; }
    public string BusyMode { get; }
    public string ArchitectModelName { get; }

    public ProxyConfig(IConfiguration config)
    {
        UpstreamBaseUrl = config["UPSTREAM_BASE_URL"]?.TrimEnd('/')
            ?? "http://litellm:4000/v1";
        HeartbeatIntervalSeconds = int.Parse(config["HEARTBEAT_INTERVAL_SECONDS"] ?? "10");

        var connectSec = int.Parse(config["UPSTREAM_CONNECT_TIMEOUT_SECONDS"] ?? "10");
        UpstreamConnectTimeout = TimeSpan.FromSeconds(connectSec);

        var totalSec = int.Parse(config["UPSTREAM_TOTAL_TIMEOUT_SECONDS"] ?? "0");
        UpstreamTotalTimeout = totalSec > 0 ? TimeSpan.FromSeconds(totalSec) : (TimeSpan?)null;

        MaxActiveArchitectRequests = int.Parse(config["MAX_ACTIVE_ARCHITECT_REQUESTS"] ?? "1");
        BusyMode = config["BUSY_MODE"] ?? "reject";
        ArchitectModelName = config["ARCHITECT_MODEL_NAME"] ?? "slowrig/architect";
    }
}

// ─── Active Request Tracking ──────────────────────────────────────────────────

public enum RequestState
{
    waiting_upstream,
    upstream_streaming,
    done,
    error,
    cancelled
}

public sealed class ActiveRequestInfo
{
    public string Id { get; set; } = string.Empty;
    public string Model { get; set; } = string.Empty;
    public DateTime StartedAtUtc { get; set; }
    public int HeartbeatsSent { get; set; }
    public double? FirstUpstreamByteAfterSeconds { get; set; }
    public RequestState State { get; set; }
    public int MessagesCount { get; set; }
    public int ToolsCount { get; set; }
    public int? MaxTokens { get; set; }
    public bool Stream { get; set; }
}

public sealed class ActiveRequestRegistry
{
    private readonly ConcurrentDictionary<string, ActiveRequestInfo> _requests
        = new();

    public void Add(ActiveRequestInfo info) => _requests[info.Id] = info;
    public void Remove(string id) => _requests.TryRemove(id, out _);
    public void Update(string id, Action<ActiveRequestInfo> update)
    {
        if (_requests.TryGetValue(id, out var info))
        {
            update(info);
        }
    }
    public IReadOnlyCollection<ActiveRequestInfo> All => _requests.Values.ToList();
}

// ─── Program ──────────────────────────────────────────────────────────────────

public static class Program
{
    public static void Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        var config = new ProxyConfig(builder.Configuration);
        builder.Services.AddSingleton(config);
        builder.Services.AddSingleton<ActiveRequestRegistry>();

        // HttpClient with long timeouts for streaming
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

            var architectRequests = registry.All
                .Count(r => r.Model == config.ArchitectModelName
                    && r.State is not RequestState.done and not RequestState.error and not RequestState.cancelled);

            var activeRequests = registry.All
                .Count(r => r.State is not RequestState.done and not RequestState.error and not RequestState.cancelled);

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
            var logger = loggerFactory.CreateLogger("Slowrig.IdeProxy.Models");
            var requestId = GenerateRequestId();
            var sw = System.Diagnostics.Stopwatch.StartNew();
            logger.LogInformation("{RequestId} request_received method=GET path=/v1/models", requestId);

            try
            {
                var client = factory.CreateClient("upstream");
                var request = new HttpRequestMessage(HttpMethod.Get, $"{cfg.UpstreamBaseUrl}/models");

                // Pass-through Authorization
                if (httpContext.Request.Headers.Authorization.Count > 0)
                {
                    var authValue = httpContext.Request.Headers.Authorization.ToString();
                    request.Headers.Authorization = AuthenticationHeaderValue.Parse(authValue);
                }

                var response = await client.SendAsync(request, httpContext.RequestAborted);
                var elapsedMs = sw.ElapsedMilliseconds;

                logger.LogInformation(
                    "{RequestId} upstream_response method=GET path=/v1/models status={Status} elapsedMs={ElapsedMs}",
                    requestId, (int)response.StatusCode, elapsedMs);

                var body = await response.Content.ReadAsStreamAsync(httpContext.RequestAborted);
                httpContext.Response.StatusCode = (int)response.StatusCode;
                httpContext.Response.ContentType = response.Content.Headers.ContentType?.ToString()
                    ?? "application/json";
                await body.CopyToAsync(httpContext.Response.Body, httpContext.RequestAborted);
            }
            catch (TaskCanceledException)
            {
                logger.LogWarning("{RequestId} client_disconnect method=GET path=/v1/models", requestId);
                throw;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "{RequestId} proxy_error method=GET path=/v1/models", requestId);
                httpContext.Response.StatusCode = StatusCodes.Status502BadGateway;
                await httpContext.Response.WriteAsync(
                    $"{{\"error\":\"{ex.Message}\"}}", httpContext.RequestAborted);
            }
        });

        // ── POST /v1/chat/completions ────────────────────────────────────────
        app.MapPost("/v1/chat/completions", async (
            HttpContext httpContext,
            [FromServices] IHttpClientFactory factory,
            [FromServices] ProxyConfig cfg,
            [FromServices] ActiveRequestRegistry registry,
            [FromServices] ILoggerFactory loggerFactory) =>
        {
            var logger = loggerFactory.CreateLogger("Slowrig.IdeProxy.Chat");
            var requestId = GenerateRequestId();
            var sw = System.Diagnostics.Stopwatch.StartNew();

            // Read body once
            var bodyBytes = await ReadRequestBodyAsync(httpContext);
            var bodyString = Encoding.UTF8.GetString(bodyBytes);

            // Extract metadata safely
            var (model, isStream, messagesCount, toolsCount, maxTokens)
                = ExtractChatMetadata(bodyString, logger, requestId);

            logger.LogInformation(
                "{RequestId} request_received model={Model} stream={Stream} messagesCount={MsgCount} toolsCount={ToolsCount} maxTokens={MaxTokens}",
                requestId, model, isStream, messagesCount, toolsCount, maxTokens);

            // Busy protection for architect
            if (model == cfg.ArchitectModelName && cfg.BusyMode == "reject")
            {
                var activeArchitect = registry.All
                    .Count(r => r.Model == cfg.ArchitectModelName
                        && r.State is not RequestState.done and not RequestState.error and not RequestState.cancelled);

                if (activeArchitect >= cfg.MaxActiveArchitectRequests)
                {
                    logger.LogWarning(
                        "{RequestId} busy_rejected model={Model} activeArchitectRequests={Active}",
                        requestId, model, activeArchitect);

                    httpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;
                    httpContext.Response.ContentType = "application/json";
                    var busyBody = JsonSerializer.Serialize(new
                    {
                        error = new
                        {
                            message = $"{cfg.ArchitectModelName} is busy. Wait until the current request finishes or restart llama-architect if it was cancelled.",
                            type = "slowrig_model_busy"
                        }
                    });
                    await httpContext.Response.WriteAsync(busyBody, httpContext.RequestAborted);
                    return;
                }
            }

            // Register active request
            var activeInfo = new ActiveRequestInfo
            {
                Id = requestId,
                Model = model,
                StartedAtUtc = DateTime.UtcNow,
                State = RequestState.waiting_upstream,
                MessagesCount = messagesCount,
                ToolsCount = toolsCount,
                MaxTokens = maxTokens,
                Stream = isStream
            };
            registry.Add(activeInfo);

            try
            {
                if (!isStream)
                {
                    // Non-streaming: simple pass-through
                    await HandleNonStreamingAsync(
                        httpContext, factory, cfg, bodyBytes, requestId, activeInfo, registry, logger);
                }
                else
                {
                    // Streaming: SSE with heartbeat
                    await HandleStreamingAsync(
                        httpContext, factory, cfg, bodyBytes, requestId, activeInfo, registry, logger);
                }
            }
            catch (TaskCanceledException)
            {
                logger.LogWarning("{RequestId} client_disconnect elapsedMs={ElapsedMs}", requestId, sw.ElapsedMilliseconds);
                activeInfo.State = RequestState.cancelled;
                throw;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "{RequestId} proxy_error elapsedMs={ElapsedMs}", requestId, sw.ElapsedMilliseconds);
                activeInfo.State = RequestState.error;
                if (httpContext.Response.HasStarted)
                {
                    // Response already started, can't change status
                    return;
                }
                httpContext.Response.StatusCode = StatusCodes.Status502BadGateway;
                httpContext.Response.ContentType = "application/json";
                var errorBody = JsonSerializer.Serialize(new
                {
                    error = new
                    {
                        message = ex.Message,
                        type = "proxy_error"
                    }
                });
                await httpContext.Response.WriteAsync(errorBody, httpContext.RequestAborted);
            }
            finally
            {
                registry.Remove(requestId);
            }
        });

        app.Run();
    }

    // ─── Helpers ──────────────────────────────────────────────────────────────

    private static string GenerateRequestId()
    {
        var guid = Guid.NewGuid().ToString("N");
        return guid[..12];
    }

    private static async Task<byte[]> ReadRequestBodyAsync(HttpContext context)
    {
        using var ms = new MemoryStream();
        await context.Request.Body.CopyToAsync(ms);
        return ms.ToArray();
    }

    private static (string model, bool stream, int messagesCount, int toolsCount, int? maxTokens)
        ExtractChatMetadata(string bodyString, ILogger logger, string requestId)
    {
        try
        {
            using var doc = JsonDocument.Parse(bodyString);
            var root = doc.RootElement;

            var model = root.TryGetProperty("model", out var modelProp)
                ? modelProp.GetString() ?? "unknown"
                : "unknown";

            var stream = root.TryGetProperty("stream", out var streamProp)
                && streamProp.GetBoolean();

            var messagesCount = 0;
            if (root.TryGetProperty("messages", out var msgProp) && msgProp.ValueKind == JsonValueKind.Array)
            {
                messagesCount = msgProp.GetArrayLength();
            }

            var toolsCount = 0;
            if (root.TryGetProperty("tools", out var toolsProp) && toolsProp.ValueKind == JsonValueKind.Array)
            {
                toolsCount = toolsProp.GetArrayLength();
            }

            int? maxTokens = null;
            if (root.TryGetProperty("max_tokens", out var mtProp))
            {
                maxTokens = mtProp.GetInt32();
            }

            return (model, stream, messagesCount, toolsCount, maxTokens);
        }
        catch (JsonException ex)
        {
            logger.LogWarning(ex, "{RequestId} failed_to_parse_request_body", requestId);
            return ("unknown", false, 0, 0, null);
        }
    }

    private static async Task HandleNonStreamingAsync(
        HttpContext httpContext,
        IHttpClientFactory factory,
        ProxyConfig cfg,
        byte[] bodyBytes,
        string requestId,
        ActiveRequestInfo activeInfo,
        ActiveRequestRegistry registry,
        ILogger logger)
    {
        logger.LogInformation("{RequestId} downstream_open stream=false", requestId);

        var client = factory.CreateClient("upstream");
        var request = new HttpRequestMessage(HttpMethod.Post, $"{cfg.UpstreamBaseUrl}/chat/completions");
        request.Content = new ByteArrayContent(bodyBytes);
        request.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json");

        if (httpContext.Request.Headers.Authorization.Count > 0)
        {
            var authValue = httpContext.Request.Headers.Authorization.ToString();
            request.Headers.Authorization = AuthenticationHeaderValue.Parse(authValue);
        }

        var response = await client.SendAsync(
            request,
            HttpCompletionOption.ResponseHeadersRead,
            httpContext.RequestAborted);

        var body = await response.Content.ReadAsStreamAsync(httpContext.RequestAborted);
        httpContext.Response.StatusCode = (int)response.StatusCode;
        httpContext.Response.ContentType = response.Content.Headers.ContentType?.ToString()
            ?? "application/json";
        await body.CopyToAsync(httpContext.Response.Body, httpContext.RequestAborted);

        activeInfo.State = RequestState.done;
        logger.LogInformation(
            "{RequestId} downstream_done stream=false status={Status} elapsedMs={ElapsedMs}",
            requestId, (int)response.StatusCode,
            (DateTime.UtcNow - activeInfo.StartedAtUtc).TotalMilliseconds);
    }

    private static async Task HandleStreamingAsync(
        HttpContext httpContext,
        IHttpClientFactory factory,
        ProxyConfig cfg,
        byte[] bodyBytes,
        string requestId,
        ActiveRequestInfo activeInfo,
        ActiveRequestRegistry registry,
        ILogger logger)
    {
        var heartbeatInterval = TimeSpan.FromSeconds(cfg.HeartbeatIntervalSeconds);
        var heartbeatUtf8 = Encoding.UTF8.GetBytes(": ping\n\n");
        var initialHeartbeat = Encoding.UTF8.GetBytes(": slowrig-ide-proxy connected\n\n");

        logger.LogInformation("{RequestId} downstream_open stream=true", requestId);

        // Start SSE response immediately
        httpContext.Response.StatusCode = StatusCodes.Status200OK;
        httpContext.Response.ContentType = "text/event-stream; charset=utf-8";
        httpContext.Response.Headers.CacheControl = "no-cache";
        httpContext.Response.Headers["X-Accel-Buffering"] = "no";

        // Write initial heartbeat
        await httpContext.Response.BodyWriter.WriteAsync(initialHeartbeat);
        await httpContext.Response.BodyWriter.FlushAsync();
        activeInfo.HeartbeatsSent++;

        logger.LogInformation("{RequestId} upstream_request_started", requestId);

        // Create upstream request
        var client = factory.CreateClient("upstream");
        var request = new HttpRequestMessage(HttpMethod.Post, $"{cfg.UpstreamBaseUrl}/chat/completions");
        request.Content = new ByteArrayContent(bodyBytes);
        request.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json");

        if (httpContext.Request.Headers.Authorization.Count > 0)
        {
            var authValue = httpContext.Request.Headers.Authorization.ToString();
            request.Headers.Authorization = AuthenticationHeaderValue.Parse(authValue);
        }

        // Create linked cancellation token
        var cts = CancellationTokenSource.CreateLinkedTokenSource(httpContext.RequestAborted);
        if (cfg.UpstreamTotalTimeout.HasValue)
        {
            var timeoutCts = new CancellationTokenSource(cfg.UpstreamTotalTimeout.Value);
            var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cts.Token, timeoutCts.Token);
            cts = linkedCts;
        }

        var upstreamResponse = await client.SendAsync(
            request,
            HttpCompletionOption.ResponseHeadersRead,
            cts.Token);

        if (!upstreamResponse.IsSuccessStatusCode)
        {
            // Upstream returned error before streaming
            logger.LogWarning(
                "{RequestId} upstream_error status={Status}",
                requestId, (int)upstreamResponse.StatusCode);
            activeInfo.State = RequestState.error;

            var errorPayload = $"{{\"error\":{{\"message\":\"Upstream returned status {(int)upstreamResponse.StatusCode}\",\"type\":\"upstream_error\"}}}}";
            var errorSse = $"data: {errorPayload}\n\ndata: [DONE]\n\n";
            var errorBytes = Encoding.UTF8.GetBytes(errorSse);
            await httpContext.Response.BodyWriter.WriteAsync(errorBytes);
            await httpContext.Response.BodyWriter.FlushAsync();
            return;
        }

        activeInfo.State = RequestState.upstream_streaming;

        var upstreamStream = await upstreamResponse.Content.ReadAsStreamAsync(cts.Token);
        var buffer = new byte[8192];
        var heartbeatCount = activeInfo.HeartbeatsSent;
        var firstByteReceived = false;

        try
        {
            while (true)
            {
                // Check if client disconnected
                if (cts.Token.IsCancellationRequested)
                {
                    logger.LogInformation(
                        "{RequestId} client_disconnect during_streaming elapsedMs={ElapsedMs}",
                        requestId, (DateTime.UtcNow - activeInfo.StartedAtUtc).TotalMilliseconds);
                    activeInfo.State = RequestState.cancelled;
                    break;
                }

                // Read with heartbeat pattern
                var readTask = ReadWithTimeoutAsync(upstreamStream, buffer, cts.Token);
                var delayTask = Task.Delay(heartbeatInterval, cts.Token);

                var completedTask = await Task.WhenAny(readTask, delayTask);

                if (completedTask == delayTask)
                {
                    // No data from upstream — send heartbeat
                    logger.LogDebug(
                        "{RequestId} heartbeat_sent count={Count} elapsedSeconds={Elapsed}",
                        requestId, ++heartbeatCount,
                        Math.Round((DateTime.UtcNow - activeInfo.StartedAtUtc).TotalSeconds, 1));

                    await httpContext.Response.BodyWriter.WriteAsync(heartbeatUtf8, cts.Token);
                    await httpContext.Response.BodyWriter.FlushAsync(cts.Token);
                    activeInfo.HeartbeatsSent = heartbeatCount;
                    continue;
                }

                // Read completed
                var bytesRead = await readTask;

                if (bytesRead == 0)
                {
                    // Upstream stream ended
                    break;
                }

                if (!firstByteReceived)
                {
                    firstByteReceived = true;
                    var elapsed = (DateTime.UtcNow - activeInfo.StartedAtUtc).TotalSeconds;
                    activeInfo.FirstUpstreamByteAfterSeconds = Math.Round(elapsed, 1);
                    logger.LogInformation(
                        "{RequestId} first_upstream_byte afterSeconds={After}",
                        requestId, activeInfo.FirstUpstreamByteAfterSeconds);
                }

                // Write upstream bytes to downstream
                await httpContext.Response.BodyWriter.WriteAsync(buffer.AsMemory(0, bytesRead), cts.Token);
                await httpContext.Response.BodyWriter.FlushAsync(cts.Token);
            }

            // Ensure [DONE] is properly sent
            await httpContext.Response.BodyWriter.FlushAsync();

            activeInfo.State = RequestState.done;
            logger.LogInformation(
                "{RequestId} upstream_done elapsedSeconds={Elapsed}",
                requestId, Math.Round((DateTime.UtcNow - activeInfo.StartedAtUtc).TotalSeconds, 1));
            logger.LogInformation(
                "{RequestId} downstream_done elapsedSeconds={Elapsed}",
                requestId, Math.Round((DateTime.UtcNow - activeInfo.StartedAtUtc).TotalSeconds, 1));
        }
        catch (OperationCanceledException)
        {
            logger.LogInformation(
                "{RequestId} client_disconnect during_streaming elapsedMs={ElapsedMs}",
                requestId, (DateTime.UtcNow - activeInfo.StartedAtUtc).TotalMilliseconds);
            activeInfo.State = RequestState.cancelled;
        }
        finally
        {
            // Cancel upstream if still running
            if (!cts.IsCancellationRequested)
            {
                cts.Cancel();
            }
        }
    }

    private static async Task<int> ReadWithTimeoutAsync(Stream stream, byte[] buffer, CancellationToken ct)
    {
        return await stream.ReadAsync(buffer, ct);
    }
}
