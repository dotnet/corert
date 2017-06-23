// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO;
using System.Collections.Generic;
using System.CommandLine;
using System.Reflection;
using System.Linq;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Reflection.PortableExecutable;
using System.Text;

using Internal.TypeSystem;
using Internal.TypeSystem.Ecma;
using Internal.IL;

using Internal.CommandLine;
using System.Text.RegularExpressions;
using System.Globalization;
using System.Resources;

namespace ILVerify
{
    class Program
    {
        private const string SystemModuleSimpleName = "mscorlib";
        private bool _help;

        private Dictionary<string, string> _inputFilePaths = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        private Dictionary<string, string> _referenceFilePaths = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        private IReadOnlyList<Regex> _includePatterns = Array.Empty<Regex>();
        private IReadOnlyList<Regex> _excludePatterns = Array.Empty<Regex>();

        private SimpleTypeSystemContext _typeSystemContext;
        private ResourceManager _stringResourceManager;

        private int _numErrors;

        private Program()
        {
        }

        private void Help(string helpText)
        {
            Console.WriteLine("ILVerify version " + typeof(Program).GetTypeInfo().Assembly.GetName().Version.ToString());
            Console.WriteLine();
            Console.WriteLine("--help          Display this usage message (Short form: -?)");
            Console.WriteLine("--reference     Reference metadata from the specified assembly (Short form: -r)");
            Console.WriteLine("--include       Use only methods/types/namespaces, which match the given regular expression(s) (Short form: -i)");
            Console.WriteLine("--include-file  Same as --include, but the regular expression(s) are declared line by line in the specified file.");
            Console.WriteLine("--exclude       Skip methods/types/namespaces, which match the given regular expression(s) (Short form: -e)");
            Console.WriteLine("--exclude-file  Same as --exclude, but the regular expression(s) are declared line by line in the specified file.");
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

                syntax.DefineOption("h|help", ref _help, "Help message for ILC");
                syntax.DefineOptionList("r|reference", ref referenceFiles, "Reference file(s) for compilation");
                syntax.DefineOptionList("i|include", ref includePatterns, "Use only methods/types/namespaces, which match the given regular expression(s)");
                syntax.DefineOption("include-file", ref includeFile, "Same as --include, but the regular expression(s) are declared line by line in the specified file.");
                syntax.DefineOptionList("e|exclude", ref excludePatterns, "Skip methods/types/namespaces, which match the given regular expression(s)");
                syntax.DefineOption("exclude-file", ref excludeFile, "Same as --exclude, but the regular expression(s) are declared line by line in the specified file.");

                syntax.DefineParameterList("in", ref inputFiles, "Input file(s) to compile");
            });

            foreach (var input in inputFiles)
                Helpers.AppendExpandedPaths(_inputFilePaths, input, true);

            foreach (var reference in referenceFiles)
                Helpers.AppendExpandedPaths(_referenceFilePaths, reference, false);

            if (!string.IsNullOrEmpty(includeFile))
            {
                if (includePatterns.Count > 0)
                    Console.WriteLine("[Warning] --include-file takes precedence over --include");
                includePatterns = File.ReadAllLines(includeFile);
            }
            _includePatterns = StringPatternsToRegexList(includePatterns);

            if (!string.IsNullOrEmpty(excludeFile))
            {
                if (excludePatterns.Count > 0)
                    Console.WriteLine("[Warning] --exclude-file takes precedence over --exclude");
                excludePatterns = File.ReadAllLines(excludeFile);
            }
            _excludePatterns = StringPatternsToRegexList(excludePatterns);

            return argSyntax;
        }

        private void VerifyMethod(MethodDesc method, MethodIL methodIL)
        {
            // Console.WriteLine("Verifying: " + method.ToString());

            try
            {
                var importer = new ILImporter(method, methodIL);

                importer.ReportVerificationError = (args) =>
                {
                    var message = new StringBuilder();

                    message.Append("[IL]: Error: ");
                    
                    message.Append("[");
                    message.Append(_typeSystemContext.GetModulePath(((EcmaMethod)method).Module));
                    message.Append(" : ");
                    message.Append(((EcmaType)method.OwningType).Name);
                    message.Append("::");
                    message.Append(method.Name);
                    message.Append("]");

                    message.Append("[offset 0x");
                    message.Append(args.Offset.ToString("X8"));
                    message.Append("]");

                    if (args.Found != null)
                    {
                        message.Append("[found ");
                        message.Append(args.Found);
                        message.Append("]");
                    }

                    if (args.Expected != null)
                    {
                        message.Append("[expected ");
                        message.Append(args.Expected);
                        message.Append("]");
                    }

                    if (args.Token != 0)
                    {
                        message.Append("[token  0x");
                        message.Append(args.Token.ToString("X8"));
                        message.Append("]");
                    }

                    message.Append(" ");

                    if (_stringResourceManager == null)
                    {
                        _stringResourceManager = new ResourceManager("ILVerify.Resources.Strings", Assembly.GetExecutingAssembly());
                    }
            
                    var str = _stringResourceManager.GetString(args.Code.ToString(), CultureInfo.InvariantCulture);
                    message.Append(string.IsNullOrEmpty(str) ? args.Code.ToString() : str);

                    Console.WriteLine(message);

                    _numErrors++;
                };

                importer.Verify();
            }
            catch (NotImplementedException e)
            {
                Console.Error.WriteLine($"Error in {method}: {e.Message}");
            }
            catch (InvalidProgramException e)
            {
                Console.Error.WriteLine($"Error in {method}: {e.Message}");
            }
            catch (VerificationException)
            {
            }
            catch (BadImageFormatException)
            {
                Console.WriteLine("Unable to resolve token");
            }
            catch (PlatformNotSupportedException e)
            {
                Console.WriteLine(e.Message);
            }
        }

        private void VerifyModule(EcmaModule module)
        {
            foreach (var methodHandle in module.MetadataReader.MethodDefinitions)
            {
                var method = (EcmaMethod)module.GetMethod(methodHandle);

                var methodIL = EcmaMethodIL.Create(method);
                if (methodIL == null)
                    continue;

                var methodName = method.ToString();
                if (_includePatterns.Count > 0 && !_includePatterns.Any(p => p.IsMatch(methodName)))
                    continue;
                if (_excludePatterns.Any(p => p.IsMatch(methodName)))
                    continue;

                VerifyMethod(method, methodIL);
            }
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

            _typeSystemContext = new SimpleTypeSystemContext();
            _typeSystemContext.InputFilePaths = _inputFilePaths;
            _typeSystemContext.ReferenceFilePaths = _referenceFilePaths;

            _typeSystemContext.SetSystemModule(_typeSystemContext.GetModuleForSimpleName(SystemModuleSimpleName));

            foreach (var inputPath in _inputFilePaths.Values)
            {
                _numErrors = 0;

                VerifyModule(_typeSystemContext.GetModuleFromPath(inputPath));

                if (_numErrors > 0)
                    Console.WriteLine(_numErrors + " Error(s) Verifying " + inputPath);
                else
                    Console.WriteLine("All Classes and Methods in " + inputPath + " Verified.");
            }

            return 0;
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
