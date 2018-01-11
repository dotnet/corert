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
using System.Text.RegularExpressions;
using Internal.CommandLine;
using Internal.TypeSystem.Ecma;
using static System.Console;

namespace ILVerify
{
    class Program : ResolverBase
    {
        private bool _help;

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
                var results = VerifyAssembly(new AssemblyName(kvp.Key), out EcmaModule module);
                int numErrors = 0;

                foreach (var result in results)
                {
                    numErrors++;
                    PrintResult(result, module, kvp.Value);
                }

                if (numErrors > 0)
                    WriteLine(numErrors + " Error(s) Verifying " + kvp.Value);
                else
                    WriteLine("All Classes and Methods in " + kvp.Value + " Verified.");
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

            if (method.Signature._parameters != null && method.Signature._parameters.Length > 0)
            {
                bool first = true;
                foreach (Internal.TypeSystem.TypeDesc parameter in method.Signature._parameters)
                {
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

        private IEnumerable<VerificationResult> VerifyAssembly(AssemblyName name, out EcmaModule module)
        {
            PEReader peReader = Resolve(name);
            module = _verifier.GetModule(peReader);

            return VerifyAssembly(peReader);
        }

        private IEnumerable<VerificationResult> VerifyAssembly(PEReader peReader)
        {
            MetadataReader metadataReader = peReader.GetMetadataReader();
            foreach (var methodHandle in metadataReader.MethodDefinitions)
            {
                var methodName = metadataReader.GetString(metadataReader.GetMethodDefinition(methodHandle).Name);
                if (ShouldVerifyMethod(methodName))
                {
                    var results = _verifier.Verify(peReader, methodHandle);
                    foreach (var result in results)
                    {
                        yield return result;
                    }
                }
            }
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
