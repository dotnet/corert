// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;

namespace System.Runtime.CompilerServices
{
    // This Enum matchs the miImpl flags defined in corhdr.h. It is used to specify 
    // certain method properties.
    [Flags]
    [System.Runtime.InteropServices.ComVisible(true)]
    public enum MethodImplOptions
    {
        // These should stay in-sync with System.Reflection.MethodImplAttributes
        NoInlining = 0x0008,
        //ForwardRef         =   0x0010,
        NoOptimization = 0x0040,
        PreserveSig = 0x0080,
        AggressiveInlining = 0x0100,
        InternalCall = 0x1000,
    }

    // Custom attribute to specify additional method properties.
    [AttributeUsage(AttributeTargets.Method | AttributeTargets.Constructor, Inherited = false)]
    [System.Runtime.InteropServices.ComVisible(true)]
    sealed public class MethodImplAttribute : Attribute
    {
        internal MethodImplOptions _val;

        public MethodImplAttribute(MethodImplOptions methodImplOptions)
        {
            _val = methodImplOptions;
        }

        public MethodImplOptions Value { get { return _val; } }
    }
}
