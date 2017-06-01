// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Reflection;
using System.Runtime.InteropServices;
using System.Runtime.Serialization;
using System.Runtime.CompilerServices;

using Internal.Runtime.Augments;
using Internal.Runtime.CompilerServices;
using Internal.Reflection.Augments;

namespace System
{
    [StructLayout(LayoutKind.Sequential)]
    public struct RuntimeMethodHandle : ISerializable
    {
        private IntPtr _value;

        public unsafe IntPtr Value => _value;

        public override bool Equals(Object obj)
        {
            if (!(obj is RuntimeMethodHandle))
                return false;

            return Equals((RuntimeMethodHandle)obj);
        }

        public bool Equals(RuntimeMethodHandle handle)
        {
            if (_value == handle._value)
                return true;

            RuntimeTypeHandle declaringType1, declaringType2;
            MethodNameAndSignature nameAndSignature1, nameAndSignature2;
            RuntimeTypeHandle[] genericArgs1, genericArgs2;

            RuntimeAugments.TypeLoaderCallbacks.GetRuntimeMethodHandleComponents(this, out declaringType1, out nameAndSignature1, out genericArgs1);
            RuntimeAugments.TypeLoaderCallbacks.GetRuntimeMethodHandleComponents(handle, out declaringType2, out nameAndSignature2, out genericArgs2);

            if (!declaringType1.Equals(declaringType2))
                return false;
            if (!nameAndSignature1.Equals(nameAndSignature2))
                return false;
            if ((genericArgs1 == null && genericArgs2 != null) || (genericArgs1 != null && genericArgs2 == null))
                return false;
            if (genericArgs1 != null)
            {
                if (genericArgs1.Length != genericArgs2.Length)
                    return false;
                for (int i = 0; i < genericArgs1.Length; i++)
                {
                    if (!genericArgs1[i].Equals(genericArgs2[i]))
                        return false;
                }
            }

            return true;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private int _rotl(int value, int shift)
        {
            return (int)(((uint)value << shift) | ((uint)value >> (32 - shift)));
        }

        public override int GetHashCode()
        {
            RuntimeTypeHandle declaringType;
            MethodNameAndSignature nameAndSignature;
            RuntimeTypeHandle[] genericArgs;
            RuntimeAugments.TypeLoaderCallbacks.GetRuntimeMethodHandleComponents(this, out declaringType, out nameAndSignature, out genericArgs);

            int hashcode = declaringType.GetHashCode();
            hashcode = (hashcode + _rotl(hashcode, 13)) ^ nameAndSignature.Name.GetHashCode();
            if (genericArgs != null)
            {
                for (int i = 0; i < genericArgs.Length; i++)
                {
                    int argumentHashCode = genericArgs[i].GetHashCode();
                    hashcode = (hashcode + _rotl(hashcode, 13)) ^ argumentHashCode;
                }
            }

            return hashcode;
        }

        public static bool operator ==(RuntimeMethodHandle left, RuntimeMethodHandle right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(RuntimeMethodHandle left, RuntimeMethodHandle right)
        {
            return !left.Equals(right);
        }

        public IntPtr GetFunctionPointer()
        {
            RuntimeTypeHandle declaringType;
            MethodNameAndSignature nameAndSignature;
            RuntimeTypeHandle[] genericArgs;
            RuntimeAugments.TypeLoaderCallbacks.GetRuntimeMethodHandleComponents(this, out declaringType, out nameAndSignature, out genericArgs);

            return ReflectionAugments.ReflectionCoreCallbacks.GetFunctionPointer(this, declaringType);
        }

        public RuntimeMethodHandle(SerializationInfo info, StreamingContext context)
        {
            if (info == null)
                throw new ArgumentNullException(nameof(info));

            try
            {
                MethodBase method = (MethodBase)info.GetValue("MethodObj", typeof(MethodBase));
                if (method == null)
                    throw new SerializationException(SR.Serialization_InsufficientState);

                this = method.MethodHandle;
            }
            catch (Exception e) when (!(e is SerializationException))
            {
                throw new SerializationException(e.Message, e);
            }
        }

        public void GetObjectData(SerializationInfo info, StreamingContext context)
        {
            if (info == null)
                throw new ArgumentNullException(nameof(info));

            try
            {
                if (_value == IntPtr.Zero)
                    throw new SerializationException(SR.Serialization_InvalidFieldState);

                RuntimeTypeHandle declaringType;
                MethodNameAndSignature nameAndSignature;
                RuntimeTypeHandle[] genericArgs;
                RuntimeAugments.TypeLoaderCallbacks.GetRuntimeMethodHandleComponents(this, out declaringType, out nameAndSignature, out genericArgs);

                // This is either a RuntimeMethodInfo or a RuntimeConstructorInfo
                MethodBase method = MethodInfo.GetMethodFromHandle(this, declaringType);
                info.AddValue("MethodObj", method, typeof(MethodBase));
            }
            catch (Exception e) when (!(e is SerializationException))
            {
                throw new SerializationException(e.Message, e);
            }
        }
    }
}
