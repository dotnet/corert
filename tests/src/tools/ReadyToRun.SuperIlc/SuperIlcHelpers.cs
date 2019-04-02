using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace ReadyToRun.SuperIlc
{
    public class BuildOptions
    {
        public DirectoryInfo InputDirectory { get; set; }
        public DirectoryInfo OutputDirectory { get; set; }
        public DirectoryInfo CrossgenDirectory { get; set; }
        public DirectoryInfo CpaotDirectory { get; set; }
        public bool NoJit { get; set; }
        public bool NoExe { get; set; }
        public bool NoEtw { get; set; }
        public bool NoCleanup { get; set; }
        public DirectoryInfo[] ReferencePath { get; set; }

        public IEnumerable<string> ReferencePaths()
        {
            return ReferencePath?.Select(x => x.ToString()) ?? Enumerable.Empty<string>();
        }

        public IEnumerable<CompilerRunner> CompilerRunners()
        {
            List<CompilerRunner> runners = new List<CompilerRunner>();
            List<string> referencePaths = ReferencePath?.Select(x => x.ToString())?.ToList();

            if (CpaotDirectory != null)
            {
                runners.Add(new CpaotRunner(CpaotDirectory.FullName, referencePaths));
            }

            if (CrossgenDirectory != null)
            {
                runners.Add(new CrossgenRunner(CrossgenDirectory.FullName, referencePaths));
            }

            if (!NoJit)
            {
                runners.Add(new JitRunner(referencePaths));
            }

            return runners;
        }
    }

    public static class SuperIlcHelpers
    {
        public static string FindCoreRun(IEnumerable<string> referencePaths)
        {
            string coreRunPath = "CoreRun.exe".FindFile(referencePaths);
            if (coreRunPath == null)
            {
                Console.Error.WriteLine("CoreRun.exe not found in reference folders, execution won't run");
            }
            return coreRunPath;
        }
    }
}
