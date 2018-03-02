// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Internal.Runtime.CompilerServices;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;

namespace System.Runtime.InteropServices
{
    public enum McgStructMarshalFlags
    {
        None,

        /// <summary>
        /// This struct has invalid layout information, most likely because it is marked LayoutKind.Auto
        /// </summary>
        HasInvalidLayout
    }

    [CLSCompliant(false)]
    public struct McgStructMarshalData
    {
        public FixupRuntimeTypeHandle FixupSafeStructType;
        public FixupRuntimeTypeHandle FixupUnsafeStructType;
        public RuntimeTypeHandle SafeStructType
        {
            get
            {
                return FixupSafeStructType.RuntimeTypeHandle;
            }
        }
        public RuntimeTypeHandle UnsafeStructType
        {
            get
            {
                return FixupUnsafeStructType.RuntimeTypeHandle;
            }
        }
        public IntPtr MarshalStub;
        public IntPtr UnmarshalStub;
        public IntPtr DestroyStructureStub;

        public McgStructMarshalFlags Flags;

        /// <summary>
        /// This struct has invalid layout information, most likely because it is marked LayoutKind.Auto
        /// We'll throw exception when this struct is getting marshalled
        /// </summary>
        public bool HasInvalidLayout
        {
            get
            {
                return (Flags & McgStructMarshalFlags.HasInvalidLayout) != 0;
            }
        }

        public int FieldOffsetStartIndex;   // start index to its field offset data
        public int NumOfFields;             // number of fields
    }

    [CLSCompliant(false)]
    public struct McgUnsafeStructFieldOffsetData
    {
        public uint Offset; // offset value  in bytes
    }

    /// <summary>
    /// Captures data for each P/invoke delegate type we decide to import
    /// </summary>
    [CLSCompliant(false)]
    public struct McgPInvokeDelegateData
    {
        /// <summary>
        /// Type of the delegate
        /// </summary>
        public FixupRuntimeTypeHandle FixupDelegate;
        public RuntimeTypeHandle Delegate
        {
            get
            {
                return FixupDelegate.RuntimeTypeHandle;
            }
        }
        /// <summary>
        /// The stub called from thunk that does the marshalling when calling managed delegate (as a function
        /// pointer) from native code
        /// </summary>
        public IntPtr ReverseStub;

        /// <summary>
        /// The stub called from thunk that does the marshalling when calling managed open static delegate (as a function
        /// pointer) from native code
        /// </summary>
        public IntPtr ReverseOpenStaticDelegateStub;

        /// <summary>
        /// This creates a delegate wrapper class that wraps the native function pointer and allows managed
        /// code to call it
        /// </summary>
        public IntPtr ForwardDelegateCreationStub;
    }
}
