// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Internal.TypeSystem
{
    public enum GenericParameterKind
    {
        Type,
        Method,
    }

    public abstract partial class GenericParameterDesc : TypeDesc
    {
        /// <summary>
        /// Gets a value indicating whether this is a type or method generic parameter.
        /// </summary>
        public abstract GenericParameterKind Kind { get; }
        
        /// <summary>
        /// Gets the zero based index of the generic parameter within the declaring type or method.
        /// </summary>
        public abstract int Index { get; }
    }
}
