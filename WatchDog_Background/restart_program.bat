@echo off

REM WatchDog_Background.exe 실행 여부 확인
tasklist | findstr /i "WatchDog_Background.exe" >nul
if %ERRORLEVEL% neq 0 (
    REM 실행 중이 아니면 프로세스 시작 및 로그 남기기
    start "" "C:\WatchDog\WatchDog_Background.exe"
    echo WatchDog restarted at %date% %time% >> C:\WatchDog\restart_log.txt
)
