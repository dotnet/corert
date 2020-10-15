mkdir "%1" 2>nul
cd "%1"

git clone https://github.com/emscripten-core/emsdk.git

cd emsdk
rem checkout a known good version to avoid a random break when emscripten changes the top of tree.
git checkout def6e49

powershell -NoProfile -NoLogo -ExecutionPolicy ByPass -command "& """%~dp0update-machine-certs.ps1""" %*"

rem Use the python that is downloaded to native-tools explicitly as its not on the path
call "%1"\..\native-tools\bin\python3 emsdk.py install 2.0.3
if %errorlevel% NEQ 0 goto fail
call emsdk activate 2.0.3
if %errorlevel% NEQ 0 goto fail

exit /b 0

fail:
echo "Failed to install emscripten"
exit /b 1
