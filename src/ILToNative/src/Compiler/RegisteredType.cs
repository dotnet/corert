// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;

using Internal.TypeSystem;

namespace ILToNative
{
    class RegisteredType
    {
        public TypeDesc Type;

        public bool IncludedInCompilation;
        public bool Constructed;

        public int UniqueMethod;

        public string MangledName;

        public RegisteredMethod Methods;

        public List<MethodDesc> VirtualSlots;
    }
}
