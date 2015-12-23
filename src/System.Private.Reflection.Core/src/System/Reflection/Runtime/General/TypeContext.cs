// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using global::System;
using global::System.Diagnostics;
using global::Internal.Reflection.Core.NonPortable;

namespace System.Reflection.Runtime.General
{
    //
    // Passed as an argument to code that parses signatures or typespecs. Specifies the subsitution values for ET_VAR and ET_MVAR elements inside the signature.
    // Both may be null if no generic parameters are expected.
    //

    internal struct TypeContext
    {
        internal TypeContext(RuntimeType[] genericTypeArguments, RuntimeType[] genericMethodArguments)
        {
            _genericTypeArguments = genericTypeArguments;
            _genericMethodArguments = genericMethodArguments;
        }

        internal RuntimeType[] GenericTypeArguments
        {
            get
            {
                return _genericTypeArguments;
            }
        }

        internal RuntimeType[] GenericMethodArguments
        {
            get
            {
                return _genericMethodArguments;
            }
        }

        private RuntimeType[] _genericTypeArguments;
        private RuntimeType[] _genericMethodArguments;
    }
}

