using System;
using System.Collections.Generic;
using System.IO;
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
        public DirectoryInfo[] ReferencePath { get; set; }
    }

    public static class SuperIlcHelpers
    {
        public static IEnumerable<CompilerRunner> CompilerRunners(
            string inputDirectory, 
            string outputDirectory, 
            string cpaotDirectory, 
            string crossgenDirectory, 
            bool noJit,
            IEnumerable<string> referencePaths)
        {
            List<CompilerRunner> runners = new List<CompilerRunner>();

            if (cpaotDirectory != null)
            {
                runners.Add(new CpaotRunner(cpaotDirectory, referencePaths));
            }

            if (crossgenDirectory != null)
            {
                runners.Add(new CrossgenRunner(crossgenDirectory, referencePaths));
            }

            if (!noJit)
            {
                runners.Add(new JitRunner(referencePaths));
            }

            return runners;
        }

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
