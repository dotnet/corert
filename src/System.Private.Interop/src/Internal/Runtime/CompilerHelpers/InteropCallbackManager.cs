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
    internal class Callbacks : InteropCallbacks
    {
        public override bool TryGetMarshallerDataForDelegate(RuntimeTypeHandle delegateTypeHandle, out McgPInvokeDelegateData data)
        {
            IntPtr[] marshallingStubs;
            if (!InteropCallbackManager.Instance.TryGetMarshallersForDelegate(delegateTypeHandle, out marshallingStubs))
            {
                data = default(McgPInvokeDelegateData);
                return false;
            }

            data = new global::System.Runtime.InteropServices.McgPInvokeDelegateData()
            {
                ReverseOpenStaticDelegateStub = marshallingStubs[0],
                ReverseStub = marshallingStubs[1]
            };
            return true;
        }
    }

    [CLSCompliant(false)]
    public sealed class InteropCallbackManager
    {
        public static InteropCallbackManager Instance { get; private set; }

        // Eager initialization called from LibraryInitializer for the assembly.
        internal static void Initialize()
        {
            Instance = new InteropCallbackManager();
            RuntimeAugments.InitializeInteropLookups(new Callbacks());
        }

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

        public unsafe bool TryGetMarshallersForDelegate(RuntimeTypeHandle delegateTypeHandle, out IntPtr[] stubs)
        {
            int delegateHashcode = delegateTypeHandle.GetHashCode();
            stubs = Array.Empty<IntPtr>();

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
                            byte* pOpen = (byte*)externalReferences.GetIntPtrFromIndex(entryParser.GetUnsigned());
                            byte* pClose = (byte*)externalReferences.GetIntPtrFromIndex(entryParser.GetUnsigned());
                            stubs = new IntPtr[2] { (IntPtr)pOpen, (IntPtr)pClose };
                            return true;
                        }
                    }
                }
            }

            return false;
        }

    }
}
