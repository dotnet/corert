// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Text;
using Internal.IL;
using Internal.TypeSystem;
using Internal.TypeSystem.Ecma;
using Newtonsoft.Json;
using Xunit;
using Xunit.Abstractions;

namespace ILVerify.Tests
{
    /// <summary>
    /// Parses the methods in the test assemblies. 
    /// It loads all assemblies from the test folder defined in <code>TestDataLoader.TESTASSEMBLYPATH</code>
    /// This class feeds the xunit Theories
    /// </summary>
    class TestDataLoader
    {
        /// <summary>
        /// The folder with the binaries which are compiled from the test driver IL Code
        /// Currently the test .il code is built manually, but the plan is to have a ProjectReference and automatically build the .il files.
        /// See: https://github.com/dotnet/corert/pull/3725#discussion_r118820770
        /// </summary>
        public static string TESTASSEMBLYPATH = @"..\..\..\ILTests\";

        private const string SPECIALTEST_PREFIX = "special.";

        /// <summary>
        /// Returns all methods that contain valid IL code based on the following naming convention:
        /// [FriendlyName]_Valid
        /// The method must contain 1 '_'. The part before the '_' is a friendly name describing what the method does. 
        /// The word after the '_' has to be 'Valid' (Case sensitive) 
        /// E.g.: 'SimpleAdd_Valid'
        /// </summary>
        public static TheoryData<TestCase> GetMethodsWithValidIL()
        {
            var methodSelector = new Func<string[], MethodDefinitionHandle, TestCase>((mparams, methodHandle) =>
            {
                if (mparams.Length == 2 && mparams[1] == "Valid")
                {
                    return new ValidILTestCase { MetadataToken = MetadataTokens.GetToken(methodHandle) };
                }
                return null;
            });
            return GetTestMethodsFromDll(methodSelector);
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
        public static TheoryData<TestCase> GetMethodsWithInvalidIL()
        {
            var methodSelector = new Func<string[], MethodDefinitionHandle, TestCase>((mparams, methodHandle) =>
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
            return GetTestMethodsFromDll(methodSelector);
        }

        private static TheoryData<TestCase> GetTestMethodsFromDll(Func<string[], MethodDefinitionHandle, TestCase> methodSelector)
        {
            var retVal = new Xunit.TheoryData<TestCase>();

            foreach (var testDllName in GetAllTestDlls())
            {
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
            foreach (var item in System.IO.Directory.GetFiles(TESTASSEMBLYPATH))
            {
                if (item.ToLower().EndsWith(".dll"))
                {
                    yield return System.IO.Path.GetFileName(item);
                }
            }
        }

        public static EcmaModule GetModuleForTestAssembly(string assemblyName)
        {
            var typeSystemContext = new SimpleTypeSystemContext();
            var coreAssembly = typeof(Object).Assembly;
            var systemRuntime = Assembly.Load("System.Runtime");

            typeSystemContext.InputFilePaths = new Dictionary<string, string>
            {
                { coreAssembly.GetName().Name, coreAssembly.Location },
                { systemRuntime.GetName().Name, systemRuntime.Location }
            };

            typeSystemContext.SetSystemModule(typeSystemContext.GetModuleForSimpleName(coreAssembly.GetName().Name));
            return typeSystemContext.GetModuleFromPath(TESTASSEMBLYPATH + assemblyName);
        }
    }

    abstract class TestCase : IXunitSerializable
    {
        public string TestName { get; set; }
        public string MethodName { get; set; }
        public int MetadataToken { get; set; }
        public string ModuleName { get; set; }

        public virtual void Deserialize(IXunitSerializationInfo info)
        {
            TestName = info.GetValue<string>(nameof(TestName));
            MethodName = info.GetValue<string>(nameof(MethodName));
            MetadataToken = info.GetValue<int>(nameof(MetadataToken));
            ModuleName = info.GetValue<string>(nameof(ModuleName));
        }

        public virtual void Serialize(IXunitSerializationInfo info)
        {
            info.AddValue(nameof(TestName), TestName);
            info.AddValue(nameof(MethodName), MethodName);
            info.AddValue(nameof(MetadataToken), MetadataToken);
            info.AddValue(nameof(ModuleName), ModuleName);
        }

        public override string ToString()
        {
            return $"[{Path.GetFileNameWithoutExtension(ModuleName)}] {TestName}";
        }
    }

    /// <summary>
    /// Describes a test case with a method that contains valid IL
    /// </summary>
    class ValidILTestCase : TestCase { }

    /// <summary>
    /// Describes a test case with a method that contains invalid IL with the expected VerifierErrors
    /// </summary>
    class InvalidILTestCase : TestCase
    {
        public List<VerifierError> ExpectedVerifierErrors { get; set; }

        public override void Serialize(IXunitSerializationInfo info)
        {
            base.Serialize(info);
            var serializedExpectedErrors = JsonConvert.SerializeObject(ExpectedVerifierErrors);
            info.AddValue(nameof(ExpectedVerifierErrors), serializedExpectedErrors);
        }

        public override void Deserialize(IXunitSerializationInfo info)
        {
            base.Deserialize(info);
            var serializedExpectedErrors = info.GetValue<string>(nameof(ExpectedVerifierErrors));
            ExpectedVerifierErrors = JsonConvert.DeserializeObject<List<VerifierError>>(serializedExpectedErrors);
        }

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
