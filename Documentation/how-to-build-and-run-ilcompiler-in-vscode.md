_Please ensure that [pre-requisites](prerequisites-for-building.md) are installed for a successful build of the repo._

_Note_:

* Instructions below assume ```~/corert``` is the repo root.

# Setting up #

Please make sure you have latest VS Code, C# extension, and .NET Core available.

This guide assumes that your VS code workspace is set to the root of the repo. 

# Running VS Code

We've checked-in reasonable default ```launch.json``` and ```tasks.json``` under ```corert/.vscode``` directory. You only need to run vscode form corert root:

```
code ~/corert
```

And then press SHIFT+COMMAND+B to start the build.

# Debugging ILC.exe using .NET Core Debugger #

Go to the debug pane and click Debug, choose .NET Core as the environment. If needed, you can change program property in launch.json (the gear button) to point to a different flavor of ilc:

```json
            "windows": {
                "program": "${workspaceRoot}/bin/Windows_NT.x64.Debug/tools/ilc.dll"
            },
            "osx": {
                "program": "${workspaceRoot}/bin/OSX.x64.Debug/tools/ilc.dll"
            },
            "linux": {
                "program": "${workspaceRoot}/bin/Linux.x64.Debug/tools/ilc.dll"
            },
```

By default we've disabled automatic build before debug. If you want to change that, you can change the ```preLaunchTask``` property to ```"build"```. But this is not currently recommended.

# Getting ILC response files

A ```.ilc.rsp``` file path can be easily obtained from a .NET core project that you want to debug by following command:

```
dotnet build /t:LinkNative /t:Rebuild /v:Detailed | grep ".ilc.rsp"
```

Once you have the ilc path, you can change ```launch.json``` accordingly:

```json
            "args": ["@obj/Debug/netcoreapp2.1/native/<netcore_app_name>.ilc.rsp"],
            "cwd": "<netcore_app_root_folder>",
```

* ```args``` - the argument to ILC
* ```cwd``` - the current directory where ILC is running. You can set it to the .NET Core project root. 
