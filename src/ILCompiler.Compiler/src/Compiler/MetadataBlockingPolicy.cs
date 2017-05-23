// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Internal.TypeSystem;

using Debug = System.Diagnostics.Debug;

namespace ILCompiler
{
    /// <summary>
    /// Represents a metadata blocking policy. A metadata blocking policy decides what types or members are never
    /// eligible to have their metadata generated into the executable.
    /// </summary>
    public abstract class MetadataBlockingPolicy
    {
        /// <summary>
        /// Returns true if type definition '<paramref name="type"/>' is reflection blocked.
        /// </summary>
        public abstract bool IsBlocked(MetadataType type);

        /// <summary>
        /// Returns true if method definition '<paramref name="method"/>' is reflection blocked.
        /// </summary>
        public abstract bool IsBlocked(MethodDesc method);

        /// <summary>
        /// Returns true if field definition '<paramref name="field"/>' is reflection blocked.
        /// </summary>
        public abstract bool IsBlocked(FieldDesc field);
    }

    /// <summary>
    /// Represents a metadata policy that blocks implementations details.
    /// </summary>
    public sealed class BlockedInternalsBlockingPolicy : MetadataBlockingPolicy
    {
        public override bool IsBlocked(MetadataType type)
        {
            Debug.Assert(type.IsTypeDefinition);

            // TODO: Make this also respect System.Runtime.CompilerServices.DisablePrivateReflectionAttribute
            return !(type is Internal.TypeSystem.Ecma.EcmaType);
        }

        public override bool IsBlocked(MethodDesc method)
        {
            Debug.Assert(method.IsTypicalMethodDefinition);

            // TODO: Make this also respect System.Runtime.CompilerServices.DisablePrivateReflectionAttribute
            return !(method is Internal.TypeSystem.Ecma.EcmaMethod);
        }

        public override bool IsBlocked(FieldDesc field)
        {
            Debug.Assert(field.IsTypicalFieldDefinition);

            // TODO: Make this also respect System.Runtime.CompilerServices.DisablePrivateReflectionAttribute
            return !(field is Internal.TypeSystem.Ecma.EcmaField);
        }
    }
}
