@echo off
setlocal

set AAPT=C:\Users\jinhao\AppData\Local\Temp\android_build\bt\android-14\aapt.exe
set BT=C:\Users\jinhao\AppData\Local\Temp\android_build\bt\android-14
set D8=%BT%\d8.bat
set ANDROID_JAR=C:\Users\jinhao\AppData\Local\Temp\android_build\plat\android-34\android.jar
set ZIPALIGN=%BT%\zipalign.exe
set SRC=z:\interest\ClassLibrary1\toast_apk
set OUT=%SRC%\build

rmdir /s /q %OUT% 2>nul
mkdir %OUT%
mkdir %OUT%\classes

echo [1/5] Compiling resources with aapt...
"%AAPT%" package -f -m -J %OUT% -M %SRC%\AndroidManifest.xml -S %SRC%\res -I "%ANDROID_JAR%"
if errorlevel 1 (echo FAILED: aapt package & exit /b 1)

echo [2/5] Compiling Java...
javac -source 1.8 -target 1.8 -classpath "%ANDROID_JAR%" -d %OUT%\classes %SRC%\src\com\emu\toast\ToastActivity.java %OUT%\com\emu\toast\R.java
if errorlevel 1 (echo FAILED: javac & exit /b 1)

echo [3/5] Converting to DEX...
call "%D8%" --output %OUT% --lib "%ANDROID_JAR%" %OUT%\classes\com\emu\toast\ToastActivity.class %OUT%\classes\com\emu\toast\R.class %OUT%\classes\com\emu\toast\R$layout.class %OUT%\classes\com\emu\toast\R$id.class
if errorlevel 1 (echo FAILED: d8 & exit /b 1)

echo [4/5] Building APK...
"%AAPT%" package -f -M %SRC%\AndroidManifest.xml -S %SRC%\res -I "%ANDROID_JAR%" -F %OUT%\toast-unsigned.apk
if errorlevel 1 (echo FAILED: aapt apk & exit /b 1)
cd %OUT%
mkdir apk_tmp
cd apk_tmp
jar xf %OUT%\toast-unsigned.apk
copy /y %OUT%\classes.dex . >nul
jar cf %OUT%\toast-unsigned2.apk *
cd %OUT%
rmdir /s /q apk_tmp

echo [5/5] Signing APK...
keytool -genkey -v -keystore %OUT%\debug.keystore -storepass android -alias androiddebugkey -keypass android -keyalg RSA -keysize 2048 -validity 10000 -dname "CN=Debug" 2>nul
"%ZIPALIGN%" -f 4 %OUT%\toast-unsigned2.apk %OUT%\toast-aligned.apk
jarsigner -verbose -sigalg SHA256withRSA -digestalg SHA-256 -keystore %OUT%\debug.keystore -storepass android -keypass android %OUT%\toast-aligned.apk androiddebugkey >nul 2>&1
copy /y %OUT%\toast-aligned.apk %OUT%\toast.apk >nul

echo.
echo Build complete: %OUT%\toast.apk
