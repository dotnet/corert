@call %dp0buildvars-setup.cmd %*
@call %~dp0run.cmd build-managed -buildArch %__BuildArch% %cleanBuild% -Configuration %__BuildType% -Platform %__BuildArch%
@exit /b %ERRORLEVEL%