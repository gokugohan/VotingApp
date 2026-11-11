# syntax=docker/dockerfile:1

# ---- Build ----
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Restore first (cached unless project files change).
COPY Directory.Build.props Directory.Packages.props VotingApp.sln ./
COPY VotingApp.Domain/VotingApp.Domain.csproj VotingApp.Domain/
COPY VotingApp.Application/VotingApp.Application.csproj VotingApp.Application/
COPY VotingApp.Infrastructure/VotingApp.Infrastructure.csproj VotingApp.Infrastructure/
COPY VotingApp/VotingApp.csproj VotingApp/
RUN dotnet restore VotingApp/VotingApp.csproj

COPY VotingApp.Domain/ VotingApp.Domain/
COPY VotingApp.Application/ VotingApp.Application/
COPY VotingApp.Infrastructure/ VotingApp.Infrastructure/
COPY VotingApp/ VotingApp/
RUN dotnet publish VotingApp/VotingApp.csproj -c Release -o /app/publish --no-restore /p:UseAppHost=false

# ---- Runtime ----
FROM mcr.microsoft.com/dotnet/aspnet:11.0 AS runtime
WORKDIR /app
ENV ASPNETCORE_HTTP_PORTS=8080 \
    ASPNETCORE_FORWARDEDHEADERS_ENABLED=true \
    DOTNET_RUNNING_IN_CONTAINER=true \
    DataProtection__KeysPath=/keys \
    DisableHttpsRedirection=true
COPY --from=build /app/publish .
# Writable locations for the non-root user: data-protection keys, candidate images, sheet scans.
RUN mkdir -p /keys /app/wwwroot/images/candidates /app/wwwroot/uploads/sheets \
    && chown -R $APP_UID /keys /app/wwwroot/images/candidates /app/wwwroot/uploads
USER $APP_UID
EXPOSE 8080
HEALTHCHECK --interval=30s --timeout=5s --start-period=30s --retries=3 \
    CMD ["dotnet", "VotingApp.dll", "--healthcheck"]
ENTRYPOINT ["dotnet", "VotingApp.dll"]
