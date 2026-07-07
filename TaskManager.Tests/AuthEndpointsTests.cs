using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Http;

namespace TaskManager.Tests;

// These exercise the behaviour we wrote on top of Identity — our status-code
// mapping, our null-guard, our error-body shape, and our cookie session wiring —
// not Identity's internals (hashing, lockout, validators).
public class AuthEndpointsTests : IClassFixture<TestAppFactory>
{
    private readonly TestAppFactory _factory;

    public AuthEndpointsTests(TestAppFactory factory) => _factory = factory;

    private static object Credentials(string? email = null) => new
    {
        email = email ?? $"user-{Guid.NewGuid():N}@example.com",
        password = "P@ssw0rd!",
    };

    // Identity's cookie defaults to a 302 redirect to a login page; our config
    // overrides that to a plain 401 for the SPA. This pins that choice.
    [Fact]
    public async Task Me_WithoutSession_Returns401NotRedirect()
    {
        var client = _factory.CreateClient(new() { AllowAutoRedirect = false });

        var response = await client.GetAsync("/account/me");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    // Without our null-guard, an empty body makes UserManager throw (500). This
    // pins that we return a clean 401 instead.
    [Fact]
    public async Task Login_WithEmptyBody_Returns401()
    {
        var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync("/account/login", new { });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    // The duplicate is detected by Identity, but the 400 + RFC 7807 `errors` body
    // is our translation — and the React client depends on that exact shape.
    [Fact]
    public async Task Register_Duplicate_Returns400WithErrorsBody()
    {
        var client = _factory.CreateClient();
        var creds = Credentials();

        var first = await client.PostAsJsonAsync("/account/register", creds);
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);

        var second = await client.PostAsJsonAsync("/account/register", creds);
        Assert.Equal(HttpStatusCode.BadRequest, second.StatusCode);

        var problem = await second.Content.ReadFromJsonAsync<HttpValidationProblemDetails>();
        Assert.NotNull(problem);
        Assert.NotEmpty(problem!.Errors);
    }

    // The whole point of cookie auth: login issues a session, protected endpoints
    // read it, and logout clears it.
    [Fact]
    public async Task CookieSession_LoginThenMeThenLogout()
    {
        var client = _factory.CreateClient();
        var email = $"roundtrip-{Guid.NewGuid():N}@example.com";
        var creds = Credentials(email);

        Assert.Equal(HttpStatusCode.OK,
            (await client.PostAsJsonAsync("/account/register", creds)).StatusCode);
        Assert.Equal(HttpStatusCode.OK,
            (await client.PostAsJsonAsync("/account/login", creds)).StatusCode);

        // The cookie set by login is reused here by the client's cookie container.
        var me = await client.GetAsync("/account/me");
        Assert.Equal(HttpStatusCode.OK, me.StatusCode);
        var body = await me.Content.ReadFromJsonAsync<MeResponse>();
        Assert.Equal(email, body!.Email);

        Assert.Equal(HttpStatusCode.OK,
            (await client.PostAsync("/account/logout", null)).StatusCode);

        // Session cleared → protected endpoint rejects again.
        var meAfterLogout = await client.GetAsync("/account/me");
        Assert.Equal(HttpStatusCode.Unauthorized, meAfterLogout.StatusCode);
    }

    private record MeResponse(string Email);
}
