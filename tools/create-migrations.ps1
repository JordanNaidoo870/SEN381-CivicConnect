# Create the initial EF Core schema migration before first boot on a new database.
# Prerequisite: dotnet ef installed and connection string stored as a user secret.
$ErrorActionPreference = 'Stop'
dotnet tool install --global dotnet-ef --version '10.*'
dotnet ef migrations add InitialCreate --project src/CivicConnect.Infrastructure --startup-project src/CivicConnect.Web --context CivicConnectDbContext --output-dir Data/Migrations
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
dotnet ef database update --project src/CivicConnect.Infrastructure --startup-project src/CivicConnect.Web --context CivicConnectDbContext
