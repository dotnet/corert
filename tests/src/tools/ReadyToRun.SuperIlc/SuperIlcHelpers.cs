// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml.Linq;
using System.Xml.XPath;

using Microsoft.Build.Construction;
using Microsoft.Build.Evaluation;

namespace ReadyToRun.SuperIlc
{
    /// <summary>
    /// This class represents a single test exclusion read from the issues.targets file.
    /// </summary>
    public class TestExclusion
    {
        /// <summary>
        /// Path components (the individual directory levels read from the issues.targets file).
        /// </summary>
        public readonly string[] PathComponents;

        /// <summary>
        /// True when an issues.targets exclusion spec ends with an '**'.
        /// </summary>
        public readonly bool OpenEnd;

        /// <summary>
        /// Issue ID for the exclusion.
        /// </summary>
        public readonly string IssueID;

        /// <summary>
        /// Initialize a test exclusion record read from the issues.targets file.
        /// </summary>
        /// <param name="pathComponents">Path components for this test exclusion</param>
        /// <param name="openEnd">True when the entry ends with '**'</param>
        /// <param name="issueID">ID of the exclusion issue</param>
        public TestExclusion(string[] pathComponents, bool openEnd, string issueID)
        {
            PathComponents = pathComponents;
            OpenEnd = openEnd;
            IssueID = issueID;
        }

        /// <summary>
        /// Check whether the test exclusion entry matches a particular test folder / name.
        /// </summary>
        /// <param name="pathComponents">Components (directory levels) representing the test path</param>
        /// <param name="firstComponent">Index of first element in pathComponents to analyze</param>
        /// <returns></returns>
        public bool Matches(string[] pathComponents, int firstComponent)
        {
            if (pathComponents[firstComponent].Equals(PathComponents[0], StringComparison.OrdinalIgnoreCase) &&
                pathComponents.Length >= firstComponent + PathComponents.Length &&
                (OpenEnd || pathComponents.Length == firstComponent + PathComponents.Length))
            {
                for (int matchIndex = 1; matchIndex < PathComponents.Length; matchIndex++)
                {
                    if (!pathComponents[firstComponent + matchIndex].Equals(PathComponents[matchIndex], StringComparison.OrdinalIgnoreCase))
                    {
                        return false;
                    }
                }
                return true;
            }
            return false;
        }
    }

    /// <summary>
    /// Map of test exclusions with search acceleration.
    /// </summary>
    public class TestExclusionMap
    {
        public readonly Dictionary<string, List<TestExclusion>> _folderToExclusions;

        public TestExclusionMap(IEnumerable<TestExclusion> exclusions)
        {
            _folderToExclusions = new Dictionary<string, List<TestExclusion>>(StringComparer.OrdinalIgnoreCase);
            foreach (TestExclusion exclusion in exclusions)
            {
                if (!_folderToExclusions.TryGetValue(exclusion.PathComponents[0], out List<TestExclusion> exclusionsPerFolder))
                {
                    exclusionsPerFolder = new List<TestExclusion>();
                    _folderToExclusions.Add(exclusion.PathComponents[0], exclusionsPerFolder);
                }
                exclusionsPerFolder.Add(exclusion);
            }
        }

        /// <summary>
        /// Locate the issue ID for a given test path if it exists; return false when not.
        /// </summary>
        /// <param name="pathComponents">Path components representing the test path to check</param>
        /// <param name="issueID">Output issue ID when found, null otherwise</param>
        /// <returns>True when the test was found in the exclusion list, false otherwise</returns>
        public bool TryGetIssue(string[] pathComponents, out string issueID)
        {
            for (int firstComponent = 0; firstComponent < pathComponents.Length; firstComponent++)
            {
                if (_folderToExclusions.TryGetValue(pathComponents[firstComponent], out List<TestExclusion> exclusions))
                {
                    foreach (TestExclusion exclusion in exclusions)
                    {
                        if (exclusion.Matches(pathComponents, firstComponent))
                        {
                            issueID = exclusion.IssueID;
                            return true;
                        }
                    }
                }
            }
            issueID = null;
            return false;
        }

        public bool TryGetIssue(string path, out string issueID)
        {
            string[] pathComponents = path.Split(new char[] { Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar });
            return TryGetIssue(pathComponents, out issueID);
        }
    }

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
        public FileInfo PackageList { get; set; }
        public bool Sequential { get; set; }
        public bool Framework { get; set; }
        public bool UseFramework { get; set; }
        public bool Release { get; set; }
        public bool LargeBubble { get; set; }
        public DirectoryInfo[] ReferencePath { get; set; }
        public FileInfo[] IssuesPath { get; set; }

        public string ConfigurationSuffix => (Release ? "-ret.out" : "-chk.out");

        public IEnumerable<string> ReferencePaths()
        {
            if (ReferencePath != null)
            {
                foreach (DirectoryInfo referencePath in ReferencePath)
                {
                    yield return referencePath.FullName;
                }
            }
        }

        /// <summary>
        /// Construct CoreRoot native path for a given compiler runner.
        /// </summary>
        /// <param name="index">Compiler runner index</param>
        /// <returns></returns>
        public string CoreRootOutputPath(CompilerIndex index, bool isFramework)
        {
            if (CoreRootDirectory == null)
            {
                return null;
            }

            string outputPath = CoreRootDirectory.FullName;
            if (!isFramework && (Framework || UseFramework))
            {
                outputPath = Path.Combine(outputPath, index.ToString() + ConfigurationSuffix);
            }
            return outputPath;
        }

        public IEnumerable<CompilerRunner> CompilerRunners(bool isFramework)
        {
            List<CompilerRunner> runners = new List<CompilerRunner>();

            if (CpaotDirectory != null)
            {
                List<string> referencePaths = new List<string>();
                referencePaths.Add(CoreRootOutputPath(CompilerIndex.CPAOT, isFramework));
                referencePaths.AddRange(ReferencePaths());
                runners.Add(new CpaotRunner(this, referencePaths));
            }

            if (Crossgen)
            {
                if (CoreRootDirectory == null)
                {
                    throw new Exception("-coreroot folder not specified, cannot use Crossgen runner");
                }
                List<string> referencePaths = new List<string>();
                referencePaths.Add(CoreRootOutputPath(CompilerIndex.Crossgen, isFramework));
                referencePaths.AddRange(ReferencePaths());
                runners.Add(new CrossgenRunner(this, referencePaths));
            }

            if (!NoJit)
            {
                runners.Add(new JitRunner(this));
            }

            return runners;
        }

        public string CoreRunPath(CompilerIndex index, bool isFramework)
        {
            string coreRunPath = Path.Combine(CoreRootOutputPath(index, isFramework), "CoreRun.exe");
            if (!File.Exists(coreRunPath))
            {
                Console.Error.WriteLine("CoreRun.exe not found in CORE_ROOT, explicit exe launches won't work");
            }
            return coreRunPath;
        }

        private static XNamespace s_xmlNamespace = "http://schemas.microsoft.com/developer/msbuild/2003";

        public IEnumerable<TestExclusion> EnumerateTestExclusions()
        {
            if (IssuesPath != null)
            {
                Dictionary<string, List<TestExclusion>> exclusionsByCondition = new Dictionary<string, List<TestExclusion>>();

                foreach (FileInfo issuesProject in IssuesPath)
                {
                    string issuesProjectPath = issuesProject.FullName;
                    XDocument issuesXml = XDocument.Load(issuesProjectPath);
                    foreach (XElement itemGroupElement in issuesXml.Root.Elements(s_xmlNamespace + "ItemGroup"))
                    {
                        string condition = itemGroupElement.Attribute("Condition")?.Value ?? "";
                        List<TestExclusion> exclusions;
                        if (!exclusionsByCondition.TryGetValue(condition, out exclusions))
                        {
                            exclusions = new List<TestExclusion>();
                            exclusionsByCondition.Add(condition, exclusions);
                        }
                        foreach (XElement excludeListElement in itemGroupElement.Elements(s_xmlNamespace + "ExcludeList"))
                        {
                            string testPath = excludeListElement.Attribute("Include")?.Value ?? "";
                            string issueID = excludeListElement.Element(s_xmlNamespace + "Issue")?.Value ?? "N/A";
                            exclusions.Add(CreateTestExclusion(testPath, issueID));
                        }
                    }
                }

                Project project = new Project();
                project.SetGlobalProperty("XunitTestBinBase", "*");
                project.SetGlobalProperty("BuildArch", "amd64");
                project.SetGlobalProperty("TargetsWindows", "true");
                project.SetGlobalProperty("AltJitArch", "amd64");
                project.SetGlobalProperty("RunTestViaIlLink", "false");

                ProjectRootElement root = project.Xml;
                root.AddTarget("GetListOfTestCmds");

                ProjectPropertyGroupElement propertyGroup = root.AddPropertyGroup();

                // Generate properties into the project to make it evaluate all conditions found in the targets file
                List<List<TestExclusion>> testExclusionLists = new List<List<TestExclusion>>();
                testExclusionLists.Capacity = exclusionsByCondition.Count;
                foreach (KeyValuePair<string, List<TestExclusion>> kvp in exclusionsByCondition)
                {
                    string propertyName = "Condition_" + testExclusionLists.Count.ToString();
                    bool emptyKey = string.IsNullOrEmpty(kvp.Key);
                    propertyGroup.AddProperty(propertyName, emptyKey ? "true" : "false");
                    if (!emptyKey)
                    {
                        propertyGroup.AddProperty(propertyName, "true").Condition = kvp.Key;
                    }
                    testExclusionLists.Add(kvp.Value);
                }

                project.Build();
                for (int exclusionListIndex = 0; exclusionListIndex < testExclusionLists.Count; exclusionListIndex++)
                {
                    string conditionValue = project.GetProperty("Condition_" + exclusionListIndex.ToString()).EvaluatedValue;
                    if (conditionValue.Equals("true", StringComparison.OrdinalIgnoreCase))
                    {
                        foreach (TestExclusion exclusion in testExclusionLists[exclusionListIndex])
                        {
                            yield return exclusion;
                        }
                    }
                }
            }
        }

        private TestExclusion CreateTestExclusion(string testPath, string issueId)
        {
            string[] pathComponents = testPath.Split(new char[] { '/' });
            int begin = 0;
            if (begin < pathComponents.Length && pathComponents[begin] == "$(XunitTestBinBase)")
            {
                begin++;
            }
            int end = pathComponents.Length;
            while (end > begin && (pathComponents[end - 1] == "*" || pathComponents[end - 1] == "**"))
            {
                end--;
            }
            bool openEnd = (end < pathComponents.Length && pathComponents[end] == "**");
            string[] outputComponents = new string[end - begin];
            Array.Copy(pathComponents, begin, outputComponents, 0, end - begin);

            return new TestExclusion(outputComponents, openEnd, issueId);
        }
    }
}
