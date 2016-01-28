// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.


/*============================================================
**
  Type:  PropertyInfo
**
==============================================================*/

using global::System;

namespace System.Reflection
{
    public abstract class PropertyInfo : MemberInfo
    {
        protected PropertyInfo()
        {
        }

        public abstract PropertyAttributes Attributes { get; }
        public abstract bool CanRead { get; }
        public abstract bool CanWrite { get; }

        public virtual MethodInfo GetMethod
        {
            get
            {
                throw NotImplemented.ByDesign;
            }
        }

        public bool IsSpecialName
        {
            get
            {
                return (Attributes & PropertyAttributes.SpecialName) != 0;
            }
        }

        public abstract Type PropertyType { get; }

        public virtual MethodInfo SetMethod
        {
            get
            {
                throw NotImplemented.ByDesign;
            }
        }

        public virtual Object GetConstantValue()
        {
            throw NotImplemented.ByDesign;
        }

        public abstract ParameterInfo[] GetIndexParameters();

        public Object GetValue(Object obj)
        {
            return GetValue(obj, null);
        }

        public virtual Object GetValue(Object obj, Object[] index)
        {
            throw NotImplemented.ByDesign;
        }

        public void SetValue(Object obj, Object value)
        {
            SetValue(obj, value, null);
        }

        public virtual void SetValue(Object obj, Object value, Object[] index)
        {
            throw NotImplemented.ByDesign;
        }

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

