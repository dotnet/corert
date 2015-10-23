// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Internal.TypeSystem;

namespace ILToNative
{
    enum IntrinsicMethodKind
    {
        None,
        RuntimeHelpersInitializeArray,
    }

    class IntrinsicMethods
    {
        public static IntrinsicMethodKind GetIntrinsicMethodClassification(MethodDesc method)
        {
            // TODO: make this reliable
            if (method.Name == "InitializeArray" && method.OwningType.Name == "System.Runtime.CompilerServices.RuntimeHelpers")
            {
                return IntrinsicMethodKind.RuntimeHelpersInitializeArray;
            }

            return IntrinsicMethodKind.None;
        }
    }
}
