@echo off
setlocal enabledelayedexpansion


set WORKSPACE=..
set LUBAN_DLL=%WORKSPACE%\MiniTemplate\Luban\Luban.dll
set CONF_ROOT=.
:: 最终生成目录
set DEST_DIR=../Assets/StreamingAssets

set TEMP_LUBAN=../Temp_LubanOnly


:: 准备临时目录
if exist "%TEMP_LUBAN%" rmdir /s /q "%TEMP_LUBAN%"
mkdir "%TEMP_LUBAN%"

::  让 Luban 在临时目录里生成
echo [Info] 正在 Luban 生成...
dotnet %LUBAN_DLL% ^
    -t all ^
    -d json ^
    --conf %CONF_ROOT%\luban.conf ^
    -x outputDataDir=%TEMP_LUBAN%

:: 把 JSON 复制过去
echo [Info] 正在同步 JSON 文件...
xcopy /y "%TEMP_LUBAN%\*.json" "%DEST_DIR%\" >nul

:: 清理临时文件夹
rmdir /s /q "%TEMP_LUBAN%"

echo [Success] 完成！JSON 已更新
pause