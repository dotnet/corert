// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.


using System;
using Internal.Runtime.TypeLoader;

namespace Internal.TypeSystem.Ecma
{
    public partial class EcmaModule
    {
        public EcmaModuleInfo RuntimeModuleInfo { get; private set; }

        public void SetRuntimeModuleInfoUNSAFE(EcmaModuleInfo moduleInfo) { RuntimeModuleInfo = moduleInfo; }
    }
}
