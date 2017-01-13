﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;

using Internal.Runtime.TypeLoader;

namespace Internal.Runtime.CompilerHelpers
{
    public class ILT_ModuleCctorContainer
    {
        public static void ILT_cctor()
        {
            TypeLoaderEnvironment.ILT_cctor();
        }
    }
}
