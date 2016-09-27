// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Internal.TypeSystem
{
    partial class FunctionPointerType
    {
        public override bool IsRuntimeDeterminedSubtype
        {
            get
            {
                if (_signature.ReturnType.IsRuntimeDeterminedSubtype)
                    return true;

                for (int i = 0; i < _signature.Length; i++)
                    if (_signature[i].IsRuntimeDeterminedSubtype)
                        return true;

                return false;
            }
        }
    }
}
