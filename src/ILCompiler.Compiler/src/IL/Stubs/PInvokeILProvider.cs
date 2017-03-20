﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;

using Internal.TypeSystem;

using Internal.IL.Stubs;

namespace Internal.IL
{
    /// <summary>
    /// Wraps the API and configuration for a particular PInvoke IL emitter. Eventually this will
    /// allow ILProvider to switch out its PInvoke IL generator with another, such as MCG.
    /// </summary>
    class PInvokeILProvider
    {
        private readonly PInvokeILEmitterConfiguration _pInvokeILEmitterConfiguration;
        private readonly InteropStateManager _interopStateManager;

        public PInvokeILProvider(PInvokeILEmitterConfiguration pInvokeILEmitterConfiguration, InteropStateManager interopStateManager)
        {
            _pInvokeILEmitterConfiguration = pInvokeILEmitterConfiguration;
            _interopStateManager = interopStateManager;
        }

        public MethodIL EmitIL(MethodDesc method)
        {
            return PInvokeILEmitter.EmitIL(method, _pInvokeILEmitterConfiguration, _interopStateManager);
        }
    }
}
