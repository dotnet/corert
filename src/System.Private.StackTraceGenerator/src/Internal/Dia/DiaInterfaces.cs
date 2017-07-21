// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Diagnostics;
using Internal.LightweightInterop;

namespace Internal.StackGenerator.Dia
{
    internal sealed class IDiaDataSource : ComInterface
    {
        public IDiaDataSource(IntPtr punk)
            : base(punk)
        {
        }

        public int LoadDataFromPdb(String pdbPath)
        {
            unsafe
            {
                fixed (char* _pdbPath = pdbPath)
                {
                    int hr = S.StdCall<int>(GetVTableMember(4), Punk, _pdbPath);
                    GC.KeepAlive(this);
                    return hr;
                }
            }
        }

        public int OpenSession(out IDiaSession session)
        {
            session = null;
            IntPtr _session;
            int hr = S.StdCall<int>(GetVTableMember(8), Punk, out _session);
            GC.KeepAlive(this);
            if (hr != S_OK)
                return hr;
            session = new IDiaSession(_session);
            return hr;
        }
    }

    internal sealed class IDiaSession : ComInterface
    {
        public IDiaSession(IntPtr punk)
            : base(punk)
        {
        }

        public int FindChildren(IDiaSymbol parent, SymTagEnum symTag, String name, NameSearchOptions compareFlags, out IDiaEnumSymbols enumSymbols)
        {
            enumSymbols = null;
            IntPtr _enumSymbols;
            int hr;
            unsafe
            {
                fixed (char* _name = name)
                {
                    hr = S.StdCall<int>(GetVTableMember(8), Punk, parent.Punk, (int)symTag, _name, (int)compareFlags, out _enumSymbols);
                }
            }
            GC.KeepAlive(this);
            GC.KeepAlive(parent);
            if (hr != S_OK)
                return hr;
            enumSymbols = new IDiaEnumSymbols(_enumSymbols);
            return hr;
        }


        public int FindSymbolByRVA(int rva, SymTagEnum symTag, out IDiaSymbol symbol)
        {
            symbol = null;
            IntPtr _symbol;
            int hr = S.StdCall<int>(GetVTableMember(14), Punk, rva, (int)symTag, out _symbol);
            GC.KeepAlive(this);
            if (hr != S_OK)
                return hr;
            symbol = new IDiaSymbol(_symbol);
            return hr;
        }

        public int FindLinesByRVA(int rva, int length, out IDiaEnumLineNumbers enumLineNumbers)
        {
            enumLineNumbers = null;
            IntPtr _enumLineNumbers;
            int hr = S.StdCall<int>(GetVTableMember(25), Punk, rva, length, out _enumLineNumbers);
            GC.KeepAlive(this);
            if (hr != S_OK)
                return hr;
            enumLineNumbers = new IDiaEnumLineNumbers(_enumLineNumbers);
            return hr;
        }

        public int FindILOffsetsByRVA(int rva, int length, out IDiaEnumLineNumbers enumLineNumbers)
        {
            enumLineNumbers = null;
            IntPtr _enumLineNumbers;
            int hr = S.StdCall<int>(GetVTableMember(46), Punk, rva, length, out _enumLineNumbers);
            GC.KeepAlive(this);
            if (hr != S_OK)
                return hr;
            enumLineNumbers = new IDiaEnumLineNumbers(_enumLineNumbers);
            return hr;
        }
    }

    internal sealed class IDiaEnumSymbols : ComInterface
    {
        public IDiaEnumSymbols(IntPtr punk)
            : base(punk)
        {
        }

        public int Count(out int count)
        {
            int hr = S.StdCall<int>(GetVTableMember(4), Punk, out count);
            GC.KeepAlive(this);
            return hr;
        }

        public int Item(int index, out IDiaSymbol symbol)
        {
            symbol = null;
            IntPtr pSymbol;
            int hr = S.StdCall<int>(GetVTableMember(5), Punk, index, out pSymbol);
            GC.KeepAlive(this);
            if (hr != S_OK)
                return hr;
            symbol = new IDiaSymbol(pSymbol);
            return hr;
        }
    }

    internal sealed class IDiaSymbol : ComInterface
    {
        public IDiaSymbol(IntPtr punk)
            : base(punk)
        {
        }

        public int GetSymTag(out SymTagEnum symTagEnum)
        {
            symTagEnum = default(SymTagEnum);
            int _symTagEnum;
            int hr = S.StdCall<int>(GetVTableMember(4), Punk, out _symTagEnum);
            GC.KeepAlive(this);
            symTagEnum = (SymTagEnum)_symTagEnum;
            return hr;
        }

        public int GetName(out String name)
        {
            name = null;
            IntPtr _name;
            int hr = S.StdCall<int>(GetVTableMember(5), Punk, out _name);
            GC.KeepAlive(this);
            if (hr != S_OK)
                return hr;
            name = _name.MarshalBstr();
            return hr;
        }

        public int GetType(out IDiaSymbol symbol)
        {
            symbol = null;
            IntPtr _symbol;
            int hr = S.StdCall<int>(GetVTableMember(8), Punk, out _symbol);
            GC.KeepAlive(this);
            if (hr != S_OK)
                return hr;
            symbol = new IDiaSymbol(_symbol);
            return hr;
        }

        public int GetDataKind(out DataKind dataKind)
        {
            dataKind = default(DataKind);
            int _dataKindEnum;
            int hr = S.StdCall<int>(GetVTableMember(9), Punk, out _dataKindEnum);
            GC.KeepAlive(this);
            dataKind = (DataKind)_dataKindEnum;
            return hr;
        }

        public int GetReference(out bool isReference)
        {
            isReference = false;
            int _isReference;
            int hr = S.StdCall<int>(GetVTableMember(48), Punk, out _isReference);
            GC.KeepAlive(this);
            isReference = (_isReference != 0);
            return hr;
        }

        public int GetBaseType(out BasicType baseType)
        {
            baseType = default(BasicType);
            int _baseType;
            int hr = S.StdCall<int>(GetVTableMember(43), Punk, out _baseType);
            GC.KeepAlive(this);
            baseType = (BasicType)_baseType;
            return hr;
        }

        public int GetLength(out long length)
        {
            int hr = S.StdCall<int>(GetVTableMember(17), Punk, out length);
            GC.KeepAlive(this);
            return hr;
        }
    }

    internal sealed class IDiaEnumLineNumbers : ComInterface
    {
        public IDiaEnumLineNumbers(IntPtr punk)
            : base(punk)
        {
        }

        public int Count(out int count)
        {
            int hr = S.StdCall<int>(GetVTableMember(4), Punk, out count);
            GC.KeepAlive(this);
            return hr;
        }

        public int Item(int index, out IDiaLineNumber lineNumber)
        {
            lineNumber = null;
            IntPtr pLineNumber;
            int hr = S.StdCall<int>(GetVTableMember(5), Punk, index, out pLineNumber);
            GC.KeepAlive(this);
            if (hr != S_OK)
                return hr;
            lineNumber = new IDiaLineNumber(pLineNumber);
            return hr;
        }
    }

    internal sealed class IDiaLineNumber : ComInterface
    {
        public IDiaLineNumber(IntPtr punk)
            : base(punk)
        {
        }

        public int SourceFile(out IDiaSourceFile sourceFile)
        {
            sourceFile = null;
            IntPtr _sourceFile;
            int hr = S.StdCall<int>(GetVTableMember(4), Punk, out _sourceFile);
            GC.KeepAlive(this);
            if (hr != S_OK)
                return hr;
            sourceFile = new IDiaSourceFile(_sourceFile);
            return hr;
        }

        public int LineNumber(out int lineNumber)
        {
            int hr = S.StdCall<int>(GetVTableMember(5), Punk, out lineNumber);
            GC.KeepAlive(this);
            return hr;
        }

        public int ColumnNumber(out int columnNumber)
        {
            int hr = S.StdCall<int>(GetVTableMember(7), Punk, out columnNumber);
            GC.KeepAlive(this);
            return hr;
        }
    }

    internal sealed class IDiaSourceFile : ComInterface
    {
        public IDiaSourceFile(IntPtr punk)
            : base(punk)
        {
        }

        public int FileName(out String fileName)
        {
            fileName = null;
            IntPtr _fileName;
            int hr = S.StdCall<int>(GetVTableMember(4), Punk, out _fileName);
            GC.KeepAlive(this);
            if (hr != S_OK)
                return hr;
            fileName = _fileName.MarshalBstr();
            return hr;
        }
    }
}
