using Microsoft.Extensions.Configuration;

namespace Slowrig.IdeProxy.Config;

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
