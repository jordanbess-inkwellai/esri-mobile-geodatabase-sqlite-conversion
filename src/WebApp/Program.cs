using WebApp.Components;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

// Configure HttpClient for DI
// IMPORTANT: Replace "https://localhost:7XYZ" with the actual base address of your WebApi project.
// If running both projects locally, this might be something like "https://localhost:7001" or "http://localhost:5001"
// depending on your WebApi's launchSettings.json.
builder.Services.AddScoped(sp => new HttpClient { BaseAddress = new Uri("https://localhost:7123") }); // Example placeholder, adjust as needed

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();

app.UseStaticFiles();
app.UseAntiforgery();

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
