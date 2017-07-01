# Updating RyuJIT

Following steps are necessary to pick up a new version of RyuJIT code generation backend from CoreCLR:

1. From the master branch of the CoreCLR repo, copy header files that are part of the contract with RyuJIT from `src\inc` on the CoreCLR side, to `src\JitInterface\src\ThunkGenerator` on the CoreRT side.
2. Inspect the diffs
    1. If an enum was modified, port the change to the managed version of the enum manually.
    2. If a JitInterface method was added or changed, update `src\JitInterface\src\ThunkGenerator\ThunkInput.txt` and run the generation script next to the file to regenerate `CorInfoBase.cs` and `jitinterface.h`. Update the managed implementation of the method in `CorInfoImpl.cs` manually.
    3. If the JitInterface GUID was updated, update it in `src\Native\jitinterface\jitwrapper.cpp`
3. Go to https://dotnet.myget.org/feed/dotnet-core/package/nuget/Microsoft.NETCore.Jit and find the latest version of the RyuJIT package. Update the version number in dependencies.props at the root of the repo.
4. Rebuild everything and run tests to validate the change.
5. Create a pull request with title "Update RyuJIT".
