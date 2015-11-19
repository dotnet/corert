// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Runtime.CompilerServices;

namespace Internal.TypeSystem
{
    // Additional members of InstantiatedMethod related to code generation.
    public partial class InstantiatedMethod
    {
        public override bool IsIntrinsic
        {
            get
            {
                return _methodDef.IsIntrinsic;
            }
        }

        public override bool IsNoInlining
        {
            get
            {
                return _methodDef.IsNoInlining;
            }
        }

        public override bool IsAggressiveInlining
        {
            get
            {
                return _methodDef.IsAggressiveInlining;
            }
        }
    }
}
