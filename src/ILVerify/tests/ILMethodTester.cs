using System;
using System.Collections.Generic;
using Internal.IL;
using Internal.TypeSystem.Ecma;
using Xunit;

namespace ILVerify.Tests
{
    abstract class TestCase
    {
        public string MethodName { get; set; }
        public ILImporter ILImporter { get; set; }
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
        public List<VerifierError> ExpectedVerifierError { get; set; }

        /// <summary>
        /// The expected exception during validation.
        /// If no exception is thrown during verification this must be null.
        /// </summary>
        public Type ExpectedException { get; set; }
    }

    /// <summary>
    /// Parses the methods in the test assemblies. 
    /// It loads all assemblies from the test folder defined in <code>TestDataLoader.TESTASSEMBLYPATH</code>
    /// This class feeds the xunit Theories
    /// </summary>
    class ILReader
    {
        public enum TestMethodType
        {
            Valid, Invalid
        }

        /// <summary>
        /// Returns all methods that contain valid IL code based on the following naming convention:
        /// [FriendlyName]_Valid
        /// The method must contain 1 '_'. The part before the '_' is a friendly name describing what the method does. 
        /// The word after the '_' has to be 'Valid'. 
        /// E.g.: 'SimpleAdd_Valid'
        /// </summary>
        public static IEnumerable<Object[]> GetMethodsWithValidIL()
        {
            var methodSelector = new Func<string[], EcmaMethod, EcmaMethodIL, TestCase>((mparams, method, methodIL) =>
            {
                if (mparams.Length == 2 && mparams[1].ToLower() == "valid")
                {
                    return new ValidILTestCase { ILImporter = new ILImporter(method, methodIL) };
                }
                return null;
            });
            return GetTestMethodsFromDll(methodSelector);
        }

        /// <summary>
        /// Returns all methods that contain valid IL code based on the following naming convention:
        /// [FriendlyName]_Invalid_[ExpectedVerifierError1].[ExpectedVerifierError2]_[ExpectedExceptionDuringValidation]
        /// The method name must contain 4 '_' characters.
        /// 1. part: a friendly name
        /// 2. part: must be the word 'Invalid'
        /// 3. part: the expected VerifierErrors as string separated by '.'.
        /// 3. part: the expected exception type (with namespace) during validation.
        /// E.g.: SimpleAdd_Invalid_ExpectedNumericType_Internal.IL.LocalVerificationException
        /// </summary>      
        public static IEnumerable<Object[]> GetMethodsWithInvalidIL()
        {
            var methodSelector = new Func<string[], EcmaMethod, EcmaMethodIL, TestCase>((mparams, method, methodIL) =>
            {
                if (mparams.Length == 4 && mparams[1].ToLower() == "invalid")
                {
                    var expectedErrors = mparams[2].Split('.');
                    List<VerifierError> verificationErros = new List<VerifierError>();

                    foreach (var item in expectedErrors)
                    {
                        if (Enum.TryParse(item, out VerifierError expectedError))
                        {
                            verificationErros.Add(expectedError);
                        }
                    }

                    var newItem = new InvalidILTestCase { ILImporter = new ILImporter(method, methodIL) };

                    if (expectedErrors.Length > 0)
                    {
                        newItem.ExpectedVerifierError = verificationErros;
                    }

                    if (mparams[3].Length > 0)
                    {
                        newItem.ExpectedException = Type.GetType(mparams[3]);

                        if (newItem.ExpectedException == null)
                        {
                            var ilVerifyAssembly = System.Reflection.Assembly.GetAssembly(typeof(VerifierError));
                            newItem.ExpectedException = ilVerifyAssembly.GetType(mparams[3]);
                        }
                    }

                    newItem.MethodName = method.Name;
                    return newItem;
                }
                return null;
            });
            return GetTestMethodsFromDll(methodSelector);
        }

        private static IEnumerable<Object[]> GetTestMethodsFromDll(Func<string[], EcmaMethod, EcmaMethodIL, TestCase> MethodSelector)
        {
            List<TestCase[]> retVal = new List<TestCase[]>();

            foreach (var testDllName in GetAllTestDlls())
            {
                var testModule = TestDataLoader.GetModuleForTestAssembly(testDllName);

                foreach (var methodHandle in testModule.MetadataReader.MethodDefinitions)
                {
                    var method = (EcmaMethod)testModule.GetMethod(methodHandle);
                    var methodIL = EcmaMethodIL.Create(method);
                    if (methodIL == null)
                        continue;

                    var methodName = method.ToString();

                    if (!String.IsNullOrEmpty(methodName) && methodName.Contains("_"))
                    {
                        var mparams = methodName.Split('_');
                        var newItem = MethodSelector(mparams, method, methodIL);

                        if (newItem != null)
                        {
                            retVal.Add(new TestCase[] { newItem });
                        }
                    }
                }
            }
            return retVal;
        }

        private static IEnumerable<string> GetAllTestDlls()
        {
            foreach (var item in System.IO.Directory.GetFiles(TestDataLoader.TESTASSEMBLYPATH))
            {
                if (item.ToLower().EndsWith(".dll"))
                {
                    yield return System.IO.Path.GetFileName(item);
                }
            }
        }
    }

    public class ILMethodTester
    {
        [Theory]
        [MemberData(nameof(ILReader.GetMethodsWithValidIL), MemberType = typeof(ILReader))]
        void TestMethodsWithValidIL(ValidILTestCase testCase)
        {
            ILImporter importer = testCase.ILImporter;

            var verifierErrors = new List<VerifierError>();
            importer.ReportVerificationError = new Action<VerificationErrorArgs>((err) =>
            {
                verifierErrors.Add(err.Code);
            });

            importer.Verify();
            Assert.Equal(0, verifierErrors.Count);
        }

        [Theory]
        [MemberData(nameof(ILReader.GetMethodsWithInvalidIL), MemberType = typeof(ILReader))]
        void TestMethodsWithInvalidIL(InvalidILTestCase invalidILTestCase)
        {
            ILImporter importer = invalidILTestCase.ILImporter;

            var verifierErrors = new List<VerifierError>();
            importer.ReportVerificationError = new Action<VerificationErrorArgs>((err) =>
            {
                verifierErrors.Add(err.Code);
            });

            if (invalidILTestCase.ExpectedException != null)
            {
                Assert.Throws(invalidILTestCase.ExpectedException, () => importer.Verify());
            }
            else
            {
                importer.Verify();
            }

            Assert.Equal(invalidILTestCase.ExpectedVerifierError.Count, verifierErrors.Count);

            foreach (var item in invalidILTestCase.ExpectedVerifierError)
            {
                Assert.True(verifierErrors.Contains(item));
            }
        }
    }
}
