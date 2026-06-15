@echo off
echo Building Gorilla Shiny Rox...
dotnet publish GorillaShinyRox.csproj -c Release -r win-x64 --self-contained false /p:PublishSingleFile=true
echo.
echo Done.
echo Your EXE should be here:
echo bin\Release\net8.0-windows\win-x64\publish\GorillaShinyRox.exe
pause
