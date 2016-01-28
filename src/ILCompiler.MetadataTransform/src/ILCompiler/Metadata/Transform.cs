// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using Internal.Metadata.NativeFormat.Writer;

using Cts = Internal.TypeSystem;

namespace ILCompiler.Metadata
{
    public abstract class Transform
    {
        public abstract IEnumerable<ScopeDefinition> Scopes { get; }

        public abstract MetadataRecord HandleType(Cts.TypeDesc type);

        // TODO: HandleTypeForwarder
    }

    public partial class Transform<TPolicy> : Transform
        where TPolicy : struct, IMetadataPolicy
    {
        private TPolicy _policy;

        public override IEnumerable<ScopeDefinition> Scopes
        {
            get
            {
                return _scopeDefs.Records;
            }
        }

        public Transform(TPolicy policy)
        {
            _policy = policy;
        }

        private bool IsBlocked(Cts.TypeDesc type)
        {
            if (type.IsArray || type.IsByRef || type.IsPointer)
                return IsBlocked(((Cts.ParameterizedType)type).ParameterType);

            if (type is Cts.SignatureVariable)
                return false;

            if (type is Cts.InstantiatedType)
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
