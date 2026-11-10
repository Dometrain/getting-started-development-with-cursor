using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace LivePoll.Infrastructure.Persistence;

public sealed class LivePollDbContextFactory : IDesignTimeDbContextFactory<LivePollDbContext>
{
    public LivePollDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException("ConnectionStrings__DefaultConnection is required.");
        }

        var options = new DbContextOptionsBuilder<LivePollDbContext>()
            .UseNpgsql(connectionString)
            .Options;
        return new LivePollDbContext(options);
    }
}