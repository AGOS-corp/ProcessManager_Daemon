@echo off
tasklist | find "WatchDog_Background.exe" >nul
if not %ERRORLEVEL%==0 start "" "C:\WatchDog\WatchDog_Background.exe"


@echo off
tasklist | find "WatchDog.exe" >nul
if not %ERRORLEVEL%==0 (
    start "" "C:\WatchDog\WatchDog_Background.exe"
    echo WatchDog restarted at %date% %time% >> C:\WatchDog\restart_log.txt
)

