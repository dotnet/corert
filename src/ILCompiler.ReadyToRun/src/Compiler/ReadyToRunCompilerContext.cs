// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Diagnostics;

using Internal.TypeSystem;

namespace ILCompiler
{
    public partial class ReadyToRunCompilerContext : CompilerTypeSystemContext
    {
        private FieldLayoutAlgorithm _r2rFieldLayoutAlgorithm;

        public ReadyToRunCompilerContext(TargetDetails details, SharedGenericsMode genericsMode)
            : base(details, genericsMode)
        {
        }

        public void InitializeAlgorithm(int numberOfTypesInModule)
        {
            Debug.Assert(_r2rFieldLayoutAlgorithm == null);
            _r2rFieldLayoutAlgorithm = new ReadyToRunMetadataFieldLayoutAlgorithm(Target, numberOfTypesInModule);
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
