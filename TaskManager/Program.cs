using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using TaskManager.Data;

var builder = WebApplication.CreateBuilder(args);

// --- Database (SQLite, zero-config for local dev) ---
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? "Data Source=taskmanager.db";
builder.Services.AddDbContext<AppDbContext>(options => options.UseSqlite(connectionString));

// --- ASP.NET Core Identity ---
// AddIdentityCore gives us the pieces we actually use — password hashing, the EF
// user store, lockout, and SignInManager — without MapIdentityApi's bearer-token
// scheme or its ~8 extra endpoints (email confirmation, password reset, 2FA, …)
// that this app doesn't wire up. We expose only register/login/logout/me below.
builder.Services
    .AddIdentityCore<ApplicationUser>(options => options.User.RequireUniqueEmail = true)
    .AddEntityFrameworkStores<AppDbContext>()
    .AddSignInManager();

// The session is a single HttpOnly cookie (HttpOnly + SameSite=Lax by default).
// Return 401/403 instead of redirecting to a login page so the SPA can react.
builder.Services
    .AddAuthentication(IdentityConstants.ApplicationScheme)
    .AddCookie(IdentityConstants.ApplicationScheme, options =>
    {
        options.Events.OnRedirectToLogin = context =>
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            return Task.CompletedTask;
        };
        options.Events.OnRedirectToAccessDenied = context =>
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            return Task.CompletedTask;
        };
    });

builder.Services.AddAuthorization();

var app = builder.Build();

// Apply migrations on startup so a reviewer just needs `dotnet run` — the SQLite
// file and schema are created automatically on first launch.
using (var scope = app.Services.CreateScope())
{
    scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.Migrate();
}

// Serve the built React app (Vite outputs into wwwroot) and fall back to
// index.html for client-side routing.
app.UseDefaultFiles();
app.UseStaticFiles();

app.UseAuthentication();
app.UseAuthorization();

// --- Auth endpoints (only what the app uses) ---

app.MapPost("/account/register", async (
    AuthRequest request, UserManager<ApplicationUser> userManager) =>
{
    var user = new ApplicationUser { UserName = request.Email, Email = request.Email };
    var result = await userManager.CreateAsync(user, request.Password);

    return result.Succeeded
        ? Results.Ok()
        // Surface Identity's validation errors (weak password, duplicate email, …)
        // as an RFC 7807 problem-details response the frontend already parses.
        : Results.ValidationProblem(
            result.Errors.ToDictionary(e => e.Code, e => new[] { e.Description }));
});

app.MapPost("/account/login", async (
    AuthRequest request, SignInManager<ApplicationUser> signInManager) =>
{
    if (string.IsNullOrEmpty(request.Email) || string.IsNullOrEmpty(request.Password))
        return Results.Unauthorized();

    // Issues the identity cookie on success. lockoutOnFailure throttles brute force.
    var result = await signInManager.PasswordSignInAsync(
        request.Email, request.Password, isPersistent: true, lockoutOnFailure: true);

    return result.Succeeded ? Results.Ok() : Results.Unauthorized();
});

app.MapPost("/account/logout", async (HttpContext http) =>
{
    await http.SignOutAsync(IdentityConstants.ApplicationScheme);
    return Results.Ok();
}).RequireAuthorization();

// Lightweight "who am I" endpoint the frontend calls on load to check session state.
// UserName is the email (we set them equal at registration).
app.MapGet("/account/me", (ClaimsPrincipal user) => Results.Ok(new
{
    email = user.FindFirstValue(ClaimTypes.Email) ?? user.Identity?.Name,
})).RequireAuthorization();

app.MapFallbackToFile("index.html");

app.Run();

record AuthRequest(string Email, string Password);

// Exposes the implicit Program class so the test project can bootstrap the app
// with WebApplicationFactory<Program>.
public partial class Program { }
