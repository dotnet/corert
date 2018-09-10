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
using System.Linq;
namespace ILVerification.Tests
{
    public class ILInterfaceTester : ResolverBase
    {
        [Fact]
        [Trait("MyTrait", "MyTrait")]
        public void InvalidClass()
        {
            string testFile = @"Tests\InterfaceTest.dll";
            EcmaModule module = TestDataLoader.GetModuleForTestAssembly(testFile);
            Verifier verifier = new Verifier(this);
            var results = verifier.Verify(module.PEReader).ToArray();
            Assert.NotNull(results);
        }

        protected override PEReader ResolveCore(string simpleName)
        {
            throw new System.NotImplementedException();
        }
    }
}
