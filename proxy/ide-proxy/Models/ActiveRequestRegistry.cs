using System.Collections.Concurrent;

namespace Slowrig.IdeProxy.Models;

public sealed class ActiveRequestRegistry
{
    private readonly ConcurrentDictionary<string, ActiveRequestInfo> _requests = new();

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

    public int CountActive(string? model = null)
    {
        return All.Count(r =>
            (model is null || r.Model == model)
            && r.State is not RequestState.done and not RequestState.error and not RequestState.cancelled);
    }
}
