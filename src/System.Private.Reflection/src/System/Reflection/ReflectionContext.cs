// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.


/*============================================================
**
  Type:  ReflectionContext
**
==============================================================*/

using global::System;

namespace System.Reflection
{
    public abstract class ReflectionContext
    {
        protected ReflectionContext() { }

        public abstract Assembly MapAssembly(Assembly assembly);

        public abstract TypeInfo MapType(TypeInfo type);

        public virtual TypeInfo GetTypeForObject(Object value)
        {
            if (value == null)
                throw new ArgumentNullException("value");

            return MapType(value.GetType().GetTypeInfo());
        }
    }
}

