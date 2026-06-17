@echo off

:: Check for admin rights using fsutil (more reliable than net session)
fsutil dirty query %SystemDrive% >nul 2>&1
if %ERRORLEVEL% equ 0 goto :main

:: Not elevated — write commands to a local temp batch and run it elevated.
:: Running the temp file (on C:\) avoids the mapped-drive elevation problem.
set "TMPBAT=%TEMP%\photo_install_run.bat"

echo @echo off                                                              > "%TMPBAT%"
echo echo Copying PhotoPresenter files...                                  >> "%TMPBAT%"
echo echo Source:      \\Tuf-pc\p\PhotoPresenter                           >> "%TMPBAT%"
echo echo Destination: C:\Program Files\PhotoPresenter                     >> "%TMPBAT%"
echo echo.                                                                  >> "%TMPBAT%"
echo robocopy "\\Tuf-pc\p\PhotoPresenter" "C:\Program Files\PhotoPresenter" /E /IS /NFL /NDL >> "%TMPBAT%"
echo echo.                                                                  >> "%TMPBAT%"
echo if errorlevel 8 echo ERROR: Copy failed. Exit code %%ERRORLEVEL%%.    >> "%TMPBAT%"
echo if not errorlevel 8 echo Copy complete.                               >> "%TMPBAT%"
echo echo.                                                                  >> "%TMPBAT%"
echo pause                                                                  >> "%TMPBAT%"

echo Set sh = CreateObject("Shell.Application")    > "%TEMP%\elevate.vbs"
echo sh.ShellExecute "%TMPBAT%", "", "", "runas", 1 >> "%TEMP%\elevate.vbs"
cscript //nologo "%TEMP%\elevate.vbs"
del "%TEMP%\elevate.vbs" >nul 2>&1
exit /b

:main
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
