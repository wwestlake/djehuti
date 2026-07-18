namespace Djehuti.Teacher.Services;

public class WebLlmInterop
{
    public WebLlmState State { get; set; } = WebLlmState.Unsupported;

    public async Task<bool> IsAvailableAsync()
    {
        return await Task.FromResult(State == WebLlmState.Ready);
    }
}
