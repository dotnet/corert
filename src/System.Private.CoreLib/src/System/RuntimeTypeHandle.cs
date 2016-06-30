// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Runtime;
using System.Diagnostics;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Runtime.CompilerServices;

using Internal.Runtime.Augments;
using Internal.Reflection.Core.NonPortable;

namespace System
{
    [StructLayout(LayoutKind.Sequential)]
    public unsafe struct RuntimeTypeHandle
    {
#if CLR_RUNTIMETYPEHANDLE
        internal RuntimeTypeHandle(RuntimeType type)
        {
            _type = type;
        }

        internal RuntimeTypeHandle(EETypePtr pEEType)
        {
            // CORERT-TODO: RuntimeTypeHandle
            throw new NotImplementedException();
        }
#else
        //
        // Caution: There can be and are multiple EEType for the "same" type (e.g. int[]). That means
        // you can't use the raw IntPtr value for comparisons. 
        //

        internal RuntimeTypeHandle(EETypePtr pEEType)
        {
            _value = pEEType.RawValue;
        }
#endif

        public override bool Equals(Object obj)
        {
            if (obj is RuntimeTypeHandle)
            {
                RuntimeTypeHandle handle = (RuntimeTypeHandle)obj;
                return Equals(handle);
            }
            return false;
        }

        public override int GetHashCode()
        {
            if (IsNull)
                return 0;

            return this.ToEETypePtr().GetHashCode();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Equals(RuntimeTypeHandle handle)
        {
#if CLR_RUNTIMETYPEHANDLE
            return Object.ReferenceEquals(_type, handle._type);
#else
            if (_value == handle._value)
            {
                return true;
            }
            else if (this.IsNull || handle.IsNull)
            {
                return false;
            }
            else
            {
                return RuntimeImports.AreTypesEquivalent(this.ToEETypePtr(), handle.ToEETypePtr());
            }
#endif
        }

        public static bool operator ==(object left, RuntimeTypeHandle right)
        {
            if (left is RuntimeTypeHandle)
                return right.Equals((RuntimeTypeHandle)left);
            return false;
        }

        public static bool operator ==(RuntimeTypeHandle left, object right)
        {
            if (right is RuntimeTypeHandle)
                return left.Equals((RuntimeTypeHandle)right);
            return false;
        }

        public static bool operator !=(object left, RuntimeTypeHandle right)
        {
            if (left is RuntimeTypeHandle)
                return !right.Equals((RuntimeTypeHandle)left);
            return true;
        }

        public static bool operator !=(RuntimeTypeHandle left, object right)
        {
            if (right is RuntimeTypeHandle)
                return !left.Equals((RuntimeTypeHandle)right);
            return true;
        }

        internal EETypePtr ToEETypePtr()
        {
#if CLR_RUNTIMETYPEHANDLE
            return _type.ToEETypePtr();
#else
            return new EETypePtr(_value);
#endif
        }

        internal bool IsNull
        {
            get
            {
#if CLR_RUNTIMETYPEHANDLE
                return _type == null;
#else
                return _value == new IntPtr(0);
#endif
            }
        }

        // Last resort string for Type.ToString() when no metadata around.
        internal String LastResortToString
        {
            get
            {
                String s;
                EETypePtr eeType = this.ToEETypePtr();
                IntPtr rawEEType = eeType.RawValue;
                IntPtr moduleBase = RuntimeImports.RhGetModuleFromEEType(rawEEType);
                uint rva = (uint)(rawEEType.ToInt64() - moduleBase.ToInt64());
                s = "EETypeRva:0x" + rva.ToString("x8");

                ReflectionExecutionDomainCallbacks callbacks = RuntimeAugments.CallbacksIfAvailable;
                if (callbacks != null)
                {
                    String penultimateLastResortString = callbacks.GetBetterDiagnosticInfoIfAvailable(this);
                    if (penultimateLastResortString != null)
                        s += "(" + penultimateLastResortString + ")";
                }
                return s;
            }
        }

#if CORERT
        [Intrinsic]
#endif
        internal static IntPtr GetValueInternal(RuntimeTypeHandle handle)
        {
            return handle.RawValue;
        }

        internal IntPtr RawValue
        {
            get
            {
#if CLR_RUNTIMETYPEHANDLE
                return ToEETypePtr().RawValue;
#else
                return _value;
#endif
            }
        }

#if CLR_RUNTIMETYPEHANDLE
        internal RuntimeType RuntimeType
        {
            get
            {
                return _type;
            }
        }
#endif

#if CLR_RUNTIMETYPEHANDLE
        private RuntimeType _type;
#else
        private IntPtr _value;
#endif
    }
}

