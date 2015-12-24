// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using global::System;
using global::System.Reflection;
using global::System.Diagnostics;
using global::System.Collections.Generic;

using global::System.Reflection.Runtime.General;
using global::System.Reflection.Runtime.TypeInfos;
using global::System.Reflection.Runtime.CustomAttributes;

using global::Internal.Reflection.Core;
using global::Internal.Reflection.Core.Execution;
using global::Internal.Reflection.Core.NonPortable;

using global::Internal.Metadata.NativeFormat;

using FieldAccessException = global::System.MemberAccessException;

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

        private Object _value;
    }
}

