using DataSetService;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddRazorPages();
builder.Services.AddServerSideBlazor();

var dbPath = builder.Configuration["DbPath"] ?? "/home/stefan-hall/gullberg.sqlite";
builder.Services.AddSingleton(new UserService(dbPath));

builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/account/login";
        options.ExpireTimeSpan = TimeSpan.FromHours(8);
        options.SlidingExpiration = true;
    });
builder.Services.AddAuthorization();

var app = builder.Build();

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
