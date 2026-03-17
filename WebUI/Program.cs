using DataSetService;
using DataSetService.FamilyTree;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.DependencyInjection;
using System.Net;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddRazorPages();
builder.Services.AddServerSideBlazor();
builder.Services.AddMemoryCache();

var dbPath = builder.Configuration["DbPath"] ?? "/home/stefan-hall/gullberg.sqlite";
builder.Services.AddSingleton(new UserService(dbPath));

var familyDbPath = builder.Configuration["FamilyDbPath"] ?? "/var/lib/FamilyWeb/gullberg.sqlite";
builder.Services.AddSingleton(new PersonService(familyDbPath));
builder.Services.AddSingleton(new FamilyTreeService(familyDbPath));

builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/account/login";
        options.ExpireTimeSpan = TimeSpan.FromHours(8);
        options.SlidingExpiration = true;
    });
builder.Services.AddAuthorization();

builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    options.KnownProxies.Add(IPAddress.Loopback);
    options.KnownProxies.Add(IPAddress.IPv6Loopback);
});

var app = builder.Build();

app.UseForwardedHeaders();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseStaticFiles();
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();

app.MapBlazorHub();
app.MapFallbackToPage("/_Host");

// Skapa en användare via miljövariabler vid uppstart (kör en gång, sedan ta bort variablerna):
// SEED_EMAIL=din@email.se SEED_PASSWORD=dittlösenord dotnet run
var seedEmail = Environment.GetEnvironmentVariable("SEED_EMAIL");
var seedPassword = Environment.GetEnvironmentVariable("SEED_PASSWORD");
if (!string.IsNullOrEmpty(seedEmail) && !string.IsNullOrEmpty(seedPassword))
{
    var userService = app.Services.GetRequiredService<UserService>();
    if (!userService.UserExists(seedEmail))
    {
        userService.CreateUser(seedEmail, seedPassword);
        Console.WriteLine($"Användare '{seedEmail}' skapad.");
    }
    else
    {
        Console.WriteLine($"Användare '{seedEmail}' finns redan.");
    }
}

app.Run();
