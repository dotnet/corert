// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

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

using System;
using System.IO;
using System.Reflection;
using System.Collections.Generic;
using System.Reflection.Runtime.General;

using Internal.LowLevelLinq;

using CustomAttributeExtensions = System.Reflection.CustomAttributeExtensionsSoonToBe;

namespace System.Reflection.Runtime.Assemblies
{
    internal sealed partial class RuntimeAssembly
    {
        public sealed override IList<CustomAttributeData> GetCustomAttributesData() => CustomAttributes.ToReadOnlyCollection();
        public sealed override object[] GetCustomAttributes(bool inherit) => CustomAttributeExtensions.GetCustomAttributes(this).ToArray();  // inherit is meaningless for Assemblies
        public sealed override object[] GetCustomAttributes(Type attributeType, bool inherit) => CustomAttributeExtensions.GetCustomAttributes(this, attributeType).ToArray();  // inherit is meaningless for Assemblies
        public sealed override bool IsDefined(Type attributeType, bool inherit) => CustomAttributeExtensions.IsDefined(this, attributeType);  // inherit is meaningless for Assemblies
    }
}

namespace System.Reflection.Runtime.MethodInfos
{
    internal abstract partial class RuntimeConstructorInfo
    {
        public sealed override IList<CustomAttributeData> GetCustomAttributesData() => CustomAttributes.ToReadOnlyCollection();
        public sealed override object[] GetCustomAttributes(bool inherit) => CustomAttributeExtensions.GetCustomAttributes(this, inherit).ToArray();
        public sealed override object[] GetCustomAttributes(Type attributeType, bool inherit) => CustomAttributeExtensions.GetCustomAttributes(this, attributeType, inherit).ToArray();
        public sealed override bool IsDefined(Type attributeType, bool inherit) => CustomAttributeExtensions.IsDefined(this, attributeType, inherit);
    }
}

namespace System.Reflection.Runtime.EventInfos
{
    internal sealed partial class RuntimeEventInfo
    {
        public sealed override IList<CustomAttributeData> GetCustomAttributesData() => CustomAttributes.ToReadOnlyCollection();
        public sealed override object[] GetCustomAttributes(bool inherit) => CustomAttributeExtensions.GetCustomAttributes(this, inherit).ToArray();
        public sealed override object[] GetCustomAttributes(Type attributeType, bool inherit) => CustomAttributeExtensions.GetCustomAttributes(this, attributeType, inherit).ToArray();
        public sealed override bool IsDefined(Type attributeType, bool inherit) => CustomAttributeExtensions.IsDefined(this, attributeType, inherit);
    }
}

namespace System.Reflection.Runtime.FieldInfos
{
    internal sealed partial class RuntimeFieldInfo
    {
        public sealed override IList<CustomAttributeData> GetCustomAttributesData() => CustomAttributes.ToReadOnlyCollection();
        public sealed override object[] GetCustomAttributes(bool inherit) => CustomAttributeExtensions.GetCustomAttributes(this, inherit).ToArray();
        public sealed override object[] GetCustomAttributes(Type attributeType, bool inherit) => CustomAttributeExtensions.GetCustomAttributes(this, attributeType, inherit).ToArray();
        public sealed override bool IsDefined(Type attributeType, bool inherit) => CustomAttributeExtensions.IsDefined(this, attributeType, inherit);
    }
}

namespace System.Reflection.Runtime.MethodInfos
{
    internal abstract partial class RuntimeMethodInfo
    {
        public sealed override IList<CustomAttributeData> GetCustomAttributesData() => CustomAttributes.ToReadOnlyCollection();
        public sealed override object[] GetCustomAttributes(bool inherit) => CustomAttributeExtensions.GetCustomAttributes(this, inherit).ToArray();
        public sealed override object[] GetCustomAttributes(Type attributeType, bool inherit) => CustomAttributeExtensions.GetCustomAttributes(this, attributeType, inherit).ToArray();
        public sealed override bool IsDefined(Type attributeType, bool inherit) => CustomAttributeExtensions.IsDefined(this, attributeType, inherit);
    }
}

namespace System.Reflection.Runtime.Modules
{
    internal sealed partial class RuntimeModule
    {
        public sealed override IList<CustomAttributeData> GetCustomAttributesData() => CustomAttributes.ToReadOnlyCollection();
        public sealed override object[] GetCustomAttributes(bool inherit) => CustomAttributeExtensions.GetCustomAttributes(this).ToArray();  // inherit is meaningless for Modules
        public sealed override object[] GetCustomAttributes(Type attributeType, bool inherit) => CustomAttributeExtensions.GetCustomAttributes(this, attributeType).ToArray();  // inherit is meaningless for Modules
        public sealed override bool IsDefined(Type attributeType, bool inherit) => CustomAttributeExtensions.IsDefined(this, attributeType);  // inherit is meaningless for Modules
    }
}

namespace System.Reflection.Runtime.ParameterInfos
{
    internal abstract partial class RuntimeParameterInfo
    {
        public sealed override IList<CustomAttributeData> GetCustomAttributesData() => CustomAttributes.ToReadOnlyCollection();
        public sealed override object[] GetCustomAttributes(bool inherit) => CustomAttributeExtensions.GetCustomAttributes(this, inherit).ToArray();
        public sealed override object[] GetCustomAttributes(Type attributeType, bool inherit) => CustomAttributeExtensions.GetCustomAttributes(this, attributeType, inherit).ToArray();
        public sealed override bool IsDefined(Type attributeType, bool inherit) => CustomAttributeExtensions.IsDefined(this, attributeType, inherit);
    }
}

namespace System.Reflection.Runtime.PropertyInfos
{
    internal sealed partial class RuntimePropertyInfo
    {
        public sealed override IList<CustomAttributeData> GetCustomAttributesData() => CustomAttributes.ToReadOnlyCollection();
        public sealed override object[] GetCustomAttributes(bool inherit) => CustomAttributeExtensions.GetCustomAttributes(this, inherit).ToArray();
        public sealed override object[] GetCustomAttributes(Type attributeType, bool inherit) => CustomAttributeExtensions.GetCustomAttributes(this, attributeType, inherit).ToArray();
        public sealed override bool IsDefined(Type attributeType, bool inherit) => CustomAttributeExtensions.IsDefined(this, attributeType, inherit);
    }
}

namespace System.Reflection.Runtime.TypeInfos
{
    internal abstract partial class RuntimeTypeInfo
    {
        public sealed override IList<CustomAttributeData> GetCustomAttributesData() => CustomAttributes.ToReadOnlyCollection();
        public sealed override object[] GetCustomAttributes(bool inherit) => CustomAttributeExtensions.GetCustomAttributes(this, inherit).ToArray();
        public sealed override object[] GetCustomAttributes(Type attributeType, bool inherit) => CustomAttributeExtensions.GetCustomAttributes(this, attributeType, inherit).ToArray();
        public sealed override bool IsDefined(Type attributeType, bool inherit) => CustomAttributeExtensions.IsDefined(this, attributeType, inherit);
    }
}
