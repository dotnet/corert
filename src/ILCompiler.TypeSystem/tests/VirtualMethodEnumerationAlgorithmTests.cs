// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;

using Internal.TypeSystem;

using Xunit;


namespace TypeSystemTests
{
    public class VirtualMethodEnumerationAlgorithmTests
    {
        TestTypeSystemContext _context;
        ModuleDesc _testModule;
        MetadataType _testType;

        public VirtualMethodEnumerationAlgorithmTests()
        {
            _context = new TestTypeSystemContext(TargetArchitecture.Unknown);
            var systemModule = _context.CreateModuleForSimpleName("CoreTestAssembly");
            _context.SetSystemModule(systemModule);

            _testModule = systemModule;

            _testType = _testModule.GetType("VirtualFunctionOverride", "SimpleGeneric`1")
                .MakeInstantiatedType(_context.GetWellKnownType(WellKnownType.Object));
        }

        [Fact]
        public void TestMetadataEnumerationAlgorithm()
        {
            var algo = new MetadataVirtualMethodEnumerationAlgorithm();

            DefType objectType = _context.GetWellKnownType(WellKnownType.Object);
            var objectMethods = algo.ComputeAllVirtualMethods(objectType).ToArray();
            Assert.Equal(4, objectMethods.Length);
            Assert.Superset(new HashSet<string> { "Equals", "GetHashCode", "ToString", "Finalize" },
                new HashSet<string>(objectMethods.Select(m => m.Name)));

            var testTypeMethods = algo.ComputeAllVirtualMethods(_testType).ToArray();
            Assert.Equal(1, testTypeMethods.Length);
            Assert.Equal("ToString", testTypeMethods[0].Name);
            Assert.Equal(_testType, testTypeMethods[0].OwningType);
        }

        [Fact]
        public void TestCachingEnumerationAlgorithm()
        {
            var algo = new CachingVirtualMethodEnumerationAlgorithm();

            DefType objectType = _context.GetWellKnownType(WellKnownType.Object);
            var objectMethods = algo.ComputeAllVirtualMethods(objectType).ToArray();
            Assert.Equal(4, objectMethods.Length);
            Assert.Superset(new HashSet<string> { "Equals", "GetHashCode", "ToString", "Finalize" },
                new HashSet<string>(objectMethods.Select(m => m.Name)));

            var testTypeMethods = algo.ComputeAllVirtualMethods(_testType).ToArray();
            Assert.Equal(1, testTypeMethods.Length);
            Assert.Equal("ToString", testTypeMethods[0].Name);
            Assert.Equal(_testType, testTypeMethods[0].OwningType);
        }
    }
}
