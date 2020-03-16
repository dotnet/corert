mkdir "%1" 2>nul
cd "%1"

git clone https://github.com/emscripten-core/emsdk.git

cd emsdk

powershell -NoProfile -NoLogo -ExecutionPolicy ByPass -command "& """%~dp0update-machine-certs.ps1""" %*"

rem call "%1"\..\native-tools\bin\python3\3.7.1\python3 emsdk.py install 1.39.8
call emsdk install 1.39.8
if %errorlevel% NEQ 0 goto fail
call emsdk activate 1.39.8
if %errorlevel% NEQ 0 goto fail

exit /b 0

fail:
echo "Failed to install emscripten"
exit /b 1
