using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

using Slowrig.IdeProxy.Config;
using Slowrig.IdeProxy.Helpers;
using Slowrig.IdeProxy.Models;

namespace Slowrig.IdeProxy.Handlers;

public static class ChatHandler
{
    public static async Task HandleAsync(
        HttpContext httpContext,
        IHttpClientFactory factory,
        ProxyConfig cfg,
        ActiveRequestRegistry registry,
        ILoggerFactory loggerFactory)
    {
        var logger = loggerFactory.CreateLogger("Slowrig.IdeProxy.Chat");
        var requestId = RequestHelpers.GenerateRequestId();
        var sw = System.Diagnostics.Stopwatch.StartNew();

        // Read body once
        var bodyBytes = await RequestHelpers.ReadBodyAsync(httpContext);
        var bodyString = Encoding.UTF8.GetString(bodyBytes);

        // Extract metadata
        var (model, isStream, messagesCount, toolsCount, maxTokens) =
            RequestHelpers.ExtractChatMetadata(bodyString, logger, requestId);

        logger.LogInformation(
            "{RequestId} request_received model={Model} stream={Stream} messagesCount={MsgCount} toolsCount={ToolsCount} maxTokens={MaxTokens}",
            requestId, model, isStream, messagesCount, toolsCount, maxTokens);

        // Busy protection for architect
        if (model == cfg.ArchitectModelName && cfg.BusyMode == "reject")
        {
            var activeArchitect = registry.CountActive(cfg.ArchitectModelName);

            if (activeArchitect >= cfg.MaxActiveArchitectRequests)
            {
                logger.LogWarning(
                    "{RequestId} busy_rejected model={Model} activeArchitectRequests={Active}",
                    requestId, model, activeArchitect);

                await WriteBusyResponse(httpContext, cfg.ArchitectModelName);
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
                await HandleNonStreamingAsync(httpContext, factory, cfg, bodyBytes, requestId, activeInfo, logger);
            else
                await HandleStreamingAsync(httpContext, factory, cfg, bodyBytes, requestId, activeInfo, logger);
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

            if (!httpContext.Response.HasStarted)
                await WriteErrorResponse(httpContext, ex.Message);
        }
        finally
        {
            registry.Remove(requestId);
        }
    }

    // ── Non-streaming ──────────────────────────────────────────────────────

    static async Task HandleNonStreamingAsync(
        HttpContext httpContext,
        IHttpClientFactory factory,
        ProxyConfig cfg,
        byte[] bodyBytes,
        string requestId,
        ActiveRequestInfo activeInfo,
        ILogger logger)
    {
        logger.LogInformation("{RequestId} downstream_open stream=false", requestId);

        var request = BuildUpstreamRequest(factory, cfg, bodyBytes, httpContext);

        var response = await request.Client.SendAsync(
            request.Message,
            HttpCompletionOption.ResponseHeadersRead,
            httpContext.RequestAborted);

        var body = await response.Content.ReadAsStreamAsync(httpContext.RequestAborted);
        httpContext.Response.StatusCode = (int)response.StatusCode;
        httpContext.Response.ContentType =
            response.Content.Headers.ContentType?.ToString() ?? "application/json";

        await body.CopyToAsync(httpContext.Response.Body, httpContext.RequestAborted);

        activeInfo.State = RequestState.done;
        logger.LogInformation(
            "{RequestId} downstream_done stream=false status={Status} elapsedMs={ElapsedMs}",
            requestId, (int)response.StatusCode,
            (DateTime.UtcNow - activeInfo.StartedAtUtc).TotalMilliseconds);
    }

    // ── Streaming ──────────────────────────────────────────────────────────

    static async Task HandleStreamingAsync(
        HttpContext httpContext,
        IHttpClientFactory factory,
        ProxyConfig cfg,
        byte[] bodyBytes,
        string requestId,
        ActiveRequestInfo activeInfo,
        ILogger logger)
    {
        var heartbeatInterval = TimeSpan.FromSeconds(cfg.HeartbeatIntervalSeconds);
        var heartbeatUtf8 = Encoding.UTF8.GetBytes(": ping\n\n");
        var initialHeartbeat = Encoding.UTF8.GetBytes(": slowrig-ide-proxy connected\n\n");

        logger.LogInformation("{RequestId} downstream_open stream=true", requestId);

        // Start SSE response immediately
        SetupSseResponse(httpContext);
        await WriteAndFlush(httpContext, initialHeartbeat);
        activeInfo.HeartbeatsSent++;

        logger.LogInformation("{RequestId} upstream_request_started", requestId);

        // Build upstream request
        var request = BuildUpstreamRequest(factory, cfg, bodyBytes, httpContext);

        // Cancellation with optional timeout
        var cts = CancellationTokenSource.CreateLinkedTokenSource(httpContext.RequestAborted);
        if (cfg.UpstreamTotalTimeout.HasValue)
        {
            var timeoutCts = new CancellationTokenSource(cfg.UpstreamTotalTimeout.Value);
            cts = CancellationTokenSource.CreateLinkedTokenSource(cts.Token, timeoutCts.Token);
        }

        var upstreamResponse = await request.Client.SendAsync(
            request.Message,
            HttpCompletionOption.ResponseHeadersRead,
            cts.Token);

        if (!upstreamResponse.IsSuccessStatusCode)
        {
            await WriteSseError(httpContext, (int)upstreamResponse.StatusCode, requestId, logger);
            activeInfo.State = RequestState.error;
            return;
        }

        activeInfo.State = RequestState.upstream_streaming;

        await StreamWithHeartbeat(
            upstreamResponse, httpContext, cts, heartbeatInterval, heartbeatUtf8,
            activeInfo, requestId, logger);
    }

    static async Task StreamWithHeartbeat(
        HttpResponseMessage upstreamResponse,
        HttpContext httpContext,
        CancellationTokenSource cts,
        TimeSpan heartbeatInterval,
        byte[] heartbeatUtf8,
        ActiveRequestInfo activeInfo,
        string requestId,
        ILogger logger)
    {
        var upstreamStream = await upstreamResponse.Content.ReadAsStreamAsync(cts.Token);
        var buffer = new byte[8192];
        var heartbeatCount = activeInfo.HeartbeatsSent;
        var firstByteReceived = false;

        try
        {
            while (true)
            {
                if (cts.Token.IsCancellationRequested)
                {
                    logger.LogInformation(
                        "{RequestId} client_disconnect during_streaming elapsedMs={ElapsedMs}",
                        requestId, (DateTime.UtcNow - activeInfo.StartedAtUtc).TotalMilliseconds);
                    activeInfo.State = RequestState.cancelled;
                    break;
                }

                var readTask = upstreamStream.ReadAsync(buffer, cts.Token);
                var delayTask = Task.Delay(heartbeatInterval, cts.Token);
                var completedTask = await Task.WhenAny(readTask, delayTask);

                if (completedTask == delayTask)
                {
                    heartbeatCount++;
                    logger.LogDebug(
                        "{RequestId} heartbeat_sent count={Count} elapsedSeconds={Elapsed}",
                        requestId, heartbeatCount,
                        Math.Round((DateTime.UtcNow - activeInfo.StartedAtUtc).TotalSeconds, 1));

                    await WriteAndFlush(httpContext, heartbeatUtf8, cts.Token);
                    activeInfo.HeartbeatsSent = heartbeatCount;
                    continue;
                }

                var bytesRead = await readTask;
                if (bytesRead == 0)
                    break;

                if (!firstByteReceived)
                {
                    firstByteReceived = true;
                    var elapsed = (DateTime.UtcNow - activeInfo.StartedAtUtc).TotalSeconds;
                    activeInfo.FirstUpstreamByteAfterSeconds = Math.Round(elapsed, 1);
                    logger.LogInformation(
                        "{RequestId} first_upstream_byte afterSeconds={After}",
                        requestId, activeInfo.FirstUpstreamByteAfterSeconds);
                }

                await WriteAndFlush(httpContext, buffer.AsMemory(0, bytesRead), cts.Token);
            }

            await httpContext.Response.BodyWriter.FlushAsync();

            activeInfo.State = RequestState.done;
            logger.LogInformation(
                "{RequestId} upstream_done elapsedSeconds={Elapsed}",
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
            if (!cts.IsCancellationRequested)
                cts.Cancel();
        }
    }

    // ── Small helpers ──────────────────────────────────────────────────────

    static (IHttpClient Client, HttpRequestMessage Message) BuildUpstreamRequest(
        IHttpClientFactory factory, ProxyConfig cfg, byte[] bodyBytes, HttpContext httpContext)
    {
        var client = factory.CreateClient("upstream");
        var message = new HttpRequestMessage(HttpMethod.Post, $"{cfg.UpstreamBaseUrl}/chat/completions");
        message.Content = new ByteArrayContent(bodyBytes);
        message.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json");

        if (httpContext.Request.Headers.Authorization.Count > 0)
        {
            var authValue = httpContext.Request.Headers.Authorization.ToString();
            message.Headers.Authorization = AuthenticationHeaderValue.Parse(authValue);
        }

        return (client, message);
    }

    static void SetupSseResponse(HttpContext ctx)
    {
        ctx.Response.StatusCode = StatusCodes.Status200OK;
        ctx.Response.ContentType = "text/event-stream; charset=utf-8";
        ctx.Response.Headers.CacheControl = "no-cache";
        ctx.Response.Headers["X-Accel-Buffering"] = "no";
    }

    static async Task WriteAndFlush(HttpContext ctx, ReadOnlyMemory<byte> data, CancellationToken ct = default)
    {
        await ctx.Response.BodyWriter.WriteAsync(data, ct);
        await ctx.Response.BodyWriter.FlushAsync(ct);
    }

    static async Task WriteBusyResponse(HttpContext ctx, string modelName)
    {
        ctx.Response.StatusCode = StatusCodes.Status429TooManyRequests;
        ctx.Response.ContentType = "application/json";
        var body = JsonSerializer.Serialize(new
        {
            error = new
            {
                message = $"{modelName} is busy. Wait until the current request finishes or restart llama-architect if it was cancelled.",
                type = "slowrig_model_busy"
            }
        });
        await ctx.Response.WriteAsync(body, ctx.RequestAborted);
    }

    static async Task WriteErrorResponse(HttpContext ctx, string message)
    {
        ctx.Response.StatusCode = StatusCodes.Status502BadGateway;
        ctx.Response.ContentType = "application/json";
        var body = JsonSerializer.Serialize(new
        {
            error = new { message, type = "proxy_error" }
        });
        await ctx.Response.WriteAsync(body, ctx.RequestAborted);
    }

    static async Task WriteSseError(
        HttpContext ctx, int statusCode, string requestId, ILogger logger)
    {
        logger.LogWarning("{RequestId} upstream_error status={Status}", requestId, statusCode);

        var errorPayload =
            $"{{\"error\":{{\"message\":\"Upstream returned status {statusCode}\",\"type\":\"upstream_error\"}}}}";
        var errorSse = $"data: {errorPayload}\n\ndata: [DONE]\n\n";
        var errorBytes = Encoding.UTF8.GetBytes(errorSse);

        await WriteAndFlush(ctx, errorBytes);
    }
}
