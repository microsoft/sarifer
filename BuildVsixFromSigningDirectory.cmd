::Build NuGet packages step
@ECHO off
SETLOCAL

set BinaryOutputDirectory=%1
set Configuration=%1
set Platform=%2

if "%BinaryOutputDirectory%" EQU "" (
set BinaryOutputDirectory=.\bld\bin\
)

if "%Configuration%" EQU "" (
set Configuration=Release
)

if "%Platform%" EQU "" (
set Platform=AnyCpu
)

set ArchiveDirectory=%BinaryOutputDirectory%\Expanded
set SigningDirectory=%BinaryOutputDirectory%\Signing
set BinaryOutputDirectory=%BinaryOutputDirectory%\%Platform%_%Configuration%

:: Copy sarifer dlls to net472
if exist %ArchiveDirectory% (rd /s /q %ArchiveDirectory%)
md %ArchiveDirectory%
md %ArchiveDirectory%\2019
md %ArchiveDirectory%\2022

powershell -File .\scripts\Unzip.ps1 %BinaryOutputDirectory%\Sarif.Sarifer\Microsoft.Sarif.Sarifer.vsix %ArchiveDirectory%\2019
powershell -File .\scripts\Unzip.ps1 %BinaryOutputDirectory%\Sarif.Sarifer.2022\Microsoft.Sarif.Sarifer.vsix %ArchiveDirectory%\2022
xcopy /Y %SigningDirectory%\net472\2019\*.dll %ArchiveDirectory%\2019
xcopy /Y %SigningDirectory%\net472\2022\*.dll %ArchiveDirectory%\2022
del /Q %BinaryOutputDirectory%\Sarif.Sarifer\Microsoft.Sarif.Sarifer.vsix
del /Q %BinaryOutputDirectory%\Sarif.Sarifer.2022\Microsoft.Sarif.Sarifer.vsix

:: This command will only output to a file with a .zip extension
powershell Compress-Archive -Path %ArchiveDirectory%\2019\* -CompressionLevel Fastest -DestinationPath %SigningDirectory%\net472\2019\Microsoft.Sarif.Sarifer.zip
powershell Compress-Archive -Path %ArchiveDirectory%\2022\* -CompressionLevel Fastest -DestinationPath %SigningDirectory%\net472\2022\Microsoft.Sarif.Sarifer.zip
pushd %SigningDirectory%\net472\2019
rename Microsoft.Sarif.Sarifer.zip Microsoft.Sarif.Sarifer.vsix
popd
pushd %SigningDirectory%\net472\2022
rename Microsoft.Sarif.Sarifer.zip Microsoft.Sarif.Sarifer.vsix
popd

goto :Exit

:ExitFailed
@echo.
@echo Build NuGet packages from layout directory step failed.
exit /b 1

:Exit