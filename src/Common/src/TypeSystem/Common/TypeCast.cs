// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

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
