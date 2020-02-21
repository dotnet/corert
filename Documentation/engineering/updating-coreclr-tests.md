# Updating CoreCLR Tests Zip

The set of CoreCLR tests run as part of CoreRT's CI and available via `tests\runtest.cmd /coreclr` download are downloaded as a zip file from the CoreCLR build. We use a specific build number to ensure we're running against a set of tests known to be compatible with CoreRT. Rolling forward to a new set of tests involves these steps:

1. Find a known good tests.zip from the CoreCLR build
   1. Go to https://github.com/dotnet/runtime/pulls and open the most-recently passing PR (it should have a green check mark next to it)
   2. In the CI checks, open the details for `Windows_NT x64 Debug Build and Test`
   3. Navigate through `Build Artifacts` -> `bin` -> `tests`
   4. Copy the URL to `tests.zip` (**TODO: Zip archive no longer built, has to be retooled at https://github.com/dotnet/runtime side**)
2. Retain the CI build so Jenkins doesn't delete `tests.zip`
   1. In the PR job page (where you clicked `Build Artifacts` earlier) ensure you're logged in to Jenkins
   2. Click the `Keep this build forever` button at the top-right
3. Paste the `tests.zip` URL into `CoreCLRTestsURL.txt`
4. Check your work by building and then running `tests\runtest.cmd /coreclr`
