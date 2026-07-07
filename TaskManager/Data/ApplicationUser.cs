using Microsoft.AspNetCore.Identity;

namespace TaskManager.Data;

// Custom user type so we have a place to hang app-specific fields later
// (e.g. a relationship to tasks). Empty for now — it behaves like IdentityUser.
public class ApplicationUser : IdentityUser
{
}
