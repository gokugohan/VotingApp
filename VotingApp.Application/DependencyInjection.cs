using Microsoft.Extensions.DependencyInjection;
using VotingApp.Application.AuditLogs;
using VotingApp.Application.Candidates;
using VotingApp.Application.Elections;
using VotingApp.Application.Geography;
using VotingApp.Application.PollingStations;
using VotingApp.Domain.Elections.Rules;

namespace VotingApp.Application
{
    public static class DependencyInjection
    {
        public static IServiceCollection AddApplication(this IServiceCollection services)
        {
            services.AddMemoryCache();
            services.AddSingleton<RoundResultsCache>();

            // Stateless election rules.
            services.AddSingleton<PresidentialResultCalculator>();
            services.AddSingleton<ParliamentarySeatAllocator>();
            services.AddSingleton<ResultSheetValidator>();
            services.AddSingleton<PartyListValidator>();

            // Use cases (scoped: they share the request's DbContext).
            services.AddScoped<AreaService>();
            services.AddScoped<CandidateService>();
            services.AddScoped<PollingStationService>();
            services.AddScoped<AuditLogService>();
            services.AddScoped<UserScopeService>();
            services.AddScoped<ElectionResultsService>();
            services.AddScoped<ElectionAdminService>();
            services.AddScoped<ResultSheetService>();
            services.AddScoped<PublicResultsService>();
            services.AddScoped<PartyListService>();
            return services;
        }
    }
}
