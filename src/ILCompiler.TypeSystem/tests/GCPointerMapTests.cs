﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Internal.TypeSystem;

using Xunit;

namespace TypeSystemTests
{
    public class GCPointerMapTests
    {
        TestTypeSystemContext _context;
        ModuleDesc _testModule;

        public GCPointerMapTests()
        {
            _context = new TestTypeSystemContext(TargetArchitecture.X86);
            var systemModule = _context.CreateModuleForSimpleName("CoreTestAssembly");
            _context.SetSystemModule(systemModule);

            _testModule = systemModule;
        }

        [Fact]
        public void TestInstanceMap()
        {
            MetadataType classWithArrayFields = _testModule.GetType("GCPointerMap", "ClassWithArrayFields");
            MetadataType classWithStringField = _testModule.GetType("GCPointerMap", "ClassWithStringField");
            MetadataType mixedStruct = _testModule.GetType("GCPointerMap", "MixedStruct");
            MetadataType structWithSameGCLayoutAsMixedStruct = _testModule.GetType("GCPointerMap", "StructWithSameGCLayoutAsMixedStruct");
            MetadataType doubleMixedStructLayout = _testModule.GetType("GCPointerMap", "DoubleMixedStructLayout");
            MetadataType explicitlyFarPointer = _testModule.GetType("GCPointerMap", "ExplicitlyFarPointer");

            {
                var map = GCPointerMap.FromInstanceLayout(classWithArrayFields);
                Assert.Equal(map.Size, 3);
                Assert.Equal("011", map.ToString());
            }

            {
                var map = GCPointerMap.FromInstanceLayout(classWithStringField);
                Assert.Equal(map.Size, 3);
                Assert.Equal("001", map.ToString());
            }

            {
                var map = GCPointerMap.FromInstanceLayout(mixedStruct);
                Assert.Equal(map.Size, 6);
                Assert.Equal("001001", map.ToString());
            }

            {
                var map1 = GCPointerMap.FromInstanceLayout(mixedStruct);
                var map2 = GCPointerMap.FromInstanceLayout(structWithSameGCLayoutAsMixedStruct);
                Assert.Equal(map1.Size, map2.Size);
                Assert.Equal(map1.ToString(), map2.ToString());
            }

            {
                var map = GCPointerMap.FromInstanceLayout(doubleMixedStructLayout);
                Assert.Equal(map.Size, 11);
                Assert.Equal("00100101001", map.ToString());
            }

            {
                var map = GCPointerMap.FromInstanceLayout(explicitlyFarPointer);
                Assert.Equal(map.Size, 118);
                Assert.Equal("0100000000000000000000000000000000000000000000000000000000000000010000000000000001000000000000000000000000000000001001", map.ToString());
            }
        }
    }
}
