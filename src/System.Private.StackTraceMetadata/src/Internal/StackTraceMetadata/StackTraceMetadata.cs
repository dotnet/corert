// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Runtime;
using System.Runtime.InteropServices;
using System.Threading;
using Internal.Metadata.NativeFormat;
using Internal.Runtime;
using Internal.Runtime.Augments;
using Internal.Runtime.TypeLoader;
using Internal.TypeSystem;

using ReflectionExecution = Internal.Reflection.Execution.ReflectionExecution;

#if BIT64
using nint = System.Int64;
using nuint = System.UInt64;
#else
using nint = System.Int32;
using nuint = System.UInt32;
#endif

namespace Internal.StackTraceMetadata
{
    /// <summary>
    /// This helper class is used to resolve non-reflectable method names using a special
    /// compiler-generated metadata blob to enhance quality of exception call stacks
    /// in situations where symbol information is not available.
    /// </summary>
    internal static class StackTraceMetadata
    {
        /// <summary>
        /// Module address-keyed map of per-module method name resolvers.
        /// </summary>
        static PerModuleMethodNameResolverHashtable _perModuleMethodNameResolverHashtable;
        [DllImport("*")]
        internal static unsafe extern int printf(byte* str, byte* unused);
        private static unsafe void PrintString(string s)
        {
            int length = s.Length;
            fixed (char* curChar = s)
            {
                for (int i = 0; i < length; i++)
                {
                    TwoByteStr curCharStr = new TwoByteStr();
                    curCharStr.first = (byte)(*(curChar + i));
                    printf((byte*)&curCharStr, null);
                }
            }
        }
        public unsafe static void PrintUint(int s)
        {
            byte[] intBytes = BitConverter.GetBytes(s);
            for (var i = 0; i < 4; i++)
            {
                TwoByteStr curCharStr = new TwoByteStr();
                var nib = (intBytes[3 - i] & 0xf0) >> 4;
                curCharStr.first = (byte)((nib <= 9 ? '0' : 'A') + (nib <= 9 ? nib : nib - 10));
                printf((byte*)&curCharStr, null);
                nib = (intBytes[3 - i] & 0xf);
                curCharStr.first = (byte)((nib <= 9 ? '0' : 'A') + (nib <= 9 ? nib : nib - 10));
                printf((byte*)&curCharStr, null);
            }
            PrintString("\n");
        }

        /// <summary>
        /// Eager startup initialization of stack trace metadata support creates
        /// the per-module method name resolver hashtable and registers the runtime augment
        /// for metadata-based stack trace resolution.
        /// </summary>
        internal static void Initialize()
        {
            PrintString("STM Initialize\n");
            _perModuleMethodNameResolverHashtable = new PerModuleMethodNameResolverHashtable();
            RuntimeAugments.InitializeStackTraceMetadataSupport(new StackTraceMetadataCallbacksImpl());
        }

        /// <summary>
        /// Locate the containing module for a method and try to resolve its name based on start address.
        /// </summary>
        public static string GetMethodNameFromStartAddressIfAvailable(IntPtr methodStartAddress)
        {
            IntPtr moduleStartAddress = RuntimeAugments.GetOSModuleFromPointer(methodStartAddress);
            int rva = (int)((nuint)methodStartAddress - (nuint)moduleStartAddress);
            foreach (TypeManagerHandle handle in ModuleList.Enumerate())
            {
                if (handle.OsModuleBase == moduleStartAddress)
                {
                    string name = _perModuleMethodNameResolverHashtable.GetOrCreateValue(handle.GetIntPtrUNSAFE()).GetMethodNameFromRvaIfAvailable(rva);
                    if (name != null)
                        return name;
                }
            }

            // We haven't found information in the stack trace metadata tables, but maybe reflection will have this
            if (ReflectionExecution.TryGetMethodMetadataFromStartAddress(methodStartAddress,
                out MetadataReader reader,
                out TypeDefinitionHandle typeHandle,
                out MethodHandle methodHandle))
            {
                return MethodNameFormatter.FormatMethodName(reader, typeHandle, methodHandle);
            }

            return null;
        }

        /// <summary>
        /// This hashtable supports mapping from module start addresses to per-module method name resolvers.
        /// </summary>
        private sealed class PerModuleMethodNameResolverHashtable : LockFreeReaderHashtable<IntPtr, PerModuleMethodNameResolver>
        {
            /// <summary>
            /// Given a key, compute a hash code. This function must be thread safe.
            /// </summary>
            protected override int GetKeyHashCode(IntPtr key)
            {
                return key.GetHashCode();
            }
    
            /// <summary>
            /// Given a value, compute a hash code which would be identical to the hash code
            /// for a key which should look up this value. This function must be thread safe.
            /// This function must also not cause additional hashtable adds.
            /// </summary>
            protected override int GetValueHashCode(PerModuleMethodNameResolver value)
            {
                return GetKeyHashCode(value.ModuleAddress);
            }
    
            /// <summary>
            /// Compare a key and value. If the key refers to this value, return true.
            /// This function must be thread safe.
            /// </summary>
            protected override bool CompareKeyToValue(IntPtr key, PerModuleMethodNameResolver value)
            {
                return key == value.ModuleAddress;
            }
    
            /// <summary>
            /// Compare a value with another value. Return true if values are equal.
            /// This function must be thread safe.
            /// </summary>
            protected override bool CompareValueToValue(PerModuleMethodNameResolver value1, PerModuleMethodNameResolver value2)
            {
                return value1.ModuleAddress == value2.ModuleAddress;
            }
    
            /// <summary>
            /// Create a new value from a key. Must be threadsafe. Value may or may not be added
            /// to collection. Return value must not be null.
            /// </summary>
            protected override PerModuleMethodNameResolver CreateValueFromKey(IntPtr key)
            {
                return new PerModuleMethodNameResolver(key);
            }
        }

        /// <summary>
        /// Implementation of stack trace metadata callbacks.
        /// </summary>
        private sealed class StackTraceMetadataCallbacksImpl : StackTraceMetadataCallbacks
        {
            public override string TryGetMethodNameFromStartAddress(IntPtr methodStartAddress)
            {
                return GetMethodNameFromStartAddressIfAvailable(methodStartAddress);
            }
        }

        /// <summary>
        /// Method name resolver for a single binary module
        /// </summary>
        private sealed class PerModuleMethodNameResolver
        {
            /// <summary>
            /// Start address of the module in question.
            /// </summary>
            private readonly IntPtr _moduleAddress;
            
            /// <summary>
            /// Dictionary mapping method RVA's to tokens within the metadata blob.
            /// </summary>
            private readonly Dictionary<int, int> _methodRvaToTokenMap;

            /// <summary>
            /// Metadata reader for the stack trace metadata.
            /// </summary>
            private readonly MetadataReader _metadataReader;

            /// <summary>
            /// Publicly exposed module address property.
            /// </summary>
            public IntPtr ModuleAddress { get { return _moduleAddress; } }

            /// <summary>
            /// Construct the per-module resolver by looking up the necessary blobs.
            /// </summary>
            public unsafe PerModuleMethodNameResolver(IntPtr moduleAddress)
            {
                _moduleAddress = moduleAddress;

                TypeManagerHandle handle = new TypeManagerHandle(moduleAddress);
                ModuleInfo moduleInfo;
                if (!ModuleList.Instance.TryGetModuleInfoByHandle(handle, out moduleInfo))
                {
                    // Module not found
                    return;
                }

                NativeFormatModuleInfo nativeFormatModuleInfo = moduleInfo as NativeFormatModuleInfo;
                if (nativeFormatModuleInfo == null)
                {
                    // It is not a native format module
                    return;
                }

                byte *metadataBlob;
                uint metadataBlobSize;

                byte *rvaToTokenMapBlob;
                uint rvaToTokenMapBlobSize;
                
                if (nativeFormatModuleInfo.TryFindBlob(
#if PROJECTN
                        (int)ReflectionMapBlob.BlobIdStackTraceEmbeddedMetadata,
#else
                        (int)ReflectionMapBlob.EmbeddedMetadata,
#endif
                        out metadataBlob,
                        out metadataBlobSize) &&
                    nativeFormatModuleInfo.TryFindBlob(
                        (int)ReflectionMapBlob.BlobIdStackTraceMethodRvaToTokenMapping,
                        out rvaToTokenMapBlob,
                        out rvaToTokenMapBlobSize))
                {
                    _metadataReader = new MetadataReader(new IntPtr(metadataBlob), (int)metadataBlobSize);

                    // RVA to token map consists of pairs of integers (method RVA - token)
                    int rvaToTokenMapEntryCount = (int)(rvaToTokenMapBlobSize / (2 * sizeof(int)));
                    _methodRvaToTokenMap = new Dictionary<int, int>(rvaToTokenMapEntryCount);
                    PopulateRvaToTokenMap(handle, (int *)rvaToTokenMapBlob, rvaToTokenMapEntryCount);
                }
            }
            
            /// <summary>
            /// Construct the dictionary mapping method RVAs to stack trace metadata tokens
            /// within a single binary module.
            /// </summary>
            /// <param name="rvaToTokenMap">List of RVA - token pairs</param>
            /// <param name="entryCount">Number of the RVA - token pairs in the list</param>
            private unsafe void PopulateRvaToTokenMap(TypeManagerHandle handle, int *rvaToTokenMap, int entryCount)
            {
                for (int entryIndex = 0; entryIndex < entryCount; entryIndex++)
                {
#if PROJECTN
                    int methodRva = rvaToTokenMap[2 * entryIndex + 0];
#else
                    int* pRelPtr32 = &rvaToTokenMap[2 * entryIndex + 0];
                    IntPtr pointer = (IntPtr)((byte*)pRelPtr32 + *pRelPtr32);
                    int methodRva = (int)((nuint)pointer - (nuint)handle.OsModuleBase);
#endif
                    int token = rvaToTokenMap[2 * entryIndex + 1];
                    _methodRvaToTokenMap[methodRva] = token;
                }
            }

            /// <summary>
            /// Try to resolve method name based on its address using the stack trace metadata
            /// </summary>
            public string GetMethodNameFromRvaIfAvailable(int rva)
            {
                if (_methodRvaToTokenMap == null)
                {
                    // No stack trace metadata for this module
                    return null;
                }
                
                int rawToken;
                if (!_methodRvaToTokenMap.TryGetValue(rva, out rawToken))
                {
                    // Method RVA not found in the map
                    return null;
                }

                return MethodNameFormatter.FormatMethodName(_metadataReader, Handle.FromIntToken(rawToken));
            }
        }
    }
}
