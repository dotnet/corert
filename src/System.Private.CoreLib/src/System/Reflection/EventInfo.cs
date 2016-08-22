// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace System.Reflection
{
    public abstract class EventInfo : MemberInfo
    {
        protected EventInfo() { }

        public override MemberTypes MemberType => MemberTypes.Event;

        public abstract EventAttributes Attributes { get; }
        public bool IsSpecialName => (Attributes & EventAttributes.SpecialName) != 0;

        public MethodInfo[] GetOtherMethods() => GetOtherMethods(nonPublic: false);
        public virtual MethodInfo[] GetOtherMethods(bool nonPublic) { throw NotImplemented.ByDesign; }

        public virtual MethodInfo AddMethod => GetAddMethod(nonPublic: true);
        public virtual MethodInfo RemoveMethod => GetRemoveMethod(nonPublic: true);
        public virtual MethodInfo RaiseMethod => GetRaiseMethod(nonPublic: true);

        public MethodInfo GetAddMethod() => GetAddMethod(nonPublic: false);
        public MethodInfo GetRemoveMethod() => GetRemoveMethod(nonPublic: false);
        public MethodInfo GetRaiseMethod() => GetRaiseMethod(nonPublic: false);

        public abstract MethodInfo GetAddMethod(bool nonPublic);
        public abstract MethodInfo GetRemoveMethod(bool nonPublic);
        public abstract MethodInfo GetRaiseMethod(bool nonPublic);

        public virtual bool IsMulticast
        {
            get
            {
                Type cl = EventHandlerType;
                Type mc = typeof(MulticastDelegate);
                return mc.GetTypeInfo().IsAssignableFrom(cl.GetTypeInfo());
            }
        }

        public virtual Type EventHandlerType
        {
            get
            {
                MethodInfo m = GetAddMethod(true);
                ParameterInfo[] p = m.GetParameters();
                Type del = typeof(Delegate);
                for (int i = 0; i < p.Length; i++)
                {
                    Type c = p[i].ParameterType;
                    if (c.GetTypeInfo().IsSubclassOf(del.GetTypeInfo()))
                        return c;
                }
                return null;
            }
        }

        public virtual void AddEventHandler(object target, Delegate handler) { throw new NotImplementedException(); }
        public virtual void RemoveEventHandler(object target, Delegate handler) { throw new NotImplementedException(); }

        public override bool Equals(object obj) => base.Equals(obj);
        public override int GetHashCode() => base.GetHashCode();
    }
}
