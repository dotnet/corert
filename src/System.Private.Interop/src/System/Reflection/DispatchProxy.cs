// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Runtime.InteropServices;

namespace System.Reflection
{
    /// <summary>
    /// Dispatch proxies are a mechanism for instantiating toolchain-generated proxy objects at
    /// runtime that can both (1) implement methods defined by an interface, and (2) forward calls
    /// made to these methods to a common proxy class that knows how to implement the behavior
    /// of these methods.
    /// </summary>
    /// <remarks>
    /// In some ways, this is the replacement for some of the functionality provided by transparent
    /// proxies on Desktop CLR, and can enable a limited form of remoting.  For example, a prominent
    /// use case of dispatch proxies is the "service contract" architecture provided by WCF. In WCF,
    /// a remote service declares a .NET interface that defines a service contract that clients can
    /// use to talk to the remote service. On the client side, the client talks to the service by
    /// interacting with an object via that interface, e.g.:
    ///
    ///   IFooService serviceChannel = ChannelFactory<IFooService>.CreateChannel();
    ///   serviceChannel.MakeRequest();
    ///
    /// This 'serviceChannel' object is a representation of a served object on the client side, so
    /// making method calls on it has the effect of making that same method call on the server side.
    /// Under the hood, WCF asks DispatchProxy to create a proxy object that implements the given
    /// service contract 'IFooService' and forwards its calls to a special WCF proxy class, e.g.:
    ///
    ///   static class ChannelFactory<T>
    ///   {
    ///       public static T CreateChannel()
    ///       {
    ///           return DispatchProxy.Create<T, WcfProxy>();
    ///       }
    ///   }
    ///
    /// At compile time, the toolchain will generate class definitions for each of the requested proxy
    /// objects, each of which need to implement one of the service contracts that the client interacts
    /// with. The toolchain also generates a mechanism to register a mapping of the interface/proxy
    /// class types to their corresponding generated proxy instance types.
    ///
    /// Using that table of mappings, the Create method can then look up the appropriate generated
    /// proxy instance class for the requested interface type and proxy class type, and return an
    /// instantiation of that proxy instance class.
    /// </remarks>
    public abstract class DispatchProxy
    {
        protected DispatchProxy()
        {
        }

        /// <summary>
        /// Whenever any method on the generated proxy type is called, this method
        /// will be invoked to dispatch control to the DispatchProxy implementation class.
        /// </summary>
        /// <param name="targetMethod">The method the caller invoked</param>
        /// <param name="args">The arguments the caller passed to the method</param>
        /// <returns>The object to return to the caller, or <c>null</c> for void methods</returns>
        protected abstract object Invoke(MethodInfo targetMethod, object[] args);

        /// <summary>
        /// Creates an object instance that derives from class <typeparamref name="TProxy"/>
        /// and implements interface <typeparamref name="T"/>.
        /// </summary>
        /// <typeparam name="T">The interface the proxy should implement.</typeparam>
        /// <typeparam name="TProxy">The base class to use for the proxy class.</typeparam>
        /// <returns>An object instance that implements <typeparamref name="T"/>.</returns>
        /// <exception cref="System.ArgumentException"><typeparamref name="T"/> is a class,
        /// or <typeparamref name="TProxy"/> is sealed or does not have a parameterless constructor</exception>
        public static T Create<T, TProxy>()
            where TProxy : DispatchProxy
        {
            RuntimeTypeHandle proxyClassTypeHandle = typeof(TProxy).TypeHandle;
            RuntimeTypeHandle interfaceTypeHandle = typeof(T).TypeHandle;

            RuntimeTypeHandle implClassTypeHandle =
                DispatchProxyHelpers.GetConcreteProxyType(proxyClassTypeHandle, interfaceTypeHandle);
            return (T)InteropExtensions.RuntimeNewObject(implClassTypeHandle);
        }
    }
}
