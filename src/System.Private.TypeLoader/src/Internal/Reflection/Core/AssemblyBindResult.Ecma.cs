// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

extern alias ECMA;

using Ecma = ECMA::System.Reflection.Metadata;

namespace Internal.Reflection.Core
{
#pragma warning disable CS0282
    public partial struct AssemblyBindResult
    {
        public Ecma.MetadataReader EcmaMetadataReader;
    }
#pragma warning restore CS0282
}
