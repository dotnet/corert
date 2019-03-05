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
using System.Runtime.Remoting;
using System.Runtime;

using Internal.Reflection.Augments;

namespace System
{
    public static partial class Activator
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

            if (!RuntimeHelpers.IsReference<T>())
            {
                // Early out for valuetypes since we don't support default constructors anyway.
                // This lets codegens that expand IsReference<T> optimize away the rest of this code.
            }
            else if (eetype.ComponentSize != 0)
            {
                // ComponentSize > 0 indicates an array-like type (e.g. string, array, etc).
                // Allocating this using the normal allocator would result in silent heap corruption.
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
#if PROJECTN
                    t = CreateInstanceIntrinsic<T>();
                    DebugAnnotations.PreviousCallContainsDebuggerStepInCode();
#else
                    t = (T)(RuntimeImports.RhNewObject(eetype));

                    // Run the default constructor. If the default constructor was missing, codegen
                    // will expand DefaultConstructorOf to ClassWithMissingConstructor::.ctor
                    // and we detect that later.
                    IntPtr defaultConstructor = DefaultConstructorOf<T>();
                    RawCalliHelper.Call(defaultConstructor, t);
                    DebugAnnotations.PreviousCallContainsDebuggerStepInCode();
#endif
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
        private extern static T CreateInstanceIntrinsic<T>();

        [Intrinsic]
        private static IntPtr DefaultConstructorOf<T>()
        {
            // Codegens must expand this intrinsic.
            // We could implement a fallback with the type loader if we wanted to, but it will be slow and unreliable.
            throw new NotSupportedException();
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
        public static object CreateInstance(Type type, bool nonPublic)
            => ReflectionAugments.ReflectionCoreCallbacks.ActivatorCreateInstance(type, nonPublic);

        [DebuggerHidden]
        [DebuggerStepThrough]
        public static object CreateInstance(Type type, BindingFlags bindingAttr, Binder binder, object[] args, CultureInfo culture, object[] activationAttributes)
            => ReflectionAugments.ReflectionCoreCallbacks.ActivatorCreateInstance(type, bindingAttr, binder, args, culture, activationAttributes);
        
        public static ObjectHandle CreateInstance(string assemblyName, string typeName) 
        {
            throw new PlatformNotSupportedException(); // https://github.com/dotnet/corefx/issues/30845
        }

        public static ObjectHandle CreateInstance(string assemblyName, 
                                                  string typeName, 
                                                  bool ignoreCase, 
                                                  BindingFlags bindingAttr, 
                                                  Binder binder, 
                                                  object[] args, 
                                                  CultureInfo culture, 
                                                  object[] activationAttributes) 
        { 
            throw new PlatformNotSupportedException(); // https://github.com/dotnet/corefx/issues/30845
        }

        public static ObjectHandle CreateInstance(string assemblyName, string typeName, object[] activationAttributes) 
        { 
            throw new PlatformNotSupportedException(); // https://github.com/dotnet/corefx/issues/30845
        }
    }
}

