// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Runtime.CompilerServices;
using System.Runtime;

namespace Internal.Runtime.CompilerServices
{
    // This structure is used to resolve a instance method given an object instance. To use this type
    // 1) New up an instance using one of the constructors below.
    // 2) Use the ToIntPtr() method to get the interned instance of this type. This will permanently allocate
    //    a block of memory that can be used to represent a virtual method resolution. This memory is interned
    //    so that repeated allocation of the same resolver will not leak.
    // 3) Use the ResolveMethod function to do the virtual lookup. This function takes advantage of 
    //    a lockless cache so the resolution is very fast for repeated lookups.
    public struct OpenMethodResolver : IEquatable<OpenMethodResolver>
    {
        public const short DispatchResolve = 0;
        public const short GVMResolve = 1;
        public const short OpenNonVirtualResolve = 2;

        private readonly short _resolveType;
        private readonly int _handle;
        private readonly IntPtr _methodHandleOrSlotOrCodePointer;
        private readonly EETypePtr _declaringType;

        public OpenMethodResolver(RuntimeTypeHandle declaringTypeOfSlot, int slot, int handle)
        {
            _resolveType = DispatchResolve;
            _declaringType = declaringTypeOfSlot.ToEETypePtr();
            _methodHandleOrSlotOrCodePointer = new IntPtr(slot);
            _handle = handle;
        }

        public unsafe OpenMethodResolver(RuntimeTypeHandle declaringTypeOfSlot, RuntimeMethodHandle gvmSlot, int handle)
        {
            _resolveType = GVMResolve;
            _methodHandleOrSlotOrCodePointer = *(IntPtr*)&gvmSlot;
            _declaringType = declaringTypeOfSlot.ToEETypePtr();
            _handle = handle;
        }

        public OpenMethodResolver(RuntimeTypeHandle declaringType, IntPtr codePointer, int handle)
        {
            _resolveType = OpenNonVirtualResolve;
            _methodHandleOrSlotOrCodePointer = codePointer;
            _declaringType = declaringType.ToEETypePtr();
            _handle = handle;
        }

        public short ResolverType
        {
            get
            {
                return _resolveType;
            }
        }

        public RuntimeTypeHandle DeclaringType
        {
            get
            {
                return new RuntimeTypeHandle(_declaringType);
            }
        }

        public unsafe RuntimeMethodHandle GVMMethodHandle
        {
            get
            {
                IntPtr localIntPtr = _methodHandleOrSlotOrCodePointer;
                IntPtr* pMethodHandle = &localIntPtr;
                return *(RuntimeMethodHandle*)pMethodHandle;
            }
        }

        public IntPtr CodePointer
        {
            get
            {
                return _methodHandleOrSlotOrCodePointer;
            }
        }

        public int Handle
        {
            get
            {
                return _handle;
            }
        }

        unsafe private IntPtr ResolveMethod(object thisObject)
        {
            if (_resolveType == DispatchResolve)
            {
                return RuntimeImports.RhResolveDispatch(thisObject, _declaringType, (ushort)_methodHandleOrSlotOrCodePointer.ToInt32());
            }
            else if (_resolveType == GVMResolve)
            {
                return TypeLoaderExports.GVMLookupForSlot(thisObject, GVMMethodHandle);
            }
            else
            {
                return _methodHandleOrSlotOrCodePointer;
            }
        }

        unsafe internal static IntPtr ResolveMethodWorker(IntPtr resolver, object thisObject)
        {
            return ((OpenMethodResolver*)resolver)->ResolveMethod(thisObject);
        }

        unsafe public static IntPtr ResolveMethod(IntPtr resolver, object thisObject)
        {
            return TypeLoaderExports.OpenInstanceMethodLookup(resolver, thisObject);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int _rotl(int value, int shift)
        {
            return (int)(((uint)value << shift) | ((uint)value >> (32 - shift)));
        }

        private static int CalcHashCode(int hashCode1, int hashCode2, int hashCode3, int hashCode4)
        {
            int length = 4;

            int hash1 = 0x449b3ad6;
            int hash2 = (length << 3) + 0x55399219;

            hash1 = (hash1 + _rotl(hash1, 5)) ^ hashCode1;
            hash2 = (hash2 + _rotl(hash2, 5)) ^ hashCode2;
            hash1 = (hash1 + _rotl(hash1, 5)) ^ hashCode3;
            hash2 = (hash2 + _rotl(hash2, 5)) ^ hashCode4;

            hash1 += _rotl(hash1, 8);
            hash2 += _rotl(hash2, 8);

            return hash1 ^ hash2;
        }

        public override int GetHashCode()
        {
            return CalcHashCode(_resolveType, _handle, _methodHandleOrSlotOrCodePointer.GetHashCode(), _declaringType.GetHashCode());
        }

        public bool Equals(OpenMethodResolver other)
        {
            if (other._resolveType != _resolveType)
                return false;

            if (other._handle != _handle)
                return false;

            if (other._methodHandleOrSlotOrCodePointer != _methodHandleOrSlotOrCodePointer)
                return false;

            return other._declaringType.Equals(_declaringType);
        }

        public override bool Equals(object obj)
        {
            if (!(obj is OpenMethodResolver))
            {
                return false;
            }

            return ((OpenMethodResolver)obj).Equals(this);
        }

        private static LowLevelDictionary<OpenMethodResolver, IntPtr> s_internedResolverHash = new LowLevelDictionary<OpenMethodResolver, IntPtr>();

        unsafe public IntPtr ToIntPtr()
        {
            lock (s_internedResolverHash)
            {
                IntPtr returnValue;
                if (s_internedResolverHash.TryGetValue(this, out returnValue))
                    return returnValue;
                returnValue = Interop.MemAlloc(new UIntPtr((uint)sizeof(OpenMethodResolver)));
                *((OpenMethodResolver*)returnValue) = this;
                s_internedResolverHash.Add(this, returnValue);
                return returnValue;
            }
        }
    }
}
