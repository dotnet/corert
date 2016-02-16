// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using Internal.Metadata.NativeFormat.Writer;

using Cts = Internal.TypeSystem;

namespace ILCompiler.Metadata
{
    /// <summary>
    /// Provides implementation of the <see cref="MetadataTransform"/> contract.
    /// This class is generic over the policy to make policy lookups cheap (policy being
    /// a struct means all the interface calls end up being constrained over the type
    /// and therefore fully inlineable).
    /// </summary>
    internal sealed partial class Transform<TPolicy> : MetadataTransform
        where TPolicy : struct, IMetadataPolicy
    {
        private TPolicy _policy;
        private HashSet<Cts.ModuleDesc> _modulesToTransform;

        public Transform(TPolicy policy, IEnumerable<Cts.ModuleDesc> modules)
        {
            _policy = policy;
            _modulesToTransform = new HashSet<Cts.ModuleDesc>(modules);
        }

        private bool IsBlocked(Cts.TypeDesc type)
        {
            if (type.IsArray || type.IsByRef || type.IsPointer)
                return IsBlocked(((Cts.ParameterizedType)type).ParameterType);

            if (type is Cts.SignatureVariable)
                return false;

            if (!type.IsTypeDefinition)
            {
                if (IsBlocked(type.GetTypeDefinition()))
                    return true;

                foreach (var arg in type.Instantiation)
                    if (IsBlocked(arg))
                        return true;

                return false;
            }

            return _policy.IsBlocked((Cts.MetadataType)type);
        }
    }
}
