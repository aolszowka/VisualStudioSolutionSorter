@ECHO OFF

SET PACKAGE_NAME=visualstudiosolutionsorter
SET BUILD_CONFIGURATION=Debug

pushd ..

REM Clear any packages out of the local nuget package cache
REM This is because `dotnet tool install --no-cache` appears to be broken?
SET PACKAGE_CACHE_FOLDER=%userprofile%\.nuget\packages\%PACKAGE_NAME%
ECHO Attempting to Clean Existing Package Cache From %PACKAGE_CACHE_FOLDER%
IF EXIST "%PACKAGE_CACHE_FOLDER%" (
    RMDIR /Q /S "%PACKAGE_CACHE_FOLDER%"
)

ECHO.
ECHO Delete Existing Packs
IF EXIST nupkg (
    RMDIR /q /s nupkg
)

ECHO.
ECHO Build
dotnet build -graphBuild -maxCpuCount --configuration %BUILD_CONFIGURATION%

ECHO.
ECHO Test
dotnet test --configuration %BUILD_CONFIGURATION%

IF %ERRORLEVEL% NEQ 0 (
    ECHO.
    ECHO Cannot build a package on error; Fix the tests.
    GOTO :Finally
)

ECHO.
ECHO Packing
dotnet pack --configuration %BUILD_CONFIGURATION% --version-suffix "dev"

IF %ERRORLEVEL% NEQ 0 (
    ECHO.
    ECHO Cannot deploy on package On error; Fix the build.
    GOTO :Finally
)

ECHO.
ECHO Uninstall Existing Tooling
dotnet tool uninstall %PACKAGE_NAME%

ECHO.
ECHO Install the Latest Prerelease
dotnet tool install --add-source=nupkg --no-cache %PACKAGE_NAME% --version=*-*

:Finally
popd
EXIT /B
