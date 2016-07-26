// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Reflection;
using System.Diagnostics;
using System.Collections.Generic;

using System.Reflection.Runtime.General;
using System.Reflection.Runtime.TypeInfos;
using System.Reflection.Runtime.CustomAttributes;

using Internal.Reflection.Core;
using Internal.Reflection.Core.Execution;

using Internal.Metadata.NativeFormat;

using FieldAccessException = System.MemberAccessException;

namespace System.Reflection.Runtime.FieldInfos
{
    internal sealed class LiteralFieldAccessor : FieldAccessor
    {
        public LiteralFieldAccessor(Object value)
        {
            _value = value;
        }

        public sealed override Object GetField(Object obj)
        {
            return _value;
        }

        public sealed override void SetField(Object obj, Object value)
        {
            throw new FieldAccessException(SR.Acc_ReadOnly);
        }

        private readonly Object _value;
    }
}

