// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.CommandLine;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Text;
using System.Text.RegularExpressions;
using Internal.CommandLine;
using Internal.TypeSystem.Ecma;
using static System.Console;

namespace ILVerify
{
    class Program : ResolverBase
    {
        private bool _help;
        private bool _verbose;
        private bool _printStatistics;

        private AssemblyName _systemModule = new AssemblyName("mscorlib");
        private Dictionary<string, string> _inputFilePaths = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase); // map of simple name to file path
        private Dictionary<string, string> _referenceFilePaths = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase); // map of simple name to file path
        private IReadOnlyList<Regex> _includePatterns = Array.Empty<Regex>();
        private IReadOnlyList<Regex> _excludePatterns = Array.Empty<Regex>();

        private Verifier _verifier;

        private Program()
        {
        }

        private void Help(string helpText)
        {
            WriteLine("ILVerify version " + typeof(Program).GetTypeInfo().Assembly.GetName().Version.ToString());
            WriteLine();
            WriteLine(helpText);
        }

        public static IReadOnlyList<Regex> StringPatternsToRegexList(IReadOnlyList<string> patterns)
        {
            List<Regex> patternList = new List<Regex>();
            foreach (var pattern in patterns)
                patternList.Add(new Regex(pattern, RegexOptions.Compiled));
            return patternList;
        }

        private ArgumentSyntax ParseCommandLine(string[] args)
        {
            IReadOnlyList<string> inputFiles = Array.Empty<string>();
            IReadOnlyList<string> referenceFiles = Array.Empty<string>();
            IReadOnlyList<string> includePatterns = Array.Empty<string>();
            IReadOnlyList<string> excludePatterns = Array.Empty<string>();
            string includeFile = string.Empty;
            string excludeFile = string.Empty;

            AssemblyName name = typeof(Program).GetTypeInfo().Assembly.GetName();
            ArgumentSyntax argSyntax = ArgumentSyntax.Parse(args, syntax =>
            {
                syntax.ApplicationName = name.Name.ToString();

                // HandleHelp writes to error, fails fast with crash dialog and lacks custom formatting.
                syntax.HandleHelp = false;
                syntax.HandleErrors = true;

                syntax.DefineOption("h|help", ref _help, "Display this usage message");
                syntax.DefineOption("s|system-module", ref _systemModule, s => new AssemblyName(s), "System module name (default: mscorlib)");
                syntax.DefineOptionList("r|reference", ref referenceFiles, "Reference metadata from the specified assembly");
                syntax.DefineOptionList("i|include", ref includePatterns, "Use only methods/types/namespaces, which match the given regular expression(s)");
                syntax.DefineOption("include-file", ref includeFile, "Same as --include, but the regular expression(s) are declared line by line in the specified file.");
                syntax.DefineOptionList("e|exclude", ref excludePatterns, "Skip methods/types/namespaces, which match the given regular expression(s)");
                syntax.DefineOption("exclude-file", ref excludeFile, "Same as --exclude, but the regular expression(s) are declared line by line in the specified file.");
                syntax.DefineOption("statistics", ref _printStatistics, "Print verification statistics");
                syntax.DefineOption("v|verbose", ref _verbose, "Verbose output");

                syntax.DefineParameterList("in", ref inputFiles, "Input file(s)");
            });

            foreach (var input in inputFiles)
                Helpers.AppendExpandedPaths(_inputFilePaths, input, true);

            foreach (var reference in referenceFiles)
                Helpers.AppendExpandedPaths(_referenceFilePaths, reference, false);

            if (!string.IsNullOrEmpty(includeFile))
            {
                if (includePatterns.Count > 0)
                    WriteLine("[Warning] --include-file takes precedence over --include");
                includePatterns = File.ReadAllLines(includeFile);
            }
            _includePatterns = StringPatternsToRegexList(includePatterns);

            if (!string.IsNullOrEmpty(excludeFile))
            {
                if (excludePatterns.Count > 0)
                    WriteLine("[Warning] --exclude-file takes precedence over --exclude");
                excludePatterns = File.ReadAllLines(excludeFile);
            }
            _excludePatterns = StringPatternsToRegexList(excludePatterns);

            if (_verbose)
            {
                WriteLine();
                foreach (var path in _inputFilePaths)
                    WriteLine($"Using input file '{path.Value}'");

                WriteLine();
                foreach (var path in _referenceFilePaths)
                    WriteLine($"Using reference file '{path.Value}'");

                WriteLine();
                foreach (var pattern in _includePatterns)
                    WriteLine($"Using include pattern '{pattern}'");

                WriteLine();
                foreach (var pattern in _excludePatterns)
                    WriteLine($"Using exclude pattern '{pattern}'");
            }

            return argSyntax;
        }

        private int Run(string[] args)
        {
            ArgumentSyntax syntax = ParseCommandLine(args);
            if (_help)
            {
                Help(syntax.GetHelpText());
                return 1;
            }

            if (_inputFilePaths.Count == 0)
                throw new CommandLineException("No input files specified");

            _verifier = new Verifier(this);
            _verifier.SetSystemModuleName(_systemModule);

            foreach (var kvp in _inputFilePaths)
            {
                VerifyAssembly(new AssemblyName(kvp.Key), kvp.Value);
            }

            return 0;
        }

        private void PrintResult(VerificationResult result, EcmaModule module, string pathOrModuleName)
        {
            Write("[IL]: Error: ");

            Write("[");
            Write(pathOrModuleName);
            Write(" : ");

            MetadataReader metadataReader = module.MetadataReader;

            TypeDefinition typeDef = metadataReader.GetTypeDefinition(metadataReader.GetMethodDefinition(result.Method).GetDeclaringType());
            string typeName = metadataReader.GetString(typeDef.Name);
            Write(typeName);

            Write("::");
            var method = (EcmaMethod)module.GetMethod(result.Method);
            PrintMethod(method);
            Write("]");

            var args = result.Error;
            if (args.Code != VerifierError.None)
            {
                Write("[offset 0x");
                Write(args.Offset.ToString("X8"));
                Write("]");

                if (args.Found != null)
                {
                    Write("[found ");
                    Write(args.Found);
                    Write("]");
                }

                if (args.Expected != null)
                {
                    Write("[expected ");
                    Write(args.Expected);
                    Write("]");
                }

                if (args.Token != 0)
                {
                    Write("[token  0x");
                    Write(args.Token.ToString("X8"));
                    Write("]");
                }
            }

            Write(" ");
            WriteLine(result.Message);
        }

        private static void PrintMethod(EcmaMethod method)
        {
            Write(method.Name);
            Write("(");

            if (method.Signature.Length > 0)
            {
                bool first = true;
                for(int i = 0; i < method.Signature.Length; i++)
                {
                    Internal.TypeSystem.TypeDesc parameter = method.Signature[0];
                    if (first)
                    {
                        first = false;
                    }
                    else
                    {
                        Write(", ");
                    }

                    Write(parameter.ToString());
                }
            }

            Write(")");
        }

        private void VerifyAssembly(AssemblyName name, string path)
        {
            PEReader peReader = Resolve(name);
            EcmaModule module = _verifier.GetModule(peReader);

            VerifyAssembly(peReader, module, path);
        }

        private void VerifyAssembly(PEReader peReader, EcmaModule module, string path)
        {
            int numErrors = 0;
            int verifiedMethodCounter = 0;
            int methodCounter = 0;

            MetadataReader metadataReader = peReader.GetMetadataReader();
            foreach (var methodHandle in metadataReader.MethodDefinitions)
            {
                // get fully qualified method name
                var methodName = GetQualifiedMethodName(metadataReader, methodHandle);

                bool verifying = ShouldVerifyMethod(methodName);
                if (_verbose)
                {
                    Write(verifying ? "Verifying " : "Skipping ");
                    WriteLine(methodName);
                }

                if (verifying)
                {
                    var results = _verifier.Verify(peReader, methodHandle);
                    foreach (var result in results)
                    {
                        PrintResult(result, module, path);
                        numErrors++;
                    }

                    verifiedMethodCounter++;
                }

                methodCounter++;
            }

            if (numErrors > 0)
                WriteLine(numErrors + " Error(s) Verifying " + path);
            else
                WriteLine("All Classes and Methods in " + path + " Verified.");

            if (_printStatistics)
            {
                WriteLine($"Methods found: {methodCounter}");
                WriteLine($"Methods verified: {verifiedMethodCounter}");
            }
        }

        /// <summary>
        /// This method returns the fully qualified method name by concatenating assembly, type and method name.
        /// This method exists to avoid additional assembly resolving, which might be triggered by calling 
        /// MethodDesc.ToString().
        /// </summary>
        private string GetQualifiedMethodName(MetadataReader metadataReader, MethodDefinitionHandle methodHandle)
        {
            var methodDef = metadataReader.GetMethodDefinition(methodHandle);
            var typeDef = metadataReader.GetTypeDefinition(methodDef.GetDeclaringType());

            var methodName = metadataReader.GetString(metadataReader.GetMethodDefinition(methodHandle).Name);
            var typeName = metadataReader.GetString(typeDef.Name);
            var namespaceName = metadataReader.GetString(typeDef.Namespace);
            var assemblyName = metadataReader.GetString(metadataReader.IsAssembly ? metadataReader.GetAssemblyDefinition().Name : metadataReader.GetModuleDefinition().Name);

            StringBuilder builder = new StringBuilder();
            builder.Append($"[{assemblyName}]");
            if (!string.IsNullOrEmpty(namespaceName))
                builder.Append($"{namespaceName}.");
            builder.Append($"{typeName}.{methodName}");

            return builder.ToString();
        }

        private bool ShouldVerifyMethod(string methodName)
        {
            if (_includePatterns.Count > 0 && !_includePatterns.Any(p => p.IsMatch(methodName)))
            {
                return false;
            }

            if (_excludePatterns.Any(p => p.IsMatch(methodName)))
            {
                return false;
            }

            return true;
        }

        protected override PEReader ResolveCore(AssemblyName name)
        {
            // Note: we use simple names instead of full names to resolve, because we can't get a full name from an assembly without reading it
            string simpleName = name.Name;

            string path = null;
            if (_inputFilePaths.TryGetValue(simpleName, out path) || _referenceFilePaths.TryGetValue(simpleName, out path))
            {
                return new PEReader(File.OpenRead(path));
            }

            return null;
        }

        private static int Main(string[] args)
        {
            try
            {
                return new Program().Run(args);
            }
            catch (Exception e)
            {
                Console.Error.WriteLine("Error: " + e.Message);
                return 1;
            }
        }
    }
}
