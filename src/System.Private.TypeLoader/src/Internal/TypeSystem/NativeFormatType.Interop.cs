// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.


using System;
using System.Collections.Generic;
using System.Reflection;
using Internal.Metadata.NativeFormat;
using System.Threading;
using Debug = System.Diagnostics.Debug;

using Internal.TypeSystem;
using Internal.NativeFormat;

namespace Internal.TypeSystem.NativeFormat
{
    public sealed partial class NativeFormatType
    {
        public override PInvokeStringFormat PInvokeStringFormat
        {
            get
            {
                return (PInvokeStringFormat)(_typeDefinition.Flags & TypeAttributes.StringFormatMask);
            }
        }
    }
}
