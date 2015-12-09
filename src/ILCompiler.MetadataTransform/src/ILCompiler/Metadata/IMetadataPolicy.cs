// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Cts = Internal.TypeSystem;

namespace ILCompiler.Metadata
{
    /// <summary>
    /// Controls metadata generation policy. Decides what types and members will get metadata.
    /// </summary>
    public interface IMetadataPolicy
    {
        /// <summary>
        /// Returns true if the type should generate TypeDefinition metadata. If false,
        /// the type should generate a TypeReference.
        /// </summary>
        /// <param name="typeDef">Uninstantiated type definition to check.</param>
        bool GeneratesMetadata(Cts.MetadataType typeDef);

        /// <summary>
        /// Returns true if a type should be blocked from generating any metadata.
        /// Blocked interfaces are skipped from interface lists, and custom attributes referring to
        /// blocked types are dropped from metadata.
        /// </summary>
        bool IsBlocked(Cts.MetadataType typeDef);
    }
}
