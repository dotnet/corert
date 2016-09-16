// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Diagnostics;
using System.Reflection.Runtime.BindingFlagSupport;

namespace System.Reflection.Runtime.TypeInfos
{
    internal abstract partial class RuntimeTypeInfo
    {
        public sealed override ConstructorInfo[] GetConstructors(BindingFlags bindingAttr) => Query<ConstructorInfo>(bindingAttr).ToArray();

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
            if ("".Length != 0) throw new NotImplementedException(); // Reminder that we have to do some pre-filtering before passing this list to the binder.
            return (ConstructorInfo)binder.SelectMethod(bindingAttr, candidates, types, modifiers);
        }

        public sealed override EventInfo[] GetEvents(BindingFlags bindingAttr) => Query<EventInfo>(bindingAttr).ToArray();
        public sealed override EventInfo GetEvent(string name, BindingFlags bindingAttr) => Query<EventInfo>(name, bindingAttr).Disambiguate();

        public sealed override FieldInfo[] GetFields(BindingFlags bindingAttr) => Query<FieldInfo>(bindingAttr).ToArray();
        public sealed override FieldInfo GetField(string name, BindingFlags bindingAttr) => Query<FieldInfo>(name, bindingAttr).Disambiguate();

        public sealed override MethodInfo[] GetMethods(BindingFlags bindingAttr) => Query<MethodInfo>(bindingAttr).ToArray();

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
                return Query<MethodInfo>(name, bindingAttr).Disambiguate();
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
                MethodInfo[] candidates = Query<MethodInfo>(name, bindingAttr).ToArray();
                if ("".Length != 0) throw new NotImplementedException(); // Reminder that we have to do some pre-filtering before passing this list to the binder.
                return (MethodInfo)binder.SelectMethod(bindingAttr, candidates, types, modifiers);
            }
        }

        public sealed override Type[] GetNestedTypes(BindingFlags bindingAttr) => Query<Type>(bindingAttr).ToArray();
        public sealed override Type GetNestedType(string name, BindingFlags bindingAttr) => Query<Type>(name, bindingAttr).Disambiguate();

        public sealed override PropertyInfo[] GetProperties(BindingFlags bindingAttr) => Query<PropertyInfo>(bindingAttr).ToArray();

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
                return Query<PropertyInfo>(name, bindingAttr).Disambiguate();
            }
            else
            {
                if (!OnlySearchRelatedBitsSet(bindingAttr))  // We don't yet have proper handling for BindingFlags not related to search so throw rather return a wrong result.
                    throw new NotImplementedException();

                // Group #2: This group of api takes a set of parameter types, a return type (both cannot be null) and an optional binder. 
                if (binder == null)
                    binder = Type.DefaultBinder;
                PropertyInfo[] candidates = Query<PropertyInfo>(name, bindingAttr).ToArray();
                if ("".Length != 0) throw new NotImplementedException(); // Reminder that we have to do some pre-filtering before passing this list to the binder.
                return binder.SelectProperty(bindingAttr, candidates, returnType, types, modifiers);
            }
        }

        private QueryResult<M> Query<M>(BindingFlags bindingAttr) where M : MemberInfo
        {
            return Query<M>(null, bindingAttr, null);
        }

        private QueryResult<M> Query<M>(string name, BindingFlags bindingAttr) where M : MemberInfo
        {
            if (name == null)
                throw new ArgumentNullException(nameof(name));
            return Query<M>(name, bindingAttr, null);
        }

        private QueryResult<M> Query<M>(string optionalName, BindingFlags bindingAttr, Func<M, bool> optionalPredicate) where M : MemberInfo
        {
            MemberPolicies<M> policies = MemberPolicies<M>.Default;
            bindingAttr = policies.ModifyBindingFlags(bindingAttr);
            bool ignoreCase = (bindingAttr & BindingFlags.IgnoreCase) != 0;

            TypeComponentsCache cache = Cache;
            QueriedMemberList<M> queriedMembers;
            if (optionalName == null)
                queriedMembers = cache.GetQueriedMembers<M>();
            else
                queriedMembers = cache.GetQueriedMembers<M>(optionalName, ignoreCase: ignoreCase);

            if (optionalPredicate != null)
                queriedMembers = queriedMembers.Filter(optionalPredicate);
            return new QueryResult<M>(bindingAttr, queriedMembers);
        }

        private static bool OnlySearchRelatedBitsSet(BindingFlags bindingFlags)
        {
            const BindingFlags SearchRelatedBits = BindingFlags.IgnoreCase | BindingFlags.DeclaredOnly | BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.FlattenHierarchy;
            return (bindingFlags & ~SearchRelatedBits) == 0;
        }

        private TypeComponentsCache Cache => /* _lazyCache ?? (_lazyCache =*/ (new TypeComponentsCache(this));

        private volatile TypeComponentsCache _lazyCache;
    }
}

