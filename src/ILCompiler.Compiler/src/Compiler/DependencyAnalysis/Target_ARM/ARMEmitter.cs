// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Diagnostics;

namespace ILCompiler.DependencyAnalysis.ARM
{
    public struct ARMEmitter
    {
        public ARMEmitter(NodeFactory factory, bool relocsOnly)
        {
            Builder = new ObjectDataBuilder(factory, relocsOnly);
            TargetRegister = new TargetRegisterMap(factory.Target.OperatingSystem);
        }

        public ObjectDataBuilder Builder;
        public TargetRegisterMap TargetRegister;

        // add reg, immediate
        public void EmitADD(Register reg, byte immediate)
        {
            Builder.EmitShort((short)(0x3000 + ((byte)reg << 8) + immediate));
        }

        // push reg
        public void EmitPUSH(Register reg)
        {
            Builder.EmitByte(0x4d);
            Builder.EmitByte(0xf8);
            Builder.EmitShort((short)(0x0d04 + ((byte)reg << 12)));
        }

        // mov reg, reg
        public void EmitMOV(Register destination, Register source)
        {
            Builder.EmitShort((short)(0x4600 + (((byte)destination & 0x8) << 4) + (((byte)source & 0x8) << 3) + (((byte)source & 0x7) << 3) + ((byte)destination & 0x7)));
        }

        // mov   reg, [reloc] & 0x0000FFFF
        // movt  reg, [reloc] & 0xFFFF0000
        public void EmitMOV(Register destination, ISymbolNode symbol)
        {
            Builder.EmitReloc(symbol, RelocType.IMAGE_REL_BASED_THUMB_MOV32);
            Builder.EmitShort(unchecked((short)0xf240));
            Builder.EmitShort((short)((byte)destination << 8));
            Builder.EmitShort(unchecked((short)0xf2c0));
            Builder.EmitShort((short)((byte)destination << 8));
        }

        // b symbol
        public void EmitJMP(ISymbolNode symbol)
        {
            Debug.Assert(!symbol.RepresentsIndirectionCell);
            Builder.EmitReloc(symbol, RelocType.IMAGE_REL_BASED_THUMB_BRANCH24);
            Builder.EmitByte(0);
            Builder.EmitByte(0xF0);
            Builder.EmitByte(0);
            Builder.EmitByte(0xB8);
        }

        // bx reg
        public void EmitJMP(Register destination)
        {
            Builder.EmitShort((short)(0x47 | ((byte)destination << 3)));
        }
    }
}
