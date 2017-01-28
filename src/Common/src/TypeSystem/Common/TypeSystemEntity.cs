// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Internal.TypeSystem
{
    public abstract class TypeSystemEntity
    {
        /// <summary>
        /// Gets the type system context this entity belongs to.
        /// </summary>
        public abstract TypeSystemContext Context { get; }

        /// <summary>
        /// Gets the kind of the type system entity (type, method, field, etc.).
        /// </summary>
        public abstract EntityKind EntityKind { get; }
    }

    /// <summary>
    /// Specifies the kind of <see cref="TypeSystemEntity"/>. 
    /// </summary>
    public enum EntityKind
    {
        TypeDesc,
        FieldDesc,
        MethodDesc,
        ModuleDesc,
        MethodSignature,
    }
}
