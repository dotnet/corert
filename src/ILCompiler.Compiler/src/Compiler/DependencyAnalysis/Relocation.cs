// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ILCompiler.DependencyAnalysis
{
    public enum RelocType
    {
        IMAGE_REL_BASED_ABSOLUTE = 0x00,
        IMAGE_REL_BASED_HIGHLOW = 0x03,
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
