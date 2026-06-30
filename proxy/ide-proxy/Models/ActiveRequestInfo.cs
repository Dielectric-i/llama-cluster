namespace Slowrig.IdeProxy.Models;

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
