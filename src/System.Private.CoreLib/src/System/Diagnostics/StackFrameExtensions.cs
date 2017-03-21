// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;

namespace System.Diagnostics
{
    public static class StackFrameExtensions
    {
        /// <summary>
        /// Return load address of the native image pointed to by the stack frame.
        /// </summary>
        public static IntPtr GetNativeImageBase(this StackFrame stackFrame)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Return stack frame native IP address.
        /// </summary>
        public static IntPtr GetNativeIP(this StackFrame stackFrame)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Return true when the stack frame information can be converted to IL offset information
        /// within the MSIL method body.
        /// </summary>
        public static bool HasILOffset(this StackFrame stackFrame)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Return true when a MethodBase reflection info is available for the stack frame
        /// </summary>
        public static bool HasMethod(this StackFrame stackFrame)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Return true when the stack frame information corresponds to a native image
        /// </summary>
        public static bool HasNativeImage(this StackFrame stackFrame)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Return true when stack frame information supports source file / line information lookup
        /// </summary>
        public static bool HasSource(this StackFrame stackFrame)
        {
            throw new NotImplementedException();
        }
    }
}
