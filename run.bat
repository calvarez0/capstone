@echo off
REM Run with .NET 10.0 by default, or specify framework as argument
REM Usage: run.bat [framework]
REM Example: run.bat net8.0

if "%1"=="" (
    dotnet run --framework net10.0
) else (
    dotnet run --framework %1
)
