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

    .method public hidebysig instance void InstanceMethodRefConstr<class T>() cil managed 
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
        // System.Action a = new System.Action(GenericTestClass<TestClass>.StaticMethod);
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
        // System.Action a = new System.Action(c.InstanceMethod);
        // a();

        ldarg.0
        ldftn       instance void class GenericTestClass`1<class TestClass>::InstanceMethod()
        newobj      instance void [System.Runtime]System.Action::.ctor(object, native int)
        callvirt    instance void [System.Runtime]System.Action::Invoke()
        ret
    }

    .method static public hidebysig void LdFtn.IntForStaticClassRefConstraint_Invalid_UnsatisfiedMethodParentInst() cil managed
    {
        // System.Action a = new System.Action(GenericTestClass<int>.StaticMethod);
        // a();

        ldnull
        ldftn       void class GenericTestClass`1<int32>::StaticMethod()
        newobj      instance void [System.Runtime]System.Action::.ctor(object, native int)
        callvirt    instance void [System.Runtime]System.Action::Invoke()
        ret
    }

    .method static public hidebysig void LdFtn.IntForStaticMethodRefConstraint_Invalid_UnsatisfiedMethodInst() cil managed
    {
        // System.Action a = new System.Action(TestClass.StaticMethod<int>);
        // a();

        ldnull
        ldftn       void class TestClass::StaticMethodRefConstr<int32>()
        newobj      instance void [System.Runtime]System.Action::.ctor(object, native int)
        callvirt    instance void [System.Runtime]System.Action::Invoke()
        ret
    }

    .method static public hidebysig void NewObj.RefConstraint_Valid() cil managed
    {
        // GenericTestClass<TestClass> c = new GenericTestClass<TestClass>();

        newobj      instance void class GenericTestClass`1<class TestClass>::.ctor()
        pop
        ret
    }

    .method static public hidebysig void NewObj.IComparableConstraint_Valid() cil managed
    {
        // GenericIComparable<int> c = new GenericIComparable<int>();

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
        // GenericValueType<int> c = new GenericValueType<int>();

        newobj      instance void class GenericValueType`1<int32>::.ctor()
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
        // GenericIComparable<TestClass> c = new GenericIComparable<TestClass>();

        newobj      instance void class GenericIComparable`1<class TestClass>::.ctor()
        pop
        ret
    }

    .method static public hidebysig void NewObj.ClassForCtorConstraint_Invalid_UnsatisfiedMethodParentInst() cil managed
    {
        // GenericCtor<TestClass> c = new GenericCtor<TestClass>();

        newobj      instance void class GenericCtor`1<class TestClass>::.ctor()
        pop
        ret
    }

    .method static public hidebysig void NewObj.ClassForValueTypeConstraint_Invalid_UnsatisfiedMethodParentInst() cil managed
    {
        // GenericValueType<TestClass> c = new GenericValueType<TestClass>();

        newobj      instance void class GenericValueType`1<class TestClass>::.ctor()
        pop
        ret
    }
}
