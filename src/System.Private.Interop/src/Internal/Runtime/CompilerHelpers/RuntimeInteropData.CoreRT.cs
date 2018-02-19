// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using Internal.Runtime.Augments;
using Internal.NativeFormat;
using Internal.Runtime.TypeLoader;
using Internal.Reflection.Execution;
using System.Runtime.InteropServices;

namespace Internal.Runtime.CompilerHelpers
{
    internal partial class RuntimeInteropData
    {
        public override IntPtr GetForwardDelegateCreationStub(RuntimeTypeHandle delegateTypeHandle)
        {
            IntPtr openStub, closedStub, delegateCreationStub;
            GetMarshallersForDelegate(delegateTypeHandle, out openStub, out closedStub, out delegateCreationStub);
            if (delegateCreationStub == IntPtr.Zero)
                throw new MissingInteropDataException(SR.DelegateMarshalling_MissingInteropData, Type.GetTypeFromHandle(delegateTypeHandle));
            return delegateCreationStub;
        }

        public override IntPtr GetDelegateMarshallingStub(RuntimeTypeHandle delegateTypeHandle, bool openStaticDelegate)
        {
            IntPtr openStub, closedStub, delegateCreationStub;
            GetMarshallersForDelegate(delegateTypeHandle, out openStub, out closedStub, out delegateCreationStub);
            IntPtr pStub = openStaticDelegate ? openStub : closedStub;
            if (pStub == IntPtr.Zero)
                throw new MissingInteropDataException(SR.DelegateMarshalling_MissingInteropData, Type.GetTypeFromHandle(delegateTypeHandle));
            return pStub;
        }

        #region "Struct Data"
        public override bool TryGetStructUnmarshalStub(RuntimeTypeHandle structureTypeHandle, out IntPtr unmarshalStub)
        {
            IntPtr marshalStub;
            IntPtr destroyStub;
            bool hasInvalidLayout;
            int size;
            return TryGetMarshallersForStruct(structureTypeHandle, out marshalStub, out unmarshalStub, out destroyStub, out hasInvalidLayout, out size);
        }

        public override bool TryGetStructMarshalStub(RuntimeTypeHandle structureTypeHandle, out IntPtr marshalStub)
        {
            IntPtr unmarshalStub;
            IntPtr destroyStub;
            bool hasInvalidLayout;
            int size;
            return TryGetMarshallersForStruct(structureTypeHandle, out marshalStub, out unmarshalStub, out destroyStub, out hasInvalidLayout, out size);
        }

        public override bool TryGetDestroyStructureStub(RuntimeTypeHandle structureTypeHandle, out IntPtr destroyStub, out bool hasInvalidLayout)
        {
            IntPtr marshalStub;
            IntPtr unmarshalStub;
            int size;
            return TryGetMarshallersForStruct(structureTypeHandle, out marshalStub, out unmarshalStub, out destroyStub, out hasInvalidLayout, out size);
        }

        public override bool TryGetStructUnsafeStructSize(RuntimeTypeHandle structureTypeHandle, out int size)
        {
            IntPtr marshalStub;
            IntPtr unmarshalStub;
            IntPtr destroyStub;
            bool hasInvalidLayout;
            return TryGetMarshallersForStruct(structureTypeHandle, out marshalStub, out unmarshalStub, out destroyStub, out hasInvalidLayout, out size);
        }

        public override bool TryGetStructFieldOffset(RuntimeTypeHandle structureTypeHandle, string fieldName, out bool structExists, out uint offset)
        {
            ExternalReferencesTable externalReferences;
            NativeParser entryParser;
            structExists = false;
            if (TryGetStructData(structureTypeHandle, out externalReferences, out entryParser))
            {
                structExists = true;
                // skip the first 4 IntPtrs(3 stubs and size)
                entryParser.SkipInteger();
                entryParser.SkipInteger();
                entryParser.SkipInteger();
                entryParser.SkipInteger();

                uint mask = entryParser.GetUnsigned();
                uint fieldCount = mask >> 1;
                for (uint index = 0; index < fieldCount; index++)
                {
                    string name = entryParser.GetString();
                    offset = entryParser.GetUnsigned();
                    if (name == fieldName)
                    {
                        return true;
                    }
                }
            }
            offset = 0;
            return false;
        }
        #endregion

        private static unsafe bool TryGetNativeReaderForBlob(NativeFormatModuleInfo module, ReflectionMapBlob blob, out NativeReader reader)
        {
            byte* pBlob;
            uint cbBlob;

            if (module.TryFindBlob((int)blob, out pBlob, out cbBlob))
            {
                reader = new NativeReader(pBlob, cbBlob);
                return true;
            }

            reader = default(NativeReader);
            return false;
        }

        private unsafe bool GetMarshallersForDelegate(RuntimeTypeHandle delegateTypeHandle, out IntPtr openStub, out IntPtr closedStub, out IntPtr delegateCreationStub)
        {
            int delegateHashcode = delegateTypeHandle.GetHashCode();
            openStub = IntPtr.Zero;
            closedStub = IntPtr.Zero;
            delegateCreationStub = IntPtr.Zero;

            foreach (NativeFormatModuleInfo module in ModuleList.EnumerateModules())
            {
                NativeReader delegateMapReader;
                if (TryGetNativeReaderForBlob(module, ReflectionMapBlob.DelegateMarshallingStubMap, out delegateMapReader))
                {
                    NativeParser delegateMapParser = new NativeParser(delegateMapReader, 0);
                    NativeHashtable delegateHashtable = new NativeHashtable(delegateMapParser);

                    ExternalReferencesTable externalReferences = default(ExternalReferencesTable);
                    externalReferences.InitializeCommonFixupsTable(module);

                    var lookup = delegateHashtable.Lookup(delegateHashcode);
                    NativeParser entryParser;
                    while (!(entryParser = lookup.GetNext()).IsNull)
                    {
                        RuntimeTypeHandle foundDelegateType = externalReferences.GetRuntimeTypeHandleFromIndex(entryParser.GetUnsigned());
                        if (foundDelegateType.Equals(delegateTypeHandle))
                        {
                            openStub = externalReferences.GetIntPtrFromIndex(entryParser.GetUnsigned());
                            closedStub = externalReferences.GetIntPtrFromIndex(entryParser.GetUnsigned());
                            delegateCreationStub = externalReferences.GetIntPtrFromIndex(entryParser.GetUnsigned());
                            return true;
                        }
                    }
                }
            }
            return false;
        }

        private unsafe bool TryGetStructData(RuntimeTypeHandle structTypeHandle, out ExternalReferencesTable externalReferences, out NativeParser entryParser)
        {
            int structHashcode = structTypeHandle.GetHashCode();
            externalReferences = default(ExternalReferencesTable);
            entryParser = default(NativeParser);
            foreach (NativeFormatModuleInfo module in ModuleList.EnumerateModules())
            {
                NativeReader structMapReader;
                if (TryGetNativeReaderForBlob(module, ReflectionMapBlob.StructMarshallingStubMap, out structMapReader))
                {
                    NativeParser structMapParser = new NativeParser(structMapReader, 0);
                    NativeHashtable structHashtable = new NativeHashtable(structMapParser);

                    externalReferences.InitializeCommonFixupsTable(module);

                    var lookup = structHashtable.Lookup(structHashcode);
                    while (!(entryParser = lookup.GetNext()).IsNull)
                    {
                        RuntimeTypeHandle foundStructType = externalReferences.GetRuntimeTypeHandleFromIndex(entryParser.GetUnsigned());
                        if (foundStructType.Equals(structTypeHandle))
                        {
                            return true;
                        }
                    }
                }
            }
            return false;
        }

        private unsafe bool TryGetMarshallersForStruct(RuntimeTypeHandle structTypeHandle, out IntPtr marshalStub, out IntPtr unmarshalStub, out IntPtr destroyStub, out bool hasInvalidLayout, out int size)
        {
            marshalStub = IntPtr.Zero;
            unmarshalStub = IntPtr.Zero;
            destroyStub = IntPtr.Zero;
            hasInvalidLayout = true;
            size = 0;

            ExternalReferencesTable externalReferences;
            NativeParser entryParser;
            if (TryGetStructData(structTypeHandle, out externalReferences, out entryParser))
            {
                marshalStub = externalReferences.GetIntPtrFromIndex(entryParser.GetUnsigned());
                unmarshalStub = externalReferences.GetIntPtrFromIndex(entryParser.GetUnsigned());
                destroyStub = externalReferences.GetIntPtrFromIndex(entryParser.GetUnsigned());
                size = (int)entryParser.GetUnsigned();
                uint mask = entryParser.GetUnsigned();
                hasInvalidLayout = (mask & 0x1) == 1;
                return true;
            }
            return false;
        }
    }
}
