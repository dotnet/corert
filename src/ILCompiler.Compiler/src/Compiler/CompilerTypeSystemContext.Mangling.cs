﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using Internal.TypeSystem;

namespace ILCompiler
{
    // Contains functionality related to name mangling
    partial class CompilerTypeSystemContext
    {
        partial class BoxedValueType : IPrefixMangledType
        {
            TypeDesc IPrefixMangledType.BaseType
            {
                get
                {
                    return ValueTypeRepresented;
                }
            }

            string IPrefixMangledType.Prefix
            {
                get
                {
                    return "Boxed";
                }
            }
        }

        partial class GenericUnboxingThunk : IPrefixMangledMethod
        {
            MethodDesc IPrefixMangledMethod.BaseMethod
            {
                get
                {
                    return _targetMethod;
                }
            }

            string IPrefixMangledMethod.Prefix
            {
                get
                {
                    return "unbox";
                }
            }
        }
    }
}
