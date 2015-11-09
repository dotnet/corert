// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

// System.Type and System.RuntimeTypeHandle are defined here as the C# compiler requires them
// In the redhawk runtime these are not used. In the class library there is an implementation that support typeof

using System;
using System.Runtime.InteropServices;

namespace System
{
    internal class Type
    {
        public RuntimeTypeHandle TypeHandle { get { return default(RuntimeTypeHandle); } }
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct RuntimeTypeHandle
    {
        private EETypePtr _pEEType;
    }
}
