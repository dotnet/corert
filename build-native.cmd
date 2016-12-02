@call %dp0buildvars-setup.cmd %*
@call %~dp0run.cmd build-managed %cleanBuild% -Platform=%__BuildArch% -Configuration=%__BuildType% 
@exit /b %ERRORLEVEL%