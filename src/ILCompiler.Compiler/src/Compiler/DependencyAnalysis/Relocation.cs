// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Diagnostics;

namespace ILCompiler.DependencyAnalysis
{
    public enum RelocType
    {
        IMAGE_REL_BASED_ABSOLUTE        = 0x00,     // No relocation required
        IMAGE_REL_BASED_ADDR32NB        = 0x02,     // The 32-bit address without an image base (RVA)
        IMAGE_REL_BASED_HIGHLOW         = 0x03,     // 32 bit address base
        IMAGE_REL_BASED_THUMB_MOV32     = 0x07,     // Thumb2: based MOVW/MOVT
        IMAGE_REL_BASED_DIR64           = 0x0A,     // 64 bit address base
        IMAGE_REL_BASED_REL32           = 0x10,     // 32-bit relative address from byte following reloc
        IMAGE_REL_BASED_THUMB_BRANCH24  = 0x13,     // Thumb2: based B, BL
        IMAGE_REL_BASED_RELPTR32        = 0x7C,     // 32-bit relative address from byte starting reloc
                                                    // This is a special NGEN-specific relocation type 
                                                    // for relative pointer (used to make NGen relocation 
                                                    // section smaller)    
        IMAGE_REL_SECREL                = 0x80,     // 32 bit offset from base of section containing target
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

        public static unsafe void WriteValue(RelocType relocType, void* location, long value)
        {
            switch (relocType)
            {
                case RelocType.IMAGE_REL_BASED_ABSOLUTE:
                case RelocType.IMAGE_REL_BASED_HIGHLOW:
                case RelocType.IMAGE_REL_BASED_REL32:
                case RelocType.IMAGE_REL_BASED_ADDR32NB:
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

        public static unsafe long ReadValue(RelocType relocType, void* location)
        {
            switch (relocType)
            {
                case RelocType.IMAGE_REL_BASED_ABSOLUTE:
                case RelocType.IMAGE_REL_BASED_ADDR32NB:
                case RelocType.IMAGE_REL_BASED_HIGHLOW:
                case RelocType.IMAGE_REL_BASED_REL32:
                case RelocType.IMAGE_REL_BASED_RELPTR32:
                case RelocType.IMAGE_REL_SECREL:
                    return *(int*)location;
                case RelocType.IMAGE_REL_BASED_DIR64:
                    return *(long*)location;
                default:
                    Debug.Fail("Invalid RelocType: " + relocType);
                    return 0;
            }
        }

        public override string ToString()
        {
            return $"{Target} ({RelocType}, 0x{Offset:X})";
        }
    }
}
