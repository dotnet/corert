using System;

namespace Internal.Runtime.CompilerServices
{
    /// <summary>
    /// Applied by the compiler to assemblies that contain embedded string resources.
    /// In UWP apps, ResourceManager will use this attribute to determine whether to search
    /// for embedded or PRI resources.
    /// </summary>
    [AttributeUsage(AttributeTargets.Assembly, AllowMultiple = false)]
    public sealed class HasEmbeddedStringResourcesAttribute : Attribute
    {
    }
}
