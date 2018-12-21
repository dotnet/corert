.assembly InterfaceImplementationInherit
{
}

.assembly extern System.Runtime
{
}

.class public auto ansi beforefieldinit BaseClass
	extends [System.Runtime]System.Object
	implements IInterface
{
	.method public final hidebysig newslot virtual 
		instance void InterfaceMethod () cil managed 
	{
		.maxstack 8

		IL_0000: nop
		IL_0001: ret
	}

	.method public hidebysig specialname rtspecialname 
		instance void .ctor () cil managed 
	{
		.maxstack 8

		IL_0000: ldarg.0
		IL_0001: call instance void [System.Runtime]System.Object::.ctor()
		IL_0006: nop
		IL_0007: ret
	}
}

.class public auto ansi beforefieldinit ChildClass_ValidType_Valid
	extends BaseClass
	implements IInterface
{
	.method public hidebysig 
		instance void ChildMethod () cil managed 
	{
		.maxstack 8

		IL_0000: nop
		IL_0001: ret
	}

	.method public hidebysig specialname rtspecialname 
		instance void .ctor () cil managed 
	{
		.maxstack 8

		IL_0000: ldarg.0
		IL_0001: call instance void BaseClass::.ctor()
		IL_0006: nop
		IL_0007: ret
	}
}

.class interface public auto ansi abstract IInterface
{
	.method public hidebysig newslot abstract virtual 
		instance void InterfaceMethod () cil managed 
	{
	}
}

