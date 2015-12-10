// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.


/*============================================================
**
  Type:  MethodBase
**
==============================================================*/

using global::System;
using global::Internal.Reflection.Augments;

namespace System.Reflection
{
    public abstract class MethodBase : MemberInfo
    {
        internal MethodBase()
        {
        }


        public abstract MethodAttributes Attributes { get; }

        public virtual CallingConventions CallingConvention { get { return CallingConventions.Standard; } }

        public virtual bool ContainsGenericParameters { get { return false; } }

        public bool IsAbstract
        {
            get
            {
                return (Attributes & MethodAttributes.Abstract) != 0;
            }
        }

        public bool IsAssembly
        {
            get
            {
                return (Attributes & MethodAttributes.MemberAccessMask) == MethodAttributes.Assembly;
            }
        }

        public bool IsConstructor
        {
            get
            {
                // To be backward compatible we only return true for instance RTSpecialName ctors.
                return (this is ConstructorInfo &&
                        !IsStatic &&
                        ((Attributes & MethodAttributes.RTSpecialName) == MethodAttributes.RTSpecialName));
            }
        }

        public bool IsFamily
        {
            get
            {
                return (Attributes & MethodAttributes.MemberAccessMask) == MethodAttributes.Family;
            }
        }

        public bool IsFamilyAndAssembly
        {
            get
            {
                return (Attributes & MethodAttributes.MemberAccessMask) == MethodAttributes.FamANDAssem;
            }
        }

        public bool IsFamilyOrAssembly
        {
            get
            {
                return (Attributes & MethodAttributes.MemberAccessMask) == MethodAttributes.FamORAssem;
            }
        }

        public bool IsFinal
        {
            get
            {
                return (Attributes & MethodAttributes.Final) != 0;
            }
        }

        public virtual bool IsGenericMethod
        {
            get
            {
                return false;
            }
        }

        public virtual bool IsGenericMethodDefinition
        {
            get
            {
                return false;
            }
        }

        public bool IsHideBySig
        {
            get
            {
                return (Attributes & MethodAttributes.HideBySig) != 0;
            }
        }

        public bool IsPrivate
        {
            get
            {
                return (Attributes & MethodAttributes.MemberAccessMask) == MethodAttributes.Private;
            }
        }

        public bool IsPublic
        {
            get
            {
                return (Attributes & MethodAttributes.MemberAccessMask) == MethodAttributes.Public;
            }
        }

        public bool IsSpecialName
        {
            get
            {
                return (Attributes & MethodAttributes.SpecialName) != 0;
            }
        }

        public bool IsStatic
        {
            get
            {
                return (Attributes & MethodAttributes.Static) != 0;
            }
        }

        public bool IsVirtual
        {
            get
            {
                return (Attributes & MethodAttributes.Virtual) != 0;
            }
        }


        public abstract MethodImplAttributes MethodImplementationFlags { get; }

        public virtual Type[] GetGenericArguments()
        {
            throw new NotSupportedException(SR.NotSupported_SubclassOverride);
        }

        public static MethodBase GetMethodFromHandle(RuntimeMethodHandle handle)
        {
            return ReflectionAugments.ReflectionCoreCallbacks.GetMethodFromHandle(handle);
        }

        public static MethodBase GetMethodFromHandle(RuntimeMethodHandle handle, RuntimeTypeHandle declaringType)
        {
            return ReflectionAugments.ReflectionCoreCallbacks.GetMethodFromHandle(handle, declaringType);
        }

        public abstract ParameterInfo[] GetParameters();

        public virtual Object Invoke(Object obj, Object[] parameters)
        {
            throw NotImplemented.ByDesign;
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

