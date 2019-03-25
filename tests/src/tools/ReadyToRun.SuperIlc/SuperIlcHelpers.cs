using System;
using System.Collections.Generic;
using System.Text;

namespace ReadyToRun.SuperIlc
{
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
