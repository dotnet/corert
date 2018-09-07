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
        /// <summary>
        /// CoreCLR DomainLocalModule::OffsetOfDataBlob() / sizeof(void *)
        /// </summary>
        private const int DomainLocalModuleDataBlobOffsetAsIntPtrCount = 6;

        /// <summary>
        /// CoreCLR ThreadLocalModule::OffsetOfDataBlob() / sizeof(void *)
        /// </summary>
        private const int ThreadLocalModuleDataBlobOffsetAsIntPtrCount = 3;

        private LayoutInt _initialNonGcStaticsOffset;

        public ReadyToRunMetadataFieldLayoutAlgorithm(TargetDetails target, int numberOfTypesInModule)
        {
            _initialNonGcStaticsOffset = new LayoutInt(DomainLocalModuleDataBlobOffsetAsIntPtrCount * target.PointerSize + numberOfTypesInModule);
        }

        protected override void PrepareRuntimeSpecificStaticFieldLayout(TypeSystemContext context, ref ComputedStaticFieldLayout layout)
        {
            layout.NonGcStatics.Size = _initialNonGcStaticsOffset;
            layout.GcStatics.Size = LayoutInt.Zero;
            layout.ThreadNonGcStatics.Size = LayoutInt.Zero;
            layout.ThreadGcStatics.Size = LayoutInt.Zero;
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
