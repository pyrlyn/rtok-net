using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Radzen;
using Rtok.Web.Client;
using Rtok.Web.Client.Services;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");
builder.Services.AddRadzenComponents();
builder.Services.AddScoped<IWsTransport, BrowserWsTransport>();
builder.Services.AddScoped<DashboardConnection>();
builder.Services.AddScoped<ThemeStore>();
await builder.Build().RunAsync();
