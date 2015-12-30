// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;

namespace System.Runtime.InteropServices.ComTypes
{
    [StructLayout(LayoutKind.Sequential)]
    public struct FILETIME
    {
        public int dwLowDateTime;
        public int dwHighDateTime;
    }
}
