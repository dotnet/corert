// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;

using Internal.TypeSystem.Ecma;
using Internal.TypeSystem;
using Internal.NativeFormat;

using Xunit;

namespace TypeSystemTests
{
    public class HashcodeTests
    {
        TestTypeSystemContext _context;
        EcmaModule _testModule;

        public HashcodeTests()
        {
            _context = new TestTypeSystemContext(TargetArchitecture.X64);
            var systemModule = _context.CreateModuleForSimpleName("CoreTestAssembly");
            _context.SetSystemModule(systemModule);

            _testModule = systemModule;
        }

        [Fact]
        public void TestMultidimensionalArrays()
        {
            MetadataType systemArrayType = _context.GetWellKnownType(WellKnownType.Array);
            TypeDesc objectType = _context.GetWellKnownType(WellKnownType.Object);

            ArrayType objectMDArrayRank1 = (ArrayType)_context.GetArrayType(objectType, 1);
            ArrayType objectMDArrayRank2 = (ArrayType)_context.GetArrayType(objectType, 2);
            ArrayType objectMDArrayRank3 = (ArrayType)_context.GetArrayType(objectType, 3);

            Assert.Equal(TypeHashingAlgorithms.ComputeArrayTypeHashCode(objectType.GetHashCode(), 1), objectMDArrayRank1.GetHashCode());
            Assert.Equal(TypeHashingAlgorithms.ComputeArrayTypeHashCode(objectType.GetHashCode(), 2), objectMDArrayRank2.GetHashCode());
            Assert.Equal(TypeHashingAlgorithms.ComputeArrayTypeHashCode(objectType.GetHashCode(), 3), objectMDArrayRank3.GetHashCode());
        }

        [Fact]
        public void TestSingleDimensionalArrays()
        {
            MetadataType systemArrayType = _context.GetWellKnownType(WellKnownType.Array);

            TypeDesc objectType = _context.GetWellKnownType(WellKnownType.Object);

            ArrayType objectArray = (ArrayType)_context.GetArrayType(objectType);

            Assert.Equal(TypeHashingAlgorithms.ComputeArrayTypeHashCode(objectType.GetHashCode(), 1), objectArray.GetHashCode());
        }
    }
}
