# Run from repository root after installing .NET 10 SDK and PostgreSQL 18.
$ErrorActionPreference = 'Stop'
dotnet restore .\CivicConnect.sln
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
dotnet build .\CivicConnect.sln --no-restore
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
dotnet run --project .\src\CivicConnect.Web\CivicConnect.Web.csproj --launch-profile https
