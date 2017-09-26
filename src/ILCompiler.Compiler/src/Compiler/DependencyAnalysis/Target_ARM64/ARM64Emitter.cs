// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Diagnostics;

namespace ILCompiler.DependencyAnalysis.ARM64
{
    public struct ARM64Emitter
    {
        public ARM64Emitter(NodeFactory factory, bool relocsOnly)
        {
            Builder = new ObjectDataBuilder(factory, relocsOnly);
            TargetRegister = new TargetRegisterMap(factory.Target.OperatingSystem);
        }

        public ObjectDataBuilder Builder;
        public TargetRegisterMap TargetRegister;

        // Assembly stub creation api. TBD, actually make this general purpose
        public void EmitMOV(Register regDst, ref AddrMode memory)
        {
            throw new NotImplementedException();
        }

        public void EmitMOV(Register regDst, Register regSrc)
        {
            throw new NotImplementedException();
        }

        public void EmitMOV(Register regDst, int imm32)
        {
            throw new NotImplementedException();
        }

        public void EmitLEAQ(Register reg, ISymbolNode symbol, int delta = 0)
        {
            throw new NotImplementedException();
        }

        public void EmitLEA(Register reg, ref AddrMode addrMode)
        {
            throw new NotImplementedException();
        }

        public void EmitCMP(ref AddrMode addrMode, sbyte immediate)
        {
            throw new NotImplementedException();
        }

        // add reg, immediate
        public void EmitADD(Register reg, byte immediate)
        {
            Builder.EmitInt((int)(0x91 << 24) | (immediate << 10) | ((byte)reg << 5) | (byte) reg);
        }

        public void EmitJMP(ISymbolNode symbol)
        {
            if (symbol.RepresentsIndirectionCell)
            {
                throw new NotImplementedException();
            }
            else
            {
                Builder.EmitReloc(symbol, RelocType.IMAGE_REL_BASED_ARM64_BRANCH26);
                Builder.EmitByte(0);
                Builder.EmitByte(0);
                Builder.EmitByte(0);
                Builder.EmitByte(0x14);
            }
        }

        public void EmitINT3()
        {
            throw new NotImplementedException();
        }

        public void EmitJmpToAddrMode(ref AddrMode addrMode)
        {
            throw new NotImplementedException();
        }

        public void EmitRET()
        {
            throw new NotImplementedException();
        }

        public void EmitRETIfEqual()
        {
            throw new NotImplementedException();
        }

        private bool InSignedByteRange(int i)
        {
            return i == (int)(sbyte)i;
        }

    }
}
