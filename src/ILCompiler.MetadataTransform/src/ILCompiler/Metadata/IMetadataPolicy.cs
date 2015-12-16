﻿// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Internal.Metadata.NativeFormat.Writer;

using Cts = Internal.TypeSystem;

namespace ILCompiler.Metadata
{
    /// <summary>
    /// Controls metadata generation policy. Decides what types and members will get metadata.
    /// </summary>
    public interface IMetadataPolicy
    {
        /// <summary>
        /// Returns true if the type should generate <see cref="TypeDefinition"/> metadata. If false,
        /// the type should generate a <see cref="TypeReference"/>.
        /// </summary>
        /// <param name="typeDef">Uninstantiated type definition to check.</param>
        bool GeneratesMetadata(Cts.MetadataType typeDef);

        /// <summary>
        /// Returns true if the method should generate <see cref="Method"/> metadata. If false,
        /// the method should generate a <see cref="MemberReference"/> when needed.
        /// </summary>
        /// <param name="methodDef">Uninstantiated method definition to check.</param>
        bool GeneratesMetadata(Cts.MethodDesc methodDef);

        /// <summary>
        /// Returns true if the field should generate <see cref="Field"/> metadata. If false,
        /// the field should generate a <see cref="MemberReference"/> when needed.
        /// </summary>
        /// <param name="fieldDef">Uninstantiated field definition to check.</param>
        bool GeneratesMetadata(Cts.FieldDesc fieldDef);

        /// <summary>
        /// Returns true if a type should be blocked from generating any metadata.
        /// Blocked interfaces are skipped from interface lists, and custom attributes referring to
        /// blocked types are dropped from metadata.
        /// </summary>
        bool IsBlocked(Cts.MetadataType typeDef);
    }
}
