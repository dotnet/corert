// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.


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

