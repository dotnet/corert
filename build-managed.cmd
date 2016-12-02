@call %dp0buildvars-setup.cmd %*
@call %~dp0run.cmd build-managed %cleanBuild% -RelativeProductBinDir=%__RelativeProductBinDir% -NuPkgRid=win7-x64 -RepoPath=%__ProjectDir%
@exit /b %ERRORLEVEL%