using System.Security.Claims;

namespace EsizzleAPI.Middleware;

public class AuthorizedUser
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string UserName { get; set; } = string.Empty;
    public List<string>? SystemRoles { get; set; }
    public int? ClientId { get; set; }
    public int AccessLevel { get; set; }
}

public class AuthorizedUserMiddleware
{
    private readonly RequestDelegate _next;

    public AuthorizedUserMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        // Extract user information from JWT claims
        if (context.User.Identity?.IsAuthenticated == true)
        {
            var authorizedUser = new AuthorizedUser
            {
                Id = int.Parse(context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "0"),
                Name = context.User.FindFirst(ClaimTypes.Name)?.Value ?? string.Empty,
                Email = context.User.FindFirst(ClaimTypes.Email)?.Value ?? string.Empty,
                UserName = context.User.FindFirst("username")?.Value ?? string.Empty,
                ClientId = int.TryParse(context.User.FindFirst("clientId")?.Value, out var clientId) ? clientId : null,
                AccessLevel = int.Parse(context.User.FindFirst("accessLevel")?.Value ?? "0"),
                SystemRoles = context.User.FindAll("role").Select(c => c.Value).ToList()
            };

            // Add the authorized user to the HttpContext so controllers can access it
            context.Items["AuthorizedUser"] = authorizedUser;
        }

        await _next(context);
    }
}