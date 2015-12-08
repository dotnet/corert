// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;

namespace Hashcode
{
    class NonNestedType
    {
        class NestedType
        {

        }

        void GenericMethod<T>()
        { }
    }

    class GenericType<X,Y>
    {
        void GenericMethod<T>()
        {
        }
    }
}
