using Microsoft.JSInterop;

namespace Djehuti.Teacher.Services;

public sealed class WebLlmInterop
{
    private readonly IJSRuntime _js;

    public WebLlmInterop(IJSRuntime js)
    {
        _js = js;
    }

    public async Task<bool> IsAvailableAsync()
    {
        return false;
    }
}
