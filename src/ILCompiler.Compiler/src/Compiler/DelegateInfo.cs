// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;

using Internal.IL.Stubs;
using Internal.TypeSystem;
using Internal.IL;

namespace ILCompiler
{
    public class DelegateInfo
    {
        public DelegateInfo(Compilation compilation, MethodDesc target)
        {
            this.Target = target;

            var systemDelegate = compilation.TypeSystemContext.GetWellKnownType(WellKnownType.MulticastDelegate).BaseType;

            if (target.Signature.IsStatic)
            {
                this.ShuffleThunk = new DelegateShuffleThunk(target);

                this.Ctor = systemDelegate.GetKnownMethod("InitializeClosedStaticThunk", null);
            }
            else
            {
                this.Ctor = systemDelegate.GetKnownMethod("InitializeClosedInstance", null);
            }
        }

        public MethodDesc Target { get; private set; }
        public MethodDesc Ctor { get; private set; }
        public DelegateShuffleThunk ShuffleThunk { get; private set; }
    }
}
