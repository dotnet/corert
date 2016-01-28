// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.


/*============================================================
**
  Type:  Module
**
==============================================================*/

using global::System;
using global::System.Collections.Generic;

namespace System.Reflection
{
    public abstract class Module
    {
        protected Module()
        {
        }

        public virtual Assembly Assembly
        {
            get
            {
                throw NotImplemented.ByDesign;
            }
        }

        public virtual IEnumerable<CustomAttributeData> CustomAttributes
        {
            get
            {
                throw NotImplemented.ByDesign;
            }
        }

        public virtual string FullyQualifiedName
        {
            get
            {
                throw NotImplemented.ByDesign;
            }
        }

        public virtual String Name
        {
            get
            {
                throw NotImplemented.ByDesign;
            }
        }

        // Equals() and GetHashCode() implement reference equality for compatibility with desktop.
        // Unfortunately, this means that implementors who don't unify instances will be on the hook
        // to override these implementations to test for semantic equivalence.
        public override bool Equals(Object o)
        {
            return base.Equals(o);
        }

        public override int GetHashCode()
        {
            return base.GetHashCode();
        }

        public virtual Type GetType(String name, bool throwOnError, bool ignoreCase)
        {
            throw NotImplemented.ByDesign;
        }

        public override String ToString()
        {
            throw NotImplemented.ByDesign;
        }
    }
}

