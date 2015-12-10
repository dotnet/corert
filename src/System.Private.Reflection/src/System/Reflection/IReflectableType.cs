// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.


/*============================================================
**
  Type:  IReflectableType
**
==============================================================*/

using global::System;

namespace System.Reflection
{
    public interface IReflectableType
    {
        TypeInfo GetTypeInfo();
    }
}

