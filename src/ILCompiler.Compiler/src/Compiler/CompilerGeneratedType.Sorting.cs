// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;

using Internal.TypeSystem;

namespace ILCompiler
{
    // Functionality related to determinstic ordering of types and members
    internal sealed partial class CompilerGeneratedType : MetadataType
    {
        protected override int ClassCode => -1036681447;

        protected override int CompareToImpl(TypeDesc other, TypeSystemComparer comparer)
        {
            // Should be a singleton
            throw new NotSupportedException();
        }
    }
}
