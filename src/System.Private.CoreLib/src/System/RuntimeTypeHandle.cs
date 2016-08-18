// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Runtime;
using System.Diagnostics;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Runtime.CompilerServices;

using Internal.Runtime;
using Internal.Runtime.Augments;
using Internal.Reflection.Core.NonPortable;

namespace System
{
    [StructLayout(LayoutKind.Sequential)]
    public unsafe struct RuntimeTypeHandle : IEquatable<RuntimeTypeHandle>
    {
        //
        // Caution: There can be and are multiple EEType for the "same" type (e.g. int[]). That means
        // you can't use the raw IntPtr value for comparisons. 
        //

        internal RuntimeTypeHandle(EETypePtr pEEType)
        {
            _value = pEEType.RawValue;
        }

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
            return new EETypePtr(_value);
        }

        internal bool IsNull
        {
            get
            {
                return _value == new IntPtr(0);
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
                s = "EETypeRva:0x" + rva.LowLevelToString();

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

        [Intrinsic]
        internal static IntPtr GetValueInternal(RuntimeTypeHandle handle)
        {
            return handle.RawValue;
        }

        internal IntPtr RawValue
        {
            get
            {
                return _value;
            }
        }

        private IntPtr _value;
    }
}

