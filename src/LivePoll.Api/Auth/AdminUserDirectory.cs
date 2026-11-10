namespace LivePoll.Api.Auth;

public sealed class AdminUserDirectory
{
    private readonly HashSet<string> adminUsers;

    public AdminUserDirectory(IConfiguration configuration)
    {
        var configured = configuration["Admin:Users"] ?? string.Empty;
        adminUsers = configured
            .Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
            .ToHashSet(StringComparer.Ordinal);
    }

    public bool IsAdmin(string username) => adminUsers.Contains(username);
}