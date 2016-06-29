// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Internal.TypeSystem
{
    /// <summary>
    /// Represents an algorithm that converts types into the canonical form. The canonical form captures the
    /// shape of a type in regards to code generation and GC layout. Static and instance method bodies of any
    /// two types with a matching canonical form can share the same code.
    /// </summary>
    public abstract class CanonicalizationAlgorithm
    {
        /// <summary>
        /// Converts the instantiation into a canonical form. Returns the canonical instantiation. The '<paramref name="changed"/>'
        /// parameter indicates whether the returned canonical instantiation is different from the specific instantiation
        /// passed as the input.
        /// </summary>
        public abstract Instantiation ConvertInstantiationToCanonForm(Instantiation instantiation, CanonicalFormKind kind, out bool changed);

        /// <summary>
        /// Converts a constituent of a constructed type to it's canonical form. Note this method is different
        /// from <see cref="TypeDesc.ConvertToCanonForm(CanonicalFormKind)"/>.
        /// </summary>
        public abstract TypeDesc ConvertToCanon(TypeDesc typeToConvert, CanonicalFormKind kind);
    }
}
