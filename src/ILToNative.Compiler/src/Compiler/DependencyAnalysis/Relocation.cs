// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ILToNative.DependencyAnalysis
{
    public enum RelocType
    {
        IMAGE_REL_BASED_DIR64 = 0x0A,
        IMAGE_REL_BASED_REL32 = 0x10,
    }
    public struct Relocation
    {
        public RelocType RelocType;
        public int Offset;
        public ISymbolNode Target;
        public int Delta;
    }
}
