// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Runtime.InteropServices;

namespace System
{
    //
    // Project N port note: Project N supports production of RuntimeMethodHandles through ldtoken to support 
    // Linq Expressions. We purposely leave the public apis on RuntimeMethodHandle unsupported as this
    // is not needed for that purpose and it only serves to build dependencies on implementation details
    // such as generic sharing.
    //
    [StructLayout(LayoutKind.Sequential)]
    public partial struct RuntimeMethodHandle
    {
        private IntPtr _value;

        public override bool Equals(Object obj)
        {
            throw new PlatformNotSupportedException();
        }

        public bool Equals(RuntimeMethodHandle handle)
        {
            throw new PlatformNotSupportedException();
        }

        public override int GetHashCode()
        {
            throw new PlatformNotSupportedException();
        }

        public static bool operator ==(RuntimeMethodHandle left, RuntimeMethodHandle right)
        {
            throw new PlatformNotSupportedException();
        }

        public static bool operator !=(RuntimeMethodHandle left, RuntimeMethodHandle right)
        {
            throw new PlatformNotSupportedException();
        }
    }
}
