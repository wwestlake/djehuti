using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Djehuti.DjeLab;
using Djehuti.DjeLab.Services;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

builder.Services.AddScoped(sp => new HttpClient { BaseAddress = new Uri(builder.HostEnvironment.BaseAddress) });
builder.Services.AddSingleton<AiConfigStore>();
builder.Services.AddSingleton<ChatHistoryStore>();
builder.Services.AddScoped<AiChatClient>();

await builder.Build().RunAsync();
