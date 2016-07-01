// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.


using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

using Internal.Runtime;
using Internal.Runtime.Augments;

using ReflectionMapBlob = Internal.Runtime.ReflectionMapBlob;

namespace Internal.Runtime.TypeLoader
{
    internal unsafe struct MetadataTable : IEnumerable<IntPtr>
    {
        public readonly uint ElementCount;
        private int _elementSize;
        private byte* _blob;

        public struct Enumerator : IEnumerator<IntPtr>
        {
            private byte* _base;
            private byte* _current;
            private byte* _limit;
            private int _elementSize;

            internal Enumerator(ref MetadataTable table)
            {
                _base = table._blob;
                _elementSize = table._elementSize;
                _current = _base - _elementSize;
                _limit = _base + (_elementSize * table.ElementCount);
            }

            public IntPtr Current { get { return (IntPtr)_current; } }

            public void Dispose() { }

            object System.Collections.IEnumerator.Current { get { return Current; } }

            public bool MoveNext()
            {
                _current += _elementSize;
                if (_current < _limit)
                {
                    return true;
                }

                _current = _limit;

                return false;
            }

            public void Reset() { _current = _base - _elementSize; }
        }

        private MetadataTable(IntPtr moduleHandle, ReflectionMapBlob blobId, int elementSize)
        {
            Debug.Assert(elementSize != 0);

            _elementSize = elementSize;

            byte* pBlob;
            uint cbBlob;

            if (!RuntimeAugments.FindBlob(moduleHandle, (int)blobId, new IntPtr(&pBlob), new IntPtr(&cbBlob)))
            {
                pBlob = null;
                cbBlob = 0;
            }

            Debug.Assert(cbBlob % elementSize == 0);

            _blob = pBlob;
            ElementCount = cbBlob / (uint)elementSize;
        }

        /// <summary>
        /// Create metadata table for the TypeMap blob on a given module.
        /// </summary>
        /// <param name="moduleHandle">Module handle is used to locate the TypeMap blob</param>
        public static MetadataTable CreateTypeMapTable(IntPtr moduleHandle)
        {
            return new MetadataTable(moduleHandle, ReflectionMapBlob.TypeMap, sizeof(TypeMapEntry));
        }

        /// <summary>
        /// Create metadata table for the CCtorContextMap blob on a given module.
        /// </summary>
        /// <param name="moduleHandle">Module handle is used to locate the CCtorContextMap blob</param>
        public static MetadataTable CreateCCtorContextMapTable(IntPtr moduleHandle)
        {
            return new MetadataTable(moduleHandle, ReflectionMapBlob.CCtorContextMap, sizeof(CctorContextEntry));
        }

        public IntPtr this[uint index]
        {
            get
            {
                Debug.Assert(index < ElementCount);
                return (IntPtr)(_blob + (index * _elementSize));
            }
        }

        public Enumerator GetEnumerator()
        {
            return new Enumerator(ref this);
        }

        IEnumerator<IntPtr> IEnumerable<IntPtr>.GetEnumerator()
        {
            return GetEnumerator();
        }

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }
    }

    internal struct DynamicInvokeMapEntry
    {
        public const uint IsImportMethodFlag = 0x40000000;
        public const uint InstantiationDetailIndexMask = 0x3FFFFFFF;
    }

    internal struct VirtualInvokeTableEntry
    {
        public const int GenericVirtualMethod = 1;
        public const int FlagsMask = 1;
    }

    [Flags]
    public enum InvokeTableFlags : uint
    {
        HasVirtualInvoke = 0x00000001,
        IsGenericMethod = 0x00000002,
        HasMetadataHandle = 0x00000004,
        IsDefaultConstructor = 0x00000008,
        RequiresInstArg = 0x00000010,
        HasEntrypoint = 0x00000020,
        IsUniversalCanonicalEntry = 0x00000040,
        HasDefaultParameters = 0x00000080,
        NeedsParameterInterpretation = 0x00000100,
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct TypeMapEntry
    {
        public IntPtr EEType;
        public int TypeDefinitionHandle;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct CctorContextEntry
    {
        public uint EETypeRva;
        public uint CctorContextRva;
    }

    [Flags]
    public enum FieldTableFlags : uint
    {
        Instance = 0x00,
        Static = 0x01,
        ThreadStatic = 0x02,

        StorageClass = 0x03,

        IsUniversalCanonicalEntry = 0x04,
        HasMetadataHandle = 0x08,
        IsGcSection = 0x10,
    }
}
