// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Diagnostics;
using System.Reflection.Runtime.General;
using System.Reflection.Runtime.BindingFlagSupport;

namespace System.Reflection.Runtime.TypeInfos
{
    internal abstract partial class RuntimeTypeInfo
    {
        public override Object InvokeMember(
            String name, BindingFlags bindingFlags, Binder binder, Object target, 
            Object[] providedArgs, ParameterModifier[] modifiers, CultureInfo culture, String[] namedParams) 
        {
            if (IsGenericParameter)
                throw new InvalidOperationException(Environment.GetResourceString("Arg_GenericParameter"));
            Contract.EndContractBlock();
        
            #region Preconditions
            if ((bindingFlags & InvocationMask) == 0)
                // "Must specify binding flags describing the invoke operation required."
                throw new ArgumentException(Environment.GetResourceString("Arg_NoAccessSpec"),"bindingFlags");

            // Provide a default binding mask if none is provided 
            if ((bindingFlags & MemberBindingMask) == 0) 
            {
                bindingFlags |= BindingFlags.Instance | BindingFlags.Public;

                if ((bindingFlags & BindingFlags.CreateInstance) == 0) 
                    bindingFlags |= BindingFlags.Static;
            }

            // There must not be more named parameters than provided arguments
            if (namedParams != null)
            {
                if (providedArgs != null)
                {
                    if (namedParams.Length > providedArgs.Length)
                        // "Named parameter array can not be bigger than argument array."
                        throw new ArgumentException(Environment.GetResourceString("Arg_NamedParamTooBig"), "namedParams");
                }
                else
                {
                    if (namedParams.Length != 0)
                        // "Named parameter array can not be bigger than argument array."
                        throw new ArgumentException(Environment.GetResourceString("Arg_NamedParamTooBig"), "namedParams");
                }
            }
            #endregion

            #region COM Interop
#if FEATURE_COMINTEROP && FEATURE_USE_LCID
            if (target != null && target.GetType().IsCOMObject)
            {
                #region Preconditions
                if ((bindingFlags & ClassicBindingMask) == 0)
                    throw new ArgumentException(Environment.GetResourceString("Arg_COMAccess"), "bindingFlags");

                if ((bindingFlags & BindingFlags.GetProperty) != 0 && (bindingFlags & ClassicBindingMask & ~(BindingFlags.GetProperty | BindingFlags.InvokeMethod)) != 0)
                    throw new ArgumentException(Environment.GetResourceString("Arg_PropSetGet"), "bindingFlags");

                if ((bindingFlags & BindingFlags.InvokeMethod) != 0 && (bindingFlags & ClassicBindingMask & ~(BindingFlags.GetProperty | BindingFlags.InvokeMethod)) != 0)
                    throw new ArgumentException(Environment.GetResourceString("Arg_PropSetInvoke"), "bindingFlags");

                if ((bindingFlags & BindingFlags.SetProperty) != 0 && (bindingFlags & ClassicBindingMask & ~BindingFlags.SetProperty) != 0)
                    throw new ArgumentException(Environment.GetResourceString("Arg_COMPropSetPut"), "bindingFlags");

                if ((bindingFlags & BindingFlags.PutDispProperty) != 0 && (bindingFlags & ClassicBindingMask & ~BindingFlags.PutDispProperty) != 0)
                    throw new ArgumentException(Environment.GetResourceString("Arg_COMPropSetPut"), "bindingFlags");

                if ((bindingFlags & BindingFlags.PutRefDispProperty) != 0 && (bindingFlags & ClassicBindingMask & ~BindingFlags.PutRefDispProperty) != 0)
                    throw new ArgumentException(Environment.GetResourceString("Arg_COMPropSetPut"), "bindingFlags");
                #endregion

#if FEATURE_REMOTING
                if(!RemotingServices.IsTransparentProxy(target))
#endif
                {
                    #region Non-TransparentProxy case
                    if (name == null)
                        throw new ArgumentNullException("name");

                    bool[] isByRef = modifiers == null ? null : modifiers[0].IsByRefArray;
                    
                    // pass LCID_ENGLISH_US if no explicit culture is specified to match the behavior of VB
                    int lcid = (culture == null ? 0x0409 : culture.LCID);

                    return InvokeDispMethod(name, bindingFlags, target, providedArgs, isByRef, lcid, namedParams);
                    #endregion
                }
#if FEATURE_REMOTING
                else
                {
                    #region TransparentProxy case
                    return ((MarshalByRefObject)target).InvokeMember(name, bindingFlags, binder, providedArgs, modifiers, culture, namedParams);
                    #endregion
                }
#endif // FEATURE_REMOTING
            }
#endif // FEATURE_COMINTEROP && FEATURE_USE_LCID
            #endregion
                
            #region Check that any named paramters are not null
            if (namedParams != null && Array.IndexOf(namedParams, null) != -1)
                // "Named parameter value must not be null."
                throw new ArgumentException(Environment.GetResourceString("Arg_NamedParamNull"),"namedParams");
            #endregion

            int argCnt = (providedArgs != null) ? providedArgs.Length : 0;
            
            #region Get a Binder
            if (binder == null)
                binder = DefaultBinder;

            bool bDefaultBinder = (binder == DefaultBinder);
            #endregion
            
            #region Delegate to Activator.CreateInstance
            if ((bindingFlags & BindingFlags.CreateInstance) != 0) 
            {
                if ((bindingFlags & BindingFlags.CreateInstance) != 0 && (bindingFlags & BinderNonCreateInstance) != 0)
                    // "Can not specify both CreateInstance and another access type."
                    throw new ArgumentException(Environment.GetResourceString("Arg_CreatInstAccess"),"bindingFlags");

                return Activator.CreateInstance(this, bindingFlags, binder, providedArgs, culture);
            }
            #endregion

            // PutDispProperty and\or PutRefDispProperty ==> SetProperty.
            if ((bindingFlags & (BindingFlags.PutDispProperty | BindingFlags.PutRefDispProperty)) != 0)
                bindingFlags |= BindingFlags.SetProperty;

            #region Name
            if (name == null)
                throw new ArgumentNullException("name");
                
            if (name.Length == 0 || name.Equals(@"[DISPID=0]")) 
            {
                name = GetDefaultMemberName();

                if (name == null) 
                {
                    // in InvokeMember we always pretend there is a default member if none is provided and we make it ToString
                    name = "ToString";
                }
            }
            #endregion

            #region GetField or SetField
            bool IsGetField = (bindingFlags & BindingFlags.GetField) != 0;
            bool IsSetField = (bindingFlags & BindingFlags.SetField) != 0;

            if (IsGetField || IsSetField)
            {
                #region Preconditions
                if (IsGetField)
                {
                    if (IsSetField)
                        // "Can not specify both Get and Set on a field."
                        throw new ArgumentException(Environment.GetResourceString("Arg_FldSetGet"),"bindingFlags");

                    if ((bindingFlags & BindingFlags.SetProperty) != 0)
                        // "Can not specify both GetField and SetProperty."
                        throw new ArgumentException(Environment.GetResourceString("Arg_FldGetPropSet"),"bindingFlags");
                }
                else
                {
                    Contract.Assert(IsSetField);

                    if (providedArgs == null) 
                        throw new ArgumentNullException("providedArgs");

                    if ((bindingFlags & BindingFlags.GetProperty) != 0)
                        // "Can not specify both SetField and GetProperty."
                        throw new ArgumentException(Environment.GetResourceString("Arg_FldSetPropGet"),"bindingFlags");

                    if ((bindingFlags & BindingFlags.InvokeMethod) != 0)
                        // "Can not specify Set on a Field and Invoke on a method."
                        throw new ArgumentException(Environment.GetResourceString("Arg_FldSetInvoke"),"bindingFlags");
                }
                #endregion
                        
                #region Lookup Field
                FieldInfo selFld = null;                
                FieldInfo[] flds = GetMember(name, MemberTypes.Field, bindingFlags) as FieldInfo[];

                Contract.Assert(flds != null);

                if (flds.Length == 1)
                {
                    selFld = flds[0];
                }
                else if (flds.Length > 0)
                {
                    selFld = binder.BindToField(bindingFlags, flds, IsGetField ? Empty.Value : providedArgs[0], culture);
                }
                #endregion
                
                if (selFld != null) 
                {
                    #region Invocation on a field
                    if (selFld.FieldType.IsArray || Object.ReferenceEquals(selFld.FieldType, typeof(System.Array)))
                    {
                        #region Invocation of an array Field
                        int idxCnt;

                        if ((bindingFlags & BindingFlags.GetField) != 0) 
                        {
                            idxCnt = argCnt;                                                        
                        }
                        else
                        {
                            idxCnt = argCnt - 1;
                        }

                        if (idxCnt > 0) 
                        {
                            // Verify that all of the index values are ints
                            int[] idx = new int[idxCnt];
                            for (int i=0;i<idxCnt;i++) 
                            {
                                try 
                                {
                                    idx[i] = ((IConvertible)providedArgs[i]).ToInt32(null);
                                }
                                catch (InvalidCastException)
                                {
                                    throw new ArgumentException(Environment.GetResourceString("Arg_IndexMustBeInt"));
                                }
                            }
                            
                            // Set or get the value...
                            Array a = (Array) selFld.GetValue(target);
                            
                            // Set or get the value in the array
                            if ((bindingFlags & BindingFlags.GetField) != 0) 
                            {
                                return a.GetValue(idx);
                            }
                            else 
                            {
                                a.SetValue(providedArgs[idxCnt],idx);
                                return null;
                            }                                               
                        }
                        #endregion
                    }
                    
                    if (IsGetField)
                    {
                        #region Get the field value
                        if (argCnt != 0)
                            throw new ArgumentException(Environment.GetResourceString("Arg_FldGetArgErr"),"bindingFlags");

                        return selFld.GetValue(target);
                        #endregion
                    }
                    else
                    {
                        #region Set the field Value
                        if (argCnt != 1)
                            throw new ArgumentException(Environment.GetResourceString("Arg_FldSetArgErr"),"bindingFlags");

                        selFld.SetValue(target,providedArgs[0],bindingFlags,binder,culture);

                        return null;
                        #endregion
                    }
                    #endregion
                }

                if ((bindingFlags & BinderNonFieldGetSet) == 0) 
                    throw new MissingFieldException(FullName, name);
            }
            #endregion                    

            #region Caching Logic
            /*
            bool useCache = false;

            // Note that when we add something to the cache, we are careful to ensure
            // that the actual providedArgs matches the parameters of the method.  Otherwise,
            // some default argument processing has occurred.  We don't want anyone
            // else with the same (insufficient) number of actual arguments to get a
            // cache hit because then they would bypass the default argument processing
            // and the invocation would fail.
            if (bDefaultBinder && namedParams == null && argCnt < 6)
                useCache = true;

            if (useCache)
            {
                MethodBase invokeMethod = GetMethodFromCache (name, bindingFlags, argCnt, providedArgs);

                if (invokeMethod != null)
                    return ((MethodInfo) invokeMethod).Invoke(target, bindingFlags, binder, providedArgs, culture);
            }
            */
            #endregion

            #region Property PreConditions
            // @Legacy - This is RTM behavior
            bool isGetProperty = (bindingFlags & BindingFlags.GetProperty) != 0;
            bool isSetProperty = (bindingFlags & BindingFlags.SetProperty) != 0;

            if (isGetProperty || isSetProperty) 
            {
                #region Preconditions
                if (isGetProperty)
                {
                    Contract.Assert(!IsSetField);

                    if (isSetProperty)
                        throw new ArgumentException(Environment.GetResourceString("Arg_PropSetGet"), "bindingFlags");
                }
                else
                {
                    Contract.Assert(isSetProperty);

                    Contract.Assert(!IsGetField);
                    
                    if ((bindingFlags & BindingFlags.InvokeMethod) != 0)
                        throw new ArgumentException(Environment.GetResourceString("Arg_PropSetInvoke"), "bindingFlags");
                }
                #endregion
            }
            #endregion

            MethodInfo[] finalists = null;
            MethodInfo finalist = null;

            #region BindingFlags.InvokeMethod
            if ((bindingFlags & BindingFlags.InvokeMethod) != 0) 
            {
                #region Lookup Methods
                MethodInfo[] semiFinalists = GetMember(name, MemberTypes.Method, bindingFlags) as MethodInfo[];
                List<MethodInfo> results = null;
                
                for(int i = 0; i < semiFinalists.Length; i ++)
                {
                    MethodInfo semiFinalist = semiFinalists[i];
                    Contract.Assert(semiFinalist != null);

                    if (!FilterApplyMethodInfo((RuntimeMethodInfo)semiFinalist, bindingFlags, CallingConventions.Any, new Type[argCnt]))
                        continue;
                    
                    if (finalist == null)
                    {
                        finalist = semiFinalist;
                    }
                    else
                    {
                        if (results == null)
                        {
                            results = new List<MethodInfo>(semiFinalists.Length);
                            results.Add(finalist);
                        }

                        results.Add(semiFinalist);
                    }
                }
                
                if (results != null)
                {
                    Contract.Assert(results.Count > 1);
                    finalists = new MethodInfo[results.Count];
                    results.CopyTo(finalists);
                }
                #endregion
            }
            #endregion
            
            Contract.Assert(finalists == null || finalist != null);

            #region BindingFlags.GetProperty or BindingFlags.SetProperty
            if (finalist == null && isGetProperty || isSetProperty) 
            {
                #region Lookup Property
                PropertyInfo[] semiFinalists = GetMember(name, MemberTypes.Property, bindingFlags) as PropertyInfo[];                        
                List<MethodInfo> results = null;

                for(int i = 0; i < semiFinalists.Length; i ++)
                {
                    MethodInfo semiFinalist = null;

                    if (isSetProperty)
                    {
                        semiFinalist = semiFinalists[i].GetSetMethod(true);
                    }
                    else
                    {
                        semiFinalist = semiFinalists[i].GetGetMethod(true);
                    }

                    if (semiFinalist == null)
                        continue;

                    if (!FilterApplyMethodInfo((RuntimeMethodInfo)semiFinalist, bindingFlags, CallingConventions.Any, new Type[argCnt]))
                        continue;
                    
                    if (finalist == null)
                    {
                        finalist = semiFinalist;
                    }
                    else
                    {
                        if (results == null)
                        {
                            results = new List<MethodInfo>(semiFinalists.Length);
                            results.Add(finalist);
                        }

                        results.Add(semiFinalist);
                    }
                }

                if (results != null)
                {
                    Contract.Assert(results.Count > 1);
                    finalists = new MethodInfo[results.Count];
                    results.CopyTo(finalists);
                }
                #endregion            
            }
            #endregion

            if (finalist != null) 
            {
                #region Invoke
                if (finalists == null && 
                    argCnt == 0 && 
                    finalist.GetParametersNoCopy().Length == 0 && 
                    (bindingFlags & BindingFlags.OptionalParamBinding) == 0)
                {
                    //if (useCache && argCnt == props[0].GetParameters().Length)
                    //    AddMethodToCache(name, bindingFlags, argCnt, providedArgs, props[0]);

                    return finalist.Invoke(target, bindingFlags, binder, providedArgs, culture);
                }
                
                if (finalists == null)
                    finalists = new MethodInfo[] { finalist };

                if (providedArgs == null)
                        providedArgs = EmptyArray<Object>.Value;

                Object state = null;

                
                MethodBase invokeMethod = null;

                try { invokeMethod = binder.BindToMethod(bindingFlags, finalists, ref providedArgs, modifiers, culture, namedParams, out state); }
                catch(MissingMethodException) { }

                if (invokeMethod == null)
                    throw new MissingMethodException(FullName, name);

                //if (useCache && argCnt == invokeMethod.GetParameters().Length)
                //    AddMethodToCache(name, bindingFlags, argCnt, providedArgs, invokeMethod);

                Object result = ((MethodInfo)invokeMethod).Invoke(target, bindingFlags, binder, providedArgs, culture);

                if (state != null)
                    binder.ReorderArgumentArray(ref providedArgs, state);

                return result;
                #endregion
            }
            
            throw new MissingMethodException(FullName, name);
        }

        private const BindingFlags MemberBindingMask        = (BindingFlags)0x000000FF;
        private const BindingFlags InvocationMask           = (BindingFlags)0x0000FF00;
        private const BindingFlags BinderNonCreateInstance  = BindingFlags.InvokeMethod | BinderGetSetField | BinderGetSetProperty;
        private const BindingFlags BinderGetSetProperty     = BindingFlags.GetProperty | BindingFlags.SetProperty;
        private const BindingFlags BinderSetInvokeProperty  = BindingFlags.InvokeMethod | BindingFlags.SetProperty;
        private const BindingFlags BinderGetSetField        = BindingFlags.GetField | BindingFlags.SetField;
        private const BindingFlags BinderSetInvokeField     = BindingFlags.SetField | BindingFlags.InvokeMethod;
        private const BindingFlags BinderNonFieldGetSet     = (BindingFlags)0x00FFF300;
        private const BindingFlags ClassicBindingMask       = 
            BindingFlags.InvokeMethod | BindingFlags.GetProperty | BindingFlags.SetProperty | 
            BindingFlags.PutDispProperty | BindingFlags.PutRefDispProperty;
    }
}

