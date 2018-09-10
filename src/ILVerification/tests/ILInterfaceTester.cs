// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.IO;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Reflection.PortableExecutable;
using ILVerify;
using Internal.TypeSystem.Ecma;
using Xunit;

namespace ILVerification.Tests
{
    public class ILInterfaceTester : ResolverBase
    {
        [Fact]
        public void InvalidClass()
        {
            string testFile = Path.Combine(Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location), @"Tests\InterfaceTest.dll");
            EcmaModule module = TestDataLoader.GetModuleForTestAssembly(testFile);
            Verifier verifier = new Verifier(this);
            verifier.Verify(module.PEReader);
        }

        private static IEnumerable<VerificationResult> Verify(TestCase testCase)
        {
            EcmaModule module = TestDataLoader.GetModuleForTestAssembly(testCase.ModuleName);
            var methodHandle = (MethodDefinitionHandle)MetadataTokens.EntityHandle(testCase.MetadataToken);
            var method = (EcmaMethod)module.GetMethod(methodHandle);
            var verifier = new Verifier((ILVerifyTypeSystemContext)method.Context);
            return verifier.Verify(module.PEReader, methodHandle);
        }

        protected override PEReader ResolveCore(string simpleName)
        {
            throw new System.NotImplementedException();
        }
    }
}
