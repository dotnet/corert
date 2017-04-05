// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Runtime.InteropServices;
 
namespace System
{
    [StructLayout(LayoutKind.Sequential)]
    public struct ArgIterator
    {
        public ArgIterator(RuntimeArgumentHandle arglist)
        {
            throw new PlatformNotSupportedException(SR.PlatformNotSupported_ArgIterator); // https://github.com/dotnet/corert/issues/395
        }

        [CLSCompliant(false)]
        public unsafe ArgIterator(RuntimeArgumentHandle arglist, void* ptr)
        {
            throw new PlatformNotSupportedException(SR.PlatformNotSupported_ArgIterator); // https://github.com/dotnet/corert/issues/395
        }

        public void End()
        {
            throw new PlatformNotSupportedException(SR.PlatformNotSupported_ArgIterator); // https://github.com/dotnet/corert/issues/395
        }

        public override bool Equals(object o)
        {
            throw new PlatformNotSupportedException(SR.PlatformNotSupported_ArgIterator); // https://github.com/dotnet/corert/issues/395
        }

        public override int GetHashCode()
        {
            throw new PlatformNotSupportedException(SR.PlatformNotSupported_ArgIterator); // https://github.com/dotnet/corert/issues/395
        }

        [CLSCompliant(false)]
        public TypedReference GetNextArg()
        {
            throw new PlatformNotSupportedException(SR.PlatformNotSupported_ArgIterator); // https://github.com/dotnet/corert/issues/395
        }

        [CLSCompliant(false)]
        public TypedReference GetNextArg(RuntimeTypeHandle rth)
        {
            throw new PlatformNotSupportedException(SR.PlatformNotSupported_ArgIterator); // https://github.com/dotnet/corert/issues/395
        }

        public unsafe RuntimeTypeHandle GetNextArgType()
        {
            throw new PlatformNotSupportedException(SR.PlatformNotSupported_ArgIterator); // https://github.com/dotnet/corert/issues/395
        }

        public int GetRemainingCount()
        {
            throw new PlatformNotSupportedException(SR.PlatformNotSupported_ArgIterator); // https://github.com/dotnet/corert/issues/395
        }
    }
}
