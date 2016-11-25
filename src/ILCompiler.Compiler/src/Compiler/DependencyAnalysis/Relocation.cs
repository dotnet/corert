// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Diagnostics;

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
        public readonly RelocType RelocType;
        public readonly int Offset;
        public readonly ISymbolNode Target;

        public Relocation(RelocType relocType, int offset, ISymbolNode target)
        {
            RelocType = relocType;
            Offset = offset;
            Target = target;
        }

        public unsafe static void WriteValue(RelocType relocType, void* location, long value)
        {
            switch (relocType)
            {
                case RelocType.IMAGE_REL_BASED_ABSOLUTE:
                case RelocType.IMAGE_REL_BASED_HIGHLOW:
                case RelocType.IMAGE_REL_BASED_REL32:
                    *(int*)location = (int)value;
                    break;
                case RelocType.IMAGE_REL_BASED_DIR64:
                    *(long*)location = value;
                    break;
                default:
                    Debug.Fail("Invalid RelocType: " + relocType);
                    break;
            }
        }

        public unsafe static long ReadValue(RelocType relocType, void* location)
        {
            switch (relocType)
            {
                case RelocType.IMAGE_REL_BASED_ABSOLUTE:
                case RelocType.IMAGE_REL_BASED_HIGHLOW:
                case RelocType.IMAGE_REL_BASED_REL32:
                    return *(int*)location;
                case RelocType.IMAGE_REL_BASED_DIR64:
                    return *(long*)location;
                default:
                    Debug.Fail("Invalid RelocType: " + relocType);
                    return 0;
            }
        }
    }
}
