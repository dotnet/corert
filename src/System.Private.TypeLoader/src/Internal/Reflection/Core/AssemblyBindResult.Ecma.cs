// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Reflection.Metadata;

namespace Internal.Reflection.Core
{
    // Auto StructLayout used to suppress warning that order of fields is not garaunteed in partial structs
    [StructLayout(LayoutKind.Auto)]
    public partial struct AssemblyBindResult
    {
        public MetadataReader EcmaMetadataReader;
    }
}
