// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Runtime.CompilerServices;

namespace Internal.TypeSystem
{
    public abstract partial class MethodDesc
    {
        public virtual bool IsIntrinsic
        {
            get
            {
                return false;
            }
        }
    }
}
