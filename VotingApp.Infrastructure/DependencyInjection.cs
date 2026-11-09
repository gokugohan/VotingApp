using System;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using VotingApp.Application.Abstractions;
using VotingApp.Infrastructure.Persistence;

namespace VotingApp.Infrastructure
{
    public static class DependencyInjection
    {
        /// <summary>
        /// Registers the SQL Server context as <see cref="IAppDbContext"/>. Identity is configured by the
        /// web host (it owns the UI); it uses <see cref="AppDbContext"/> as its store.
        /// </summary>
        public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
        {
            var connectionString = configuration.GetConnectionString("DefaultConnection");
            if (string.IsNullOrWhiteSpace(connectionString))
            {
                throw new InvalidOperationException(
                    "Connection string 'DefaultConnection' is not configured. " +
                    "Use 'dotnet user-secrets set ConnectionStrings:DefaultConnection \"...\"' in development, " +
                    "or the ConnectionStrings__DefaultConnection environment variable in production.");
            }

            services.AddDbContext<AppDbContext>(options =>
                options.UseSqlServer(connectionString, sql =>
                {
                    sql.MigrationsAssembly(typeof(AppDbContext).Assembly.FullName);
                    // Load multiple collections (ballot + entries, rounds + contestants) without row explosion.
                    sql.UseQuerySplittingBehavior(QuerySplittingBehavior.SplitQuery);
                    // Ride out transient faults (failover, brief network loss) on results night.
                    sql.EnableRetryOnFailure(maxRetryCount: 5, maxRetryDelay: TimeSpan.FromSeconds(10), errorNumbersToAdd: null);
                }));
            services.AddScoped<IAppDbContext>(sp => sp.GetRequiredService<AppDbContext>());
            return services;
        }
    }
}
