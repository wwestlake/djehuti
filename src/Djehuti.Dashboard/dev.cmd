@echo off
REM Dev launcher for Djehuti.Dashboard preview. Adds local Node 22 to PATH.
set "PATH=C:\Users\wwestlake\AppData\Local\node22\node-v22.20.0-win-x64;%PATH%"
cd /d "%~dp0"
npm run dev -- --port 5182 --strictPort
