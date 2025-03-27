namespace KeySpinner;

public interface IKeyQueue
{
    void Enqueue(ApiKey apiKey);
    ApiKey? Dequeue();

    bool Contains(ApiKey apiKey);
    int Count { get; }
}
