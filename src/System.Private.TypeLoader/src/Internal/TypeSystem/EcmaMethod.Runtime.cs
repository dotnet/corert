// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.


using System;
using System.Runtime.CompilerServices;
using System.Reflection;
using Internal.Runtime.CompilerServices;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using Internal.TypeSystem;

namespace Internal.TypeSystem.Ecma
{
    public sealed partial class EcmaMethod
    {        
        public override MethodNameAndSignature NameAndSignature
        {
            get
            {
                int handleAsToken = MetadataTokens.GetToken(_handle);
                IntPtr moduleHandle;
                
                unsafe
                {
                    moduleHandle = new IntPtr(Module.RuntimeModuleInfo.DynamicModulePtr);
                }

                return new MethodNameAndSignature(Name, RuntimeSignature.CreateFromMethodHandle(moduleHandle, handleAsToken));
            }
        }
    }
}
