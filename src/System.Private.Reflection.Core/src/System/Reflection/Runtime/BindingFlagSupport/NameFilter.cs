// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Globalization;
using Internal.Metadata.NativeFormat;

namespace System.Reflection.Runtime.BindingFlagSupport
{
    internal abstract class NameFilter
    {
        protected NameFilter(string expectedName)
        {
            ExpectedName = expectedName;
        }

        public abstract bool Matches(string name);
        public abstract bool Matches(ConstantStringValueHandle stringHandle, MetadataReader reader);

        protected string ExpectedName { get; }
    }

    internal sealed class NameFilterCaseSensitive : NameFilter
    {
        public NameFilterCaseSensitive(string expectedName)
            : base(expectedName)
        {
        }

        public sealed override bool Matches(string name) => name.Equals(ExpectedName, StringComparison.Ordinal);
        public sealed override bool Matches(ConstantStringValueHandle stringHandle, MetadataReader reader) => stringHandle.StringEquals(ExpectedName, reader);
    }

    internal sealed class NameFilterCaseInsensitive : NameFilter
    {
        public NameFilterCaseInsensitive(string expectedName)
            : base(expectedName)
        {
        }

        public sealed override bool Matches(string name) => name.Equals(ExpectedName, StringComparison.OrdinalIgnoreCase);
        public sealed override bool Matches(ConstantStringValueHandle stringHandle, MetadataReader reader) => stringHandle.GetConstantStringValue(reader).Value.Equals(ExpectedName, StringComparison.OrdinalIgnoreCase);
    }
}

