// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.


using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Reflection;
using System.Threading;
using Internal.Metadata.NativeFormat;
using Internal.Runtime.CompilerServices;

using Internal.TypeSystem;

namespace Internal.TypeSystem.Ecma
{
    public sealed partial class EcmaMethod
    {        
        public override MethodNameAndSignature NameAndSignature
        {
            get
            {
                throw new NotImplementedException();
/*                int handleAsToken = _handle.ToInt();

                IntPtr moduleHandle = Internal.Runtime.TypeLoader.ModuleList.Instance.GetModuleForMetadataReader(MetadataReader);
                return new MethodNameAndSignature(Name, RuntimeMethodSignature.CreateFromMethodHandle(moduleHandle, handleAsToken));*/
            }
        }
    }
}
