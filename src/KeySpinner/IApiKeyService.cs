namespace KeySpinner;

public interface IApiKeyService
{
    ApiKey? GetAvailableKey();
    void ReleaseKey(ApiKey apiKey);
    bool IsKeyRateLimited(ApiKey apiKey);
    void RotateKeys();

    string PrintKeyStatus(ApiKey? apiKey);

    string PrintKeyStatus(KeyStatus status);
}
