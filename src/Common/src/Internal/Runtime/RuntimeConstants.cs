// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Internal.Runtime
{
    internal static class FatFunctionPointerConstants
    {
        /// <summary>
        /// Offset by which fat function pointers are shifted to distinguish them
        /// from real function pointers.
        /// </summary>
#if WASM
        // WebAssembly uses index tables, not addresses.  This is going to be contentious I imagine as it is baked into Ilc and hence an Ilc for x64 will not be able to compile Wasm.  Comments welcome.
        public const int Offset = 1 << 31;
#else
        public const int Offset = 1 << 31;
//        public const int Offset = 2;
#endif
    }

    internal static class IndirectionConstants
    {
        /// <summary>
        /// Flag set on pointers to indirection cells to distinguish them
        /// from pointers to the object directly
        /// </summary>
        public const int IndirectionCellPointer = 0x1;

        /// <summary>
        /// Flag set on RVAs to indirection cells to distinguish them
        /// from RVAs to the object directly
        /// </summary>
        public const uint RVAPointsToIndirection = 0x80000000u;
    }

    internal static class GCStaticRegionConstants
    {
        /// <summary>
        /// Flag set if the corresponding GCStatic entry has not yet been initialized and
        /// the corresponding EEType pointer has been changed into a instance pointer of
        /// that EEType.
        /// </summary>
        public const int Uninitialized = 0x1;
        
        /// <summary>
        /// Flag set if the next pointer loc points to GCStaticsPreInitDataNode.
        /// Otherise it is the next GCStatic entry.
        /// </summary>
        public const int HasPreInitializedData = 0x2; 

        public const int Mask = Uninitialized | HasPreInitializedData;
    }

    internal static class ArrayTypesConstants
    {
        /// <summary>
        /// Maximum allowable size for array element types.
        /// </summary>
        public const int MaxSizeForValueClassInArray = 0xFFFF;
    }
}
