// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;

using Internal.IL.Stubs;
using Internal.TypeSystem;

namespace ILToNative
{
    public class DelegateInfo
    {
        public DelegateInfo(Compilation compilation, MethodDesc target)
        {
            this.Target = target;

            var systemDelegate = compilation.TypeSystemContext.GetWellKnownType(WellKnownType.MulticastDelegate).BaseType;

            // TODO: Delegates on valuetypes
            if (target.OwningType.IsValueType)
                throw new NotImplementedException();

            if (target.Signature.IsStatic)
            {
                var shuffleThunk = new DelegateShuffleThunk(target);
                compilation.AddMethod(shuffleThunk);
                this.ShuffleThunk = shuffleThunk;

                this.Ctor = systemDelegate.GetMethod("InitializeClosedStaticThunk", null);
            }
            else
            {
                this.Ctor = systemDelegate.GetMethod("InitializeClosedInstance", null);
            }
        }

        public MethodDesc Target { get; private set; }
        public MethodDesc Ctor { get; private set; }
        public DelegateShuffleThunk ShuffleThunk { get; private set; }
    }
}
