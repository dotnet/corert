// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;

namespace ILCompiler.DependencyAnalysis.X64
{
    public enum AddrModeSize
    {
        Int8 = 1,
        Int16 = 2,
        Int32 = 4,
        Int64 = 8,
        Int128 = 16
    }

    public struct AddrMode
    {
        public AddrMode(Register baseRegister, Register? indexRegister, int offset, byte scale, AddrModeSize size)
        {
            _baseReg = baseRegister;
            _indexReg = indexRegister;
            _offset = offset;
            _scale = scale;
            _size = size;
        }

        private Register _baseReg;
        private Register? _indexReg;
        private int _offset;
        private byte _scale;
        private AddrModeSize _size;

        public Register BaseReg
        {
            get { return _baseReg; }
        }
        public int Offset
        {
            get { return _offset; }
        }

        public Register? IndexReg
        {
            get { return _indexReg; }
        }

        public byte Scale
        {
            get { return _scale; }
        }

        public AddrModeSize Size
        {
            get { return _size; }
        }
    }
}
