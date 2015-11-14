// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Runtime.CompilerServices;

namespace Internal.TypeSystem
{
    // Additional members of MethodForInstantiatedType related to code generation.
    public partial class MethodForInstantiatedType
    {
        public override bool IsIntrinsic
        {
            get
            {
                return _typicalMethodDef.IsIntrinsic;
            }
        }

        public override bool IsNoInlining
        {
            get
            {
                return _typicalMethodDef.IsNoInlining;
            }
        }

        public override bool IsAggressiveInlining
        {
            get
            {
                return _typicalMethodDef.IsAggressiveInlining;
            }
        }
    }
}
