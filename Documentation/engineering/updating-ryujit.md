# Updating RyuJIT

Following steps are necessary to pick up a new version of RyuJIT code generation backend from CoreCLR:

1. From the master branch of the https://github.com/dotnet/runtime/ repo, copy header files that are part of the contract with RyuJIT from `src\coreclr\src\inc` on the CoreCLR side, to `src\JitInterface\src\ThunkGenerator` on the CoreRT side.
2. Inspect the diffs
    1. If an enum was modified, port the change to the managed version of the enum manually.
    2. If a JitInterface method was added or changed, update `src\JitInterface\src\ThunkGenerator\ThunkInput.txt` and run the generation script next to the file to regenerate `CorInfoBase.cs` and `jitinterface.h`. Update the managed implementation of the method in `CorInfoImpl.cs` manually.
    3. If the JitInterface GUID was updated (`JITEEVersionIdentifier` in `corinfo.h`), update it in `src\Native\jitinterface\jitwrapper.cpp`
3. Determine the latest Microsoft.NETCore.App.Runtime package to use
    1. Run `dotnet restore Microsoft.NETCore.App.Runtime.csproj` in this directory
    2. The restore operation is going to fail, but the error message will include the latest Microsoft.NETCore.App.Runtime package version
    3. Update the version number in dependencies.proprs at the root of the repo.
4. Rebuild everything and run tests to validate the change.
5. Create a pull request with title "Update RyuJIT".
