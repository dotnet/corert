// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;

namespace System.Reflection
{
    public abstract class MemberInfo : ICustomAttributeProvider
    {
        protected MemberInfo() { }

        public abstract MemberTypes MemberType { get; }
        public abstract string Name { get; }
        public abstract Type DeclaringType { get; }
        public abstract Type ReflectedType { get; }

        public virtual Module Module
        {
            get
            {
                // This check is necessary because for some reason, Type adds a new "Module" property that hides the inherited one instead 
                // of overriding.

                // @todo: Restore as soon as we finish Type.
                //Type type = this as Type;
                //if (type != null)
                //    return type.Module;

                throw NotImplemented.ByDesign;
            }
        }

        public abstract bool IsDefined(Type attributeType, bool inherit);
        public abstract object[] GetCustomAttributes(bool inherit);
        public abstract object[] GetCustomAttributes(Type attributeType, bool inherit);

        public virtual IEnumerable<CustomAttributeData> CustomAttributes => GetCustomAttributesData();
        public virtual IList<CustomAttributeData> GetCustomAttributesData() { throw NotImplemented.ByDesign; }

        public virtual int MetadataToken { get { throw new InvalidOperationException(); } }

        public override bool Equals(object obj) => base.Equals(obj);
        public override int GetHashCode() => base.GetHashCode();
    }
}
