// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.


/*============================================================
**
  Type:  MethodInfo
**
==============================================================*/

using global::System;

namespace System.Reflection
{
    public abstract class MethodInfo : MethodBase
    {
        protected MethodInfo()
        {
        }

        public virtual ParameterInfo ReturnParameter
        {
            get
            {
                throw NotImplemented.ByDesign;
            }
        }

        public virtual Type ReturnType
        {
            get
            {
                throw NotImplemented.ByDesign;
            }
        }

        public virtual Delegate CreateDelegate(Type delegateType)
        {
            throw new NotSupportedException(SR.NotSupported_SubclassOverride);
        }

        public virtual Delegate CreateDelegate(Type delegateType, object target)
        {
            throw new NotSupportedException(SR.NotSupported_SubclassOverride);
        }

        public override Type[] GetGenericArguments()
        {
            throw new NotSupportedException(SR.NotSupported_SubclassOverride);
        }

        public virtual MethodInfo GetGenericMethodDefinition()
        {
            throw new NotSupportedException(SR.NotSupported_SubclassOverride);
        }

        public virtual MethodInfo MakeGenericMethod(params Type[] typeArguments)
        {
            throw new NotSupportedException(SR.NotSupported_SubclassOverride);
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

