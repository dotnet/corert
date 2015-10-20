// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Runtime;
using System.Diagnostics;
using System.Collections.Generic;

namespace Internal.Reflection.Core.NonPortable
{
    //
    // This structure serves as the dictionary key for looking up constructed generic types.
    //
    internal struct ConstructedGenericTypeKey : IEquatable<ConstructedGenericTypeKey>
    {
        public ConstructedGenericTypeKey(RuntimeType genericTypeDefinition, RuntimeType[] genericTypeArguments)
        {
            Debug.Assert(genericTypeDefinition != null);
            Debug.Assert(genericTypeArguments != null);

            _genericTypeDefinition = genericTypeDefinition;
            _genericTypeArguments = genericTypeArguments;
        }

        public bool IsAvailable
        {
            get
            {
                return _genericTypeDefinition != null;
            }
        }

        public RuntimeType GenericTypeDefinition
        {
            get
            {
                return _genericTypeDefinition;
            }
        }

        public RuntimeType[] GenericTypeArguments
        {
            get
            {
                return _genericTypeArguments;
            }
        }

        public override bool Equals(Object obj)
        {
            if (!(obj is ConstructedGenericTypeKey))
                return false;
            return Equals((ConstructedGenericTypeKey)obj);
        }

        public bool Equals(ConstructedGenericTypeKey other)
        {
            if (!_genericTypeDefinition.Equals(other._genericTypeDefinition))
                return false;

            if (_genericTypeArguments.Length != other._genericTypeArguments.Length)
                return false;

            for (int i = 0; i < _genericTypeArguments.Length; i++)
            {
                if (!_genericTypeArguments[i].Equals(other._genericTypeArguments[i]))
                    return false;
            }
            return true;
        }

        public override int GetHashCode()
        {
            uint hashcode = (uint)_genericTypeDefinition.GetHashCode();
            for (int i = 0; i < _genericTypeArguments.Length; i++)
                hashcode = (hashcode + ((hashcode << 11) + (hashcode >> 21))) ^ (uint)_genericTypeArguments[i].GetHashCode();
            return (int)hashcode;
        }

        public static readonly ConstructedGenericTypeKey Unavailable = default(ConstructedGenericTypeKey);

        private RuntimeType _genericTypeDefinition;
        private RuntimeType[] _genericTypeArguments;
    }
}
