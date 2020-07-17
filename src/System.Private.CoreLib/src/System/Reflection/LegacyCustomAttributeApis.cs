// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

//
// The older-style CustomAttribute-related members on the various Reflection types. The implementation dependency
// stack on .Net Native differs from that of CoreClr due to the difference in development history.
//
// - IEnumerable<CustomAttributeData> xInfo.get_CustomAttributes is at the very bottom of the dependency stack.
//
// - CustomAttributeExtensions layers on top of that (primarily because it's the one with the nice generic methods.)
//
// - Everything else is a thin layer over one of these two.
//
//

using System.Reflection;
using System.Collections.Generic;
using Internal.LowLevelLinq;

namespace System
{
    public abstract partial class Attribute
    {
        public static Attribute GetCustomAttribute(Assembly element, Type attributeType) => CustomAttributeExtensions.GetCustomAttribute(element, attributeType);
        public static Attribute GetCustomAttribute(Assembly element, Type attributeType, bool inherit) => CustomAttributeExtensions.GetCustomAttribute(element, attributeType); // "inherit" is meaningless for assemblies

        public static Attribute GetCustomAttribute(MemberInfo element, Type attributeType) => CustomAttributeExtensions.GetCustomAttribute(element, attributeType);
        public static Attribute GetCustomAttribute(MemberInfo element, Type attributeType, bool inherit) => CustomAttributeExtensions.GetCustomAttribute(element, attributeType, inherit);

        public static Attribute GetCustomAttribute(Module element, Type attributeType) => CustomAttributeExtensions.GetCustomAttribute(element, attributeType);
        public static Attribute GetCustomAttribute(Module element, Type attributeType, bool inherit) => CustomAttributeExtensions.GetCustomAttribute(element, attributeType); // "inherit" is meaningless for modules

        public static Attribute GetCustomAttribute(ParameterInfo element, Type attributeType) => CustomAttributeExtensions.GetCustomAttribute(element, attributeType);
        public static Attribute GetCustomAttribute(ParameterInfo element, Type attributeType, bool inherit) => CustomAttributeExtensions.GetCustomAttribute(element, attributeType, inherit);

        public static Attribute[] GetCustomAttributes(Assembly element) => CustomAttributeExtensions.GetCustomAttributes(element).ToArray();
        public static Attribute[] GetCustomAttributes(Assembly element, bool inherit) => CustomAttributeExtensions.GetCustomAttributes(element).ToArray(); // "inherit" is meaningless for assemblies
        public static Attribute[] GetCustomAttributes(Assembly element, Type attributeType) => CustomAttributeExtensions.GetCustomAttributes(element, attributeType).AsAttributeArray();
        public static Attribute[] GetCustomAttributes(Assembly element, Type attributeType, bool inherit) => CustomAttributeExtensions.GetCustomAttributes(element, attributeType).AsAttributeArray(); // "inherit" is meaningless for modules

        public static Attribute[] GetCustomAttributes(MemberInfo element) => CustomAttributeExtensions.GetCustomAttributes(element).ToArray();
        public static Attribute[] GetCustomAttributes(MemberInfo element, bool inherit) => CustomAttributeExtensions.GetCustomAttributes(element, inherit).ToArray();
        public static Attribute[] GetCustomAttributes(MemberInfo element, Type type) => CustomAttributeExtensions.GetCustomAttributes(element, type).AsAttributeArray();
        public static Attribute[] GetCustomAttributes(MemberInfo element, Type type, bool inherit) => CustomAttributeExtensions.GetCustomAttributes(element, type, inherit).AsAttributeArray();

        public static Attribute[] GetCustomAttributes(Module element) => CustomAttributeExtensions.GetCustomAttributes(element).ToArray();
        public static Attribute[] GetCustomAttributes(Module element, bool inherit) => CustomAttributeExtensions.GetCustomAttributes(element).ToArray(); // "inherit" is meaningless for assemblies
        public static Attribute[] GetCustomAttributes(Module element, Type attributeType) => CustomAttributeExtensions.GetCustomAttributes(element, attributeType).AsAttributeArray();
        public static Attribute[] GetCustomAttributes(Module element, Type attributeType, bool inherit) => CustomAttributeExtensions.GetCustomAttributes(element, attributeType).AsAttributeArray(); // "inherit" is meaningless for modules

        public static Attribute[] GetCustomAttributes(ParameterInfo element) => CustomAttributeExtensions.GetCustomAttributes(element).ToArray();
        public static Attribute[] GetCustomAttributes(ParameterInfo element, bool inherit) => CustomAttributeExtensions.GetCustomAttributes(element, inherit).ToArray();
        public static Attribute[] GetCustomAttributes(ParameterInfo element, Type attributeType) => CustomAttributeExtensions.GetCustomAttributes(element, attributeType).AsAttributeArray();
        public static Attribute[] GetCustomAttributes(ParameterInfo element, Type attributeType, bool inherit) => CustomAttributeExtensions.GetCustomAttributes(element, attributeType, inherit).AsAttributeArray();

        public static bool IsDefined(Assembly element, Type attributeType) => CustomAttributeExtensions.IsDefined(element, attributeType);
        public static bool IsDefined(Assembly element, Type attributeType, bool inherit) => CustomAttributeExtensions.IsDefined(element, attributeType); // "inherit" is meaningless for assemblies

        public static bool IsDefined(MemberInfo element, Type attributeType) => CustomAttributeExtensions.IsDefined(element, attributeType);
        public static bool IsDefined(MemberInfo element, Type attributeType, bool inherit) => CustomAttributeExtensions.IsDefined(element, attributeType, inherit);

        public static bool IsDefined(Module element, Type attributeType) => CustomAttributeExtensions.IsDefined(element, attributeType);
        public static bool IsDefined(Module element, Type attributeType, bool inherit) => CustomAttributeExtensions.IsDefined(element, attributeType); // "inherit" is meaningless for modules

        public static bool IsDefined(ParameterInfo element, Type attributeType) => CustomAttributeExtensions.IsDefined(element, attributeType);
        public static bool IsDefined(ParameterInfo element, Type attributeType, bool inherit) => CustomAttributeExtensions.IsDefined(element, attributeType, inherit);
    }
}

namespace System.Reflection
{
    public partial class CustomAttributeData
    {
        public static IList<CustomAttributeData> GetCustomAttributes(Assembly target)
        {
            if (target == null)
                throw new ArgumentNullException(nameof(target));

            return target.GetCustomAttributesData();
        }

        public static IList<CustomAttributeData> GetCustomAttributes(MemberInfo target)
        {
            if (target == null)
                throw new ArgumentNullException(nameof(target));

            return target.GetCustomAttributesData();
        }

        public static IList<CustomAttributeData> GetCustomAttributes(Module target)
        {
            if (target == null)
                throw new ArgumentNullException(nameof(target));

            return target.GetCustomAttributesData();
        }

        public static IList<CustomAttributeData> GetCustomAttributes(ParameterInfo target)
        {
            if (target == null)
                throw new ArgumentNullException(nameof(target));

            return target.GetCustomAttributesData();
        }
    }
}
