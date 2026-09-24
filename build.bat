@echo off
rem Builds a single self-contained EXE into .\publish (no .NET needed on the target PC)
where dotnet >nul 2>nul || (echo .NET 8 SDK not found: https://dotnet.microsoft.com/download/dotnet/8.0 & pause & exit /b 1)
dotnet publish "%~dp0PersonalExpenseTracker.csproj" -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:EnableCompressionInSingleFile=true -o "%~dp0publish" || (pause & exit /b 1)
echo.
echo Done: %~dp0publish\PersonalExpenseTracker.exe
pause
