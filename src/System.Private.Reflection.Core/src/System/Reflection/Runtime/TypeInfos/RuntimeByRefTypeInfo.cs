// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Reflection;
using System.Diagnostics;
using System.Collections.Generic;
using System.Reflection.Runtime.General;

namespace System.Reflection.Runtime.TypeInfos
{
    //
    // The runtime's implementation of TypeInfo's for byref types. 
    //
    internal sealed partial class RuntimeByRefTypeInfo : RuntimeHasElementTypeInfo
    {
        private RuntimeByRefTypeInfo(UnificationKey key)
            : base(key)
        {
        }

        protected sealed override bool IsByRefImpl()
        {
            return true;
        }

        protected sealed override string Suffix
        {
            get
            {
                return "&";
            }
        }
    }
}
