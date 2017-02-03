// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

//
// Activator is an object that contains the Activation (CreateInstance/New) 
//  methods for late bound support.
//

using System.Reflection;
using System.Globalization;
using System.Runtime.CompilerServices;

using Internal.Reflection.Augments;

namespace System
{
    public static class Activator
    {
        // The following 2 methods and helper class implement the functionality of Activator.CreateInstance<T>()

        // This method is the public surface area. It wraps the CreateInstance intrinsic with the appropriate try/catch
        // block so that the correct exceptions are generated. Also, it handles the cases where the T type doesn't have 
        // a default constructor. (Those result in running the ClassWithMissingConstructor's .ctor, which flips the magic
        // thread static to cause the CreateInstance<T> method to throw the right exception.)
        //
        public static T CreateInstance<T>()
        {
            T t = default(T);

            bool missingDefaultConstructor = false;

            // ProjectN:936613 - Early exit for variable sized types (strings, arrays, etc.) as we cannot call
            // CreateInstanceIntrinsic on them since the intrinsic will attempt to allocate an instance of these types
            // and that is verboten (it results in silent heap corruption!).
            if (EETypePtr.EETypePtrOf<T>().ComponentSize != 0)
            {
                // ComponentSize > 0 indicates an array-like type (e.g. string, array, etc).
                missingDefaultConstructor = true;
            }
            else
            {
                bool oldValueOfMissingDefaultCtorMarkerBool = s_createInstanceMissingDefaultConstructor;

                try
                {
                    t = CreateInstanceIntrinsic<T>();
                }
                catch (Exception e)
                {
                    throw new TargetInvocationException(e);
                }

                if (s_createInstanceMissingDefaultConstructor != oldValueOfMissingDefaultCtorMarkerBool)
                {
                    missingDefaultConstructor = true;

                    // We didn't call the real .ctor (because there wasn't one), but we still allocated
                    // an uninitialized object. If it has a finalizer, it would run - prevent that.
                    GC.SuppressFinalize(t);
                }
            }

            if (missingDefaultConstructor)
            {
                throw new MissingMemberException(SR.Format(SR.MissingConstructor_Name, typeof(T)));
            }

            return t;
        }

        [Intrinsic]
        private extern static T CreateInstanceIntrinsic<T>();

        [ThreadStatic]
        internal static bool s_createInstanceMissingDefaultConstructor;
        internal class ClassWithMissingConstructor
        {
            private ClassWithMissingConstructor()
            {
                s_createInstanceMissingDefaultConstructor = !s_createInstanceMissingDefaultConstructor;
            }
            internal static void MissingDefaultConstructorStaticEntryPoint()
            {
                s_createInstanceMissingDefaultConstructor = !s_createInstanceMissingDefaultConstructor;
            }
        }

        public static object CreateInstance(Type type) => CreateInstance(type, nonPublic: false);
        public static object CreateInstance(Type type, bool nonPublic) => ReflectionAugments.ReflectionCoreCallbacks.ActivatorCreateInstance(type, nonPublic);

        public static object CreateInstance(Type type, params object[] args) => CreateInstance(type, ConstructorDefault, null, args, null, null);
        public static object CreateInstance(Type type, object[] args, object[] activationAttributes) => CreateInstance(type, ConstructorDefault, null, args, null, activationAttributes);
        public static object CreateInstance(Type type, BindingFlags bindingAttr, Binder binder, object[] args, CultureInfo culture) => CreateInstance(type, bindingAttr, binder, args, culture, null);
        public static object CreateInstance(Type type, BindingFlags bindingAttr, Binder binder, object[] args, CultureInfo culture, object[] activationAttributes)
        {
            return ReflectionAugments.ReflectionCoreCallbacks.ActivatorCreateInstance(type, bindingAttr, binder, args, culture, activationAttributes);
        }

        private const BindingFlags ConstructorDefault = BindingFlags.Instance | BindingFlags.Public | BindingFlags.CreateInstance;
    }
}

