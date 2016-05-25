// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Internal.Metadata.NativeFormat.Writer;

using Cts = Internal.TypeSystem;

namespace ILCompiler.Metadata
{
    /// <summary>
    /// Controls metadata generation policy. Decides what types and members will get metadata.
    /// </summary>
    /// <remarks>
    /// Thread safety: the implementers are required to be thread safe.
    /// </remarks>
    public interface IMetadataPolicy
    {
        /// <summary>
        /// Returns true if the type should generate <see cref="TypeDefinition"/> metadata. If false,
        /// the type will generate a <see cref="TypeReference"/> if required within the object graph.
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

        /// <summary>
        /// Return the Module that should be treated as defining the type. Typically implementations
        /// will return typeDef.Module, but in some circumstances it may return a different value.
        /// </summary>
        Cts.ModuleDesc GetModuleOfType(Cts.MetadataType typeDef);
    }
}
