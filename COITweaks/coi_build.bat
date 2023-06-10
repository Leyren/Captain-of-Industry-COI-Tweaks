SET OLDDIR=%CD%
dotnet build --configuration Release
cd bin/Release/net46
xcopy COITweaks.dll "%APPDATA%\Captain of Industry\Mods\COITweaks\" /v /y
cd %COI_ROOT%
startgame REM this is a script to start the game from CLI
cd %OLDDIR%