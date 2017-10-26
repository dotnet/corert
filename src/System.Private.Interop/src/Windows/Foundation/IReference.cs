// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Windows.Foundation
{
    [global::System.Runtime.CompilerServices.DependencyReductionRootAttribute]
    public interface IReference<T>
    {
        [global::System.Runtime.InteropServices.McgAccessor(global::System.Runtime.InteropServices.McgAccessorKind.PropertyGet, "Value")]
        T get_Value();
    }
}
