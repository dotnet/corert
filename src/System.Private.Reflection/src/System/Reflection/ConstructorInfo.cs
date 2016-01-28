// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.


/*============================================================
**
  Type:  ConstructorInfo
**
==============================================================*/

using global::System;

namespace System.Reflection
{
    public abstract class ConstructorInfo : MethodBase
    {
        protected ConstructorInfo()
        {
        }

        public static readonly String ConstructorName = ".ctor";

        public static readonly String TypeConstructorName = ".cctor";

        // Equals() and GetHashCode() implement reference equality for compatibility with desktop.
        // Unfortunately, this means that implementors who don't unify instances will be on the hook
        // to override these implementations to test for semantic equivalence.
        public override bool Equals(object obj)
        {
            return base.Equals(obj);
        }

        public override int GetHashCode()
        {
            return base.GetHashCode();
        }

        public virtual Object Invoke(Object[] parameters)
        {
            throw NotImplemented.ByDesign;
        }
    }
}

