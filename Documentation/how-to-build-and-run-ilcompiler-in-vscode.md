_Please ensure that [pre-requisites](prerequisites-for-building.md) are installed for a successful build of the repo._

_Note_:

* Instructions below assume `~/corert` is the repo root.

# Setting up #

Please make sure you have latest VS Code, C# extension, and .NET Core available. This guide is tested under C# 1.6.2 + VS Code 1.8.1 + CLI 1.0.0-preview4-004233.

This guide assumes that your VS code workspace is set to the root of the repo. 

# Enabling Building from VS Code #

Press ```SHIFT+COMMAND+B```. VS code will automatically ask you to configure the Task Runner. Choose 'Other', then change your tasks.json as follows:

* Change command to ```${workspaceRoot}/build.sh```
* Add ```"suppressTaskName" : true``` under the build Task. This is important to avoid always passing "build" task name as argument to build.sh. 

The full tasks.json is as follows:

```json
{
    // See https://go.microsoft.com/fwlink/?LinkId=733558
    // for the documentation about the tasks.json format
    "version": "0.1.0",
    "command": "${workspaceRoot}/build.sh",
    "isShellCommand": false,
    "args": [],
    "tasks": [
        {
            "taskName": "build",
            "args": [ ],
            "isBuildCommand": true,
            "showOutput": "silent",
            "suppressTaskName" : true,
            "problemMatcher": "$msCompile"
        }
    ]
}
```

# Enabling Debugging of ILC.exe #

## Preparing the environment ##

* Make sure you've done a build. You can either build from command line or from VS Code. 
* Go to ~/corert/bin/Product/OSX.x64.Debug/packaging/publish1/
* Copy ilc.exe to ilc.dll. This is needed to make .NET Core Debugger happy as it only recognize DLL targets.
* Create a ilc.runtimeconfig.json file as follows:

```json
{
  "runtimeOptions": {
    "framework": {
      "name": "Microsoft.NETCore.App",
      "version": "1.0.1"
    }
  }
}
```

Without the steps above, launching ILC from VS Code / .NET Core Debugger would simply fail with the following error message:

> WARNING: The target process exited without raising a CoreCLR started event. Ensure that the target process is configured to use Microsoft.NETCore.App 1.0.0 or newer. This may be expected if the target process did not run .NET code.

NOTE: It is important to never put a .deps.json file here. This makes sure dotnet/CLI always resolves .NET assemblies from the ILC folder, which is important for ILC since it has its own copies of framework that are tested to work with ILC.

## launch.json changes ##

Launch VS code from your corert repo folder, click the Debug button, and choose .NET Core as your environment.

VS code will ask you to change launch.json. Then make the following changes:

```json
            "preLaunchTask": "",
            "program": "${workspaceRoot}/bin/Product/<OS>.<Arch>.<Flavor>/packaging/publish1/ilc.dll",
            "args": ["@obj/Debug/netcoreapp1.0/native/<netcore_app_name>.ilc.rsp"],
            "cwd": "<netcore_app_root_folder>",
```

* preLaunchTask - this sets the prelaunch task to empty, and avoids invoking the build everytime when you debug (incremental build isn't quite there yet).
* program - This needs to be set to your ILC you built, and make sure to use the .DLL version (see above).
* args - This can be set to the .ilc.rsp file. Usually you can get it from your own .NET Core project by running:

```
dotnet build /t:LinkNative /t:Rebuild /v:Detailed | grep ".ilc.rsp"
```
* cwd - Set this to the root folder of the dotnet core project you are running ILC against

That's it. Once you've made the changes above, now you can debug ILC in VS Code .NET Core debugger just like any other dotnet core app. 