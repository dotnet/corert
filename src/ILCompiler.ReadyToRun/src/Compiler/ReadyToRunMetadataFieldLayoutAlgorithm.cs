// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;

using Internal.TypeSystem;

namespace ILCompiler
{
    internal class ReadyToRunMetadataFieldLayoutAlgorithm : MetadataFieldLayoutAlgorithm
    {
        private const int DomainLocalModuleDataBlobOffset = 0x30;

        private const int ThreadLocalModuleDataBlobOffset = 0x18;

        private LayoutInt _initialNonGcStaticsOffset;

        public ReadyToRunMetadataFieldLayoutAlgorithm(int numberOfTypesInModule)
        {
            _initialNonGcStaticsOffset = new LayoutInt(DomainLocalModuleDataBlobOffset + numberOfTypesInModule);
        }

        protected override void PrepareRuntimeSpecificStaticFieldLayout(TypeSystemContext context, ref ComputedStaticFieldLayout layout)
        {
            layout.NonGcStatics.Size = _initialNonGcStaticsOffset;
            layout.GcStatics.Size = LayoutInt.Zero;
            layout.ThreadStatics.Size = LayoutInt.Zero;
        }

        protected override void FinalizeRuntimeSpecificStaticFieldLayout(TypeSystemContext context, ref ComputedStaticFieldLayout layout)
        {
            if (layout.NonGcStatics.Size == _initialNonGcStaticsOffset)
            {
                // No non-GC statics, set statics size to 0
                layout.NonGcStatics.Size = LayoutInt.Zero;
            }
        }
    }
}
