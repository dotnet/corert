// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.Diagnostics;
using Internal.LowLevelLinq;

namespace System.Reflection.Runtime.BindingFlagSupport
{
    /// <summary>
    /// Extension methods offering source-code compatibility with certain instance methods of <see cref="System.Type"/> on other platforms.
    /// </summary>
    internal static class LowLevelTypeExtensions
    {
        /// <summary>
        /// Searches for the constructors defined for the current Type, using the specified BindingFlags.
        /// </summary>
        /// <param name="type">Type to retrieve constructors for</param>
        /// <param name="bindingAttr">A bitmask comprised of one or more BindingFlags that specify how the search is conducted.
        /// -or- 
        /// Zero, to return null. </param>
        /// <returns>An array of ConstructorInfo objects representing all constructors defined for the current Type that match the specified binding constraints, including the type initializer if it is defined. Returns an empty array of type ConstructorInfo if no constructors are defined for the current Type, if none of the defined constructors match the binding constraints, or if the current Type represents a type parameter in the definition of a generic type or generic method.</returns>
        public static ConstructorInfo[] GetConstructors(Type type, BindingFlags bindingAttr)
        {
            GetTypeInfoOrThrow(type);

            IEnumerable<ConstructorInfo> constructors = type.GetMembers<ConstructorInfo>(MemberEnumerator.AnyName, bindingAttr);
            return constructors.ToArray();
        }

        /// <summary>
        /// Returns the EventInfo object representing the specified event, using the specified binding constraints.
        /// </summary>
        /// <param name="type">Type on which to perform lookup</param>
        /// <param name="name">The string containing the name of an event which is declared or inherited by the current Type. </param>
        /// <param name="bindingAttr">A bitmask comprised of one or more BindingFlags that specify how the search is conducted.
        /// -or- 
        /// Zero, to return null. </param>
        /// <returns>The object representing the specified event that is declared or inherited by the current Type, if found; otherwise, null.</returns>
        public static EventInfo GetEvent(Type type, string name, BindingFlags bindingAttr)
        {
            GetTypeInfoOrThrow(type);

            IEnumerable<EventInfo> events = MemberEnumerator.GetMembers<EventInfo>(type, name, bindingAttr);
            return Disambiguate(events);
        }

        /// <summary>
        /// Searches for events that are declared or inherited by the current Type, using the specified binding constraints.
        /// </summary>
        /// <param name="type">Type on which to perform lookup</param>
        /// <param name="bindingAttr">A bitmask comprised of one or more BindingFlags that specify how the search is conducted.
        /// -or- 
        /// Zero, to return null. </param>
        /// <returns>An array of EventInfo objects representing all events that are declared or inherited by the current Type that match the specified binding constraints.
        /// -or- 
        /// An empty array of type EventInfo, if the current Type does not have events, or if none of the events match the binding constraints.</returns>
        public static EventInfo[] GetEvents(Type type, BindingFlags bindingAttr)
        {
            GetTypeInfoOrThrow(type);

            IEnumerable<EventInfo> events = MemberEnumerator.GetMembers<EventInfo>(type, MemberEnumerator.AnyName, bindingAttr);
            return events.ToArray();
        }

        /// <summary>
        /// Searches for the specified field, using the specified binding constraints.
        /// </summary>
        /// <param name="type">Type on which to perform lookup</param>
        /// <param name="name">The string containing the name of the data field to get. </param>
        /// <param name="bindingAttr">A bitmask comprised of one or more BindingFlags that specify how the search is conducted.
        /// -or- 
        /// Zero, to return null.</param>
        /// <returns>An object representing the field that matches the specified requirements, if found; otherwise, null.</returns>
        public static FieldInfo GetField(Type type, string name, BindingFlags bindingAttr)
        {
            GetTypeInfoOrThrow(type);

            IEnumerable<FieldInfo> fields = MemberEnumerator.GetMembers<FieldInfo>(type, name, bindingAttr);
            return Disambiguate(fields);
        }

        /// <summary>
        /// Searches for the fields defined for the current Type, using the specified binding constraints.
        /// </summary>
        /// <param name="type">Type on which to perform lookup</param>
        /// <param name="bindingAttr">A bitmask comprised of one or more BindingFlags that specify how the search is conducted
        /// -or- 
        /// Zero, to return an empty array. </param>
        /// <returns>An array of FieldInfo objects representing all fields defined for the current Type that match the specified binding constraints.
        /// -or- 
        /// An empty array of type FieldInfo, if no fields are defined for the current Type, or if none of the defined fields match the binding constraints.</returns>
        public static FieldInfo[] GetFields(Type type, BindingFlags bindingAttr)
        {
            GetTypeInfoOrThrow(type);

            return MemberEnumerator.GetMembers<FieldInfo>(type, MemberEnumerator.AnyName, bindingAttr).ToArray();
        }

        /// <summary>
        /// Searches for the public members with the specified name.
        /// </summary>
        /// <param name="type">Type on which to perform lookup</param>
        /// <param name="name"> The string containing the name of the public members to get.  </param>
        /// <param name="bindingAttr">A bitmask comprised of one or more BindingFlags that specify how the search is conducted
        /// -or- 
        /// Zero, to return an empty array. </param>
        /// <returns>An array of MemberInfo objects representing the public members with the specified name, if found; otherwise, an empty array.</returns>
        public static MemberInfo[] GetMember(Type type, string name, BindingFlags bindingAttr)
        {
            GetTypeInfoOrThrow(type);

            LowLevelList<MemberInfo> members = GetMembers(type, name, bindingAttr);
            return members.ToArray();
        }

        /// <summary>
        /// Searches for the members defined for the current Type, using the specified binding constraints.
        /// </summary>
        /// <param name="type">Type on which to perform lookup</param>
        /// <param name="bindingAttr">A bitmask comprised of one or more BindingFlags that specify how the search is conducted
        /// -or- 
        /// Zero, to return an empty array. </param>
        /// <returns>An array of MemberInfo objects representing all members defined for the current Type that match the specified binding constraints.
        /// -or- 
        /// An empty array of type MemberInfo, if no members are defined for the current Type, or if none of the defined members match the binding constraints.</returns>
        public static MemberInfo[] GetMembers(Type type, BindingFlags bindingAttr)
        {
            GetTypeInfoOrThrow(type);

            LowLevelList<MemberInfo> members = GetMembers(type, MemberEnumerator.AnyName, bindingAttr);
            return members.ToArray();
        }

        /// <summary>
        /// Searches for the specified method, using the specified binding constraints.
        /// </summary>
        /// <param name="type">Type on which to perform lookup</param>
        /// <param name="name">The string containing the name of the method to get.</param>
        /// <param name="bindingAttr">A bitmask comprised of one or more BindingFlags that specify how the search is conducted
        /// -or- 
        /// Zero, to return null. </param>
        /// <returns>An object representing the method that matches the specified requirements, if found; otherwise, null.</returns>
        public static MethodInfo GetMethod(Type type, string name, BindingFlags bindingAttr)
        {
            GetTypeInfoOrThrow(type);

            IEnumerable<MethodInfo> methods = MemberEnumerator.GetMembers<MethodInfo>(type, name, bindingAttr);
            return Disambiguate(methods);
        }

        /// <summary>
        /// Returns all the public methods of the current Type.
        /// </summary>
        /// <param name="type">Type on which to perform lookup</param>
        /// <param name="bindingAttr">A bitmask comprised of one or more BindingFlags that specify how the search is conducted
        /// -or- 
        /// Zero, to return an empty array. </param>
        /// <returns>An array of MethodInfo objects representing all the public methods defined for the current Type
        /// -or- 
        /// An empty array of type MethodInfo, if no public methods are defined for the current Type.</returns>
        public static MethodInfo[] GetMethods(Type type, BindingFlags bindingAttr)
        {
            GetTypeInfoOrThrow(type);

            IEnumerable<MethodInfo> methods = MemberEnumerator.GetMembers<MethodInfo>(type, MemberEnumerator.AnyName, bindingAttr);
            return methods.ToArray();
        }

        /// <summary>
        /// Searches for the specified method, using the specified binding constraints.
        /// </summary>
        /// <param name="type">Type on which to perform lookup</param>
        /// <param name="name">The string containing the name of the method to get. </param>
        /// <param name="bindingAttr">A bitmask comprised of one or more BindingFlags that specify how the search is conducted
        /// -or- 
        /// Zero, to return an empty array. </param>
        /// <returns>An array of MethodInfo objects representing all the public methods defined for the current Type
        /// -or- 
        /// An empty array of type MethodInfo, if no public methods are defined for the current Type.</returns>
        public static MethodInfo[] GetMethods(Type type, string name, BindingFlags bindingAttr)
        {
            GetTypeInfoOrThrow(type);

            IEnumerable<MethodInfo> methods = MemberEnumerator.GetMembers<MethodInfo>(type, name, bindingAttr);
            return methods.ToArray();
        }

        /// <summary>
        /// Searches for the specified nested type, using the specified binding constraints.
        /// </summary>
        /// <param name="type">Type on which to perform lookup</param>
        /// <param name="name">The string containing the name of the nested type to get. </param>
        /// <param name="bindingAttr">A bitmask comprised of one or more BindingFlags that specify how the search is conducted
        /// -or- 
        /// Zero, to return null. </param>
        /// <returns>An object representing the nested type that matches the specified requirements, if found; otherwise, null.</returns>
        public static Type GetNestedType(Type type, string name, BindingFlags bindingAttr)
        {
            GetTypeInfoOrThrow(type);

            IEnumerable<TypeInfo> nestedTypes = MemberEnumerator.GetMembers<TypeInfo>(type, name, bindingAttr);
            TypeInfo nestedType = Disambiguate(nestedTypes);
            return nestedType == null ? null : nestedType.AsType();
        }

        /// <summary>
        /// Searches for the types nested in the current Type, using the specified binding constraints.
        /// </summary>
        /// <param name="type">Type on which to perform lookup</param>
        /// <param name="bindingAttr">A bitmask comprised of one or more BindingFlags that specify how the search is conducted -or- Zero, to return null. </param>
        /// <returns>An array of Type objects representing all the types nested in the current Type that match the specified binding constraints (the search is not recursive), or an empty array of type Type, if no nested types are found that match the binding constraints.</returns>
        public static Type[] GetNestedTypes(Type type, BindingFlags bindingAttr)
        {
            GetTypeInfoOrThrow(type);

            IEnumerable<TypeInfo> types = MemberEnumerator.GetMembers<TypeInfo>(type, MemberEnumerator.AnyName, bindingAttr);
            return types.Select(t => t.AsType()).ToArray();
        }

        /// <summary>
        /// Searches for the properties of the current Type, using the specified binding constraints.
        /// </summary>
        /// <param name="type">Type on which to perform lookup</param>
        /// <param name="bindingAttr">A bitmask comprised of one or more BindingFlags that specify how the search is conducted -or- Zero, to return null. </param>
        /// <returns>An array of PropertyInfo objects representing all properties of the current Type that match the specified binding constraints.
        /// -or- 
        /// An empty array of type PropertyInfo, if the current Type does not have properties, or if none of the properties match the binding constraints.</returns>
        public static PropertyInfo[] GetProperties(this Type type, BindingFlags bindingAttr)
        {
            GetTypeInfoOrThrow(type);

            IEnumerable<PropertyInfo> properties = MemberEnumerator.GetMembers<PropertyInfo>(type, MemberEnumerator.AnyName, bindingAttr);
            return properties.ToArray();
        }

        /// <summary>
        /// Searches for the specified property, using the specified binding constraints.
        /// </summary>
        /// <param name="type">Type on which to perform lookup</param>
        /// <param name="name">The string containing the name of the property to get. </param>
        /// <param name="bindingAttr">A bitmask comprised of one or more BindingFlags that specify how the search is conducted
        /// -or- 
        /// Zero, to return an empty array. </param>
        /// <returns>An array of PropertyInfo objects representing all the public property defined for the current Type
        /// -or- 
        /// An empty array of type PropertyInfo, if no properties matching the constraints are found.</returns>
        public static PropertyInfo[] GetProperties(this Type type, string name, BindingFlags bindingAttr)
        {
            GetTypeInfoOrThrow(type);

            IEnumerable<PropertyInfo> properties = MemberEnumerator.GetMembers<PropertyInfo>(type, name, bindingAttr);
            return properties.ToArray();
        }

        /// <summary>
        /// Searches for the specified property, using the specified binding constraints.
        /// </summary>
        /// <param name="type">Type on which to perform lookup</param>
        /// <param name="name">The string containing the name of the property to get. </param>
        /// <param name="bindingAttr">A bitmask comprised of one or more BindingFlags that specify how the search is conducted.
        /// -or- 
        /// Zero, to return null. </param>
        /// <returns>A bitmask comprised of one or more BindingFlags that specify how the search is conducted.
        /// -or- 
        /// Zero, to return null. </returns>
        public static PropertyInfo GetProperty(Type type, string name, BindingFlags bindingAttr)
        {
            GetTypeInfoOrThrow(type);

            IEnumerable<PropertyInfo> properties = MemberEnumerator.GetMembers<PropertyInfo>(type, name, bindingAttr);
            return Disambiguate(properties);
        }

        private static LowLevelList<MemberInfo> GetMembers(Type type, object nameFilterOrAnyName, BindingFlags bindingAttr)
        {
            LowLevelList<MemberInfo> members = new LowLevelList<MemberInfo>();

            members.AddRange(MemberEnumerator.GetMembers<MethodInfo>(type, nameFilterOrAnyName, bindingAttr, allowPrefixing: true));
            members.AddRange(MemberEnumerator.GetMembers<ConstructorInfo>(type, nameFilterOrAnyName, bindingAttr, allowPrefixing: true));
            members.AddRange(MemberEnumerator.GetMembers<PropertyInfo>(type, nameFilterOrAnyName, bindingAttr, allowPrefixing: true));
            members.AddRange(MemberEnumerator.GetMembers<EventInfo>(type, nameFilterOrAnyName, bindingAttr, allowPrefixing: true));
            members.AddRange(MemberEnumerator.GetMembers<FieldInfo>(type, nameFilterOrAnyName, bindingAttr, allowPrefixing: true));
            members.AddRange(MemberEnumerator.GetMembers<TypeInfo>(type, nameFilterOrAnyName, bindingAttr, allowPrefixing: true));

            return members;
        }

        private static TypeInfo GetTypeInfoOrThrow(Type type, string parameterName = "type")
        {
            Debug.Assert(type != null);

            TypeInfo typeInfo = type.GetTypeInfo();

            if (typeInfo == null)
            {
                throw new ArgumentException(SR.TypeIsNotReflectable, parameterName);
            }
            return typeInfo;
        }

        private static T Disambiguate<T>(IEnumerable<T> members) where T : MemberInfo
        {
            IEnumerator<T> enumerator = members.GetEnumerator();
            if (!enumerator.MoveNext())
            {
                return null;
            }

            T result = enumerator.Current;
            if (!enumerator.MoveNext())
            {
                return result;
            }

            T anotherResult = enumerator.Current;
            if (anotherResult.DeclaringType.Equals(result.DeclaringType))
            {
                throw new AmbiguousMatchException();
            }

            return result;
        }
    }
}
