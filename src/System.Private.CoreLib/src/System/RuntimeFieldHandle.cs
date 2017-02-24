// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Diagnostics.Contracts;
using System.Runtime.InteropServices;
using System.Runtime.Serialization;
using System.Runtime.CompilerServices;

using Internal.Runtime.Augments;

namespace System
{
    [Serializable]
    [StructLayoutAttribute(LayoutKind.Sequential)]
    public struct RuntimeFieldHandle : ISerializable
    {
        private IntPtr _value;

        public IntPtr Value => _value;

        public override bool Equals(Object obj)
        {
            if (!(obj is RuntimeFieldHandle))
                return false;

            return Equals((RuntimeFieldHandle)obj);
        }

        public bool Equals(RuntimeFieldHandle handle)
        {
            string fieldName1, fieldName2;
            RuntimeTypeHandle declaringType1, declaringType2;

            RuntimeAugments.TypeLoaderCallbacks.GetRuntimeFieldHandleComponents(this, out declaringType1, out fieldName1);
            RuntimeAugments.TypeLoaderCallbacks.GetRuntimeFieldHandleComponents(handle, out declaringType2, out fieldName2);

            return declaringType1.Equals(declaringType2) && fieldName1 == fieldName2;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private int _rotl(int value, int shift)
        {
            return (int)(((uint)value << shift) | ((uint)value >> (32 - shift)));
        }

        public override int GetHashCode()
        {
            string fieldName;
            RuntimeTypeHandle declaringType;
            RuntimeAugments.TypeLoaderCallbacks.GetRuntimeFieldHandleComponents(this, out declaringType, out fieldName);

            int hashcode = declaringType.GetHashCode();
            return (hashcode + _rotl(hashcode, 13)) ^ fieldName.GetHashCode();
        }

        public static bool operator ==(RuntimeFieldHandle left, RuntimeFieldHandle right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(RuntimeFieldHandle left, RuntimeFieldHandle right)
        {
            return !left.Equals(right);
        }

        public void GetObjectData(SerializationInfo info, StreamingContext context)
        {
            if (info == null)
                throw new ArgumentNullException(nameof(info));
            Contract.EndContractBlock();

            // TODO
            throw new PlatformNotSupportedException();
        }
    }
}
