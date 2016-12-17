// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Diagnostics;
using Internal.Runtime.Augments;

namespace Internal.Runtime.CompilerServices
{
    public struct RuntimeSignature
    {
        private IntPtr _ptrField;
        private int _intField;
        private bool _isNativeLayoutSignature;

        public static RuntimeSignature CreateFromNativeLayoutSignature(IntPtr moduleHandle, int token)
        {
            return new RuntimeSignature
            {
                _ptrField = moduleHandle,
                _intField = token,
                _isNativeLayoutSignature = true,
            };
        }

        public static RuntimeSignature CreateFromMethodHandle(IntPtr moduleHandle, int token)
        {
            return new RuntimeSignature
            {
                _ptrField = moduleHandle,
                _intField = token,
                _isNativeLayoutSignature = false,
            };
        }

        public bool IsNativeLayoutSignature
        {
            get
            {
                return _isNativeLayoutSignature;
            }
        }

        public int Token
        {
            get
            {
                return _intField;
            }
        }

        public IntPtr ModuleHandle
        {
            get
            {
                return _ptrField;
            }
        }

        public bool Equals(RuntimeSignature other)
        {
            if (IsNativeLayoutSignature == other.IsNativeLayoutSignature)
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
        public bool StructuralEquals(RuntimeSignature other)
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
}
