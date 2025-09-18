using Microsoft.Extensions.Diagnostics.HealthChecks;
using PokerService.Data;
using StackExchange.Redis;

namespace PokerService.Services
{
    public class DatabaseHealthCheck : IHealthCheck
    {
        private readonly PokerDbContext _dbContext;

        public DatabaseHealthCheck(PokerDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        public async Task<HealthCheckResult> CheckHealthAsync(
            HealthCheckContext context,
            CancellationToken cancellationToken = default)
        {
            try
            {
                // Try to open connection and execute a simple query
                await _dbContext.Database.CanConnectAsync(cancellationToken);

                return HealthCheckResult.Healthy("Database connection is healthy");
            }
            catch (Exception ex)
            {
                return HealthCheckResult.Unhealthy(
                    "Database connection failed",
                    exception: ex);
            }
        }
    }

    public class RedisHealthCheck : IHealthCheck
    {
        private readonly IConnectionMultiplexer _redis;

        public RedisHealthCheck(IConnectionMultiplexer redis)
        {
            _redis = redis;
        }

        public async Task<HealthCheckResult> CheckHealthAsync(
            HealthCheckContext context,
            CancellationToken cancellationToken = default)
        {
            try
            {
                var database = _redis.GetDatabase();
                await database.PingAsync();

                if (_redis.IsConnected)
                {
                    return HealthCheckResult.Healthy("Redis connection is healthy");
                }
                else
                {
                    return HealthCheckResult.Degraded("Redis connection is degraded");
                }
            }
            catch (Exception ex)
            {
                return HealthCheckResult.Unhealthy(
                    "Redis connection failed",
                    exception: ex);
            }
        }
    }
}
