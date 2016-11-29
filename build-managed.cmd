@call %dp0buildvars-setup.cmd %*
@call %~dp0run.cmd build-managed
@exit /b %ERRORLEVEL%