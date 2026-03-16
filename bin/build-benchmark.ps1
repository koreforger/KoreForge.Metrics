[CmdletBinding()]
param()

Push-Location (Resolve-Path "$PSScriptRoot\..")
try {
    dotnet run --project tst/KF.Metrics.Benchmarks/KF.Metrics.Benchmarks.csproj -c Release
} finally {
    Pop-Location
}