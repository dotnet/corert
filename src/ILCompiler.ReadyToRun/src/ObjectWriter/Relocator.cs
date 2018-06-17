// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Reflection.PortableExecutable;

namespace ILCompiler.PEWriter
{
    public static class RelocType
    {
        /// <summary>
        /// No relocation required
        /// </summary>
        public const ushort IMAGE_REL_BASED_ABSOLUTE = 0x00;

        /// <summary>
        /// The 32-bit address without an image base (RVA)
        /// </summary>
        public const ushort IMAGE_REL_BASED_ADDR32NB = 0x02;

        /// <summary>
        /// 32 bit address base
        /// </summary>
        public const ushort IMAGE_REL_BASED_HIGHLOW = 0x03;

        /// <summary>
        /// Thumb2: based MOVW/MOVT
        /// </summary>
        public const ushort IMAGE_REL_BASED_THUMB_MOV32 = 0x07;

        /// <summary>
        /// 64 bit address base
        /// </summary>
        public const ushort IMAGE_REL_BASED_DIR64 = 0x0A;

        /// <summary>
        /// 32-bit relative address from byte following reloc
        /// </summary>
        public const ushort IMAGE_REL_BASED_REL32 = 0x10;

        /// <summary>
        /// Thumb2: based B, BL
        /// </summary>
        public const ushort IMAGE_REL_BASED_THUMB_BRANCH24 = 0x13;

        /// <summary>
        /// Arm64: B, BL
        /// </summary>
        public const ushort IMAGE_REL_BASED_ARM64_BRANCH26 = 0x14;

        /// <summary>
        /// 32-bit relative address from byte starting reloc
        /// This is a special NGEN-specific relocation type
        /// for relative pointer (used to make NGen relocation
        /// section smaller)
        /// </summary>
        public const ushort IMAGE_REL_BASED_RELPTR32 = 0x7C;

        /// <summary>
        /// 32 bit offset from base of section containing target
        /// </summary>
        public const ushort IMAGE_REL_SECREL = 0x80;

        /// <summary>
        /// ADRP
        /// </summary>
        public const ushort IMAGE_REL_BASED_ARM64_PAGEBASE_REL21 = 0x81;

        /// <summary>
        /// ADD/ADDS (immediate) with zero shift, for page offset
        /// </summary>
        public const ushort IMAGE_REL_BASED_ARM64_PAGEOFFSET_12A = 0x82;

        /// <summary>
        /// LDR (indexed, unsigned immediate), for page offset
        /// </summary>
        public const ushort IMAGE_REL_BASED_ARM64_PAGEOFFSET_12L = 0x83;
    }

    /// <summary>
    /// Abstract class representing a relocator i.e. an algorithm capable of
    /// modifying a sequence of bytes by applying the given relocation type
    /// with RVA and delta parameters.
    /// </summary>
    public abstract class Relocator
    {
        /// <summary>
        /// Retrieve the relocator for a given relocation type.
        /// </summary>
        /// <param name="relocationType">Relocation type</param>
        /// <returns></returns>
        public static Relocator GetRelocator(ushort relocationType)
        {
            switch (relocationType)
            {
                case RelocType.IMAGE_REL_BASED_ABSOLUTE:
                case RelocType.IMAGE_REL_BASED_HIGHLOW:
                    // TODO: ADDR32NB doesn't create a file-level relocation record while
                    // the other three types do.
                case RelocType.IMAGE_REL_BASED_ADDR32NB:
                    return Absolute32.Instance;

                case RelocType.IMAGE_REL_BASED_REL32:
                    return Relative32.Instance;

                case RelocType.IMAGE_REL_BASED_DIR64:
                    // TODO: file-level relocation record
                    return Absolute64.Instance;

                case RelocType.IMAGE_REL_BASED_THUMB_MOV32:
                    return ThumbMov32.Instance;

                case RelocType.IMAGE_REL_BASED_THUMB_BRANCH24:
                    return ThumbBranch24.Instance;
/*
    
                case RelocType.IMAGE_REL_BASED_ARM64_BRANCH26:
                case RelocType.IMAGE_REL_BASED_RELPTR32:
                case RelocType.IMAGE_REL_SECREL:

                case RelocType.IMAGE_REL_BASED_ARM64_PAGEBASE_REL21:
                case RelocType.IMAGE_REL_BASED_ARM64_PAGEOFFSET_12A:
                case RelocType.IMAGE_REL_BASED_ARM64_PAGEOFFSET_12L:
*/

                default:
                    throw new NotSupportedException();
            }
        }

        /// <summary>
        /// Return file relocation type for the given relocation type. If the relocation
        /// doesn't require a file-level relocation entry in the .reloc section, 0 is returned
        /// corresponding to the IMAGE_REL_BASED_ABSOLUTE no-op relocation record.
        /// </summary>
        /// <param name="relocationType">Relocation type</param>
        /// <returns>File-level relocation type or 0 (IMAGE_REL_BASED_ABSOLUTE) if none is required</returns>
        public static ushort GetFileRelocationType(ushort relocationType)
        {
            switch (relocationType)
            {
                case RelocType.IMAGE_REL_BASED_HIGHLOW:
                case RelocType.IMAGE_REL_BASED_DIR64:
                case RelocType.IMAGE_REL_BASED_THUMB_MOV32:
                    return relocationType;
                    
                default:
                    return RelocType.IMAGE_REL_BASED_ABSOLUTE;
            }
        }

        /// <summary>
        /// Number of bytes this relocator needs to process
        /// </summary>
        /// <returns></returns>
        abstract public int GetLength();

        /// <summary>
        /// Relocate a given byte buffer for a given source / target RVA pair.
        /// </summary>
        /// <param name="bytes">Byte buffer of the required length representing the data to relocate</param>
        /// <param name="sourceRVA">RVA corresponding to the byte buffer being relocated</param>
        /// <param name="targetRVA">RVA corresponding to the relocation target</param>
        /// <param name="defaultImageBase">Default image load address</param>
        abstract public void Relocate(byte[] bytes, int sourceRVA, int targetRVA, ulong defaultImageBase);

        /// <summary>
        /// 32-bit absolute address relocator.
        /// </summary>
        private sealed class Absolute32 : Relocator
        {
            public static readonly Relocator Instance = new Absolute32();
    
            /// <summary>
            /// Number of bytes this relocator needs to process
            /// </summary>
            /// <returns></returns>
            override public int GetLength()
            {
                return 4;
            }
    
            /// <summary>
            /// Relocate a given byte buffer for a given source / target RVA pair.
            /// </summary>
            /// <param name="bytes">Byte buffer of the required length representing the data to relocate</param>
            /// <param name="sourceRVA">RVA corresponding to the byte buffer being relocated</param>
            /// <param name="targetRVA">RVA corresponding to the relocation target</param>
            /// <param name="defaultImageBase">Default image load address</param>
            override public void Relocate(byte[] bytes, int sourceRVA, int targetRVA, ulong defaultImageBase)
            {
                int location = unchecked(BitConverter.ToInt32(bytes, 0) + targetRVA + (int)defaultImageBase);
                WriteInt32(location, bytes, 0);
            }
        }

        /// <summary>
        /// RVA difference.
        /// </summary>
        private sealed class Relative32 : Relocator
        {
            public static readonly Relocator Instance = new Relative32();

            /// <summary>
            /// Number of bytes this relocator needs to process
            /// </summary>
            /// <returns></returns>
            override public int GetLength()
            {
                return 4;
            }
    
            /// <summary>
            /// Relocate a given byte buffer for a given source / target RVA pair.
            /// </summary>
            /// <param name="bytes">Byte buffer of the required length representing the data to relocate</param>
            /// <param name="sourceRVA">RVA corresponding to the byte buffer being relocated</param>
            /// <param name="targetRVA">RVA corresponding to the relocation target</param>
            /// <param name="defaultImageBase">Default image load address</param>
            override public void Relocate(byte[] bytes, int sourceRVA, int targetRVA, ulong defaultImageBase)
            {
                int location = BitConverter.ToInt32(bytes, 0) + (targetRVA - sourceRVA);
                WriteInt32(location, bytes, 0);
            }
        }
    
        /// <summary>
        /// 64-bit absolute address relocator.
        /// </summary>
        private sealed class Absolute64 : Relocator
        {
            public static readonly Relocator Instance = new Absolute64();
    
            /// <summary>
            /// Number of bytes this relocator needs to process
            /// </summary>
            /// <returns></returns>
            override public int GetLength()
            {
                return 8;
            }
    
            /// <summary>
            /// Relocate a given byte buffer for a given source / target RVA pair.
            /// </summary>
            /// <param name="bytes">Byte buffer of at least the required length representing the data to relocate</param>
            /// <param name="sourceRVA">RVA corresponding to the byte buffer being relocated</param>
            /// <param name="targetRVA">RVA corresponding to the relocation target</param>
            /// <param name="defaultImageBase">Default image load address</param>
            override public void Relocate(byte[] bytes, int sourceRVA, int targetRVA, ulong defaultImageBase)
            {
                long location = unchecked(BitConverter.ToInt64(bytes, 0) + targetRVA + (long)defaultImageBase);
                WriteInt64(location, bytes, 0);
            }
        }

        /// <summary>
        /// Thumb32 MOVW / MOVT instruction pair
        /// </summary>
        private sealed class ThumbMov32 : Relocator
        {
            public static readonly Relocator Instance = new Relative32();

            /// <summary>
            /// Number of bytes this relocator needs to process
            /// </summary>
            /// <returns></returns>
            override public int GetLength()
            {
                return 8;
            }
    
            /// <summary>
            /// Relocate a given byte buffer for a given source / target RVA pair.
            /// </summary>
            /// <param name="bytes">Byte buffer of the required length representing the data to relocate</param>
            /// <param name="sourceRVA">RVA corresponding to the byte buffer being relocated</param>
            /// <param name="targetRVA">RVA corresponding to the relocation target</param>
            /// <param name="defaultImageBase">Default image load address</param>
            override public void Relocate(byte[] bytes, int sourceRVA, int targetRVA, ulong defaultImageBase)
            {
                int location = GetThumb2Mov32(bytes);
                location = unchecked(location + targetRVA + (int)defaultImageBase);
                
                PutThumb2Imm16(unchecked((ushort)location), bytes, 0);
                PutThumb2Imm16(unchecked((ushort)(location >> 16)), bytes, 4);
    
                Debug.Assert((uint)GetThumb2Mov32(bytes) == location);
            }

            /// <summary>
            /// Patch a MOVW / MOVT Thumb2 instruction by updating its 16-bit immediate operand to imm16.
            /// </summary>
            /// <param name="int16">Immediate 16-bit operand to inject into the instruction</param>
            /// <param name="bytes">Byte array containing the instruction to patch</param>
            /// <param name="offset">Offset of the MOVW / MOVT instruction</param>
            private static void PutThumb2Imm16(ushort imm16, byte[] bytes, int offset)
            {
                const ushort Mask1 = 0xf000;
                const ushort Val1 = (Mask1 >> 12);
                const ushort Mask2 = 0x0800;
                const ushort Val2 = (Mask2 >> 1);
                const ushort Mask3 = 0x0700;
                const ushort Val3 = (Mask3 << 4);
                const ushort Mask4 = 0x00ff;
                const ushort Val4 = (Mask4 << 0);
                const ushort Val = Val1 | Val2 | Val3 | Val4;

                ushort opcode0 = BitConverter.ToUInt16(bytes, offset);
                ushort opcode1 = BitConverter.ToUInt16(bytes, offset + 2);

                opcode0 &= unchecked((ushort)~Val);
                opcode0 |= unchecked((ushort)(((imm16 & Mask1) >> 12) | ((imm16 & Mask2) >> 1) | ((imm16 & Mask3) << 4) | ((imm16 & Mask4) << 0)));

                WriteUInt16(opcode0, bytes, offset);
                WriteUInt16(opcode1, bytes, offset + 2);
            }

            /// <summary>
            /// Decode the 32-bit immediate operand from a MOVW / MOVT instruction pair (8 bytes total).
            /// </summary>
            /// <param name="bytes">Byte array containing the 8-byte sequence MOVW - MOVT</param>
            private static int GetThumb2Mov32(byte[] bytes)
            {
                Debug.Assert(((uint)BitConverter.ToUInt16(bytes, 0) & 0xFBF0) == 0xF240);
                Debug.Assert(((uint)BitConverter.ToUInt16(bytes, 4) & 0xFBF0) == 0xF2C0);

                return (int)GetThumb2Imm16(bytes, 0) + ((int)(GetThumb2Imm16(bytes, 4) << 16));
            }
            
            /// <summary>
            /// Decode the 16-bit immediate operand from a MOVW / MOVT instruction.
            /// </summary>
            private static ushort GetThumb2Imm16(byte[] bytes, int offset)
            {
                uint opcode0 = BitConverter.ToUInt16(bytes, offset);
                uint opcode1 = BitConverter.ToUInt16(bytes, offset + 2);
                uint result =
                    ((opcode0 << 12) & 0xf000) |
                    ((opcode0 <<  1) & 0x0800) |
                    ((opcode1 >>  4) & 0x0700) |
                    ((opcode1 >>  0) & 0x00ff);
                return (ushort)result;
            }
        }
    
        /// <summary>
        /// Thumb 24-bit B, BL relocator.
        /// </summary>
        private sealed class ThumbBranch24 : Relocator
        {
            public static readonly Relocator Instance = new Absolute64();
    
            /// <summary>
            /// Number of bytes this relocator needs to process
            /// </summary>
            /// <returns></returns>
            override public int GetLength()
            {
                return 4;
            }
    
            /// <summary>
            /// Relocate a given byte buffer for a given source / target RVA pair.
            /// </summary>
            /// <param name="bytes">Byte buffer of at least the required length representing the data to relocate</param>
            /// <param name="sourceRVA">RVA corresponding to the byte buffer being relocated</param>
            /// <param name="targetRVA">RVA corresponding to the relocation target</param>
            /// <param name="defaultImageBase">Default image load address</param>
            override public void Relocate(byte[] bytes, int sourceRVA, int targetRVA, ulong defaultImageBase)
            {
                // Target location is relative to the byte after the branch instruction
                int location = GetThumb2BlRel24(bytes, 0) + targetRVA - sourceRVA - 4;
                
                PutThumb2BlRel24(location, bytes, 0);
            }

            /// <summary>
            /// Extract the 24-bit rel offset from bl instruction
            /// </summary>
            /// <param name="bytes">Byte buffer containing the instruction to analyze</param>
            /// <param name="offset">Offset of the instruction within the buffer</param>
            private static unsafe int GetThumb2BlRel24(byte[] bytes, int offset)
            {
                uint opcode0 = BitConverter.ToUInt16(bytes, offset + 0);
                uint opcode1 = BitConverter.ToUInt16(bytes, offset + 2);
    
                uint s  = opcode0 >> 10;
                uint j2 = opcode1 >> 11;
                uint j1 = opcode1 >> 13;
    
                uint ret =
                    ((s << 24)              & 0x1000000) |
                    (((j1 ^ s ^ 1) << 23)   & 0x0800000) |
                    (((j2 ^ s ^ 1) << 22)   & 0x0400000) |
                    ((opcode0 << 12)        & 0x03FF000) |
                    ((opcode1 <<  1)        & 0x0000FFE);
    
                // Sign-extend and return
                return (int)((ret << 7) >> 7);
            }
    
            /// <summary>
            /// Returns whether the offset fits into bl instruction
            /// </summary>
            /// <param name="imm24">Immediate operand to check.</param>
            private static bool FitsInThumb2BlRel24(int imm24)
            {
                return ((imm24 << 7) >> 7) == imm24;
            }
    
            /// <summary>
            /// Deposit the 24-bit rel offset into bl instruction
            /// </summary>
            /// <param name="imm24">Immediate operand to inject into the instruction</param>
            /// <param name="bytes">Byte buffer containing the BL instruction to patch</param>
            /// <param name="offset">Offset of the instruction within the buffer</param>
            private static void PutThumb2BlRel24(int imm24, byte[] bytes, int offset)
            {
                // Verify that we got a valid offset
                Debug.Assert(FitsInThumb2BlRel24(imm24));
    
                // Ensure that the ThumbBit is not set on the offset
                // as it cannot be encoded.
                Debug.Assert((imm24 & 1/*THUMB_CODE*/) == 0);
    
                ushort opcode0 = BitConverter.ToUInt16(bytes, 0);
                ushort opcode1 = BitConverter.ToUInt16(bytes, 2);
                opcode0 &= 0xF800;
                opcode1 &= 0xD000;
    
                uint s  =  (unchecked((uint)imm24) & 0x1000000) >> 24;
                uint j1 = ((unchecked((uint)imm24) & 0x0800000) >> 23) ^ s ^ 1;
                uint j2 = ((unchecked((uint)imm24) & 0x0400000) >> 22) ^ s ^ 1;
    
                opcode0 |= (ushort)(((unchecked((uint)imm24) & 0x03FF000) >> 12) | (s << 10));
                opcode1 |= (ushort)(((unchecked((uint)imm24) & 0x0000FFE) >>  1) | (j1 << 13) | (j2 << 11));
    
                WriteUInt16(opcode0, bytes, offset + 0);
                WriteUInt16(opcode1, bytes, offset + 2);
    
                Debug.Assert(GetThumb2BlRel24(bytes, 0) == imm24);
            }
        }

        /// <summary>
        /// Helper to write 16-bit value to a byte array.
        /// </summary>
        /// <param name="value">Value to write to the byte array</param>
        /// <param name="bytes">Target byte array</param>
        /// <param name="offset">Offset in the array</param>
        static void WriteUInt16(ushort value, byte[] bytes, int offset)
        {
            bytes[offset + 0] = unchecked((byte)value);
            bytes[offset + 1] = (byte)(value >> 8);
        }

        /// <summary>
        /// Helper to write 32-bit value to a byte array.
        /// </summary>
        /// <param name="value">Value to write to the byte array</param>
        /// <param name="bytes">Target byte array</param>
        /// <param name="offset">Offset in the array</param>
        static void WriteUInt32(uint value, byte[] bytes, int offset)
        {
            bytes[offset + 0] = unchecked((byte)(value >> 0));
            bytes[offset + 1] = unchecked((byte)(value >> 8));
            bytes[offset + 2] = unchecked((byte)(value >> 16));
            bytes[offset + 3] = unchecked((byte)(value >> 24));
        }

        /// <summary>
        /// We use the same byte encoding for signed and unsigned 32-bit values
        /// so this method just forwards to WriteUInt32.
        /// </summary>
        /// <param name="value">Value to write to the byte array</param>
        /// <param name="bytes">Target byte array</param>
        /// <param name="offset">Offset in the array</param>
        static void WriteInt32(int value, byte[] bytes, int offset)
        {
            WriteUInt32(unchecked((uint)value), bytes, offset);
        }

        /// <summary>
        /// Helper to write 64-bit value to a byte array.
        /// </summary>
        /// <param name="value">Value to write to the byte array</param>
        /// <param name="bytes">Target byte array</param>
        /// <param name="offset">Offset in the array</param>
        static void WriteUInt64(ulong value, byte[] bytes, int offset)
        {
            bytes[offset + 0] = unchecked((byte)(value >> 0));
            bytes[offset + 1] = unchecked((byte)(value >> 8));
            bytes[offset + 2] = unchecked((byte)(value >> 16));
            bytes[offset + 3] = unchecked((byte)(value >> 24));
            bytes[offset + 4] = unchecked((byte)(value >> 32));
            bytes[offset + 5] = unchecked((byte)(value >> 40));
            bytes[offset + 6] = unchecked((byte)(value >> 48));
            bytes[offset + 7] = unchecked((byte)(value >> 56));
        }

        /// <summary>
        /// We use the same byte encoding for signed and unsigned 64-bit values
        /// so this method just forwards to WriteUInt64.
        /// </summary>
        /// <param name="value">Value to write to the byte array</param>
        /// <param name="bytes">Target byte array</param>
        /// <param name="offset">Offset in the array</param>
        static void WriteInt64(long value, byte[] bytes, int offset)
        {
            WriteUInt64(unchecked((ulong)value), bytes, offset);
        }
    }



    /*
            public static unsafe void WriteValue(RelocType relocType, void* location, long value)
            {
                switch (relocType)
                {
                    case RelocType.IMAGE_REL_BASED_THUMB_BRANCH24:
                        PutThumb2BlRel24((ushort*)location, (uint)value);
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
                    case RelocType.IMAGE_REL_SECREL:
                        return *(int*)location;
                    case RelocType.IMAGE_REL_BASED_THUMB_BRANCH24:
                        return (long)GetThumb2BlRel24((ushort*)location);
                    default:
                        Debug.Fail("Invalid RelocType: " + relocType);
                        return 0;
                }
            }

            public override string ToString()
            {
                return $"{Target} ({RelocType}, 0x{Offset:X})";
            }
    */
}
