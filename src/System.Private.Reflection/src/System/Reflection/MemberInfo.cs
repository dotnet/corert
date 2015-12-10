// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.


/*============================================================
**
  Type:  MemberInfo
**
==============================================================*/

using global::System;
using global::System.Collections.Generic;

namespace System.Reflection
{
    public abstract class MemberInfo
    {
        protected MemberInfo()
        {
        }

        public virtual IEnumerable<CustomAttributeData> CustomAttributes
        {
            get
            {
                throw NotImplemented.ByDesign;
            }
        }

        public abstract Type DeclaringType { get; }

        public virtual Module Module
        {
            get
            {
                throw NotImplemented.ByDesign;
            }
        }

        public abstract String Name { get; }

        // Equals() and GetHashCode() implement reference equality for compatibility with desktop.
        // Unfortunately, this means that implementors who don't unify instances will be on the hook
        // to override these implementations to test for semantic equivalence.
        public override bool Equals(Object obj)
        {
            return base.Equals(obj);
        }

        public override int GetHashCode()
        {
            return base.GetHashCode();
        }
    }
}

