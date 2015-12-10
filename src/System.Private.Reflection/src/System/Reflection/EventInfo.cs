// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.


/*============================================================
**
  Type:  EventInfo
**
==============================================================*/

using global::System;

namespace System.Reflection
{
    public abstract class EventInfo : MemberInfo
    {
        protected EventInfo()
        {
        }

        public virtual MethodInfo AddMethod
        {
            get
            {
                throw NotImplemented.ByDesign;
            }
        }

        public abstract EventAttributes Attributes { get; }

        public virtual Type EventHandlerType
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
                return (Attributes & EventAttributes.SpecialName) != 0;
            }
        }

        public virtual MethodInfo RaiseMethod
        {
            get
            {
                throw NotImplemented.ByDesign;
            }
        }

        public virtual MethodInfo RemoveMethod
        {
            get
            {
                throw NotImplemented.ByDesign;
            }
        }


        public virtual void AddEventHandler(Object target, Delegate handler)
        {
            MethodInfo addMethod = AddMethod;
            if (!addMethod.IsPublic)
                throw new InvalidOperationException(SR.InvalidOperation_NoPublicAddMethod);

            addMethod.Invoke(target, new object[] { handler });
        }

        public virtual void RemoveEventHandler(Object target, Delegate handler)
        {
            MethodInfo removeMethod = RemoveMethod;
            if (!removeMethod.IsPublic)
                throw new InvalidOperationException(SR.InvalidOperation_NoPublicRemoveMethod);

            removeMethod.Invoke(target, new object[] { handler });
        }


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
    }
}

