// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Linq;

using Cts = Internal.TypeSystem;

using Xunit;

namespace MetadataTransformTests
{
    public class SimpleTests
    {
        TestTypeSystemContext _context;
        Cts.Ecma.EcmaModule _systemModule;

        public SimpleTests()
        {
            _context = new TestTypeSystemContext();
            _systemModule = _context.CreateModuleForSimpleName("PrimaryMetadataAssembly");
            _context.SetSystemModule(_systemModule);
        }

        [Fact]
        public void TestBlockedInterface()
        {
            var policy = new SingleFileMetadataPolicy();
            var transform = new ILCompiler.Metadata.Transform<SingleFileMetadataPolicy>(policy);

            int count = 0;
            foreach (Cts.MetadataType type in _systemModule.GetAllTypes())
            {
                if (!policy.IsBlocked(type))
                {
                    transform.HandleType(type);
                    count++;
                }
            }

            Assert.Equal(1, transform.Scopes.Count());
            var transformedTypes = transform.Scopes.Single().GetAllTypes().ToList();
            
            Assert.Equal(count, transformedTypes.Count);

            Assert.Equal(1, _systemModule.GetAllTypes().Cast<Cts.MetadataType>().Single(t => t.Name == "__ComObject").ExplicitlyImplementedInterfaces.Length);
            Assert.Equal(0, transformedTypes.Single(t => t.Name.Value == "__ComObject").Interfaces.Count);
        }
    }
}
