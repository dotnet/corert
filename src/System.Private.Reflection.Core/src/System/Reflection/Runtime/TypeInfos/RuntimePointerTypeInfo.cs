// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using global::System;
using global::System.Reflection;
using global::System.Diagnostics;
using global::System.Collections.Generic;
using global::System.Reflection.Runtime.General;
using global::Internal.Reflection.Core.NonPortable;

namespace System.Reflection.Runtime.TypeInfos
{
    //
    // The runtime's implementation of TypeInfo's for pointer types. 
    //
    internal sealed partial class RuntimePointerTypeInfo : RuntimeHasElementTypeInfo
    {
        private RuntimePointerTypeInfo(UnificationKey key)
            : base(key)
        {
        }

        protected sealed override bool IsPointerImpl()
        {
            return true;
        }

        protected sealed override string Suffix
        {
            get
            {
                return "*";
            }
        }
    }
}
