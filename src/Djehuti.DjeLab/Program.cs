using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Djehuti.DjeLab;
using Djehuti.DjeLab.Services;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

builder.Services.AddScoped(sp => new HttpClient { BaseAddress = new Uri(builder.HostEnvironment.BaseAddress) });
builder.Services.AddScoped<DjeLabStorageScopeService>();
builder.Services.AddScoped<AiConfigStore>();
builder.Services.AddScoped<ChatHistoryStore>();
builder.Services.AddScoped<AiChatClient>();
builder.Services.AddScoped<DjeLabFilesClient>();
builder.Services.AddSingleton<WorkspaceActions>();

await builder.Build().RunAsync();
