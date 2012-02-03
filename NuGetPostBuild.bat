REM Calls NuGet
echo on
CALL "%~1..\NuGet.exe" pack "%~2"  -OutputDirectory "%~1.."
