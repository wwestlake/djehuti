@echo off
REM Dev launcher for Djehuti.DjeLab (Blazor WebAssembly) preview.
cd /d "%~dp0"
dotnet run --urls http://localhost:5183
