echo starting
echo "%1"
echo "%2"
echo "%3"

call "%1"

set PATH=$(Build.SourcesDirectory)\native-tools\bin;%PATH%

call "%2" wasm "%3"
