// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using global::System;
using global::System.Reflection;
using global::System.Collections.Generic;

using global::Internal.Runtime.Augments;

using global::Internal.Reflection.Core;
using global::Internal.Reflection.Core.Execution;
using global::Internal.Reflection.Execution.MethodInvokers;

using global::Internal.Metadata.NativeFormat;

using global::System.Runtime.CompilerServices;
using global::System.Runtime.InteropServices;

using global::Internal.Runtime;

using Debug = System.Diagnostics.Debug;

using TargetException = System.ArgumentException;

namespace Internal.Reflection.Execution
{
    internal sealed partial class ExecutionEnvironmentImplementation : ExecutionEnvironment
    {
        internal unsafe struct MetadataTable : IEnumerable<IntPtr>
        {
            public readonly uint ElementCount;
            int _elementSize;
            byte* _blob;

            public struct Enumerator : IEnumerator<IntPtr>
            {
                byte* _base;
                byte* _current;
                byte* _limit;
                int _elementSize;

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

            public MetadataTable(IntPtr moduleHandle, ReflectionMapBlob blobId, int elementSize)
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

        struct ExternalReferencesTable
        {
            unsafe uint* _base;
            uint _count;
            IntPtr _moduleHandle;

            unsafe public ExternalReferencesTable(IntPtr moduleHandle, ReflectionMapBlob blobId)
            {
                _moduleHandle = moduleHandle;

                uint* pBlob;
                uint cbBlob;
                if (RuntimeAugments.FindBlob(moduleHandle, (int)blobId, (IntPtr)(&pBlob), (IntPtr)(&cbBlob)))
                {
                    _count = cbBlob / sizeof(uint);
                    _base = pBlob;
                }
                else
                {
                    _count = 0;
                    _base = null;
                }
            }

            unsafe public uint GetRvaFromIndex(uint index)
            {
                Debug.Assert(_moduleHandle != IntPtr.Zero);

                if (index >= _count)
                    throw new BadImageFormatException();

                return _base[index];
            }

            unsafe public IntPtr GetIntPtrFromIndex(uint index)
            {
                uint rva = GetRvaFromIndex(index);
                if ((rva & 0x80000000) != 0)
                {
                    // indirect through IAT
                    return *(IntPtr*)((byte*)_moduleHandle + (rva & ~0x80000000));
                }
                else
                {
                    return (IntPtr)((byte*)_moduleHandle + rva);
                }
            }

            public RuntimeTypeHandle GetRuntimeTypeHandleFromIndex(uint index)
            {
                return RuntimeAugments.CreateRuntimeTypeHandle(GetIntPtrFromIndex(index));
            }
        }


        [StructLayout(LayoutKind.Sequential)]
        struct TypeMapEntry
        {
            public uint EETypeRva;
            public int TypeDefinitionHandle;
        }

        [StructLayout(LayoutKind.Sequential)]
        struct BlockReflectionTypeMapEntry
        {
            public uint EETypeRva;
        }

        [StructLayout(LayoutKind.Sequential)]
        struct ArrayMapEntry
        {
            public uint ElementEETypeRva;
            public uint ArrayEETypeRva;
        }

        [StructLayout(LayoutKind.Sequential)]
        struct GenericInstanceMapEntry
        {
            public uint TypeSpecEETypeRva;
            public uint TypeDefEETypeRva;
            public uint ArgumentIndex;
        }

        struct DynamicInvokeMapEntry
        {
            public const uint IsImportMethodFlag = 0x40000000;
            public const uint InstantiationDetailIndexMask = 0x3FFFFFFF;
        }


        [Flags]
        enum InvokeTableFlags : uint
        {
            HasVirtualInvoke = 0x00000001,
            IsGenericMethod = 0x00000002,
            HasMetadataHandle = 0x00000004,
            IsDefaultConstructor = 0x00000008,
            RequiresInstArg = 0x00000010,
            HasEntrypoint = 0x00000020,
            IsUniversalCanonicalEntry = 0x00000040,
            HasDefaultParameters = 0x00000080,
        }

        struct VirtualInvokeTableEntry
        {
            public const int GenericVirtualMethod = 1;
            public const int FlagsMask = 1;
        }

        static class FieldAccessFlags
        {
            public const int RemoteStaticFieldRVA = unchecked((int)0x80000000);
        }

        [Flags]
        enum FieldTableFlags : uint
        {
            Instance = 0x00,
            Static = 0x01,
            ThreadStatic = 0x02,

            StorageClass = 0x03,

            IsUniversalCanonicalEntry = 0x04,
            HasMetadataHandle = 0x08,
            IsGcSection = 0x10,
        }

        /// <summary>
        /// This structure describes one static field in an external module. It is represented
        /// by an indirection cell pointer and an offset within the cell - the final address
        /// of the static field is essentially *IndirectionCell + Offset.
        /// </summary>
        [StructLayout(LayoutKind.Sequential)]
        struct RemoteStaticFieldDescriptor
        {
            public unsafe IntPtr* IndirectionCell;
            public int Offset;
        }

        [StructLayout(LayoutKind.Sequential)]
        struct CctorContextEntry
        {
            public uint EETypeRva;
            public uint CctorContextRva;
        }

        [StructLayout(LayoutKind.Sequential)]
        struct PointerTypeMapEntry
        {
            public uint PointerTypeEETypeRva;
            public uint ElementTypeEETypeRva;
        }

        private const uint s_NotActuallyAMetadataHandle = 0x80000000;
    }
}
