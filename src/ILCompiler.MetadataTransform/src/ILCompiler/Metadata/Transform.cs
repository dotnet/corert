// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Internal.Metadata.NativeFormat.Writer;

using Cts = Internal.TypeSystem;

namespace ILCompiler.Metadata
{
    public abstract class Transform
    {
        public abstract MetadataRecord HandleType(Cts.TypeDesc type);

        // TODO: HandleTypeForwarder
    }

    public partial class Transform<TPolicy> : Transform
        where TPolicy : struct, IMetadataPolicy
    {
        private TPolicy _policy;

        public Transform(TPolicy policy)
        {
            _policy = policy;
        }

        private bool IsBlocked(Cts.TypeDesc type)
        {
            if (type.IsArray || type.IsByRef || type.IsPointer)
                return IsBlocked(((Cts.ParameterizedType)type).ParameterType);

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