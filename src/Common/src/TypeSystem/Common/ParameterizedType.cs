// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;

namespace Internal.TypeSystem
{
    public abstract class ParameterizedType : TypeDesc
    {
        TypeDesc _parameterType;

        internal ParameterizedType(TypeDesc parameterType)
        {
            _parameterType = parameterType;
        }

        public TypeDesc ParameterType
        {
            get
            {
                return _parameterType;
            }
        }

        public override TypeSystemContext Context
        {
            get 
            { 
                return _parameterType.Context;
            }
        }
    }
}
