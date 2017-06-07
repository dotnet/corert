// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.


using System;
using Internal.Runtime.Augments;
using Internal.NativeFormat;
using Internal.Runtime.TypeLoader;
using Internal.Reflection.Execution;
using System.Runtime.InteropServices;

namespace Internal.Runtime.CompilerHelpers
{
    internal partial class RuntimeInteropData : InteropCallbacks
    {
        private static RuntimeInteropData s_interopData;
        public static RuntimeInteropData Instance
        {
            get
            {
                if (s_interopData == null)
                {
                    s_interopData = new RuntimeInteropData();
                }
                return s_interopData;
            }
        }
    }
}
