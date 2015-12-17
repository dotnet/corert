// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;

using Cts = Internal.TypeSystem;
using Internal.NativeFormat;

using Xunit;

namespace MetadataTransformTests
{
    public class SimpleTests
    {
        TestTypeSystemContext _context;
        Cts.Ecma.EcmaModule _testModule;

        public SimpleTests()
        {
            _context = new TestTypeSystemContext();
            var systemModule = _context.CreateModuleForSimpleName("PrimaryMetadataAssembly");
            _context.SetSystemModule(systemModule);

            _testModule = systemModule;
        }

        [Fact]
        public void SimpleTest()
        {
            var transform = new ILCompiler.Metadata.Transform<SingleFileMetadataPolicy>(new SingleFileMetadataPolicy());

            foreach (var type in _testModule.GetAllTypes())
            {
                transform.HandleType(type);
            }


        }
    }
}
