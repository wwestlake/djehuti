using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Djehuti.Teacher;
using Djehuti.Teacher.Services;
using Djehuti.Teacher.Services.Helper;
using Djehuti.Teacher.Services.Providers;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

// Single HttpClient for the whole app. BaseAddress only matters for relative-URI
// calls (TeacherApiClient, AuthCorner, YourLessons -- all same-origin /djehuti/api/...);
// absolute-URI calls (external LLM providers, Ollama) bypass BaseAddress entirely,
// so one client serves both. A second, BaseAddress-less registration used to exist
// here "for external API calls" but since HttpClient ignores BaseAddress for absolute
// URIs anyway, it was redundant -- and because both registrations were unkeyed, the
// second one silently won constructor injection everywhere, leaving TeacherApiClient
// with no BaseAddress and breaking every relative-URL call app-wide.
builder.Services.AddScoped(sp => new HttpClient { BaseAddress = new Uri(builder.HostEnvironment.BaseAddress) });
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
