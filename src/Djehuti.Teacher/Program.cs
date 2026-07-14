using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Djehuti.Teacher;
using Djehuti.Teacher.Services;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

builder.Services.AddScoped(sp => new HttpClient { BaseAddress = new Uri(builder.HostEnvironment.BaseAddress) });
builder.Services.AddSingleton<AiConfigStore>();
builder.Services.AddScoped<TeacherApiClient>();
// Singleton: one model download/engine instance for the whole app session.
builder.Services.AddSingleton<WebLlmClient>();

await builder.Build().RunAsync();
