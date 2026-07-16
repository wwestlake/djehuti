using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Djehuti.Teacher;
using Djehuti.Teacher.Services;
using Djehuti.Teacher.Services.Helper;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

builder.Services.AddScoped(sp => new HttpClient { BaseAddress = new Uri(builder.HostEnvironment.BaseAddress) });
// Additional HttpClient for external API calls (OpenAI, Anthropic, etc) -- no BaseAddress so it can reach any endpoint
builder.Services.AddScoped(sp => new HttpClient());
builder.Services.AddSingleton<AiConfigStore>();
builder.Services.AddScoped<TeacherApiClient>();
builder.Services.AddScoped<ApiLlmClient>();
// Singleton: one model download/engine instance for the whole app session.
builder.Services.AddSingleton<WebLlmClient>();
// Helper system for error guidance and fallback options
builder.Services.AddScoped(sp => new HelperSystem(
    sp.GetRequiredService<WebLlmClient>(),
    sp.GetRequiredService<AiConfigStore>(),
    sp.GetRequiredService<HttpClient>()
));

await builder.Build().RunAsync();
