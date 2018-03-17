// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Reflection.PortableExecutable;
using System.Text;
using ILVerify;
using Internal.TypeSystem.Ecma;

namespace ILVerification.Tests
{
    /// <summary>
    /// Parses the methods in a given test assembly.
    /// </summary>
    class TestDataLoader
    {
        private const string SPECIALTEST_PREFIX = "special.";

        /// <summary>
        /// Returns the folder where the test assemblies are located.
        /// </summary>
        private static string TestFolder()
        {
            string assemblyPath = typeof(ILMethodTester).GetTypeInfo().Assembly.Location;
            return Path.GetDirectoryName(assemblyPath);
        }

        /// <summary>
        /// Returns all methods that contain valid IL code based on the following naming convention:
        /// [FriendlyName]_Valid
        /// The method must contain 1 '_'. The part before the '_' is a friendly name describing what the method does.
        /// The word after the '_' has to be 'Valid' (Case sensitive)
        /// E.g.: 'SimpleAdd_Valid'
        /// </summary>
        public static List<ValidILTestCase> GetMethodsWithValidIL(string assemblyName)
        {
            var methodSelector = new Func<string[], MethodDefinitionHandle, ValidILTestCase>((mparams, methodHandle) =>
            {
                if (mparams.Length == 2 && mparams[1] == "Valid")
                {
                    return new ValidILTestCase { MetadataToken = MetadataTokens.GetToken(methodHandle) };
                }
                return null;
            });
            return GetTestMethodsFromDll(assemblyName, methodSelector);
        }

        /// <summary>
        /// Returns all methods that contain valid IL code based on the following naming convention:
        /// [FriendlyName]_Invalid_[ExpectedVerifierError1].[ExpectedVerifierError2]....[ExpectedVerifierErrorN]
        /// The method name must contain 2 '_' characters.
        /// 1. part: a friendly name
        /// 2. part: must be the word 'Invalid' (Case sensitive)
        /// 3. part: the expected VerifierErrors as string separated by '.'.
        /// E.g.: SimpleAdd_Invalid_ExpectedNumericType
        /// </summary>
        public static List<InvalidILTestCase> GetMethodsWithInvalidIL(string assemblyName)
        {
            var methodSelector = new Func<string[], MethodDefinitionHandle, InvalidILTestCase>((mparams, methodHandle) =>
            {
                if (mparams.Length == 3 && mparams[1] == "Invalid")
                {
                    var expectedErrors = mparams[2].Split('.');
                    var verificationErros = new List<VerifierError>();

                    foreach (var item in expectedErrors)
                    {
                        if (Enum.TryParse(item, out VerifierError expectedError))
                        {
                            verificationErros.Add(expectedError);
                        }
                    }

                    var newItem = new InvalidILTestCase { MetadataToken = MetadataTokens.GetToken(methodHandle) };

                    if (expectedErrors.Length > 0)
                    {
                        newItem.ExpectedVerifierErrors = verificationErros;
                    }

                    return newItem;
                }
                return null;
            });
            return GetTestMethodsFromDll(assemblyName, methodSelector);
        }

        private static List<T> GetTestMethodsFromDll<T>(string assemblyName, Func<string[], MethodDefinitionHandle, T> methodSelector) where T : TestCase
        {
            var retVal = new List<T>();

            string testDllName = assemblyName + ".dll";

            var testModule = GetModuleForTestAssembly(testDllName);

            foreach (var methodHandle in testModule.MetadataReader.MethodDefinitions)
            {
                var method = (EcmaMethod)testModule.GetMethod(methodHandle);
                var methodName = method.Name;

                if (!String.IsNullOrEmpty(methodName) && methodName.Contains("_"))
                {
                    var mparams = methodName.Split('_');
                    var specialMethodHandle = HandleSpecialTests(mparams, method);
                    var newItem = methodSelector(mparams, specialMethodHandle);

                    if (newItem != null)
                    {
                        newItem.TestName = mparams[0];
                        newItem.MethodName = methodName;
                        newItem.ModuleName = testDllName;

                        retVal.Add(newItem);
                    }
                }
            }
            return retVal;
        }

        private static MethodDefinitionHandle HandleSpecialTests(string[] methodParams, EcmaMethod method)
        {
            if (!methodParams[0].StartsWith(SPECIALTEST_PREFIX))
                return method.Handle;

            // Cut off special prefix
            var specialParams = methodParams[0].Substring(SPECIALTEST_PREFIX.Length);

            // Get friendly name / special name
            int delimiter = specialParams.IndexOf('.');
            if (delimiter < 0)
                return method.Handle;

            var friendlyName = specialParams.Substring(0, delimiter);
            var specialName = specialParams.Substring(delimiter + 1);

            // Substitute method parameters with friendly name
            methodParams[0] = friendlyName;

            var specialMethodHandle = (EcmaMethod)method.OwningType.GetMethod(specialName, method.Signature);
            return specialMethodHandle == null ? method.Handle : specialMethodHandle.Handle;
        }

        private static IEnumerable<string> GetAllTestDlls()
        {
            foreach (var item in Directory.GetFiles(TestFolder()))
            {
                if (item.ToLower().EndsWith(".dll"))
                {
                    yield return Path.GetFileName(item);
                }
            }
        }

        public static EcmaModule GetModuleForTestAssembly(string assemblyName)
        {
            var simpleNameToPathMap = new Dictionary<string, string>();

            foreach (var fileName in GetAllTestDlls())
            {
                simpleNameToPathMap.Add(Path.GetFileNameWithoutExtension(fileName), Path.Combine(TestFolder(), fileName));
            }

            Assembly coreAssembly = typeof(object).GetTypeInfo().Assembly;
            simpleNameToPathMap.Add(coreAssembly.GetName().Name, coreAssembly.Location);

            Assembly systemRuntime = Assembly.Load(new AssemblyName("System.Runtime"));
            simpleNameToPathMap.Add(systemRuntime.GetName().Name, systemRuntime.Location);

            var resolver = new TestResolver(simpleNameToPathMap);
            var typeSystemContext = new ILVerifyTypeSystemContext(resolver);
            typeSystemContext.SetSystemModule(typeSystemContext.GetModule(resolver.Resolve(coreAssembly.GetName())));

            return typeSystemContext.GetModule(resolver.Resolve(new AssemblyName(Path.GetFileNameWithoutExtension(assemblyName))));
        }

        private sealed class TestResolver : ResolverBase
        {
            Dictionary<string, string> _simpleNameToPathMap;
            public TestResolver(Dictionary<string, string> simpleNameToPathMap)
            {
                _simpleNameToPathMap = simpleNameToPathMap;
            }

            protected override PEReader ResolveCore(AssemblyName name)
            {
                if (_simpleNameToPathMap.TryGetValue(name.Name, out string path))
                {
                    return new PEReader(File.OpenRead(path));
                }

                return null;
            }
        }
    }

    abstract class TestCase
    {
        public string TestName { get; set; }
        public string MethodName { get; set; }
        public int MetadataToken { get; set; }
        public string ModuleName { get; set; }

        public override string ToString()
        {
            return $"[{Path.GetFileNameWithoutExtension(ModuleName)}] {TestName}";
        }
    }

    /// <summary>
    /// Describes a test case with a method that contains valid IL
    /// </summary>
    sealed class ValidILTestCase : TestCase { }

    /// <summary>
    /// Describes a test case with a method that contains invalid IL with the expected VerifierErrors
    /// </summary>
    sealed class InvalidILTestCase : TestCase
    {
        public List<VerifierError> ExpectedVerifierErrors { get; set; }

        public override string ToString()
        {
            return base.ToString() + GetErrorsString(ExpectedVerifierErrors);
        }

        private static string GetErrorsString(List<VerifierError> errors)
        {
            if (errors == null || errors.Count <= 0)
                return String.Empty;

            var errorsString = new StringBuilder(" (");

            for (int i = 0; i < errors.Count - 1; ++i)
                errorsString.Append(errors[i]).Append(", ");

            errorsString.Append(errors[errors.Count - 1]);
            errorsString.Append(")");

            return errorsString.ToString();
        }
    }
}
