using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace TaskManager.Data;

// IdentityUserContext wires up the Identity user tables (users, claims, logins,
// tokens) but omits the role tables — we don't use roles. App-specific entities
// (e.g. tasks) get added here as the app grows.
public class AppDbContext : IdentityUserContext<ApplicationUser>
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }
}
