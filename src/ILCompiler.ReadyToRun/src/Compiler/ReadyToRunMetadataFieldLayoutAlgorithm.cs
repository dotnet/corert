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

        /// <summary>
        /// In accordance with the algorithm in
        /// <a href="https://github.com/dotnet/coreclr/blob/master/src/vm/ceeload.cpp">Module::BuildStaticsOffsets</a>
        /// we need to take the number of type definitions within the MSIL module into account
        /// when calculating the statics base
        /// </summary>
        private LayoutInt _initialNonGcStaticsOffset;

        /// <summary>
        /// The algorithm for laying out non-GC TLS is basically the same as for normal statics except for
        /// the additive constant representing the DataBlobOffset in the respective CoreCLR structures.
        /// </summary>
        private LayoutInt _initialNonGcThreadStaticsOffset;

        public ReadyToRunMetadataFieldLayoutAlgorithm(TargetDetails target, int numberOfTypesInModule)
        {
            _initialNonGcStaticsOffset = new LayoutInt(DomainLocalModuleDataBlobOffsetAsIntPtrCount * target.PointerSize + numberOfTypesInModule);
            _initialNonGcThreadStaticsOffset = new LayoutInt(ThreadLocalModuleDataBlobOffsetAsIntPtrCount * target.PointerSize + numberOfTypesInModule);
        }

        protected override void PrepareRuntimeSpecificStaticFieldLayout(TypeSystemContext context, ref ComputedStaticFieldLayout layout)
        {
            layout.NonGcStatics.Size = _initialNonGcStaticsOffset;
            layout.GcStatics.Size = LayoutInt.Zero;
            layout.ThreadNonGcStatics.Size = _initialNonGcThreadStaticsOffset;
            layout.ThreadGcStatics.Size = LayoutInt.Zero;
        }

        protected override void FinalizeRuntimeSpecificStaticFieldLayout(TypeSystemContext context, ref ComputedStaticFieldLayout layout)
        {
            if (layout.NonGcStatics.Size == _initialNonGcStaticsOffset)
            {
                // No non-GC statics, set statics size to 0
                layout.NonGcStatics.Size = LayoutInt.Zero;
            }
            if (layout.ThreadNonGcStatics.Size == _initialNonGcThreadStaticsOffset)
            {
                // No non-GC thread-local statics, set statics size to 0
                layout.ThreadNonGcStatics.Size = LayoutInt.Zero;
            }
        }
    }
}
