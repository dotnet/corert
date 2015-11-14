// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;

using Internal.TypeSystem;

namespace ILCompiler
{
    class RegisteredMethod
    {
        public MethodDesc Method;

        public bool IncludedInCompilation;

        public Object MethodCode;
    }
}
