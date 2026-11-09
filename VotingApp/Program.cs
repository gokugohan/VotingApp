using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Serilog;
using VotingApp.Application;
using VotingApp.Infrastructure;
using VotingApp.Infrastructure.Identity;
using VotingApp.Infrastructure.Persistence;
using VotingApp.Web;

namespace VotingApp
{
    public static class Program
    {
        public static async Task<int> Main(string[] args)
        {
            // Container health probe (the runtime image has no curl): `dotnet VotingApp.dll --healthcheck`.
            if (args.Length > 0 && args[0] == "--healthcheck")
            {
                return await ProbeAsync();
            }

            // Bootstrap logger: captures start-up failures before configuration is loaded.
            Log.Logger = new LoggerConfiguration().WriteTo.Console().CreateBootstrapLogger();
            try
            {
                var app = BuildApp(args);
                await InitializeDatabaseAsync(app);
                await app.RunAsync();
                return 0;
            }
            catch (Exception ex) when (ex is not HostAbortedException)
            {
                Log.Fatal(ex, "Application terminated unexpectedly");
                return 1;
            }
            finally
            {
                await Log.CloseAndFlushAsync();
            }
        }

        public static WebApplication BuildApp(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            builder.Host.UseSerilog((context, services, logger) => logger
                .ReadFrom.Configuration(context.Configuration)
                .ReadFrom.Services(services)
                .Enrich.FromLogContext()
                .Enrich.WithProperty("Application", "VotingApp"));

            builder.Services
                .AddApplication()
                .AddInfrastructure(builder.Configuration)
                .AddWeb(builder.Configuration, builder.Environment);

            var app = builder.Build();
            app.UseWebPipeline();
            return app;
        }

        private static async Task<int> ProbeAsync()
        {
            var port = Environment.GetEnvironmentVariable("ASPNETCORE_HTTP_PORTS")?.Split(';', ',')[0] ?? "8080";
            try
            {
                using var http = new System.Net.Http.HttpClient { Timeout = TimeSpan.FromSeconds(3) };
                var response = await http.GetAsync($"http://localhost:{port}/health/live");
                return response.IsSuccessStatusCode ? 0 : 1;
            }
            catch (Exception)
            {
                return 1;
            }
        }

        /// <summary>Optionally applies migrations (Database:MigrateOnStartup), then seeds roles and permissions.</summary>
        private static async Task InitializeDatabaseAsync(WebApplication app)
        {
            using var scope = app.Services.CreateScope();
            var services = scope.ServiceProvider;
            var logger = services.GetRequiredService<ILogger<AppDbContext>>();

            if (app.Configuration.GetValue<bool>("Database:MigrateOnStartup"))
            {
                logger.LogInformation("Applying database migrations");
                await services.GetRequiredService<AppDbContext>().Database.MigrateAsync();
            }

            try
            {
                await IdentitySeeder.SeedAsync(
                    services.GetRequiredService<UserManager<AppUser>>(),
                    services.GetRequiredService<RoleManager<IdentityRole>>(),
                    app.Configuration,
                    logger);
            }
            catch (Exception ex)
            {
                // Keep the site up (e.g. DB briefly unreachable); /health/ready reports the database state.
                logger.LogError(ex, "Seeding roles/permissions failed");
            }
        }
    }
}
