@echo off
chcp 65001 > nul
echo ========================================
echo 条形码识别工具 - Release 打包脚本
echo ========================================
echo.

REM 设置版本号
set VERSION=1.0.0
set OUTPUT_DIR=publish\BarcodeRenamer2_v%VERSION%

echo [1/4] 清理旧的发布文件...
if exist publish rmdir /s /q publish
mkdir %OUTPUT_DIR%

echo [2/4] 开始编译 Release 版本...
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true

if %ERRORLEVEL% NEQ 0 (
    echo.
    echo ❌ 编译失败！
    pause
    exit /b 1
)

echo [3/4] 复制发布文件...
xcopy /y /q bin\Release\net6.0-windows\win-x64\publish\*.* %OUTPUT_DIR%\

echo [4/4] 创建压缩包...
cd publish
powershell -Command "Compress-Archive -Path 'BarcodeRenamer2_v%VERSION%' -DestinationPath 'BarcodeRenamer2_v%VERSION%.zip' -Force"
cd ..

echo.
echo ========================================
echo ✅ 打包完成！
echo ========================================
echo.
echo 发布文件位置: %OUTPUT_DIR%
echo 压缩包位置: publish\BarcodeRenamer2_v%VERSION%.zip
echo.
echo 文件列表:
dir /b %OUTPUT_DIR%
echo.
pause
