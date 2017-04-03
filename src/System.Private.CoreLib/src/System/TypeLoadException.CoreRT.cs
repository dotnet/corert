// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace System
{
    public partial class TypeLoadException
    {
        internal TypeLoadException(string message, string typeName)
            : base(message)
        {
            HResult = __HResults.COR_E_TYPELOAD;
            _typeName = typeName;
        }
    }
}
