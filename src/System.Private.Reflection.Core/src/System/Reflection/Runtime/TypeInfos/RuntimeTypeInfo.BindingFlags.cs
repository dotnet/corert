// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Diagnostics;
using System.Reflection.Runtime.BindingFlagSupport;

using Internal.LowLevelLinq;

namespace System.Reflection.Runtime.TypeInfos
{
    internal abstract partial class RuntimeTypeInfo
    {
        public sealed override ConstructorInfo[] GetConstructors(BindingFlags bindingAttr) =>  LowLevelTypeExtensions.GetConstructors(this, bindingAttr);

        protected sealed override ConstructorInfo GetConstructorImpl(BindingFlags bindingAttr, Binder binder, CallingConventions callConvention, Type[] types, ParameterModifier[] modifiers)
        {
            Debug.Assert(types != null);

            if (callConvention != CallingConventions.Any)
                throw new NotImplementedException();

            if (!OnlySearchRelatedBitsSet(bindingAttr))  // We don't yet have proper handling for BindingFlags not related to search so throw rather return a wrong result.
                throw new NotImplementedException();

            if (binder == null)
                binder = Type.DefaultBinder;
            ConstructorInfo[] candidates = GetConstructors(bindingAttr);
            return (ConstructorInfo)binder.SelectMethod(bindingAttr, candidates, types, modifiers);
        }

        public sealed override EventInfo[] GetEvents(BindingFlags bindingAttr) => LowLevelTypeExtensions.GetEvents(this, bindingAttr);
        public sealed override EventInfo GetEvent(string name, BindingFlags bindingAttr) => LowLevelTypeExtensions.GetEvent(this, name, bindingAttr);

        public sealed override FieldInfo[] GetFields(BindingFlags bindingAttr) => LowLevelTypeExtensions.GetFields(this, bindingAttr);
        public sealed override FieldInfo GetField(string name, BindingFlags bindingAttr) => LowLevelTypeExtensions.GetField(this, name, bindingAttr);

        public sealed override MemberInfo[] GetMembers(BindingFlags bindingAttr) => LowLevelTypeExtensions.GetMembers(this, bindingAttr);
        public sealed override MemberInfo[] GetMember(string name, BindingFlags bindingAttr) => LowLevelTypeExtensions.GetMember(this, name, bindingAttr);

        public sealed override MethodInfo[] GetMethods(BindingFlags bindingAttr) => LowLevelTypeExtensions.GetMethods(this, bindingAttr);

        protected sealed override MethodInfo GetMethodImpl(string name, BindingFlags bindingAttr, Binder binder, CallingConventions callConvention, Type[] types, ParameterModifier[] modifiers)
        {
            Debug.Assert(name != null);

            // GetMethodImpl() is a funnel for two groups of api. We can distinguish by comparing "types" to null.
            if (types == null)
            {
                // Group #1: This group of api accept only a name and BindingFlags. The other parameters are hard-wired by the non-virtual api entrypoints. 
                Debug.Assert(binder == null);
                Debug.Assert(callConvention == CallingConventions.Any);
                Debug.Assert(modifiers == null);
                return LowLevelTypeExtensions.GetMethod(this, name, bindingAttr);
            }
            else
            {
                if (!OnlySearchRelatedBitsSet(bindingAttr))  // We don't yet have proper handling for BindingFlags not related to search so throw rather return a wrong result.
                    throw new NotImplementedException();

                // Group #2: This group of api takes a set of parameter types and an optional binder. 
                if (callConvention != CallingConventions.Any)
                    throw new NotImplementedException();
                if (binder == null)
                    binder = Type.DefaultBinder;
                MethodInfo[] candidates = LowLevelTypeExtensions.GetMethods(this, name, bindingAttr);
                return (MethodInfo)binder.SelectMethod(bindingAttr, candidates, types, modifiers);
            }
        }

        public sealed override Type[] GetNestedTypes(BindingFlags bindingAttr) => LowLevelTypeExtensions.GetNestedTypes(this, bindingAttr);
        public sealed override Type GetNestedType(string name, BindingFlags bindingAttr) => LowLevelTypeExtensions.GetNestedType(this, name, bindingAttr);

        public sealed override PropertyInfo[] GetProperties(BindingFlags bindingAttr) => LowLevelTypeExtensions.GetProperties(this, bindingAttr);

        protected sealed override PropertyInfo GetPropertyImpl(string name, BindingFlags bindingAttr, Binder binder, Type returnType, Type[] types, ParameterModifier[] modifiers)
        {
            Debug.Assert(name != null);

            if (!OnlySearchRelatedBitsSet(bindingAttr))  // We don't yet have proper handling for BindingFlags not related to search so throw rather return a wrong result.
                throw new NotImplementedException();

            // GetPropertyImpl() is a funnel for two groups of api. We can distinguish by comparing "types" to null.
            if (types == null && returnType == null)
            {
                // Group #1: This group of api accept only a name and BindingFlags. The other parameters are hard-wired by the non-virtual api entrypoints. 
                Debug.Assert(binder == null);
                Debug.Assert(modifiers == null);
                return LowLevelTypeExtensions.GetProperty(this, name, bindingAttr);
            }
            else
            {
                if (!OnlySearchRelatedBitsSet(bindingAttr))  // We don't yet have proper handling for BindingFlags not related to search so throw rather return a wrong result.
                    throw new NotImplementedException();

                // Group #2: This group of api takes a set of parameter types, a return type (both cannot be null) and an optional binder. 
                if (binder == null)
                    binder = Type.DefaultBinder;
                PropertyInfo[] candidates = LowLevelTypeExtensions.GetProperties(this, name, bindingAttr);
                return binder.SelectProperty(bindingAttr, candidates, returnType, types, modifiers);
            }
        }

        private static bool OnlySearchRelatedBitsSet(BindingFlags bindingFlags)
        {
            const BindingFlags SearchRelatedBits = BindingFlags.IgnoreCase | BindingFlags.DeclaredOnly | BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.FlattenHierarchy;
            return (bindingFlags & ~SearchRelatedBits) == 0;
        }
    }
}

