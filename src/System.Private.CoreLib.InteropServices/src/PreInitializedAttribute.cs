// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Threading;

namespace System.Runtime.CompilerServices
{
    // This attribute should be placed on a static field by the code author to indicate that it is expected to
    // be completely pre-initialized by the tool chain.  The tool chain will produce an error if the field
    // cannot be completely pre-initialized.
    [AttributeUsage(AttributeTargets.Field)]
    public sealed class PreInitializedAttribute : Attribute
    {
    }
}
