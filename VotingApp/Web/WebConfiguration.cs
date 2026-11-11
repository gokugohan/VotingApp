using System;
using System.IO;
using System.Linq;
using System.Security.Claims;
using System.Threading.RateLimiting;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;
using Serilog;
using Serilog.Context;
using VotingApp.Application.Abstractions;
using VotingApp.Authorization;
using VotingApp.Authorization.Claims;
using VotingApp.Authorization.Handlers;
using VotingApp.Infrastructure;
using VotingApp.Infrastructure.Identity;
using VotingApp.Infrastructure.Persistence;

namespace VotingApp.Web
{
    /// <summary>Web host composition: MVC, Identity, security, observability and the request pipeline.</summary>
    public static class WebConfiguration
    {
        public const string CorrelationHeader = "X-Correlation-Id";

        public static IServiceCollection AddWeb(this IServiceCollection services, IConfiguration configuration, IWebHostEnvironment environment)
        {
            // Labels and validation messages come from the same translation files as the views. Registered
            // before MVC so its default resource-file localizer is not used.
            services.AddSingleton<Microsoft.Extensions.Localization.IStringLocalizerFactory, VotingApp.Web.Localization.CatalogLocalizerFactory>();
            services.AddControllersWithViews(o =>
                {
                    o.ModelMetadataDetailsProviders.Add(new VotingApp.Web.Localization.DefaultValidationMessages());
                    // Forms are read with the invariant culture, whatever language the page is shown in.
                    var form = o.ValueProviderFactories.IndexOf(o.ValueProviderFactories.OfType<Microsoft.AspNetCore.Mvc.ModelBinding.FormValueProviderFactory>().Single());
                    o.ValueProviderFactories[form] = new VotingApp.Web.Localization.InvariantFormValueProviderFactory();
                })
                .AddNewtonsoftJson(o => o.SerializerSettings.ReferenceLoopHandling = Newtonsoft.Json.ReferenceLoopHandling.Ignore)
                .AddDataAnnotationsLocalization(o => o.DataAnnotationLocalizerProvider = (_, factory) => factory.Create(typeof(object)));
            services.AddRazorPages();
            services.AddHttpContextAccessor();
            services.AddScoped<ICurrentUser, CurrentUser>();
            services.AddScoped<ImageStorage>();
            services.AddScoped<SheetScanStorage>();
            services.AddSingleton<VotingApp.Web.Ui.TimorMap>();
            services.AddSingleton<VotingApp.Web.Localization.TranslationCatalog>();
            services.AddScoped<VotingApp.Web.Localization.Texts>();
            services.AddDatabaseDeveloperPageExceptionFilter();

            // ---- Identity ----
            services.AddIdentity<AppUser, IdentityRole>(options =>
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
                .AddErrorDescriber<VotingApp.Web.Localization.TranslatedIdentityErrors>()
                .AddDefaultUI()
                .AddClaimsPrincipalFactory<CustomUserClaimsPrincipalFactory>()
                .AddEntityFrameworkStores<AppDbContext>();

            services.ConfigureApplicationCookie(options =>
            {
                options.Cookie.HttpOnly = true;
                options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
                options.Cookie.SameSite = SameSiteMode.Lax;
                options.ExpireTimeSpan = TimeSpan.FromHours(8); // one working shift
                options.SlidingExpiration = true;
            });

            // Keys protect auth cookies and antiforgery tokens. Persist them when running several
            // instances or containers, otherwise users are signed out on every restart.
            var keysPath = configuration["DataProtection:KeysPath"];
            var dataProtection = services.AddDataProtection().SetApplicationName("VotingApp");
            if (!string.IsNullOrWhiteSpace(keysPath))
            {
                dataProtection.PersistKeysToFileSystem(new DirectoryInfo(keysPath));
            }

            // AJAX posts send the antiforgery token in this header (see wwwroot/js/voting_broadcasting.js).
            services.AddAntiforgery(options => options.HeaderName = "RequestVerificationToken");

            // ---- Authorization (permission claims) ----
            services.AddScoped<IAuthorizationHandler, PermissionAuthorizationHandler>();
            services.AddSingleton<IAuthorizationPolicyProvider, PermissionPolicyProvider>();

            // ---- Health ----
            services.AddHealthChecks()
                .AddDbContextCheck<AppDbContext>("database", tags: new[] { "ready" });

            // ---- Rate limiting: brute force on login and runaway clients on writes ----
            services.AddRateLimiter(options =>
            {
                options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
                options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
                {
                    if (!HttpMethods.IsPost(context.Request.Method))
                        return RateLimitPartition.GetNoLimiter("reads");

                    var ip = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
                    if (context.Request.Path.StartsWithSegments("/Identity/Account/Login", StringComparison.OrdinalIgnoreCase))
                    {
                        return RateLimitPartition.GetFixedWindowLimiter("login:" + ip, _ => new FixedWindowRateLimiterOptions
                        {
                            PermitLimit = configuration.GetValue("RateLimiting:LoginPerMinute", 10),
                            Window = TimeSpan.FromMinutes(1),
                        });
                    }

                    var user = context.User?.FindFirstValue(ClaimTypes.NameIdentifier) ?? ip;
                    return RateLimitPartition.GetFixedWindowLimiter("write:" + user, _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = configuration.GetValue("RateLimiting:WritesPerMinute", 120),
                        Window = TimeSpan.FromMinutes(1),
                    });
                });
            });

            return services;
        }

        public static WebApplication UseWebPipeline(this WebApplication app)
        {
            if (app.Environment.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
                app.UseMigrationsEndPoint();
            }
            else
            {
                app.UseExceptionHandler("/Home/Error");
                app.UseHsts();
            }

            app.Use(CorrelationAndSecurityHeaders);
            app.Use(VotingApp.Web.Localization.AppCultures.Apply);
            app.UseSerilogRequestLogging(options =>
            {
                options.EnrichDiagnosticContext = (diagnostics, http) =>
                {
                    diagnostics.Set("UserId", http.User?.FindFirstValue(ClaimTypes.NameIdentifier));
                    diagnostics.Set("ClientIp", http.Connection.RemoteIpAddress?.ToString());
                };
                // Health probes are frequent and uninteresting.
                options.GetLevel = (http, _, ex) =>
                    ex != null || http.Response.StatusCode >= 500 ? Serilog.Events.LogEventLevel.Error
                    : http.Request.Path.StartsWithSegments("/health") ? Serilog.Events.LogEventLevel.Verbose
                    : Serilog.Events.LogEventLevel.Information;
            });

            if (!app.Configuration.GetValue<bool>("DisableHttpsRedirection"))
            {
                app.UseHttpsRedirection();
            }
            app.UseStaticFiles();
            app.UseRouting();
            app.UseAuthentication();
            app.UseAuthorization();
            app.UseRateLimiter();

            app.MapHealthChecks("/health/live", new HealthCheckOptions { Predicate = _ => false });
            app.MapHealthChecks("/health/ready", new HealthCheckOptions { Predicate = check => check.Tags.Contains("ready") });
            app.MapControllerRoute(name: "default", pattern: "{controller=Home}/{action=Index}/{id?}");
            app.MapRazorPages();
            return app;
        }

        /// <summary>
        /// Propagates (or creates) a correlation id: echoed in the response, attached to every log line,
        /// and shown on the error page as the request id. Also sets baseline security headers.
        /// </summary>
        private static async Task CorrelationAndSecurityHeaders(HttpContext context, Func<Task> next)
        {
            var incoming = context.Request.Headers[CorrelationHeader].ToString();
            var correlationId = !string.IsNullOrWhiteSpace(incoming) && incoming.Length <= 64 && incoming.All(c => char.IsLetterOrDigit(c) || c is '-' or '_')
                ? incoming
                : context.TraceIdentifier;
            context.TraceIdentifier = correlationId;

            context.Response.OnStarting(() =>
            {
                var headers = context.Response.Headers;
                headers[CorrelationHeader] = correlationId;
                headers["X-Content-Type-Options"] = "nosniff";
                headers["X-Frame-Options"] = "DENY";
                headers["Referrer-Policy"] = "strict-origin-when-cross-origin";
                headers["Permissions-Policy"] = "camera=(), microphone=(), geolocation=()";
                return Task.CompletedTask;
            });

            using (LogContext.PushProperty("CorrelationId", correlationId))
            {
                await next();
            }
        }
    }
}
