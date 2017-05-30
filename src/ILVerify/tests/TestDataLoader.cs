// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Text;
using Internal.IL;
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
        /// </summary>
        public static string TESTASSEMBLYPATH = @"..\..\..\ILTests\";

        /// <summary>
        /// Returns all methods that contain valid IL code based on the following naming convention:
        /// [FriendlyName]_Valid
        /// The method must contain 1 '_'. The part before the '_' is a friendly name describing what the method does. 
        /// The word after the '_' has to be 'Valid'. 
        /// E.g.: 'SimpleAdd_Valid'
        /// </summary>
        public static TheoryData<TestCase> GetMethodsWithValidIL()
        {
            var methodSelector = new Func<string[], MethodDefinitionHandle, TestCase>((mparams, methodHandle) =>
            {
                if (mparams.Length == 2 && mparams[1].ToLower() == "valid")
                {
                    return new ValidILTestCase { MetaDataToken = MetadataTokens.GetToken(methodHandle) };
                }
                return null;
            });
            return GetTestMethodsFromDll(methodSelector);
        }

        /// <summary>
        /// Returns all methods that contain valid IL code based on the following naming convention:
        /// [FriendlyName]_Invalid_[ExpectedVerifierError1].[ExpectedVerifierError2]_[ExpectedExceptionDuringValidation]
        /// The method name must contain 3 '_' characters.
        /// 1. part: a friendly name
        /// 2. part: must be the word 'Invalid'
        /// 3. part: the expected VerifierErrors as string separated by '.'.      
        /// E.g.: SimpleAdd_Invalid_ExpectedNumericType
        /// </summary>      
        public static TheoryData<TestCase> GetMethodsWithInvalidIL()
        {
            var methodSelector = new Func<string[], MethodDefinitionHandle, TestCase>((mparams, methodHandle) =>
            {
                if (mparams.Length == 3 && mparams[1].ToLower() == "invalid")
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

                    var newItem = new InvalidILTestCase { MetaDataToken = MetadataTokens.GetToken(methodHandle) };

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
                    var methodName = method.ToString();

                    if (!String.IsNullOrEmpty(methodName) && methodName.Contains("_"))
                    {
                        var mparams = methodName.Split('_');
                        var newItem = methodSelector(mparams, methodHandle);

                        if (newItem != null)
                        {
                            newItem.MethodName = methodName;
                            newItem.ModuleName = testDllName;

                            retVal.Add(newItem);
                        }
                    }
                }
            }
            return retVal;
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
        public string MethodName { get; set; }
        public int MetaDataToken { get; set; }
        public string ModuleName { get; set; }

        public virtual void Deserialize(IXunitSerializationInfo info)
        {
            MethodName = info.GetValue<string>(nameof(MethodName));
            MetaDataToken = info.GetValue<int>(nameof(MetaDataToken));
            ModuleName = info.GetValue<string>(nameof(ModuleName));
        }

        public virtual void Serialize(IXunitSerializationInfo info)
        {
            info.AddValue(nameof(MethodName), MethodName);
            info.AddValue(nameof(MetaDataToken), MetaDataToken);
            info.AddValue(nameof(ModuleName), ModuleName);
        }

        public override string ToString()
        {
            return $"{ModuleName} - {MethodName}";
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
    }
}
