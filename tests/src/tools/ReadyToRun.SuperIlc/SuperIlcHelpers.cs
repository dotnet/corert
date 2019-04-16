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
        public DirectoryInfo CoreRootDirectory { get; set; }
        public DirectoryInfo CpaotDirectory { get; set; }
        public bool Crossgen { get; set; }
        public bool NoJit { get; set; }
        public bool NoExe { get; set; }
        public bool NoEtw { get; set; }
        public bool NoCleanup { get; set; }
        public bool Sequential { get; set; }
        public DirectoryInfo[] ReferencePath { get; set; }

        public IEnumerable<string> ReferencePaths()
        {
            if (CoreRootDirectory != null)
            {
                yield return CoreRootDirectory.FullName;
            }
            if (ReferencePath != null)
            {
                foreach (DirectoryInfo referencePath in ReferencePath)
                {
                    yield return referencePath.FullName;
                }
            }
        }

        public IEnumerable<CompilerRunner> CompilerRunners()
        {
            List<CompilerRunner> runners = new List<CompilerRunner>();
            List<string> referencePaths = ReferencePaths().ToList();

            if (CpaotDirectory != null)
            {
                runners.Add(new CpaotRunner(CpaotDirectory.FullName, referencePaths));
            }

            if (Crossgen)
            {
                if (CoreRootDirectory == null)
                {
                    throw new Exception("-coreroot folder not specified, cannot use Crossgen runner");
                }
                runners.Add(new CrossgenRunner(CoreRootDirectory.FullName, referencePaths));
            }

            if (!NoJit)
            {
                runners.Add(new JitRunner(referencePaths));
            }

            return runners;
        }

        public string CoreRunPath()
        {
            string coreRunPath = "CoreRun.exe".FindFile(ReferencePaths());
            if (coreRunPath == null)
            {
                Console.Error.WriteLine("CoreRun.exe not found in reference folders, explicit exe launches won't work");
            }
            return coreRunPath;
        }
    }
}
