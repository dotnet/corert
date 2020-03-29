mkdir "%1" 2>nul
cd "%1"

git clone https://github.com/emscripten-core/emsdk.git

cd emsdk

powershell -NoProfile -NoLogo -ExecutionPolicy ByPass -command "& """%~dp0update-machine-certs.ps1""" %*"

rem Use the python that is downloaded to native-tools explicitly as its not on the path
call "%1"\..\native-tools\bin\python3 emsdk.py install 1.39.8
if %errorlevel% NEQ 0 goto fail
call emsdk activate 1.39.8
if %errorlevel% NEQ 0 goto fail
 
exit /b 0

fail:
echo "Failed to install emscripten"
exit /b 1
