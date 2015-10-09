// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Reflection;
using System.Runtime.InteropServices;
using System;
using Internal.Runtime.Augments;
using System.Diagnostics;

namespace Internal.Runtime.CompilerServices
{
    [System.Runtime.CompilerServices.DependencyReductionRoot]
    public class MethodNameAndSignature
    {
        public string Name { get; private set; }
        public IntPtr Signature { get; private set; }

        public MethodNameAndSignature(string name, IntPtr signature)
        {
            Name = name;
            Signature = signature;
        }

        public override bool Equals(object compare)
        {
            if (compare == null)
                return false;

            MethodNameAndSignature other = compare as MethodNameAndSignature;
            if (other == null)
                return false;

            if (Name != other.Name)
                return false;

            // Optimistically compare signatures by pointer first
            if (Signature == other.Signature)
                return true;

            // Walk both signatures to check for equality the slow way
            return RuntimeAugments.TypeLoaderCallbacks.CompareMethodSignatures(Signature, other.Signature);
        }

        public override int GetHashCode()
        {
            int hash = Name.GetHashCode();
            
            return hash;
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    [CLSCompliant(false)]
    public unsafe struct RuntimeMethodHandleInfo
    {
        public IntPtr NativeLayoutInfoSignature;

        public static unsafe RuntimeMethodHandle InfoToHandle(RuntimeMethodHandleInfo* info)
        {
            RuntimeMethodHandle returnValue = default(RuntimeMethodHandle);
            *(RuntimeMethodHandleInfo**)&returnValue = info;
            return returnValue;
        }
    }
}
