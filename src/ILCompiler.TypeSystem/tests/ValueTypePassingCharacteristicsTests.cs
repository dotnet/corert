// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Internal.TypeSystem;

using Xunit;

namespace TypeSystemTests
{
    public class ValueTypePassingCharacteristicsTests
    {
        TestTypeSystemContext _context;
        ModuleDesc _testModule;
        DefType _singleType;
        DefType _doubleType;

        public ValueTypePassingCharacteristicsTests()
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
            var simpleHfaFloatStruct = _testModule.GetType("ValueTypePassingCharacteristics", "SimpleHfaFloatStruct");
            Assert.True(simpleHfaFloatStruct.IsHfa);
            Assert.Equal(_singleType, simpleHfaFloatStruct.HfaElementType);

            var simpleHfaFloatStructWithManyFields = _testModule.GetType("ValueTypePassingCharacteristics", "SimpleHfaFloatStructWithManyFields");
            Assert.True(simpleHfaFloatStructWithManyFields.IsHfa);
            Assert.Equal(_singleType, simpleHfaFloatStructWithManyFields.HfaElementType);

            var simpleHfaDoubleStruct = _testModule.GetType("ValueTypePassingCharacteristics", "SimpleHfaDoubleStruct");
            Assert.True(simpleHfaDoubleStruct.IsHfa);
            Assert.Equal(_doubleType, simpleHfaDoubleStruct.HfaElementType);
        }

        [Fact]
        public void TestCompositeHfa()
        {
            var compositeHfaFloatStruct = _testModule.GetType("ValueTypePassingCharacteristics", "CompositeHfaFloatStruct");
            Assert.True(compositeHfaFloatStruct.IsHfa);
            Assert.Equal(_singleType, compositeHfaFloatStruct.HfaElementType);

            var compositeHfaDoubleStruct = _testModule.GetType("ValueTypePassingCharacteristics", "CompositeHfaDoubleStruct");
            Assert.True(compositeHfaDoubleStruct.IsHfa);
            Assert.Equal(_doubleType, compositeHfaDoubleStruct.HfaElementType);
        }

        [Fact]
        public void TestHfaNegative()
        {
            var nonHfaEmptyStruct = _testModule.GetType("ValueTypePassingCharacteristics", "NonHfaEmptyStruct");
            Assert.False(nonHfaEmptyStruct.IsHfa);

            var nonHfaStruct = _testModule.GetType("ValueTypePassingCharacteristics", "NonHfaStruct");
            Assert.False(nonHfaStruct.IsHfa);

            var nonHfaMixedStruct = _testModule.GetType("ValueTypePassingCharacteristics", "NonHfaMixedStruct");
            Assert.False(nonHfaMixedStruct.IsHfa);

            var nonHfaCompositeStruct = _testModule.GetType("ValueTypePassingCharacteristics", "NonHfaCompositeStruct");
            Assert.False(nonHfaCompositeStruct.IsHfa);

            var nonHfaStructWithManyFields = _testModule.GetType("ValueTypePassingCharacteristics", "NonHfaStructWithManyFields");
            Assert.False(nonHfaStructWithManyFields.IsHfa);

            var objectType = _context.GetWellKnownType(WellKnownType.Object);
            Assert.False(objectType.IsHfa);
        }
    }
}
