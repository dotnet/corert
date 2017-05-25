// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

//
// Activator is an object that contains the Activation (CreateInstance/New) 
//  methods for late bound support.
//

using System.Diagnostics;
using System.Globalization;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime;

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
        [DebuggerGuidedStepThrough]
        public static T CreateInstance<T>()
        {
            T t = default(T);

            bool missingDefaultConstructor = false;

            EETypePtr eetype = EETypePtr.EETypePtrOf<T>();

            // ProjectN:936613 - Early exit for variable sized types (strings, arrays, etc.) as we cannot call
            // CreateInstanceIntrinsic on them since the intrinsic will attempt to allocate an instance of these types
            // and that is verboten (it results in silent heap corruption!).
            if (eetype.ComponentSize != 0)
            {
                // ComponentSize > 0 indicates an array-like type (e.g. string, array, etc).
                missingDefaultConstructor = true;
            }
            else if (eetype.IsInterface)
            {
                // Do not attempt to allocate interface types either
                missingDefaultConstructor = true;
            }
            else
            {
                bool oldValueOfMissingDefaultCtorMarkerBool = s_createInstanceMissingDefaultConstructor;

                try
                {
                    t = CreateInstanceIntrinsic<T>();
                    System.Diagnostics.DebugAnnotations.PreviousCallContainsDebuggerStepInCode();
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
                throw new MissingMethodException(SR.Format(SR.MissingConstructor_Name, typeof(T)));
            }

            return t;
        }

        [Intrinsic]
        [DebuggerGuidedStepThrough]
        private static T CreateInstanceIntrinsic<T>()
        {
            // Fallback implementation for codegens that don't support this intrinsic.
            // This uses the type loader and doesn't have the kind of guarantees about it always working
            // as the intrinsic expansion has. Also, it's slower.

            EETypePtr eetype = EETypePtr.EETypePtrOf<T>();

            // The default(T) check can be evaluated statically and will result in the body of this method
            // becoming empty for valuetype Ts. We still need a dynamic IsNullable check to cover Nullables though.
            // This will obviously need work once we start supporting default valuetype constructors.
            if (default(T) == null && !eetype.IsNullable)
            {
                object o = null;
                TypeLoaderExports.ActivatorCreateInstanceAny(ref o, eetype.RawValue);
                System.Diagnostics.DebugAnnotations.PreviousCallContainsDebuggerStepInCode();
                return (T)o;
            }

            return default(T);
        }

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

        [DebuggerHidden]
        [DebuggerStepThrough]
        public static object CreateInstance(Type type) => CreateInstance(type, nonPublic: false);

        [DebuggerHidden]
        [DebuggerStepThrough]
        public static object CreateInstance(Type type, bool nonPublic) => ReflectionAugments.ReflectionCoreCallbacks.ActivatorCreateInstance(type, nonPublic);

        [DebuggerHidden]
        [DebuggerStepThrough]
        public static object CreateInstance(Type type, params object[] args) => CreateInstance(type, ConstructorDefault, null, args, null, null);

        [DebuggerHidden]
        [DebuggerStepThrough]
        public static object CreateInstance(Type type, object[] args, object[] activationAttributes) => CreateInstance(type, ConstructorDefault, null, args, null, activationAttributes);

        [DebuggerHidden]
        [DebuggerStepThrough]
        public static object CreateInstance(Type type, BindingFlags bindingAttr, Binder binder, object[] args, CultureInfo culture) => CreateInstance(type, bindingAttr, binder, args, culture, null);

        [DebuggerHidden]
        [DebuggerStepThrough]
        public static object CreateInstance(Type type, BindingFlags bindingAttr, Binder binder, object[] args, CultureInfo culture, object[] activationAttributes)
        {
            return ReflectionAugments.ReflectionCoreCallbacks.ActivatorCreateInstance(type, bindingAttr, binder, args, culture, activationAttributes);
        }

        private const BindingFlags ConstructorDefault = BindingFlags.Instance | BindingFlags.Public | BindingFlags.CreateInstance;
    }
}

