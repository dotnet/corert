// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;

using Internal.Metadata.NativeFormat.Writer;

using Cts = Internal.TypeSystem;
using Ecma = System.Reflection.Metadata;

using Debug = System.Diagnostics.Debug;
using CallingConventions = System.Reflection.CallingConventions;

namespace ILCompiler.Metadata
{
    public partial class Transform<TPolicy>
    {
        private MetadataRecord HandleMethod(Cts.MethodDesc method)
        {
            throw new NotImplementedException();
        }

        private Method HandleMethodDefinition(Cts.MethodDesc method)
        {
            Debug.Assert(_policy.GeneratesMetadata(method));
            throw new NotImplementedException();
        }
    }
}
