@echo off
setlocal
set "AROPIX_MSBUILD=%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\MSBuild.exe"
if not exist "%AROPIX_MSBUILD%" set "AROPIX_MSBUILD=%WINDIR%\Microsoft.NET\Framework\v4.0.30319\MSBuild.exe"
if not exist "%AROPIX_MSBUILD%" (
  echo No se encontro MSBuild de .NET Framework 4.x.
  exit /b 1
)
"%AROPIX_MSBUILD%" "%~dp0..\Aropix.csproj" /nologo /verbosity:minimal /p:Configuration=Release %*
exit /b %errorlevel%