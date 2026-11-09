using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Threading.Tasks;
using VotingApp.Data;
using VotingApp.Data.Seed;

namespace VotingApp
{
    public static class Program
    {
        public static async Task Main(string[] args)
        {
            var host = CreateHostBuilder(args).Build();

            using (var scope = host.Services.CreateScope())
            {
                var serviceProvider = scope.ServiceProvider;
                var logger = serviceProvider.GetRequiredService<ILogger<AppDbContext>>();
                try
                {
                    await UserAndRoleDataInitializer.SeedDataAsync(
                        serviceProvider.GetRequiredService<UserManager<AppUser>>(),
                        serviceProvider.GetRequiredService<RoleManager<IdentityRole>>(),
                        serviceProvider.GetRequiredService<IConfiguration>(),
                        logger);
                }
                catch (Exception ex)
                {
                    // Keep the site up (e.g. DB temporarily unreachable) but make the failure visible.
                    logger.LogError(ex, "Seeding roles/permissions failed");
                }
            }

            await host.RunAsync();
        }

        public static IHostBuilder CreateHostBuilder(string[] args) =>
            Host.CreateDefaultBuilder(args)
                .ConfigureWebHostDefaults(webBuilder =>
                {
                    webBuilder.UseStartup<Startup>();
                });
    }
}
