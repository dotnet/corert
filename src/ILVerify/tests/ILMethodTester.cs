// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Linq;
using System.Reflection.Metadata.Ecma335;
using Internal.IL;
using Internal.TypeSystem.Ecma;
using Xunit;

namespace ILVerify.Tests
{
    public class ILMethodTester
    {
        [Theory(DisplayName = "")]
        [MemberData(nameof(TestDataLoader.GetMethodsWithValidIL), MemberType = typeof(TestDataLoader))]
        [Trait("", "Valid IL Tests")]
        void TestMethodsWithValidIL(ValidILTestCase validIL)
        {
            VerificationResult result = Verify(validIL);
            Assert.Equal(0, result.NumErrors);
        }

        [Theory(DisplayName = "")]
        [MemberData(nameof(TestDataLoader.GetMethodsWithInvalidIL), MemberType = typeof(TestDataLoader))]
        [Trait("", "Invalid IL Tests")]
        void TestMethodsWithInvalidIL(InvalidILTestCase invalidIL)
        {
            VerificationResult result = null;
            
            try
            {
                result = Verify(invalidIL);
            }
            catch
            {
                //in some cases ILVerify throws exceptions when things look too wrong to continue
                //currently these are not caught. In tests we just catch these and do the asserts.
                //Once these exceptions are better handled and ILVerify instead of crashing aborts the verification
                //gracefully we can remove this empty catch block.
            }
            finally
            {
                Assert.NotNull(result);
                Assert.Equal(invalidIL.ExpectedVerifierErrors.Count, result.NumErrors);

                foreach (var item in invalidIL.ExpectedVerifierErrors)
                {
                    var actual = result._errors.Select(e => e.ToString());
                    Assert.True(result._errors.Contains(item), $"Actual errors where: {string.Join(',', actual)}");
                }
            }
        }

        private static VerificationResult Verify(TestCase testCase)
        {
            EcmaModule module = TestDataLoader.GetModuleForTestAssembly(testCase.ModuleName);
            var method = (EcmaMethod)module.GetMethod(MetadataTokens.EntityHandle(testCase.MetadataToken));
            var methodIL = EcmaMethodIL.Create(method);

            var verifier = new Verifier((SimpleTypeSystemContext)method.Context);
            return verifier.VerifyMethod(method, methodIL, testCase.ToString());
        }
    }
}
