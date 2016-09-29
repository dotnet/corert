// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Reflection;
using System.Runtime.InteropServices;
using System;
using Internal.Runtime.Augments;
using System.Diagnostics;

namespace Internal.Runtime.CompilerServices
{
    public struct RuntimeMethodSignature
    {
        private IntPtr _ptrField;
        private bool _isNativeLayoutSignature;
        private int _intField;

        public static RuntimeMethodSignature CreateFromNativeLayoutSignature(IntPtr signature)
        {
            return new RuntimeMethodSignature
            {
                _ptrField = signature,
                _isNativeLayoutSignature = true
            };
        }

        public static RuntimeMethodSignature CreateFromMethodHandle(IntPtr moduleHandle, int token)
        {
            return new RuntimeMethodSignature
            {
                _ptrField = moduleHandle,
                _isNativeLayoutSignature = false,
                _intField = token
            };
        }

        public bool IsNativeLayoutSignature
        {
            get
            {
                return _isNativeLayoutSignature;
            }
        }

        public IntPtr NativeLayoutSignature
        {
            get
            {
                if (_isNativeLayoutSignature)
                    return _ptrField;
                else
                    return IntPtr.Zero;
            }
        }

        public int Token
        {
            get
            {
                if (!_isNativeLayoutSignature)
                    return _intField;
                else
                    return 0;
            }
        }

        public IntPtr ModuleHandle
        {
            get
            {
                if (!_isNativeLayoutSignature)
                    return _ptrField;
                else
                    return IntPtr.Zero;
            }
        }

        public bool Equals(RuntimeMethodSignature other)
        {
            if (IsNativeLayoutSignature && other.IsNativeLayoutSignature)
            {
                if (NativeLayoutSignature == other.NativeLayoutSignature)
                    return true;
            }
            else if (!IsNativeLayoutSignature && !other.IsNativeLayoutSignature)
            {
                if ((ModuleHandle == other.ModuleHandle) && (Token == other.Token))
                    return true;
            }

            // Walk both signatures to check for equality the slow way
            return RuntimeAugments.TypeLoaderCallbacks.CompareMethodSignatures(this, other);
        }

        /// <summary>
        /// Fast equality check
        /// </summary>
        public bool StructuralEquals(RuntimeMethodSignature other)
        {
            if (_ptrField != other._ptrField)
                return false;

            if (_intField != other._intField)
                return false;

            if (_isNativeLayoutSignature != other._isNativeLayoutSignature)
                return false;

            return true;
        }
    }

    [System.Runtime.CompilerServices.DependencyReductionRoot]
    public class MethodNameAndSignature
    {
        public string Name { get; private set; }
        public RuntimeMethodSignature Signature { get; private set; }

        public MethodNameAndSignature(string name, RuntimeMethodSignature signature)
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

            return Signature.Equals(other.Signature);
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
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Security", "CA2111:PointersShouldNotBeVisible")]
        public IntPtr NativeLayoutInfoSignature;

        public static unsafe RuntimeMethodHandle InfoToHandle(RuntimeMethodHandleInfo* info)
        {
            RuntimeMethodHandle returnValue = default(RuntimeMethodHandle);
            *(RuntimeMethodHandleInfo**)&returnValue = info;
            return returnValue;
        }
    }
}
