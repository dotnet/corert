// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Reflection;
using System.Diagnostics;

namespace System.Reflection.Runtime.CustomAttributes
{
    //
    // If a CustomAttributeData implementation derives from this, it is a hint that it has a AttributeType implementation
    // that's more efficient than building a ConstructorInfo and gettings its DeclaredType.
    //
    public abstract class RuntimeImplementedCustomAttributeData : CustomAttributeData
    {
        public new abstract Type AttributeType { get; }
    }
}

