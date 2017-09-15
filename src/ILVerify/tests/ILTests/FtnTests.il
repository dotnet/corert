// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

.assembly extern System.Runtime
{
}

.assembly FtnTests
{
}

// Generic test class with class constraint
.class public auto ansi beforefieldinit GenericTestClass`1<class T>
       extends [System.Runtime]System.Object
{
    .method public hidebysig specialname rtspecialname instance void .ctor() cil managed 
    {
        ldarg.0
        call      instance void [System.Runtime]System.Object::.ctor()
        ret
    }

    .method public hidebysig instance void InstanceMethod() cil managed 
    {
        ret
    }

    .method public hidebysig static void StaticMethod() cil managed 
    {
        ret
    }
}

// Generic test class with IComparable constraint
.class public auto ansi beforefieldinit GenericIComparable`1<([System.Runtime]System.IComparable) T>
       extends [System.Runtime]System.Object
{
    .method public hidebysig specialname rtspecialname instance void .ctor() cil managed 
    {
        ldarg.0
        call      instance void [System.Runtime]System.Object::.ctor()
        ret
    }

    .method public hidebysig instance void InstanceMethod() cil managed 
    {
        ret
    }

    .method public hidebysig static void StaticMethod() cil managed 
    {
        ret
    }
}

// Generic test class with default ctor constraint
.class public auto ansi beforefieldinit GenericCtor`1<.ctor T>
       extends [System.Runtime]System.Object
{
    .method public hidebysig specialname rtspecialname instance void .ctor() cil managed 
    {
        ldarg.0
        call      instance void [System.Runtime]System.Object::.ctor()
        ret
    }
}

// Generic test class with default valuetype constraint
.class public auto ansi beforefieldinit GenericValueType`1<valuetype T>
       extends [System.Runtime]System.Object
{
    .method public hidebysig specialname rtspecialname instance void .ctor() cil managed 
    {
        ldarg.0
        call      instance void [System.Runtime]System.Object::.ctor()
        ret
    }
}

// Test class with generic methods with constraints and private default ctor
.class public auto ansi beforefieldinit TestClass
       extends [System.Runtime]System.Object
{
    .method private hidebysig specialname rtspecialname instance void .ctor() cil managed 
    {
        ldarg.0
        call      instance void [System.Runtime]System.Object::.ctor()
        ret
    }

    .method private hidebysig specialname rtspecialname instance void .ctor(int32 i) cil managed 
    {
        ldarg.0
        call      instance void [System.Runtime]System.Object::.ctor()
        ret
    }

    .method public hidebysig instance void InstanceMethodRefConstr<class T>() cil managed 
    {
        ret
    }

    .method public hidebysig instance void InstanceMethod() cil managed 
    {
        ret
    }

    .method public hidebysig newslot virtual instance void VirtInstanceMethod() cil managed 
    {
        ret
    }

    .method public hidebysig static void StaticMethod() cil managed 
    {
        ret
    }

    .method public hidebysig static void StaticMethodRefConstr<class T>() cil managed 
    {
        ret
    }
}

.class public auto ansi beforefieldinit FtnTestsType
       extends [System.Runtime]System.Object
{
    .method static public hidebysig void LdFtn.StaticClassRefConstraint_Valid() cil managed
    {
        // var a = new System.Action(GenericTestClass<TestClass>.StaticMethod);
        // a();

        ldnull
        ldftn       void class GenericTestClass`1<class TestClass>::StaticMethod()
        newobj      instance void [System.Runtime]System.Action::.ctor(object, native int)
        callvirt    instance void [System.Runtime]System.Action::Invoke()
        ret
    }

    .method static public hidebysig void LdFtn.InstanceClassRefConstraint_Valid(class GenericTestClass`1<class TestClass> c) cil managed
    {
        // (GenericTestClass<TestClass> c)
        // var a = new System.Action(c.InstanceMethod);
        // a();

        ldarg.0
        ldftn       instance void class GenericTestClass`1<class TestClass>::InstanceMethod()
        newobj      instance void [System.Runtime]System.Action::.ctor(object, native int)
        callvirt    instance void [System.Runtime]System.Action::Invoke()
        ret
    }

    .method static public hidebysig void LdFtn.IntForStaticClassRefConstraint_Invalid_UnsatisfiedMethodParentInst() cil managed
    {
        // var a = new System.Action(GenericTestClass<int>.StaticMethod);
        // a();

        ldnull
        ldftn       void class GenericTestClass`1<int32>::StaticMethod()
        newobj      instance void [System.Runtime]System.Action::.ctor(object, native int)
        callvirt    instance void [System.Runtime]System.Action::Invoke()
        ret
    }

    .method static public hidebysig void LdFtn.IntForStaticMethodRefConstraint_Invalid_UnsatisfiedMethodInst() cil managed
    {
        // var a = new System.Action(TestClass.StaticMethod<int>);
        // a();

        ldnull
        ldftn       void TestClass::StaticMethodRefConstr<int32>()
        newobj      instance void [System.Runtime]System.Action::.ctor(object, native int)
        callvirt    instance void [System.Runtime]System.Action::Invoke()
        ret
    }

    .method static public hidebysig void LdvirtFtn.InstanceMethod_Valid(class TestClass c) cil managed
    {
        // (TestClass c)
        // var a = new System.Action(c.InstanceMethod);
        // a();

        ldarg.0
        dup
        ldvirtftn   instance void TestClass::InstanceMethod()
        newobj      instance void [System.Runtime]System.Action::.ctor(object, native int)
        callvirt    instance void [System.Runtime]System.Action::Invoke()
        ret
    }

    .method static public hidebysig void LdvirtFtn.VirtInstanceMethod_Valid(class TestClass c) cil managed
    {
        // (TestClass c)
        // var a = new System.Action(c.InstanceMethod);
        // a();

        ldarg.0
        dup
        ldvirtftn   instance void TestClass::VirtInstanceMethod()
        newobj      instance void [System.Runtime]System.Action::.ctor(object, native int)
        callvirt    instance void [System.Runtime]System.Action::Invoke()
        ret
    }

    .method static public hidebysig void LdvirtFtn.ValueTypeInstanceMethod_Valid(int32 i) cil managed
    {
        // (int i)
        // var f = new System.Func<int, int>(i.CompareTo);
        // f(0);

        ldarg.0
        box         [System.Runtime]System.Int32
        dup
        ldvirtftn   instance int32 [System.Runtime]System.Int32::CompareTo(int32)
        newobj      instance void class [System.Runtime]System.Func`2<int32, int32>::.ctor(object, native int)
        ldc.i4.0
        callvirt    instance !1 class [System.Runtime]System.Func`2<int32, int32>::Invoke(!0)
        pop
        ret
    }

    .method static public hidebysig void LdvirtFtn.StaticMethod_Invalid_LdvirtftnOnStatic() cil managed
    {
        ldnull
        dup
        ldvirtftn   void TestClass::StaticMethod()
        newobj      instance void [System.Runtime]System.Action::.ctor(object, native int)
        callvirt    instance void [System.Runtime]System.Action::Invoke()
        ret
    }

    .method static public hidebysig void LdvirtFtn.ValueTypeNoBox_Invalid_StackObjRef.StackUnexpected.StackUnexpected.DelegateCtorSigO(int32 i) cil managed
    {
        // (int i)
        // var f = new System.Func<int, int>(i.CompareTo);
        // f(0);

        ldarg.0
        dup
        ldvirtftn   instance int32 [System.Runtime]System.Int32::CompareTo(int32)
        newobj      instance void class [System.Runtime]System.Func`2<int32, int32>::.ctor(object, native int)
        ldc.i4.0
        callvirt    instance !1 class [System.Runtime]System.Func`2<int32, int32>::Invoke(!0)
        pop
        ret
    }

    .method static public hidebysig void LdvirtFtn.ValueTypeWrongBox_Invalid_StackUnexpected.DelegateCtorSigO(int32 i) cil managed
    {
        // (int i)
        // var f = new System.Func<int, int>(i.CompareTo);
        // f(0);

        ldarg.0
        dup
        box         [System.Runtime]System.Int32
        ldvirtftn   instance int32 [System.Runtime]System.Int32::CompareTo(int32)
        newobj      instance void class [System.Runtime]System.Func`2<int32, int32>::.ctor(object, native int)
        ldc.i4.0
        callvirt    instance !1 class [System.Runtime]System.Func`2<int32, int32>::Invoke(!0)
        pop
        ret
    }

    .method static public hidebysig void LdvirtFtn.Ctor_Invalid_LdftnCtor(class TestClass c) cil managed
    {
        // (TestClass c)
        // var a = new System.Action<int>(c..ctor);

        ldarg.0
        dup
        ldvirtftn   instance void TestClass::.ctor(int32)
        newobj      instance void class [System.Runtime]System.Action`1<int32>::.ctor(object, native int)
        pop
        ret
    }

    .method static public hidebysig void LdvirtFtn.ObjectForTestClassInstance_Invalid_StackUnexpected(object c) cil managed
    {
        // (object c)
        // var a = new System.Action(c.InstanceMethod);

        ldarg.0
        dup
        ldvirtftn   instance void TestClass::InstanceMethod()
        newobj      instance void [System.Runtime]System.Action::.ctor(object, native int)
        pop
        ret
    }

    .method static public hidebysig void NewObj.RefConstraint_Valid() cil managed
    {
        // var c = new GenericTestClass<TestClass>();

        newobj      instance void class GenericTestClass`1<class TestClass>::.ctor()
        pop
        ret
    }

    .method static public hidebysig void NewObj.IComparableConstraint_Valid() cil managed
    {
        // var c = new GenericIComparable<int>();

        newobj      instance void class GenericIComparable`1<int32>::.ctor()
        pop
        ret
    }

    .method static public hidebysig void NewObj.CtorConstraint_Valid() cil managed
    {
        // GenericCtor<object> c = new GenericCtor<object>();

        newobj      instance void class GenericCtor`1<class [System.Runtime]System.Object>::.ctor()
        pop
        ret
    }

    .method static public hidebysig void NewObj.ValueTypeConstraint_Valid() cil managed
    {
        // var c = new GenericValueType<int>();

        newobj      instance void class GenericValueType`1<int32>::.ctor()
        pop
        ret
    }

    .method static public hidebysig void NewObj.RefGenericForRefConstraint_Valid<class T>() cil managed
    {
        // var c = new GenericTestClass`1<T>();

        newobj      instance void class GenericTestClass`1<!!T>::.ctor()
        pop
        ret
    }

    .method static public hidebysig void NewObj.ValueTypeGenericForValueTypeConstraint_Valid<valuetype T>() cil managed
    {
        // var c = new GenericValueType`1<T>();

        newobj      instance void class GenericValueType`1<!!T>::.ctor()
        pop
        ret
    }

    .method static public hidebysig void NewObj.CtorGenericForCtorConstraint_Valid<.ctor T>() cil managed
    {
        // var c = new GenericCtor`1<T>();

        newobj      instance void class GenericCtor`1<!!T>::.ctor()
        pop
        ret
    }

    .method static public hidebysig void NewObj.ValueTypeGenericForCtorConstraint_Valid<valuetype T>() cil managed
    {
        // var c = new GenericCtor`1<T>();

        newobj      instance void class GenericCtor`1<!!T>::.ctor()
        pop
        ret
    }

    .method static public hidebysig void NewObj.IntForRefConstraint_Invalid_UnsatisfiedMethodParentInst() cil managed
    {
        // GenericTestClass<int> c = new GenericTestClass<int>();

        newobj      instance void class GenericTestClass`1<int32>::.ctor()
        pop
        ret
    }

    .method static public hidebysig void NewObj.ClassForIComparableConstraint_Invalid_UnsatisfiedMethodParentInst() cil managed
    {
        // var c = new GenericIComparable<TestClass>();

        newobj      instance void class GenericIComparable`1<class TestClass>::.ctor()
        pop
        ret
    }

    .method static public hidebysig void NewObj.ClassForCtorConstraint_Invalid_UnsatisfiedMethodParentInst() cil managed
    {
        // var c = new GenericCtor<TestClass>();

        newobj      instance void class GenericCtor`1<class TestClass>::.ctor()
        pop
        ret
    }

    .method static public hidebysig void NewObj.ClassForValueTypeConstraint_Invalid_UnsatisfiedMethodParentInst() cil managed
    {
        // var c = new GenericValueType<TestClass>();

        newobj      instance void class GenericValueType`1<class TestClass>::.ctor()
        pop
        ret
    }
}
