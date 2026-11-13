@echo off
chcp 65001 >nul
rem ZETX FORMATTER - kaynaktan derleme (Windows'ta hazir gelen .NET Framework 4.x derleyicisi ile)
set CSC=%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe
if not exist "%CSC%" set CSC=%WINDIR%\Microsoft.NET\Framework\v4.0.30319\csc.exe
if not exist "%CSC%" (echo .NET Framework derleyicisi bulunamadi. & pause & exit /b 1)
"%CSC%" /nologo /target:winexe /platform:anycpu /optimize+ /codepage:65001 /out:"ZETX-FORMATTER.exe" /win32icon:kaynak\icon.ico /r:System.dll /r:System.Core.dll /r:System.Drawing.dll /r:System.Windows.Forms.dll /r:System.Management.dll kaynak\*.cs
if errorlevel 1 (echo Derleme basarisiz. & pause & exit /b 1)
echo Derlendi: ZETX-FORMATTER.exe
pause
