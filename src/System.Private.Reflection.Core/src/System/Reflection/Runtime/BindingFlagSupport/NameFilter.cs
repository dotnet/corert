// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

extern alias ECMA;

using System.Globalization;
using NativeFormat=Internal.Metadata.NativeFormat;
using Ecma = ECMA::System.Reflection.Metadata;


namespace System.Reflection.Runtime.BindingFlagSupport
{
    internal abstract class NameFilter
    {
        protected NameFilter(string expectedName)
        {
            ExpectedName = expectedName;
        }

        public abstract bool Matches(string name);
        public abstract bool Matches(NativeFormat.ConstantStringValueHandle stringHandle, NativeFormat.MetadataReader reader);
        public abstract bool Matches(Ecma.StringHandle stringHandle, Ecma.MetadataReader reader);

        protected string ExpectedName { get; }
    }

    internal sealed class NameFilterCaseSensitive : NameFilter
    {
        public NameFilterCaseSensitive(string expectedName)
            : base(expectedName)
        {
        }

        public sealed override bool Matches(string name) => name.Equals(ExpectedName, StringComparison.Ordinal);
        public sealed override bool Matches(NativeFormat.ConstantStringValueHandle stringHandle, NativeFormat.MetadataReader reader) => stringHandle.StringEquals(ExpectedName, reader);
        public sealed override bool Matches(Ecma.StringHandle stringHandle, Ecma.MetadataReader reader) => reader.StringComparer.Equals(stringHandle, ExpectedName, false);
    }

    internal sealed class NameFilterCaseInsensitive : NameFilter
    {
        public NameFilterCaseInsensitive(string expectedName)
            : base(expectedName)
        {
        }

        public sealed override bool Matches(string name) => name.Equals(ExpectedName, StringComparison.OrdinalIgnoreCase);
        public sealed override bool Matches(NativeFormat.ConstantStringValueHandle stringHandle, NativeFormat.MetadataReader reader) => stringHandle.GetConstantStringValue(reader).Value.Equals(ExpectedName, StringComparison.OrdinalIgnoreCase);
        public sealed override bool Matches(Ecma.StringHandle stringHandle, Ecma.MetadataReader reader) => reader.StringComparer.Equals(stringHandle, ExpectedName, true);
    }
}

