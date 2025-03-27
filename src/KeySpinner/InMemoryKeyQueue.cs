using System.Collections.Concurrent;

namespace KeySpinner;

public class InMemoryKeyQueue : IKeyQueue
{
    private readonly ConcurrentQueue<ApiKey> _queues;

    public InMemoryKeyQueue(IEnumerable<ApiKey> apiKeys)
    {
        _queues = new ConcurrentQueue<ApiKey>();
        foreach (var apiKey in apiKeys.DistinctBy(k => k.Key))
        {
            _queues.Enqueue(apiKey);
        }
    }

    public void Enqueue(ApiKey apiKey)
    {
        _queues.Enqueue(apiKey);
    }


    public ApiKey? Dequeue()
    {
        if (_queues.TryDequeue(out ApiKey? apiKey))
        {
            return apiKey;
        }

        return null;
    }

    public bool Contains(ApiKey apiKey)
    {
        return _queues.Contains(apiKey);
    }

    public int Count => _queues.Count;
}
