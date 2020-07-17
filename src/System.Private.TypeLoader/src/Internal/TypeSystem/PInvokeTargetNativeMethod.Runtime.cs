// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.


using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Reflection;
using System.Threading;
using Internal.Metadata.NativeFormat;
using Internal.Runtime.CompilerServices;

using Internal.TypeSystem;

namespace Internal.IL.Stubs
{
    public sealed partial class PInvokeTargetNativeMethod
    {        
        public override MethodNameAndSignature NameAndSignature
        {
            get
            {
                throw new NotImplementedException();
            }
        }
    }
}
