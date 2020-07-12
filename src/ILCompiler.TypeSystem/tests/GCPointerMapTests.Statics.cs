// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Internal.TypeSystem;

using Xunit;

namespace TypeSystemTests
{
    public partial class GCPointerMapTests
    {
        [Fact]
        public void TestStaticMap()
        {
            MetadataType mixedStaticClass = _testModule.GetType("GCPointerMap", "MixedStaticClass");
            var map = GCPointerMap.FromStaticLayout(mixedStaticClass);
            Assert.Equal(12, map.Size);
            Assert.Equal("010100101001", map.ToString());
        }

        [Fact]
        public void TestThreadStaticMap()
        {
            MetadataType mixedThreadStaticClass = _testModule.GetType("GCPointerMap", "MixedThreadStaticClass");
            var map = GCPointerMap.FromThreadStaticLayout(mixedThreadStaticClass);
            Assert.Equal(14, map.Size);
            Assert.Equal("00010010100110", map.ToString());
        }
    }
}
