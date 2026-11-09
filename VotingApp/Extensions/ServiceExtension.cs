using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System;
using VotingApp.Authorization;
using VotingApp.Authorization.Handlers;
using VotingApp.Data;
using VotingApp.Data.Interface;
using VotingApp.Data.Repository;
using VotingApp.Services.Elections;

namespace VotingApp.Extensions
{
    public static class ServiceExtension
    {
        public static void Configure(this IServiceCollection services, IConfiguration configuration)
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
                options.UseSqlServer(connectionString,
                assembly => assembly.MigrationsAssembly(typeof(AppDbContext).Assembly.FullName)));
            services.AddDatabaseDeveloperPageExceptionFilter();

            services.AddIdentity<AppUser, IdentityRole>(
                options =>
                {
                    options.Password.RequiredLength = 8;
                    options.Password.RequireDigit = true;
                    options.Password.RequireLowercase = true;
                    options.Password.RequireUppercase = true;
                    options.Password.RequireNonAlphanumeric = false;
                    options.Password.RequiredUniqueChars = 4;

                    options.Lockout.MaxFailedAccessAttempts = 5;
                    options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
                    options.Lockout.AllowedForNewUsers = true;

                    options.SignIn.RequireConfirmedAccount = false;
                    options.User.RequireUniqueEmail = true;
                })
                .AddDefaultTokenProviders()
                .AddDefaultUI()
                .AddEntityFrameworkStores<AppDbContext>();

            // AJAX posts send the antiforgery token in this header (see wwwroot/js/voting_broadcasting.js).
            services.AddAntiforgery(options => options.HeaderName = "RequestVerificationToken");

            services.AddHttpContextAccessor();
            services.AddScoped<IRepositoryWrapper, RepositoryWrapper>();
            services.AddSignalR();
            services.AddAutoMapper(typeof(Startup));

            services.AddScoped<IAuthorizationHandler, PermissionAuthorizationHandler>();
            services.AddSingleton<IAuthorizationPolicyProvider, PermissionPolicyProvider>();

            services.AddSingleton<PresidentialResultCalculator>();
            services.AddSingleton<ParliamentarySeatAllocator>();
            services.AddSingleton<ResultSheetValidator>();
            services.AddSingleton<PartyListValidator>();
        }
    }
}
