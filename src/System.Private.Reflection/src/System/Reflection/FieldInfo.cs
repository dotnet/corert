// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.


/*============================================================
**
  Type:  FieldInfo
**
==============================================================*/

using global::System;
using global::Internal.Reflection.Augments;

namespace System.Reflection
{
    public abstract class FieldInfo : MemberInfo
    {
        protected FieldInfo()
        {
        }

        public abstract FieldAttributes Attributes { get; }
        public abstract Type FieldType { get; }

        public bool IsAssembly
        {
            get
            {
                return (Attributes & FieldAttributes.FieldAccessMask) == FieldAttributes.Assembly;
            }
        }

        public bool IsFamily
        {
            get
            {
                return (Attributes & FieldAttributes.FieldAccessMask) == FieldAttributes.Family;
            }
        }

        public bool IsFamilyAndAssembly
        {
            get
            {
                return (Attributes & FieldAttributes.FieldAccessMask) == FieldAttributes.FamANDAssem;
            }
        }

        public bool IsFamilyOrAssembly
        {
            get
            {
                return (Attributes & FieldAttributes.FieldAccessMask) == FieldAttributes.FamORAssem;
            }
        }

        public bool IsInitOnly
        {
            get
            {
                return (Attributes & FieldAttributes.InitOnly) != 0;
            }
        }

        public bool IsLiteral
        {
            get
            {
                return (Attributes & FieldAttributes.Literal) != 0;
            }
        }

        public bool IsPrivate
        {
            get
            {
                return (Attributes & FieldAttributes.FieldAccessMask) == FieldAttributes.Private;
            }
        }

        public bool IsPublic
        {
            get
            {
                return (Attributes & FieldAttributes.FieldAccessMask) == FieldAttributes.Public;
            }
        }

        public bool IsSpecialName
        {
            get
            {
                return (Attributes & FieldAttributes.SpecialName) != 0;
            }
        }

        public bool IsStatic
        {
            get
            {
                return (Attributes & FieldAttributes.Static) != 0;
            }
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


        public static FieldInfo GetFieldFromHandle(RuntimeFieldHandle handle)
        {
            return ReflectionAugments.ReflectionCoreCallbacks.GetFieldFromHandle(handle);
        }

        public static FieldInfo GetFieldFromHandle(RuntimeFieldHandle handle, RuntimeTypeHandle declaringType)
        {
            return ReflectionAugments.ReflectionCoreCallbacks.GetFieldFromHandle(handle, declaringType);
        }

        public abstract Object GetValue(Object obj);

        public virtual void SetValue(Object obj, Object value)
        {
            throw NotImplemented.ByDesign;
        }
    }
}

