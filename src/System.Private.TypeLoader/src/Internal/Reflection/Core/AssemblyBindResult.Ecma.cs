// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Reflection.Metadata;

namespace Internal.Reflection.Core
{
    // disable warning that indicates that the order of fields in a partial struct is not specified
    // This warning is disabled to allow the assembly bind result to have a set of fields that differ
    // based on which set of compilation directives is currently in use.
#pragma warning disable CS0282
    public partial struct AssemblyBindResult
    {
        public MetadataReader EcmaMetadataReader;
    }
#pragma warning restore CS0282
}
