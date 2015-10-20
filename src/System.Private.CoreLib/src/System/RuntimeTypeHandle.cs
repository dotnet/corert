// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Runtime;
using System.Diagnostics;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Runtime.CompilerServices;

using Internal.Runtime.Augments;

namespace System
{
    [StructLayout(LayoutKind.Sequential)]
    public unsafe struct RuntimeTypeHandle
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

            return (int)RuntimeImports.RhGetEETypeHash(this.EEType);
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
                return RuntimeImports.AreTypesEquivalent(this.EEType, handle.EEType);
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

        internal EETypePtr EEType
        {
            get
            {
                return new EETypePtr(_value);
            }
        }

        internal RuntimeImports.RhEETypeClassification Classification
        {
            get
            {
                return RuntimeImports.RhGetEETypeClassification(this.EEType);
            }
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
                EETypePtr eeType = this.EEType;
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

