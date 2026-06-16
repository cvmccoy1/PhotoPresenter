@echo off
echo Copying PhotoPresenter files...
echo Source:      \\Tuf-pc\p\PhotoPresenter
echo Destination: C:\Program Files\PhotoPresenter
echo.

robocopy "\\Tuf-pc\p\PhotoPresenter" "C:\Program Files\PhotoPresenter" /E /IS /NFL /NDL

echo.
if errorlevel 8 (
    echo ERROR: Copy failed. Exit code %ERRORLEVEL%.
) else (
    echo Copy complete.
)
echo.
pause
