// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

//
//
//
// Implements System.RuntimeType
//
// ======================================================================================


using System;
using System.Reflection;
using System.Runtime.ConstrainedExecution;
using System.Globalization;
using System.Threading;
using System.Diagnostics;
using System.Security.Permissions;
using System.Collections;
using System.Collections.Generic;
using System.Runtime;
using System.Runtime.Serialization;    
using System.Runtime.CompilerServices;
using System.Security;
using System.Text;
using System.Runtime.Remoting;
#if FEATURE_REMOTING
using System.Runtime.Remoting.Proxies;
using System.Runtime.Remoting.Messaging;
using System.Runtime.Remoting.Activation;
using System.Runtime.Remoting.Metadata;
#endif
using MdSigCallingConvention = System.Signature.MdSigCallingConvention;
using RuntimeTypeCache = System.RuntimeType.RuntimeTypeCache;
using System.Runtime.InteropServices;
using DebuggerStepThroughAttribute = System.Reflection.DebuggerStepThroughAttribute;
using MdToken = System.Reflection.MetadataToken;
using System.Runtime.Versioning;
using System.Diagnostics.Contracts;

namespace System 
{
    [Serializable]
    internal class RuntimeType : 
        System.Reflection.TypeInfo, ISerializable, ICloneable
    {
        // Used by GetMethodCandidates/GetConstructorCandidates, InvokeMember, and CreateInstanceImpl to perform the necessary filtering.
        // Should only be called by FilterApplyMethodInfo and FilterApplyConstructorInfo.
        private static bool FilterApplyMethodBase(
            MethodBase methodBase, BindingFlags methodFlags, BindingFlags bindingFlags, CallingConventions callConv, Type[] argumentTypes)
        {
            Contract.Requires(methodBase != null);

            bindingFlags ^= BindingFlags.DeclaredOnly;

            #region Apply Base Filter
            if ((bindingFlags & methodFlags) != methodFlags)
                return false;
            #endregion

            #region Check CallingConvention
            if ((callConv & CallingConventions.Any) == 0)
            {
                if ((callConv & CallingConventions.VarArgs) != 0 && 
                    (methodBase.CallingConvention & CallingConventions.VarArgs) == 0)
                    return false;

                if ((callConv & CallingConventions.Standard) != 0 && 
                    (methodBase.CallingConvention & CallingConventions.Standard) == 0)
                    return false;
            }
            #endregion

            #region If argumentTypes supplied
            if (argumentTypes != null)
            {
                ParameterInfo[] parameterInfos = methodBase.GetParametersNoCopy();

                if (argumentTypes.Length != parameterInfos.Length)
                {
                    #region Invoke Member, Get\Set & Create Instance specific case
                    // If the number of supplied arguments differs than the number in the signature AND
                    // we are not filtering for a dynamic call -- InvokeMethod or CreateInstance -- filter out the method.
                    if ((bindingFlags & 
                        (BindingFlags.InvokeMethod | BindingFlags.CreateInstance | BindingFlags.GetProperty | BindingFlags.SetProperty)) == 0)
                        return false;
                    
                    bool testForParamArray = false;
                    bool excessSuppliedArguments = argumentTypes.Length > parameterInfos.Length;

                    if (excessSuppliedArguments) 
                    { // more supplied arguments than parameters, additional arguments could be vararg
                        #region Varargs
                        // If method is not vararg, additional arguments can not be passed as vararg
                        if ((methodBase.CallingConvention & CallingConventions.VarArgs) == 0)
                        {
                            testForParamArray = true;
                        }
                        else 
                        {
                            // If Binding flags did not include varargs we would have filtered this vararg method.
                            // This Invariant established during callConv check.
                            Contract.Assert((callConv & CallingConventions.VarArgs) != 0);
                        }
                        #endregion
                    }
                    else 
                    {// fewer supplied arguments than parameters, missing arguments could be optional
                        #region OptionalParamBinding
                        if ((bindingFlags & BindingFlags.OptionalParamBinding) == 0)
                        {
                            testForParamArray = true;
                        }
                        else
                        {
                            // From our existing code, our policy here is that if a parameterInfo 
                            // is optional then all subsequent parameterInfos shall be optional. 

                            // Thus, iff the first parameterInfo is not optional then this MethodInfo is no longer a canidate.
                            if (!parameterInfos[argumentTypes.Length].IsOptional)
                                testForParamArray = true;
                        }
                        #endregion
                    }

                    #region ParamArray
                    if (testForParamArray)
                    {
                        if  (parameterInfos.Length == 0)
                            return false;

                        // The last argument of the signature could be a param array. 
                        bool shortByMoreThanOneSuppliedArgument = argumentTypes.Length < parameterInfos.Length - 1;

                        if (shortByMoreThanOneSuppliedArgument)
                            return false;

                        ParameterInfo lastParameter = parameterInfos[parameterInfos.Length - 1];

                        if (!lastParameter.ParameterType.IsArray)
                            return false;

                        if (!lastParameter.IsDefined(typeof(ParamArrayAttribute), false))
                            return false;
                    }
                    #endregion

                    #endregion
                }
                else
                {
                    #region Exact Binding
                    if ((bindingFlags & BindingFlags.ExactBinding) != 0)
                    {
                        // Legacy behavior is to ignore ExactBinding when InvokeMember is specified.
                        // Why filter by InvokeMember? If the answer is we leave this to the binder then why not leave
                        // all the rest of this  to the binder too? Further, what other semanitc would the binder
                        // use for BindingFlags.ExactBinding besides this one? Further, why not include CreateInstance 
                        // in this if statement? That's just InvokeMethod with a constructor, right?
                        if ((bindingFlags & (BindingFlags.InvokeMethod)) == 0)
                        {
                            for(int i = 0; i < parameterInfos.Length; i ++)
                            {
                                // a null argument type implies a null arg which is always a perfect match
                                if ((object)argumentTypes[i] != null && !Object.ReferenceEquals(parameterInfos[i].ParameterType, argumentTypes[i]))
                                    return false;
                            }
                        }
                    }
                    #endregion
                }
            }
            #endregion
        
            return true;
        }
    }
}
