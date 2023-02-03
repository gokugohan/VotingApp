using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using VotingApp.Authorization;
using VotingApp.Authorization.Handlers;
using VotingApp.Data;
using VotingApp.Data.Interface;
using VotingApp.Data.Repository;

namespace VotingApp.Extensions
{
    public static class ServiceExtension
    {
        public static void Configure(this IServiceCollection services, IConfiguration configuration)
        {
            // Add identity db context. identity dbcontext must be integrated with frontend application
            services.AddDbContext<AppDbContext>(options =>
                options.UseSqlServer(configuration.GetConnectionString("DefaultConnection"),
                assembly => assembly.MigrationsAssembly(typeof(AppDbContext).Assembly.FullName)));
            services.AddIdentity<AppUser, IdentityRole>(
                options =>
                {
                    options.Password.RequireDigit = false;
                    options.Password.RequiredLength = 1;
                    options.Password.RequiredUniqueChars = 0;
                    options.Password.RequireLowercase = false;
                    options.Password.RequireUppercase = false;
                    options.Password.RequireNonAlphanumeric = false;
                    options.SignIn.RequireConfirmedAccount = false;
                    options.User.RequireUniqueEmail = false;

                })
                .AddDefaultTokenProviders()
                .AddDefaultUI()
                .AddEntityFrameworkStores<AppDbContext>();

            //services.ConfigureApplicationCookie(options =>
            //{
            //    options.Cookie.HttpOnly = true;
            //    options.ExpireTimeSpan = TimeSpan.FromMinutes(60);
            //    options.LoginPath = "/Account/Login";
            //    options.AccessDeniedPath = "/AccessDenied";
            //    options.SlidingExpiration = true;
            //});



            services.AddScoped<IRepositoryWrapper, RepositoryWrapper>();
            services.AddSignalR();
            services.AddAutoMapper(typeof(Startup));

            services.AddScoped<IAuthorizationHandler, PermissionAuthorizationHandler>();


            services.AddSingleton<IAuthorizationPolicyProvider, PermissionPolicyProvider>();


        }
    }
}
