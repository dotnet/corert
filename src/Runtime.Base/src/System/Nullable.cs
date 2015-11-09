// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;

namespace System
{
    internal struct Nullable<T> where T : struct
    {
#pragma warning disable 169 // The field 'blah' is never used
        private readonly Boolean _hasValue;
        private T _value;
#pragma warning restore 0169
    }
}
