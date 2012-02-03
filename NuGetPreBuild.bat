REM Calls NuGet
CALL "%~1..\NuGet.exe" install "%~1packages.config" -o "%~1..\Packages"