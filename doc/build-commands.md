# Build & Test Commands

Copy/paste-ready commands for recurring workflows. Run everything from the repo root unless stated otherwise.

## Restore Dependencies

```powershell
dotnet restore
```

## Build

| Scenario | Command |
| --- | --- |
| Build every project (Debug) | `dotnet build` |
| Build Release binaries | `dotnet build -c Release` |
| Build only the core library | `dotnet build src/KF.Metrics/KF.Metrics.csproj` |
| Build ASP.NET integration | `dotnet build src/KF.Metrics.AspNet/KF.Metrics.AspNet.csproj` |
| Publish libraries for deployment | `dotnet publish -c Release src/KF.Metrics/KF.Metrics.csproj` |
| Build benchmarks (Release) | `dotnet build -c Release tst/KF.Metrics.Benchmarks/KF.Metrics.Benchmarks.csproj` |

Front-end assets are not applicable today. If a UI is introduced later, document its npm/yarn build commands here.

## Tests & Coverage

| Task | Command |
| --- | --- |
| Run all xUnit tests | `dotnet test` |
| Run tests with verbose logs | `dotnet test -v n` |
| Collect code coverage (Cobertura XML) | `dotnet test --collect:"XPlat Code Coverage" --results-directory TestResults` |
| Generate HTML coverage after collecting | `dotnet tool run reportgenerator -reports:"TestResults/<run-id>/coverage.cobertura.xml" -targetdir:"TestResults/coverage-html" -reporttypes:Html` |
| Open coverage report (after generation) | `Start-Process TestResults/coverage-html/index.html` |
| Run tests + coverage + auto-open report | `.\scr\build-test-codecoverage.ps1` |

## Cleaning Artifacts

| Task | Command |
| --- | --- |
| Dotnet clean (all configurations) | `dotnet clean` |
| Remove all `bin`/`obj`/`TestResults` folders | `.\scr\build-clean.ps1` |
| Manually delete global TestResults | `Remove-Item TestResults -Recurse -Force` |

## Benchmarks

| Scenario | Command |
| --- | --- |
| Run benchmark suite (Release) | `dotnet run -c Release --project tst/KF.Metrics.Benchmarks` |
| Collect BenchmarkDotNet artifacts | Check `tst/KF.Metrics.Benchmarks/BenchmarkDotNet.Artifacts` after running |

## Diagnostics & Utilities

| Task | Command |
| --- | --- |
| List optional dotnet tools (reportgenerator) | `dotnet tool list` |
| Update local reportgenerator tool | `dotnet tool update dotnet-reportgenerator-globaltool` |
| Launch coverage HTML in default browser | `Start-Process .\TestResults\coverage-html\index.html` |
| Inspect previous coverage runs | `Get-ChildItem TestResults -Directory` |

## Suggested Workflow Snippets

1. **Fresh build from scratch**

	```powershell
	.\scr\build-clean.ps1
	dotnet restore
	dotnet build -c Release
	```

2. **Run tests and view coverage**

	```powershell
	.\scr\build-test-codecoverage.ps1
	```

3. **Publish libraries (Release)**

	```powershell
	dotnet publish -c Release src/KF.Metrics/KF.Metrics.csproj
	dotnet publish -c Release src/KF.Metrics.AspNet/KF.Metrics.AspNet.csproj
	```

Add future commands (front-end builds, container packaging, etc.) to this file as new components land.
