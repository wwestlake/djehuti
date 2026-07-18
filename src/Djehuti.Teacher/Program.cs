using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Djehuti.Teacher;
using Djehuti.Teacher.Services;
using Djehuti.Teacher.Services.Helper;
using Djehuti.Teacher.Services.Providers;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

builder.Services.AddScoped(sp => new HttpClient { BaseAddress = new Uri(builder.HostEnvironment.BaseAddress) });
// Additional HttpClient for external API calls (OpenAI, Anthropic, etc) -- no BaseAddress so it can reach any endpoint
builder.Services.AddScoped(sp => new HttpClient());
builder.Services.AddSingleton<AiConfigStore>();
builder.Services.AddScoped<MultiProviderConfigStore>();
builder.Services.AddScoped<TeacherApiClient>();
// API LLM client with multi-provider support
builder.Services.AddScoped(sp =>
{
    var http = sp.GetRequiredService<HttpClient>();
    var configStore = sp.GetRequiredService<MultiProviderConfigStore>();
    var providers = sp.GetRequiredService<Dictionary<string, IProviderDefinition>>();
    return new ApiLlmClient(http, configStore, providers);
});
// Singleton: one model download/engine instance for the whole app session.
builder.Services.AddSingleton<WebLlmClient>();
// Helper system for error guidance and fallback options
builder.Services.AddScoped(sp => new HelperSystem(
    sp.GetRequiredService<WebLlmClient>(),
    sp.GetRequiredService<AiConfigStore>(),
    sp.GetRequiredService<HttpClient>()
));
// Provider definitions
builder.Services.AddSingleton<Dictionary<string, IProviderDefinition>>(sp =>
{
    return new()
    {
        { "openai", new OpenAiProvider() },
        { "anthropic", new AnthropicProvider() },
        { "google", new GoogleGeminiProvider() },
        { "mistral", new MistralProvider() }
    };
});

await builder.Build().RunAsync();
