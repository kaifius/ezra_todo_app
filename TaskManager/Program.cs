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

// --- Task endpoints (todo CRUD, scoped to the signed-in user) ---

var tasks = app.MapGroup("/api/tasks").RequireAuthorization();

tasks.MapGet("/", async (ClaimsPrincipal user, AppDbContext db) =>
{
    var userId = user.GetUserId();
    var items = await db.Tasks
        .Where(t => t.UserId == userId)
        .OrderBy(t => t.Position).ThenBy(t => t.Id)
        .Select(t => TaskResponse.From(t))
        .ToListAsync();

    return Results.Ok(items);
});

tasks.MapGet("/{id:int}", async (int id, ClaimsPrincipal user, AppDbContext db) =>
{
    var userId = user.GetUserId();
    var task = await db.Tasks.FirstOrDefaultAsync(t => t.Id == id && t.UserId == userId);

    return task is null ? Results.NotFound() : Results.Ok(TaskResponse.From(task));
});

tasks.MapPost("/", async (TaskRequest request, ClaimsPrincipal user, AppDbContext db) =>
{
    var title = request.Title?.Trim();
    if (string.IsNullOrEmpty(title))
        return TitleRequired();

    var userId = user.GetUserId();
    // Append to the end of this user's list.
    var maxPosition = await db.Tasks
        .Where(t => t.UserId == userId)
        .MaxAsync(t => (int?)t.Position) ?? -1;

    var task = new TaskItem
    {
        Title = title,
        IsCompleted = request.IsCompleted ?? false,
        Position = maxPosition + 1,
        CreatedAt = DateTimeOffset.UtcNow,
        UserId = userId!,
    };

    db.Tasks.Add(task);
    await db.SaveChangesAsync();

    return Results.Created($"/api/tasks/{task.Id}", TaskResponse.From(task));
});

tasks.MapPut("/{id:int}", async (int id, TaskRequest request, ClaimsPrincipal user, AppDbContext db) =>
{
    var title = request.Title?.Trim();
    if (string.IsNullOrEmpty(title))
        return TitleRequired();

    var userId = user.GetUserId();
    var task = await db.Tasks.FirstOrDefaultAsync(t => t.Id == id && t.UserId == userId);
    if (task is null)
        return Results.NotFound();

    task.Title = title;
    // Completion and position are optional on update; leave them untouched when
    // the client omits them (e.g. a rename doesn't clear the checkbox).
    if (request.IsCompleted is bool completed)
        task.IsCompleted = completed;
    if (request.Position is int position)
        task.Position = position;

    await db.SaveChangesAsync();

    return Results.Ok(TaskResponse.From(task));
});

tasks.MapDelete("/{id:int}", async (int id, ClaimsPrincipal user, AppDbContext db) =>
{
    var userId = user.GetUserId();
    var task = await db.Tasks.FirstOrDefaultAsync(t => t.Id == id && t.UserId == userId);
    if (task is null)
        return Results.NotFound();

    db.Tasks.Remove(task);
    await db.SaveChangesAsync();

    return Results.NoContent();
});

app.MapFallbackToFile("index.html");

app.Run();

// Same RFC 7807 `errors` body shape the auth endpoints and React client use.
static IResult TitleRequired() => Results.ValidationProblem(new Dictionary<string, string[]>
{
    ["Title"] = new[] { "Title is required." },
});

record AuthRequest(string Email, string Password);

// Nullable so an omitted field means "leave unchanged" on update, rather than
// resetting to false/0.
record TaskRequest(string? Title, bool? IsCompleted, int? Position);

record TaskResponse(int Id, string Title, bool IsCompleted, int Position, DateTimeOffset CreatedAt)
{
    public static TaskResponse From(TaskItem t) =>
        new(t.Id, t.Title, t.IsCompleted, t.Position, t.CreatedAt);
}

static class ClaimsPrincipalExtensions
{
    // The Identity cookie carries the user id as the NameIdentifier claim.
    public static string? GetUserId(this ClaimsPrincipal user) =>
        user.FindFirstValue(ClaimTypes.NameIdentifier);
}

// Exposes the implicit Program class so the test project can bootstrap the app
// with WebApplicationFactory<Program>.
public partial class Program { }
