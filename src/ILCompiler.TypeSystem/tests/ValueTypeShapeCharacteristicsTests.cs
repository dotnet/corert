// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Internal.TypeSystem;

using Xunit;

namespace TypeSystemTests
{
    public class ValueTypeShapeCharacteristicsTests
    {
        TestTypeSystemContext _context;
        ModuleDesc _testModule;
        DefType _singleType;
        DefType _doubleType;

        public ValueTypeShapeCharacteristicsTests()
        {
            _context = new TestTypeSystemContext(TargetArchitecture.X64);
            var systemModule = _context.CreateModuleForSimpleName("CoreTestAssembly");
            _context.SetSystemModule(systemModule);

            _testModule = systemModule;

            _singleType = _context.GetWellKnownType(WellKnownType.Single);
            _doubleType = _context.GetWellKnownType(WellKnownType.Double);
        }

        [Fact]
        public void TestHfaPrimitives()
        {
            Assert.True(_singleType.IsHfa);
            Assert.Equal(_singleType, _singleType.HfaElementType);
            
            Assert.True(_doubleType.IsHfa);
            Assert.Equal(_doubleType, _doubleType.HfaElementType);
        }

        [Fact]
        public void TestSimpleHfa()
        {
            var simpleHfaFloatStruct = _testModule.GetType("ValueTypeShapeCharacteristics", "SimpleHfaFloatStruct");
            Assert.True(simpleHfaFloatStruct.IsHfa);
            Assert.Equal(_singleType, simpleHfaFloatStruct.HfaElementType);

            var simpleHfaFloatStructWithManyFields = _testModule.GetType("ValueTypeShapeCharacteristics", "SimpleHfaFloatStructWithManyFields");
            Assert.True(simpleHfaFloatStructWithManyFields.IsHfa);
            Assert.Equal(_singleType, simpleHfaFloatStructWithManyFields.HfaElementType);

            var simpleHfaDoubleStruct = _testModule.GetType("ValueTypeShapeCharacteristics", "SimpleHfaDoubleStruct");
            Assert.True(simpleHfaDoubleStruct.IsHfa);
            Assert.Equal(_doubleType, simpleHfaDoubleStruct.HfaElementType);
        }

        [Fact]
        public void TestCompositeHfa()
        {
            var compositeHfaFloatStruct = _testModule.GetType("ValueTypeShapeCharacteristics", "CompositeHfaFloatStruct");
            Assert.True(compositeHfaFloatStruct.IsHfa);
            Assert.Equal(_singleType, compositeHfaFloatStruct.HfaElementType);

            var compositeHfaDoubleStruct = _testModule.GetType("ValueTypeShapeCharacteristics", "CompositeHfaDoubleStruct");
            Assert.True(compositeHfaDoubleStruct.IsHfa);
            Assert.Equal(_doubleType, compositeHfaDoubleStruct.HfaElementType);
        }

        [Fact]
        public void TestHfaNegative()
        {
            var nonHfaEmptyStruct = _testModule.GetType("ValueTypeShapeCharacteristics", "NonHfaEmptyStruct");
            Assert.False(nonHfaEmptyStruct.IsHfa);

            var nonHfaStruct = _testModule.GetType("ValueTypeShapeCharacteristics", "NonHfaStruct");
            Assert.False(nonHfaStruct.IsHfa);

            var nonHfaMixedStruct = _testModule.GetType("ValueTypeShapeCharacteristics", "NonHfaMixedStruct");
            Assert.False(nonHfaMixedStruct.IsHfa);

            var nonHfaCompositeStruct = _testModule.GetType("ValueTypeShapeCharacteristics", "NonHfaCompositeStruct");
            Assert.False(nonHfaCompositeStruct.IsHfa);

            var nonHfaStructWithManyFields = _testModule.GetType("ValueTypeShapeCharacteristics", "NonHfaStructWithManyFields");
            Assert.False(nonHfaStructWithManyFields.IsHfa);

            var objectType = _context.GetWellKnownType(WellKnownType.Object);
            Assert.False(objectType.IsHfa);
        }
    }
}
