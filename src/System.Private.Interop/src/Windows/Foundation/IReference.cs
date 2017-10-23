// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Windows.Foundation
{
    [System.Runtime.CompilerServices.DependencyReductionRootAttribute]
    public interface IReference<T>
    {
        [System.Runtime.InteropServices.McgAccessor(System.Runtime.InteropServices.McgAccessorKind.PropertyGet, "Value")]
        T get_Value();
    }
}
