// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;

using Internal.Metadata.NativeFormat.Writer;

using Cts = Internal.TypeSystem;
using Ecma = System.Reflection.Metadata;

using Debug = System.Diagnostics.Debug;
using MethodAttributes = System.Reflection.MethodAttributes;
using MethodImplAttributes = System.Reflection.MethodImplAttributes;

namespace ILCompiler.Metadata
{
    public partial class Transform<TPolicy>
    {
        private EntityMap<Cts.MethodDesc, MetadataRecord> _methods
            = new EntityMap<Cts.MethodDesc, MetadataRecord>(EqualityComparer<Cts.MethodDesc>.Default);

        private Action<Cts.MethodDesc, Method> _initMethodDef;
        private Action<Cts.MethodDesc, MemberReference> _initMethodRef;

        private MetadataRecord HandleMethod(Cts.MethodDesc method)
        {
            // TODO: MethodSpecs
            Debug.Assert(method.IsTypicalMethodDefinition);

            MetadataRecord rec;

            if (_policy.GeneratesMetadata(method))
            {
                rec = HandleMethodDefinition(method);
            }
            else
            {
                rec = _methods.GetOrCreate(method, _initMethodRef ?? (_initMethodRef = InitializeMethodReference));
            }

            Debug.Assert(rec is Method || rec is MemberReference);

            return rec;
        }

        private Method HandleMethodDefinition(Cts.MethodDesc method)
        {
            Debug.Assert(method.IsMethodDefinition);
            Debug.Assert(_policy.GeneratesMetadata(method));
            return (Method)_methods.GetOrCreate(method, _initMethodDef ?? (_initMethodDef = InitializeMethodDefinition));
        }

        private void InitializeMethodDefinition(Cts.MethodDesc entity, Method record)
        {
            record.Name = HandleString(entity.Name);
            record.Signature = HandleMethodSignature(entity.Signature);

            if (entity.HasInstantiation)
            {
                var genericParams = new List<GenericParameter>(entity.Instantiation.Length);
                foreach (var p in entity.Instantiation)
                    genericParams.Add(HandleGenericParameter((Cts.GenericParameterDesc)p));
                record.GenericParameters = genericParams;
            }

            if (entity.Signature.Length > 0)
            {
                List<Parameter> parameters = new List<Parameter>(entity.Signature.Length);
                for (ushort i = 0; i < entity.Signature.Length; i++)
                {
                    parameters.Add(new Parameter
                    {
                        Sequence = i
                    });
                }

                var ecmaEntity = entity as Cts.Ecma.EcmaMethod;
                if (ecmaEntity != null)
                {
                    Ecma.MetadataReader reader = ecmaEntity.MetadataReader;
                    Ecma.MethodDefinition methodDef = reader.GetMethodDefinition(ecmaEntity.Handle);
                    Ecma.ParameterHandleCollection paramHandles = methodDef.GetParameters();

                    Debug.Assert(paramHandles.Count == entity.Signature.Length);

                    int i = 0;
                    foreach (var paramHandle in paramHandles)
                    {
                        Ecma.Parameter param = reader.GetParameter(paramHandle);
                        parameters[i].Flags = param.Attributes;
                        parameters[i].Name = HandleString(reader.GetString(param.Name));
                        
                        // TODO: CustomAttributes
                        // TODO: DefaultValue

                        i++;
                    }
                }

                record.Parameters = parameters;
            }

            record.Flags = GetMethodAttributes(entity);
            record.ImplFlags = GetMethodImplAttributes(entity);
            
            //TODO: MethodImpls
            //TODO: RVA
            //TODO: CustomAttributes
        }

        private void InitializeMethodReference(Cts.MethodDesc entity, MemberReference record)
        {
            record.Name = HandleString(entity.Name);
            record.Parent = HandleType(entity.OwningType);
            record.Signature = HandleMethodSignature(entity.Signature);
        }

        private MethodSignature HandleMethodSignature(Cts.MethodSignature signature)
        {
            List<ParameterTypeSignature> parameters;
            if (signature.Length > 0)
            {
                parameters = new List<ParameterTypeSignature>(signature.Length);
                for (int i = 0; i < signature.Length; i++)
                {
                    parameters.Add(HandleParameterTypeSignature(signature[i]));
                }
            }
            else
            {
                parameters = null;
            }
            
            return new MethodSignature
            {
                // TODO: CallingConvention
                GenericParameterCount = signature.GenericParameterCount,
                Parameters = parameters,
                ReturnType = new ReturnTypeSignature
                {
                    // TODO: CustomModifiers
                    Type = HandleType(signature.ReturnType)
                },
                // TODO-NICE: VarArgParameters
            };
        }

        private MethodAttributes GetMethodAttributes(Cts.MethodDesc method)
        {
            var ecmaMethod = method as Cts.Ecma.EcmaMethod;
            if (ecmaMethod != null)
            {
                Ecma.MetadataReader reader = ecmaMethod.MetadataReader;
                Ecma.MethodDefinition methodDef = reader.GetMethodDefinition(ecmaMethod.Handle);
                return methodDef.Attributes;
            }
            else
                throw new NotImplementedException();
        }

        private MethodImplAttributes GetMethodImplAttributes(Cts.MethodDesc method)
        {
            var ecmaMethod = method as Cts.Ecma.EcmaMethod;
            if (ecmaMethod != null)
            {
                Ecma.MetadataReader reader = ecmaMethod.MetadataReader;
                Ecma.MethodDefinition methodDef = reader.GetMethodDefinition(ecmaMethod.Handle);
                return methodDef.ImplAttributes;
            }
            else
                throw new NotImplementedException();
        }
    }
}
