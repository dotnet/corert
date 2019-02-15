// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Internal.TypeSystem;
using Internal.TypeSystem.Ecma;

namespace ILCompiler
{
    public class WebAssemblyMetadataBlockingPolicy: MetadataBlockingPolicy
    {
        public override bool IsBlocked(MetadataType type)
        {
            return !(type is EcmaType);
        }

        public override bool IsBlocked(MethodDesc method)
        {
            return true;
        }

        public override bool IsBlocked(FieldDesc field)
        {
            return false;
        }
    }
}
