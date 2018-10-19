// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Runtime;
using System.Runtime.InteropServices;
using System.Runtime.CompilerServices;
using System.Runtime.Serialization;
using System.Diagnostics;

using Internal.Runtime;
using Internal.Runtime.Augments;

namespace System
{
    [StructLayout(LayoutKind.Sequential)]
    public unsafe struct RuntimeTypeHandle : IEquatable<RuntimeTypeHandle>, ISerializable
    {
        //
        // Caution: There can be and are multiple EEType for the "same" type (e.g. int[]). That means
        // you can't use the raw IntPtr value for comparisons. 
        //

        internal RuntimeTypeHandle(EETypePtr pEEType)
        {
            _value = pEEType.RawValue;
        }

        public override bool Equals(object obj)
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

        public static unsafe void PrintString(string s)
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

        public struct TwoByteStr
        {
            public byte first;
            public byte second;
        }

        [DllImport("*")]
        private static unsafe extern int printf(byte* str, byte* unused);

        public static void PrintLine(string s)
        {
            PrintString(s);
            PrintString("\n");
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
                PrintLine("one is null");
                return false;
            }
            else
            {
                PrintLine("checkgin ToEETypePtr");
                AreTypesEquivalentInternal(this.ToEETypePtr().ToPointer(), handle.ToEETypePtr().ToPointer());
                return RuntimeImports.AreTypesEquivalent(this.ToEETypePtr(), handle.ToEETypePtr());
            }
        }

        internal static unsafe bool AreTypesEquivalentInternal(EEType* pType1, EEType* pType2)
        {
            PrintString("p1 hashcode ");
            PrintLine(pType1->HashCode.ToString());

            PrintString("p2 hashcode ");
            PrintLine(pType2->HashCode.ToString());

            if (pType1 == pType2)
                return true;

            if (pType1->IsCloned)
            {
                pType1 = pType1->CanonicalEEType;
                PrintLine("p1 is cloned");

            }

            if (pType2->IsCloned)
            {
                pType2 = pType2->CanonicalEEType;
                PrintLine("p2 is cloned");
            }

            if (pType1 == pType2)
                return true;

            if (pType1->IsParameterizedType && pType2->IsParameterizedType)
            {
                PrintLine("IsParameterizedType");
                return AreTypesEquivalentInternal(pType1->RelatedParameterType, pType2->RelatedParameterType) && pType1->ParameterizedTypeShape == pType2->ParameterizedTypeShape;
            }

                PrintLine("not equivalent");
            return false;
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

        public IntPtr Value => _value;

        public ModuleHandle GetModuleHandle()
        {
            Type type = Type.GetTypeFromHandle(this);
            if (type == null)
                return default(ModuleHandle);

            return type.Module.ModuleHandle;
        }

        public void GetObjectData(SerializationInfo info, StreamingContext context)
        {
            throw new PlatformNotSupportedException();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
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
        internal string LastResortToString
        {
            get
            {
                string s;
                EETypePtr eeType = this.ToEETypePtr();
                IntPtr rawEEType = eeType.RawValue;
                IntPtr moduleBase = RuntimeImports.RhGetOSModuleFromEEType(rawEEType);
                if (moduleBase != IntPtr.Zero)
                {
                    uint rva = (uint)(rawEEType.ToInt64() - moduleBase.ToInt64());
                    s = "EETypeRva:0x" + rva.LowLevelToString();
                }
                else
                {
                    s = "EETypePointer:0x" + rawEEType.LowLevelToString();
                }

                ReflectionExecutionDomainCallbacks callbacks = RuntimeAugments.CallbacksIfAvailable;
                if (callbacks != null)
                {
                    string penultimateLastResortString = callbacks.GetBetterDiagnosticInfoIfAvailable(this);
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

