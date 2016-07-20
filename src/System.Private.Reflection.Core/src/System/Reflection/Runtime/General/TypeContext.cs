// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using global::System;
using global::System.Diagnostics;
using global::System.Reflection.Runtime.General;
using global::System.Reflection.Runtime.TypeInfos;

using global::Internal.Reflection.Core.NonPortable;

namespace System.Reflection.Runtime.General
{
    //
    // Passed as an argument to code that parses signatures or typespecs. Specifies the subsitution values for ET_VAR and ET_MVAR elements inside the signature.
    // Both may be null if no generic parameters are expected.
    //

    internal struct TypeContext
    {
        internal TypeContext(RuntimeTypeInfo[] genericTypeArguments, RuntimeTypeInfo[] genericMethodArguments)
        {
            _genericTypeArguments = genericTypeArguments;
            _genericMethodArguments = genericMethodArguments;
        }

        internal RuntimeTypeInfo[] GenericTypeArguments
        {
            get
            {
                return _genericTypeArguments;
            }
        }

        internal RuntimeTypeInfo[] GenericMethodArguments
        {
            get
            {
                return _genericMethodArguments;
            }
        }

        private RuntimeTypeInfo[] _genericTypeArguments;
        private RuntimeTypeInfo[] _genericMethodArguments;
    }
}

