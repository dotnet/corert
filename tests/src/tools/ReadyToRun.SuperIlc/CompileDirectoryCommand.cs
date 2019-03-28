// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace ReadyToRun.SuperIlc
{
    class CompileDirectoryCommand
    {
        public static int CompileDirectory(BuildOptions options)
        {
            if (options.InputDirectory == null)
            {
                Console.Error.WriteLine("--input-directory is a required argument.");
                return 1;
            }

            if (options.OutputDirectory == null)
            {
                options.OutputDirectory = options.InputDirectory;
            }

            if (options.OutputDirectory.IsParentOf(options.InputDirectory))
            {
                Console.Error.WriteLine("Error: Input and output folders must be distinct, and the output directory (which gets deleted) better not be a parent of the input directory.");
                return 1;
            }

            List<string> referencePaths = options.ReferencePath?.Select(x => x.ToString())?.ToList();
            string coreRunPath = SuperIlcHelpers.FindCoreRun(referencePaths);

            IEnumerable<CompilerRunner> runners = SuperIlcHelpers.CompilerRunners(
                options.InputDirectory.ToString(), options.OutputDirectory.ToString(), options.CpaotDirectory.ToString(), options.CrossgenDirectory.ToString(), options.NoJit, referencePaths);

            PathExtensions.DeleteOutputFolders(options.InputDirectory.FullName, recursive: false);

            Application application = Application.FromDirectory(options.InputDirectory.FullName, runners, options.OutputDirectory.FullName, options.NoExe, options.NoEtw, coreRunPath);
            if (application == null)
            {
                Console.Error.WriteLine($"No managed app found in {options.InputDirectory.FullName}");
            }

            string timeStamp = DateTime.Now.ToString("MMDD-hhmm");
            string applicationSetLogPath = Path.Combine(options.InputDirectory.ToString(), "directory-" + timeStamp + ".log");

            using (ApplicationSet applicationSet = new ApplicationSet(new Application[] { application }, runners, coreRunPath, applicationSetLogPath))
            {
                return applicationSet.Build(coreRunPath, runners, applicationSetLogPath) ? 0 : 1;
            }
        }
    }    
}
