// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

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
using Internal.Runtime.CompilerServices;

namespace System
{
    public static partial class Activator
    {
        // The following methods and helper class implement the functionality of Activator.CreateInstance<T>()
        // The implementation relies on several compiler intrinsics that expand to quick dictionary lookups in shared
        // code, and direct constant references in unshared code.
        //
        // This method is the public surface area. It wraps the CreateInstance intrinsic with the appropriate try/catch
        // block so that the correct exceptions are generated. Also, it handles the cases where the T type doesn't have 
        // a default constructor.
        [DebuggerGuidedStepThrough]
        public static T CreateInstance<T>()
        {
            if (!RuntimeHelpers.IsReference<T>())
            {
                // Early out for valuetypes since we don't support default constructors anyway for now.
                // This lets codegens that expand IsReference<T> optimize away the rest of this code.
                return default;
            }
            else
            {
                // Grab the pointer to the default constructor of the type. If T doesn't have a default
                // constructor, the intrinsic returns a marker pointer that we check for.
                IntPtr defaultConstructor = DefaultConstructorOf<T>();

                // Check if we got the marker back.
                //
                // TODO: might want to disambiguate the different cases for abstract class, interface, etc.
                if (defaultConstructor == DefaultConstructorOf<ClassWithMissingConstructor>())
                    throw new MissingMethodException(SR.Format(SR.MissingConstructor_Name, typeof(T)));

                // Grab a pointer to the optimized allocator for the type and call it.
                // TODO: we need RyuJIT to respect that RawCalliHelper doesn't do fat pointer transform
                // IntPtr allocator = AllocatorOf<T>();
                // T t = RawCalliHelper.Call<T>(allocator, EETypePtr.EETypePtrOf<T>().RawValue);
                T t = (T)RuntimeImports.RhNewObject(EETypePtr.EETypePtrOf<T>());

                try
                {
                    // Call the default constructor on the allocated instance.
                    RawCalliHelper.Call(defaultConstructor, t);

                    // Debugger goo so that stepping in works. Only affects debug info generation.
                    // The call gets optimized away.
                    DebugAnnotations.PreviousCallContainsDebuggerStepInCode();

                    return t;
                }
                catch (Exception e)
                {
                    throw new TargetInvocationException(e);
                }
            }
        }

        [Intrinsic]
        private static IntPtr DefaultConstructorOf<T>()
        {
            // Codegens must expand this intrinsic to the pointer to the default constructor of T
            // or to a marker that lets us detect there's no default constructor.
            // We could implement a fallback with the type loader if we wanted to, but it will be slow and unreliable.
            throw new NotSupportedException();
        }

        [Intrinsic]
        private static IntPtr AllocatorOf<T>()
        {
            // Codegens must expand this intrinsic to the pointer to the allocator suitable to allocate an instance of T.
            // We could implement a fallback with the type loader if we wanted to, but it will be slow and unreliable.
            throw new NotSupportedException();
        }

        internal static IntPtr GetFallbackDefaultConstructor()
        {
            return DefaultConstructorOf<ClassWithMissingConstructor>();
        }

        // Marker class. DefaultConstructorOf<T> expands to this type's constructor if
        // the constructor is missing.
        private class ClassWithMissingConstructor
        {
            public Guid G;

            private ClassWithMissingConstructor()
            {
                // Ensure we have a unique method body for this that never gets folded with another ctor.
                G = new Guid(0x68be9718, 0xf787, 0x45ab, 0x84, 0x3b, 0x1f, 0x31, 0xb6, 0x12, 0x65, 0xeb);
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

