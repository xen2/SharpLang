.assembly extern mscorlib
{
  .publickeytoken = (B7 7A 5C 56 19 34 E0 89 )
  .ver 4:0:0:0
}
.assembly SimpleFaultHandler { }

.method public static void TestFault(bool throwException) cil managed
{
	.maxstack 3
	
	.try
	{
		.try
		{
			// if (throwException)
			//     throw new Exception();
			ldarg.0
			brfalse throwExceptionFalse
			newobj instance void [mscorlib]System.Exception::.ctor()
			throw
			throwExceptionFalse: leave.s endTry1
		}
		fault
		{
			ldstr "Fault"
			call void [mscorlib]System.Console::WriteLine(string)
			endfault
		}
	endTry1:
		leave.s endTry2
	}
	catch [mscorlib]System.Exception
	{
		pop
		ldstr "Catch"
		call void [mscorlib]System.Console::WriteLine(string)
		leave.s endTry2
	}

endTry2:
	ldstr "End"
	call void [mscorlib]System.Console::WriteLine(string)
	ret
}

.method public static int32 Main() cil managed
{
	.maxstack 1
	.entrypoint

	ldc.i4.0
	call void TestFault(bool)
	ldc.i4.1
	call void TestFault(bool)
	ldc.i4.0
	ret
}