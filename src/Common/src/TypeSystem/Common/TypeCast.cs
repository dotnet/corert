// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;

namespace Internal.TypeSystem
{
    static public class TypeCast
    {
        //
        // Is the source type derived from the target type?
        //
        static public bool IsDerived(TypeDesc derivedType, TypeDesc baseType)
        {
            for (;;)
            {
                if (derivedType == baseType)
                    return true;

                derivedType = derivedType.BaseType;
                if (derivedType == null)
                    return false;
            }
        }
    }
}
