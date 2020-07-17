// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Globalization;
using System.Collections.Generic;

namespace System.Reflection
{
    public partial class CustomAttributeData
    {
        protected CustomAttributeData() { }

        public virtual Type AttributeType
        {
            get
            {
                return Constructor.DeclaringType;
            }
        }

        public virtual ConstructorInfo Constructor => null;
        public virtual IList<CustomAttributeTypedArgument> ConstructorArguments { get { throw new NullReferenceException(); } }
        public virtual IList<CustomAttributeNamedArgument> NamedArguments => null;

        public override bool Equals(object obj) => obj == (object)this;
        public override int GetHashCode() => base.GetHashCode();

        public override string ToString()
        {
            string ctorArgs = "";
            for (int i = 0; i < ConstructorArguments.Count; i++)
                ctorArgs += string.Format(CultureInfo.CurrentCulture, i == 0 ? "{0}" : ", {0}", ConstructorArguments[i]);

            string namedArgs = "";
            for (int i = 0; i < NamedArguments.Count; i++)
                namedArgs += string.Format(CultureInfo.CurrentCulture, i == 0 && ctorArgs.Length == 0 ? "{0}" : ", {0}", NamedArguments[i]);

            return string.Format(CultureInfo.CurrentCulture, "[{0}({1}{2})]", Constructor.DeclaringType.FullName, ctorArgs, namedArgs);
        }
    }
}
