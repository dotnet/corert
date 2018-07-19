// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO;
using System.IO.MemoryMappedFiles;
using System.Diagnostics;
using System.Collections.Generic;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;

using Internal.TypeSystem;
using Internal.TypeSystem.Ecma;
using Internal.IL;

using Interlocked = System.Threading.Interlocked;

namespace ILCompiler
{
    public partial class ReadyToRunCompilerContext : CompilerTypeSystemContext
    {
        private FieldLayoutAlgorithm _r2rFieldLayoutAlgorithm;

        public ReadyToRunCompilerContext(TargetDetails details, SharedGenericsMode genericsMode)
            : base(details, genericsMode)
        {
        }

        public void InitializeAlgorithm(TargetDetails target, int numberOfTypesInModule)
        {
            Debug.Assert(_r2rFieldLayoutAlgorithm == null);
            _r2rFieldLayoutAlgorithm = new ReadyToRunMetadataFieldLayoutAlgorithm(target, numberOfTypesInModule);
        }

        public override FieldLayoutAlgorithm GetLayoutAlgorithmForType(DefType type)
        {
            if (type == UniversalCanonType)
                throw new NotImplementedException();
            else if (type.IsRuntimeDeterminedType)
                throw new NotImplementedException();
            /* TODO
            else if (_simdHelper.IsVectorOfT(type))
                throw new NotImplementedException();
            */
            else
            {
                Debug.Assert(_r2rFieldLayoutAlgorithm != null);
                return _r2rFieldLayoutAlgorithm;
            }
        }
    }
}
