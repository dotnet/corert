// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Runtime.CompilerServices;
using Internal.Runtime;

namespace System.Runtime
{
    /// <summary>
    /// TypeManagerHandle represents an AOT module in MRT based runtimes.
    /// These handles are either a pointer to an OS module, or a pointer to a TypeManager
    /// When this is a pointer to a TypeManager, then the pointer will have its lowest bit
    /// set to indicate that it is a TypeManager pointer instead of OS module.
    /// </summary>
    public partial struct TypeManagerHandle
    {
        private IntPtr _handleValue;

        public TypeManagerHandle(IntPtr handleValue)
        {
            _handleValue = handleValue;
        }

        public IntPtr GetIntPtrUNSAFE()
        {
            return _handleValue;
        }

        public override int GetHashCode()
        {
            return _handleValue.GetHashCode();
        }

        public override bool Equals(object o)
        {
            if (!(o is TypeManagerHandle))
                return false;

            return _handleValue == ((TypeManagerHandle)o)._handleValue;
        }

        public static bool operator ==(TypeManagerHandle left, TypeManagerHandle right)
        {
            return left._handleValue == right._handleValue;
        }

        public static bool operator !=(TypeManagerHandle left, TypeManagerHandle right)
        {
            return left._handleValue != right._handleValue;
        }

        public bool Equals(TypeManagerHandle other)
        {
            return _handleValue == other._handleValue;
        }

        public bool IsNull
        {
            get
            {
                return _handleValue == IntPtr.Zero;
            }
        }
    }
}
