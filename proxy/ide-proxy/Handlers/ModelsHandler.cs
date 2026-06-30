using System.Net.Http.Headers;
using System.Text.Json;

using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

using Slowrig.IdeProxy.Config;
using Slowrig.IdeProxy.Helpers;

namespace Slowrig.IdeProxy.Handlers;

public static class ModelsHandler
{
    public static async Task HandleAsync(
        HttpContext httpContext,
        IHttpClientFactory factory,
        ProxyConfig cfg,
        ILoggerFactory loggerFactory)
    {
        var logger = loggerFactory.CreateLogger("Slowrig.IdeProxy.Models");
        var requestId = RequestHelpers.GenerateRequestId();
        var sw = System.Diagnostics.Stopwatch.StartNew();

        logger.LogInformation(
            "{RequestId} request_received method=GET path=/v1/models", requestId);

        try
        {
            var client = factory.CreateClient("upstream");
            var request = new HttpRequestMessage(HttpMethod.Get, $"{cfg.UpstreamBaseUrl}/models");

            ForwardAuth(httpContext, request);

            var response = await client.SendAsync(request, httpContext.RequestAborted);
            var elapsedMs = sw.ElapsedMilliseconds;

            logger.LogInformation(
                "{RequestId} upstream_response method=GET path=/v1/models status={Status} elapsedMs={ElapsedMs}",
                requestId, (int)response.StatusCode, elapsedMs);

            var body = await response.Content.ReadAsStreamAsync(httpContext.RequestAborted);
            httpContext.Response.StatusCode = (int)response.StatusCode;
            httpContext.Response.ContentType =
                response.Content.Headers.ContentType?.ToString() ?? "application/json";

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
    }

    private static void ForwardAuth(HttpContext httpContext, HttpRequestMessage request)
    {
        if (httpContext.Request.Headers.Authorization.Count > 0)
        {
            var authValue = httpContext.Request.Headers.Authorization.ToString();
            request.Headers.Authorization = AuthenticationHeaderValue.Parse(authValue);
        }
    }
}
