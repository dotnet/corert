// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Globalization;
using Internal.Reflection.Augments;

namespace System.Reflection
{
    public abstract class MethodBase : MemberInfo
    {
        protected MethodBase() { }

        public abstract ParameterInfo[] GetParameters();
        public abstract MethodAttributes Attributes { get; }
        public virtual MethodImplAttributes MethodImplementationFlags => GetMethodImplementationFlags();
        public abstract MethodImplAttributes GetMethodImplementationFlags();
        public virtual MethodBody GetMethodBody() { throw new InvalidOperationException(); }
        public virtual CallingConventions CallingConvention => CallingConventions.Standard;

        public bool IsAbstract => (Attributes & MethodAttributes.Abstract) != 0;
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
        public bool IsFinal => (Attributes & MethodAttributes.Final) != 0;
        public bool IsHideBySig => (Attributes & MethodAttributes.HideBySig) != 0;
        public bool IsSpecialName => (Attributes & MethodAttributes.SpecialName) != 0;
        public bool IsStatic => (Attributes & MethodAttributes.Static) != 0;
        public bool IsVirtual => (Attributes & MethodAttributes.Virtual) != 0;

        public bool IsAssembly => (Attributes & MethodAttributes.MemberAccessMask) == MethodAttributes.Assembly;
        public bool IsFamily => (Attributes & MethodAttributes.MemberAccessMask) == MethodAttributes.Family;
        public bool IsFamilyAndAssembly => (Attributes & MethodAttributes.MemberAccessMask) == MethodAttributes.FamANDAssem;
        public bool IsFamilyOrAssembly => (Attributes & MethodAttributes.MemberAccessMask) == MethodAttributes.FamORAssem;
        public bool IsPrivate => (Attributes & MethodAttributes.MemberAccessMask) == MethodAttributes.Private;
        public bool IsPublic => (Attributes & MethodAttributes.MemberAccessMask) == MethodAttributes.Public;

        public virtual bool IsGenericMethod => false;
        public virtual bool IsGenericMethodDefinition => false;
        public virtual Type[] GetGenericArguments() { throw new NotSupportedException(SR.NotSupported_SubclassOverride); }
        public virtual bool ContainsGenericParameters => false;

        public object Invoke(object obj, object[] parameters) => Invoke(obj, BindingFlags.Default, binder: null, parameters: parameters, culture: null);
        public abstract object Invoke(object obj, BindingFlags invokeAttr, Binder binder, object[] parameters, CultureInfo culture);

        public abstract RuntimeMethodHandle MethodHandle { get; }
        public static MethodBase GetMethodFromHandle(RuntimeMethodHandle handle) => ReflectionAugments.ReflectionCoreCallbacks.GetMethodFromHandle(handle);
        public static MethodBase GetMethodFromHandle(RuntimeMethodHandle handle, RuntimeTypeHandle declaringType) => ReflectionAugments.ReflectionCoreCallbacks.GetMethodFromHandle(handle, declaringType);

        public static MethodBase GetCurrentMethod() { throw new NotImplementedException(); }

        public override bool Equals(object obj) => base.Equals(obj);
        public override int GetHashCode() => GetHashCode();

        public static bool operator ==(MethodBase left, MethodBase right)
        {
            if (object.ReferenceEquals(left, right))
                return true;

            if ((object)left == null || (object)right == null)
                return false;

            MethodInfo method1, method2;
            ConstructorInfo constructor1, constructor2;

            if ((method1 = left as MethodInfo) != null && (method2 = right as MethodInfo) != null)
                return method1 == method2;
            else if ((constructor1 = left as ConstructorInfo) != null && (constructor2 = right as ConstructorInfo) != null)
                return constructor1 == constructor2;

            return false;
        }

        public static bool operator !=(MethodBase left, MethodBase right) => !(left == right);

        // This is not an api but needs to be declared public so that System.Private.Reflection.Core can access (and override it)
        public virtual ParameterInfo[] GetParametersNoCopy() => GetParameters();
    }
}
