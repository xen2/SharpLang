using System;
using System.Collections.Generic;
using System.Linq;
using Mono.Cecil;
using Mono.Cecil.Cil;
using Mono.Cecil.Rocks;
using SharpLang.CompilerServices.Cecil;
using SharpLLVM;

namespace SharpLang.CompilerServices
{
    public partial class Compiler
    {
        private void EmitStloc(List<StackValue> stack, List<StackValue> locals, int localIndex)
        {
            var value = stack.Pop();
            var local = locals[localIndex];

            // Convert from stack to local value
            var stackValue = ConvertFromStackToLocal(local.Type, value);

            // Store value into local
            LLVM.BuildStore(builder, stackValue, local.Value);
        }

        private void EmitLdloc(List<StackValue> stack, List<StackValue> locals, int operandIndex)
        {
            var local = locals[operandIndex];

            // Load value from local
            var value = LLVM.BuildLoad(builder, local.Value, string.Empty);

            // Convert from local to stack value
            value = ConvertFromLocalToStack(local.Type, value);

            // Add value to stack
            stack.Add(new StackValue(local.StackType, local.Type, value));
        }

        private void EmitLdloca(List<StackValue> stack, List<StackValue> locals, int operandIndex)
        {
            var local = locals[operandIndex];

            var refType = GetType(local.Type.TypeReference.MakeByReferenceType());

            // Convert from local to stack value
            var value = ConvertFromLocalToStack(refType, local.Value);

            // Add value to stack
            // TODO: Choose appropriate type + conversions
            stack.Add(new StackValue(StackValueType.Reference, refType, value));
        }

        private void EmitLdarg(List<StackValue> stack, List<StackValue> args, int operandIndex)
        {
            var arg = args[operandIndex];

            // Load value from local argument
            var value = LLVM.BuildLoad(builder, arg.Value, string.Empty);

            // Convert from local to stack value
            value = ConvertFromLocalToStack(arg.Type, value);

            // Add value to stack
            stack.Add(new StackValue(arg.StackType, arg.Type, value));
        }

        private void EmitLdarga(List<StackValue> stack, List<StackValue> args, int operandIndex)
        {
            var arg = args[operandIndex];

            var refType = GetType(arg.Type.TypeReference.MakeByReferenceType());

            // Convert from local to stack value
            var value = ConvertFromLocalToStack(refType, arg.Value);

            // Add value to stack
            // TODO: Choose appropriate type + conversions
            stack.Add(new StackValue(StackValueType.Reference, refType, value));
        }

        private void EmitStarg(List<StackValue> stack, List<StackValue> args, int operandIndex)
        {
            var value = stack.Pop();
            var arg = args[operandIndex];

            // Convert from stack to local value
            var stackValue = ConvertFromStackToLocal(arg.Type, value);

            // Store value into local argument
            LLVM.BuildStore(builder, stackValue, arg.Value);
        }

        private void EmitLdobj(List<StackValue> stack, Type type, InstructionFlags instructionFlags)
        {
            var address = stack.Pop();

            // Load value at address
            var pointerCast = LLVM.BuildPointerCast(builder, address.Value, LLVM.PointerType(type.DefaultType, 0), string.Empty);
            var loadInst = LLVM.BuildLoad(builder, pointerCast, string.Empty);
            SetInstructionFlags(loadInst, instructionFlags);

            // Convert to stack type
            var value = ConvertFromLocalToStack(type, loadInst);

            // Add to stack
            stack.Add(new StackValue(type.StackType, type, value));
        }

        private void EmitStobj(List<StackValue> stack, Type type, InstructionFlags instructionFlags)
        {
            var value = stack.Pop();
            var address = stack.Pop();

            // Convert to local type
            var sourceValue = ConvertFromStackToLocal(type, value);

            // Store value at address
            var pointerCast = LLVM.BuildPointerCast(builder, address.Value, LLVM.PointerType(type.DefaultType, 0), string.Empty);
            var storeInst = LLVM.BuildStore(builder, sourceValue, pointerCast);
            SetInstructionFlags(storeInst, instructionFlags);
        }

        private void EmitInitobj(StackValue address, Type type)
        {
            var value = address.Value;
            var expectedType = LLVM.PointerType(type.DefaultType, 0);

            // If necessary, cast to expected type
            if (LLVM.TypeOf(value) != expectedType)
            {
                value = LLVM.BuildPointerCast(builder, value, expectedType, string.Empty);
            }

            // Store null value (should be all zero)
            LLVM.BuildStore(builder, LLVM.ConstNull(type.DefaultType), value);
        }

        private void EmitNewobj(FunctionCompilerContext functionContext, Type type, Function ctor)
        {
            var stack = functionContext.Stack;

            // Make sure .cctor has been called
            EnsureClassInitialized(functionContext, GetClass(type));

            var allocatedObject = AllocateObject(type);

            // Add it to stack, right before arguments
            var ctorNumParams = ctor.ParameterTypes.Length;
            stack.Insert(stack.Count - ctorNumParams + 1, new StackValue(StackValueType.Object, type, allocatedObject));

            // Invoke ctor
            EmitCall(functionContext, ctor.Signature, ctor.GeneratedValue);

            if (type.StackType != StackValueType.Object)
            {
                allocatedObject = LLVM.BuildLoad(builder, allocatedObject, string.Empty);
            }

            // Add created object on the stack
            stack.Add(new StackValue(type.StackType, type, allocatedObject));
        }

        private ValueRef AllocateObject(Type type, StackValueType stackValueType = StackValueType.Unknown)
        {
            if (stackValueType == StackValueType.Unknown)
                stackValueType = type.StackType;

            // Resolve class
            var @class = GetClass(type);

            if (stackValueType != StackValueType.Object)
            {
                // Value types are allocated on the stack
                return LLVM.BuildAlloca(builder, type.DataType, string.Empty);
            }

            // TODO: Improve performance (better inlining, etc...)
            // Invoke malloc
            var typeSize = LLVM.BuildIntCast(builder, LLVM.SizeOf(type.ObjectType), int32Type, string.Empty);
            var allocatedData = LLVM.BuildCall(builder, allocObjectFunction, new[] { typeSize }, string.Empty);
            var allocatedObject = LLVM.BuildPointerCast(builder, allocatedData, LLVM.PointerType(type.ObjectType, 0), string.Empty);

            // Store vtable global into first field of the object
            var indices = new[]
            {
                LLVM.ConstInt(int32Type, 0, false),                                     // Pointer indirection
                LLVM.ConstInt(int32Type, (int)ObjectFields.RuntimeTypeInfo, false),     // Access RTTI
            };

            var vtablePointer = LLVM.BuildInBoundsGEP(builder, allocatedObject, indices, string.Empty);
            LLVM.BuildStore(builder, @class.GeneratedRuntimeTypeInfoGlobal, vtablePointer);
            return allocatedObject;
        }

        private void EmitRet(List<StackValue> stack, MethodReference method)
        {
            if (method.ReturnType.MetadataType == MetadataType.Void)
            {
                // Emit ret void
                LLVM.BuildRetVoid(builder);
            }
            else
            {
                // Get last item from stack
                var stackItem = stack.Pop();

                // Get return type
                var returnType = CreateType(ResolveGenericsVisitor.Process(method, method.ReturnType));
                LLVM.BuildRet(builder, ConvertFromStackToLocal(returnType, stackItem));
            }
        }

        private void EmitLdstr(List<StackValue> stack, string operand)
        {
            var stringClass = GetClass(corlib.MainModule.GetType(typeof(string).FullName));

            var stringConstantDataGlobal = CreateStringConstant(operand, !CharUsesUTF8, false);

            // Allocate object
            var allocatedObject = AllocateObject(stringClass.Type);

            // Prepare indices
            var indices = new[]
            {
                LLVM.ConstInt(int32Type, 0, false),                         // Pointer indirection
                LLVM.ConstInt(int32Type, (int)ObjectFields.Data, false),    // Data
                LLVM.ConstInt(int32Type, 1, false),                         // Access length
            };

            // Update array with size and 0 data
            var sizeLocation = LLVM.BuildInBoundsGEP(builder, allocatedObject, indices, string.Empty);
            LLVM.BuildStore(builder, LLVM.ConstInt(int32Type, (ulong)operand.Length, false), sizeLocation);

            indices[2] = LLVM.ConstInt(int32Type, 2, false);                // Access data pointer
            var dataPointerLocation = LLVM.BuildInBoundsGEP(builder, allocatedObject, indices, string.Empty);
            LLVM.BuildStore(builder, stringConstantDataGlobal, dataPointerLocation);

            // Push on stack
            stack.Add(new StackValue(StackValueType.Object, stringClass.Type, allocatedObject));
        }

        private ValueRef CreateStringConstant(string str, bool utf16, bool nullTerminate)
        {
            ValueRef stringConstantData;

            // Create string data global
            if (utf16)
            {
                var utf16String = str.Select(x => LLVM.ConstInt(LLVM.Int16TypeInContext(context), x, false));
                if (nullTerminate)
                    utf16String = utf16String.Concat(new[] { LLVM.ConstNull(LLVM.Int16TypeInContext(context)) });

                stringConstantData = LLVM.ConstArray(LLVM.Int16TypeInContext(context), utf16String.ToArray());
            }
            else // utf8
            {
                stringConstantData = LLVM.ConstStringInContext(context, str, (uint)str.Length, !nullTerminate);
            }

            // Create string constant with private linkage
            var stringConstantDataGlobal = LLVM.AddGlobal(module, LLVM.TypeOf(stringConstantData), ".str");
            LLVM.SetLinkage(stringConstantDataGlobal, Linkage.PrivateLinkage);

            // Cast from i8-array to i8*
            LLVM.SetInitializer(stringConstantDataGlobal, stringConstantData);
            var zero = LLVM.ConstInt(int32Type, 0, false);
            stringConstantDataGlobal = LLVM.ConstInBoundsGEP(stringConstantDataGlobal, new[] {zero, zero});

            return stringConstantDataGlobal;
        }

        private void EmitI4(List<StackValue> stack, int operandIndex)
        {
            // Add constant integer value to stack
            stack.Add(new StackValue(StackValueType.Int32, int32,
                LLVM.ConstInt(int32Type, (uint)operandIndex, true)));
        }

        private void EmitI8(List<StackValue> stack, long operandIndex)
        {
            // Add constant integer value to stack
            stack.Add(new StackValue(StackValueType.Int64, int64,
                LLVM.ConstInt(int64Type, (ulong)operandIndex, true)));
        }

        private void EmitR4(List<StackValue> stack, float operandIndex)
        {
            // Add constant integer value to stack
            stack.Add(new StackValue(StackValueType.Float, @float,
                LLVM.ConstReal(@float.DataType, operandIndex)));
        }

        private void EmitR8(List<StackValue> stack, double operandIndex)
        {
            // Add constant integer value to stack
            stack.Add(new StackValue(StackValueType.Float, @double,
                LLVM.ConstReal(@double.DataType, operandIndex)));
        }

        private void EmitLdnull(List<StackValue> stack)
        {
            // Add constant integer value to stack
            stack.Add(new StackValue(StackValueType.Object, @object, LLVM.ConstNull(@object.DefaultType)));
        }

        private void EmitCall(FunctionCompilerContext functionContext, FunctionSignature targetMethod, ValueRef overrideMethod)
        {
            var stack = functionContext.Stack;

            // Build argument list
            var targetNumParams = targetMethod.ParameterTypes.Length;
            var args = new ValueRef[targetNumParams];
            for (int index = 0; index < targetNumParams; index++)
            {
                // TODO: Casting/implicit conversion?
                var stackItem = stack[stack.Count - targetNumParams + index];
                args[index] = ConvertFromStackToLocal(targetMethod.ParameterTypes[index], stackItem);
            }

            // Remove arguments from stack
            stack.RemoveRange(stack.Count - targetNumParams, targetNumParams);

            // Invoke method
            ValueRef callResult;
            var actualMethod = overrideMethod;

            callResult = GenerateInvoke(functionContext, actualMethod, args);

            // Mark method as needed (if non-virtual call)
            if (LLVM.IsAGlobalVariable(actualMethod).Value != IntPtr.Zero)
            {
                LLVM.SetLinkage(actualMethod, Linkage.ExternalLinkage);
            }

            // Push return result on stack
            if (targetMethod.ReturnType.TypeReference.MetadataType != MetadataType.Void)
            {
                // Convert return value from local to stack value
                var returnValue = ConvertFromLocalToStack(targetMethod.ReturnType, callResult);

                // Add value to stack
                stack.Add(new StackValue(targetMethod.ReturnType.StackType, targetMethod.ReturnType, returnValue));
            }
        }

        private void EmitBr(BasicBlockRef targetBasicBlock)
        {
            // Unconditional branch
            LLVM.BuildBr(builder, targetBasicBlock);
        }

        /// <summary>
        /// Helper function for Brfalse/Brtrue: compare stack value with zero using zeroPredicate,
        /// and accordingly jump to either target or next block.
        /// </summary>
        private void EmitBrCommon(StackValue stack, IntPredicate zeroPredicate, BasicBlockRef targetBasicBlock, BasicBlockRef nextBasicBlock)
        {
            // Compare stack value with zero, and accordingly jump to either target or next block
            ValueRef cmpInst;
            switch (stack.StackType)
            {
                case StackValueType.NativeInt:
                {
                    var zero = LLVM.ConstPointerNull(LLVM.TypeOf(stack.Value));
                    cmpInst = LLVM.BuildICmp(builder, zeroPredicate, stack.Value, zero, string.Empty);
                    break;
                }
                case StackValueType.Int32:
                {
                    var zero = LLVM.ConstInt(int32Type, 0, false);
                    cmpInst = LLVM.BuildICmp(builder, zeroPredicate, stack.Value, zero, string.Empty);
                    break;
                }
                case StackValueType.Object:
                {
                    var zero = LLVM.ConstPointerNull(LLVM.TypeOf(stack.Value));
                    cmpInst = LLVM.BuildICmp(builder, zeroPredicate, stack.Value, zero, string.Empty);
                    break;
                }
                default:
                    throw new NotImplementedException();
            }

            LLVM.BuildCondBr(builder, cmpInst, targetBasicBlock, nextBasicBlock);
        }

        private void EmitBrfalse(List<StackValue> stack, BasicBlockRef targetBasicBlock, BasicBlockRef nextBasicBlock)
        {
            // Stack element should be equal to zero.
            EmitBrCommon(stack.Pop(), IntPredicate.IntEQ, targetBasicBlock, nextBasicBlock);
        }

        private void EmitBrtrue(List<StackValue> stack, BasicBlockRef targetBasicBlock, BasicBlockRef nextBasicBlock)
        {
            // Stack element should be different from zero.
            EmitBrCommon(stack.Pop(), IntPredicate.IntNE, targetBasicBlock, nextBasicBlock);
        }

        private void EmitStfld(List<StackValue> stack, Field field, InstructionFlags instructionFlags)
        {
            var value = stack.Pop();
            var @object = stack.Pop();

            var objectValue = ConvertReferenceToExpectedType(@object, field.DeclaringClass.Type);

            // Build indices for GEP
            var indices = BuildFieldIndices(field, @object.StackType, field.DeclaringClass.Type);

            // Find field address using GEP
            var fieldAddress = LLVM.BuildInBoundsGEP(builder, objectValue, indices, string.Empty);

            // Convert stack value to appropriate type
            var fieldValue = ConvertFromStackToLocal(field.Type, value);

            // Store value in field
            var storeInst = LLVM.BuildStore(builder, fieldValue, fieldAddress);

            // Set instruction flags
            SetInstructionFlags(storeInst, instructionFlags);
        }

        private void EmitLdfld(List<StackValue> stack, Field field, InstructionFlags instructionFlags)
        {
            var @object = stack.Pop();

            ValueRef value;
            if (@object.StackType == StackValueType.Value)
            {
                value = LLVM.BuildExtractValue(builder, @object.Value, (uint)field.StructIndex, string.Empty);
            }
            else
            {
                var objectValue = ConvertReferenceToExpectedType(@object, field.DeclaringClass.Type);

                // Build indices for GEP
                var indices = BuildFieldIndices(field, @object.StackType, field.DeclaringClass.Type);

                // Find field address using GEP
                var fieldAddress = LLVM.BuildInBoundsGEP(builder, objectValue, indices, string.Empty);

                // Load value from field and create "fake" local
                value = LLVM.BuildLoad(builder, fieldAddress, string.Empty);

                // Set instruction flags
                SetInstructionFlags(value, instructionFlags);
            }

            // Convert from local to stack value
            value = ConvertFromLocalToStack(field.Type, value);

            // Add value to stack
            stack.Add(new StackValue(field.Type.StackType, field.Type, value));
        }

        private ValueRef ConvertReferenceToExpectedType(StackValue stackValue, Type type)
        {
            var expectedType = stackValue.StackType == StackValueType.Object
                ? LLVM.PointerType(type.ObjectType, 0)
                : LLVM.PointerType(type.ValueType, 0);

            if (LLVM.TypeOf(stackValue.Value) == expectedType)
                return stackValue.Value;

            return LLVM.BuildPointerCast(builder, stackValue.Value, expectedType, string.Empty);
        }

        private void EmitLdflda(List<StackValue> stack, Field field)
        {
            var @object = stack.Pop();

            var refType = GetType(field.Type.TypeReference.MakeByReferenceType());

            // Build indices for GEP
            var indices = BuildFieldIndices(field, @object.StackType, @object.Type);

            // Find field address using GEP
            var fieldAddress = LLVM.BuildInBoundsGEP(builder, @object.Value, indices, string.Empty);

            // Add value to stack
            stack.Add(new StackValue(StackValueType.Reference, refType, fieldAddress));
        }

        private static void SetInstructionFlags(ValueRef instruction, InstructionFlags instructionFlags)
        {
            // Set instruction flags (if necessary)
            if ((instructionFlags & InstructionFlags.Volatile) != 0)
                LLVM.SetVolatile(instruction, true);
            if ((instructionFlags & InstructionFlags.Unaligned) != 0)
                LLVM.SetAlignment(instruction, 1);
        }

        private ValueRef[] BuildFieldIndices(Field field, StackValueType stackValueType, Type type)
        {
            // Build indices for GEP
            var indices = new List<ValueRef>(3);

            if (stackValueType == StackValueType.Reference || stackValueType == StackValueType.Object || stackValueType == StackValueType.NativeInt)
            {
                // First pointer indirection
                indices.Add(LLVM.ConstInt(int32Type, 0, false));
            }

            if (stackValueType == StackValueType.Object)
            {
                // Access data
                indices.Add(LLVM.ConstInt(int32Type, (int)ObjectFields.Data, false));

                // For now, go through hierarchy and check that type match
                // Other options:
                // - cast
                // - store class depth (and just do a substraction)
                int depth = 0;
                var @class = GetClass(type);
                while (@class != null)
                {
                    if (@class == field.DeclaringClass)
                        break;

                    @class = @class.BaseType;
                    depth++;
                }

                if (@class == null)
                    throw new InvalidOperationException(string.Format("Could not find field {0} in hierarchy of {1}", field.FieldDefinition, type.TypeReference));

                // Apply GEP indices to find right object (parent is always stored in first element)
                for (int i = 0; i < depth; ++i)
                    indices.Add(LLVM.ConstInt(int32Type, 0, false));
            }

            // Access the appropriate field
            indices.Add(LLVM.ConstInt(int32Type, (uint)field.StructIndex, false));
            return indices.ToArray();
        }

        private void EmitStsfld(List<StackValue> stack, Field field, InstructionFlags instructionFlags)
        {
            var value = stack.Pop();

            var runtimeTypeInfoGlobal = field.DeclaringClass.GeneratedRuntimeTypeInfoGlobal;

            // Get static field GEP indices
            var indices = BuildStaticFieldIndices(field);

            // Find static field address in runtime type info
            var staticFieldAddress = LLVM.BuildInBoundsGEP(builder, runtimeTypeInfoGlobal, indices, string.Empty);

            // Convert stack value to appropriate type
            var fieldValue = ConvertFromStackToLocal(field.Type, value);

            // Store value in static field
            var storeInst = LLVM.BuildStore(builder, fieldValue, staticFieldAddress);

            // Set instruction flags
            SetInstructionFlags(storeInst, instructionFlags);
        }

        private void EmitLdsfld(List<StackValue> stack, Field field, InstructionFlags instructionFlags)
        {
            var runtimeTypeInfoGlobal = field.DeclaringClass.GeneratedRuntimeTypeInfoGlobal;

            // Get static field GEP indices
            var indices = BuildStaticFieldIndices(field);

            // Find static field address in runtime type info
            var staticFieldAddress = LLVM.BuildInBoundsGEP(builder, runtimeTypeInfoGlobal, indices, string.Empty);

            // Load value from field and create "fake" local
            var value = LLVM.BuildLoad(builder, staticFieldAddress, string.Empty);

            // Convert from local to stack value
            value = ConvertFromLocalToStack(field.Type, value);

            // Set instruction flags
            SetInstructionFlags(value, instructionFlags);

            // Add value to stack
            stack.Add(new StackValue(field.Type.StackType, field.Type, value));
        }

        private void EmitLdsflda(List<StackValue> stack, Field field)
        {
            var runtimeTypeInfoGlobal = field.DeclaringClass.GeneratedRuntimeTypeInfoGlobal;

            var refType = GetType(field.Type.TypeReference.MakeByReferenceType());

            // Get static field GEP indices
            var indices = BuildStaticFieldIndices(field);

            // Find static field address in runtime type info
            var staticFieldAddress = LLVM.BuildInBoundsGEP(builder, runtimeTypeInfoGlobal, indices, string.Empty);

            // Add value to stack
            stack.Add(new StackValue(StackValueType.Reference, refType, staticFieldAddress));
        }

        private ValueRef[] BuildStaticFieldIndices(Field field)
        {
            var indices = new[]
            {
                LLVM.ConstInt(int32Type, 0, false),                                         // Pointer indirection
                LLVM.ConstInt(int32Type, (int)RuntimeTypeInfoFields.StaticFields, false),   // Access static fields
                LLVM.ConstInt(int32Type, (ulong)field.StructIndex, false),                  // Access specific static field
            };

            return indices;
        }

        private void EmitNewarr(List<StackValue> stack, Type elementType)
        {
            var arrayClass = GetClass(new ArrayType(elementType.TypeReference));

            var numElements = stack.Pop();

            // Compute object size
            var typeSize = LLVM.BuildIntCast(builder, LLVM.SizeOf(elementType.DefaultType), int32Type, string.Empty);

            // Compute array size (object size * num elements)
            var numElementsCasted = ConvertToNativeInt(numElements);
            var arraySize = LLVM.BuildMul(builder, typeSize, numElementsCasted, string.Empty);

            // Invoke malloc
            var allocatedData = LLVM.BuildCall(builder, allocObjectFunction, new[] { arraySize }, string.Empty);
            var values = LLVM.BuildPointerCast(builder, allocatedData, LLVM.PointerType(elementType.DefaultType, 0), string.Empty);

            var numElementsAsPointer = LLVM.BuildIntToPtr(builder, numElements.Value, intPtrType, string.Empty);

            // Allocate object
            var allocatedObject = AllocateObject(arrayClass.Type);

            // Prepare indices
            var indices = new[]
            {
                LLVM.ConstInt(int32Type, 0, false),                         // Pointer indirection
                LLVM.ConstInt(int32Type, (int)ObjectFields.Data, false),    // Data
                LLVM.ConstInt(int32Type, 1, false),                         // Access length
            };

            // Update array with size and 0 data
            var sizeLocation = LLVM.BuildInBoundsGEP(builder, allocatedObject, indices, string.Empty);
            LLVM.BuildStore(builder, numElementsAsPointer, sizeLocation);

            indices[2] = LLVM.ConstInt(int32Type, 2, false);                // Access data pointer
            var dataPointerLocation = LLVM.BuildInBoundsGEP(builder, allocatedObject, indices, string.Empty);
            LLVM.BuildStore(builder, values, dataPointerLocation);

            // Push on stack
            stack.Add(new StackValue(StackValueType.Object, arrayClass.Type, allocatedObject));
        }

        private void EmitLdlen(List<StackValue> stack)
        {
            var array = stack.Pop();

            // Prepare indices
            var indices = new[]
            {
                LLVM.ConstInt(int32Type, 0, false),                         // Pointer indirection
                LLVM.ConstInt(int32Type, (int) ObjectFields.Data, false),   // Data
                LLVM.ConstInt(int32Type, 1, false),                         // Access length
            };

            // Load data pointer
            var arraySizeLocation = LLVM.BuildInBoundsGEP(builder, array.Value, indices, string.Empty);
            var arraySize = LLVM.BuildLoad(builder, arraySizeLocation, string.Empty);

            // Add constant integer value to stack
            stack.Add(new StackValue(StackValueType.NativeInt, intPtr, arraySize));
        }

        private void EmitLdelema(List<StackValue> stack, Type elementType)
        {
            var index = stack.Pop();
            var array = stack.Pop();

            var indexValue = ConvertToNativeInt(index);

            var refType = GetType(elementType.TypeReference.MakeByReferenceType());

            // Load array data pointer
            var arrayFirstElement = LoadArrayDataPointer(array);

            // Find pointer of element at requested index
            var arrayElementPointer = LLVM.BuildGEP(builder, arrayFirstElement, new[] { indexValue }, string.Empty);

            // Convert
            arrayElementPointer = ConvertFromLocalToStack(refType, arrayElementPointer);

            // Push loaded element address onto the stack
            stack.Add(new StackValue(refType.StackType, refType, arrayElementPointer));
        }

        private void EmitLdelem(List<StackValue> stack)
        {
            var index = stack.Pop();
            var array = stack.Pop();

            var indexValue = ConvertToNativeInt(index);

            // Get element type
            var elementType = GetType(((ArrayType)array.Type.TypeReference).ElementType);

            // Load array data pointer
            var arrayFirstElement = LoadArrayDataPointer(array);

            // Find pointer of element at requested index
            var arrayElementPointer = LLVM.BuildGEP(builder, arrayFirstElement, new[] { indexValue }, string.Empty);

            // Load element
            var element = LLVM.BuildLoad(builder, arrayElementPointer, string.Empty);

            // Convert
            element = ConvertFromLocalToStack(elementType, element);

            // Push loaded element onto the stack
            stack.Add(new StackValue(elementType.StackType, elementType, element));
        }

        private void EmitStelem(List<StackValue> stack)
        {
            var value = stack.Pop();
            var index = stack.Pop();
            var array = stack.Pop();

            var indexValue = ConvertToNativeInt(index);

            // Get element type
            var elementType = GetType(((ArrayType)array.Type.TypeReference).ElementType);

            // Load array data pointer
            var arrayFirstElement = LoadArrayDataPointer(array);

            // Find pointer of element at requested index
            var arrayElementPointer = LLVM.BuildGEP(builder, arrayFirstElement, new[] { indexValue }, string.Empty);

            // Convert
            var convertedElement = ConvertFromStackToLocal(elementType, value);

            // Store element
            LLVM.BuildStore(builder, convertedElement, arrayElementPointer);
        }

        private ValueRef LoadArrayDataPointer(StackValue array)
        {
            // Prepare indices
            var indices = new[]
            {
                LLVM.ConstInt(int32Type, 0, false),                         // Pointer indirection
                LLVM.ConstInt(int32Type, (int) ObjectFields.Data, false),   // Data
                LLVM.ConstInt(int32Type, 2, false),                         // Access data pointer
            };

            // Load data pointer
            var dataPointerLocation = LLVM.BuildInBoundsGEP(builder, array.Value, indices, string.Empty);
            var dataPointer = LLVM.BuildLoad(builder, dataPointerLocation, string.Empty);

            return dataPointer;
        }

        /// <summary>
        /// Generates invoke if inside a try block, otherwise a call.
        /// </summary>
        /// <param name="functionContext">The function context.</param>
        /// <param name="function">The function.</param>
        /// <param name="args">The arguments.</param>
        /// <returns></returns>
        private ValueRef GenerateInvoke(FunctionCompilerContext functionContext, ValueRef function, ValueRef[] args)
        {
            ValueRef callResult;

            if (functionContext.LandingPadBlock.Value != IntPtr.Zero)
            {
                var nextBlock = LLVM.AppendBasicBlockInContext(context, functionContext.Function.GeneratedValue, string.Empty);
                LLVM.MoveBasicBlockAfter(nextBlock, LLVM.GetInsertBlock(builder));
                callResult = LLVM.BuildInvoke(builder, function, args, nextBlock, functionContext.LandingPadBlock, string.Empty);
                LLVM.PositionBuilderAtEnd(builder, nextBlock);
                functionContext.BasicBlock = nextBlock;
            }
            else
            {
                callResult = LLVM.BuildCall(builder, function, args, string.Empty);
            }

            return callResult;
        }

        private ValueRef ConvertToNativeInt(StackValue index)
        {
            // NatveInt: cast to integer
            if (index.StackType == StackValueType.NativeInt)
                return LLVM.BuildPtrToInt(builder, index.Value, nativeIntType, string.Empty);

            // Integer of different size: cast
            if (LLVM.GetIntTypeWidth(LLVM.TypeOf(index.Value)) != intPtrSize * 8)
                return LLVM.BuildIntCast(builder, index.Value, nativeIntType, string.Empty);

            // Otherwise, return as is
            return index.Value;
        }

        private void EmitIsOrCastclass(FunctionCompilerContext functionContext, List<StackValue> stack, Class @class, Code opcode, int instructionOffset)
        {
            var functionGlobal = functionContext.Function.GeneratedValue;

            var obj = stack.Pop();

            var currentBlock = LLVM.GetInsertBlock(builder);

            // Prepare basic blocks (for PHI instruction)
            var typeIsNotNullBlock = LLVM.AppendBasicBlockInContext(context, functionGlobal, string.Format("L_{0:x4}_type_not_null", instructionOffset));
            var typeNotMatchBlock = LLVM.AppendBasicBlockInContext(context, functionGlobal, string.Format("L_{0:x4}_type_not_match", instructionOffset));
            var typeCheckDoneBlock = LLVM.AppendBasicBlockInContext(context, functionGlobal, string.Format("L_{0:x4}_type_check_done", instructionOffset));

            // Properly order block for easy LLVM bitcode reading
            LLVM.MoveBasicBlockAfter(typeIsNotNullBlock, currentBlock);
            LLVM.MoveBasicBlockAfter(typeNotMatchBlock, typeIsNotNullBlock);
            LLVM.MoveBasicBlockAfter(typeCheckDoneBlock, typeNotMatchBlock);

            var isObjNonNull = LLVM.BuildICmp(builder, IntPredicate.IntNE, obj.Value, LLVM.ConstPointerNull(LLVM.TypeOf(obj.Value)), string.Empty);
            LLVM.BuildCondBr(builder, isObjNonNull, typeIsNotNullBlock, typeNotMatchBlock);

            LLVM.PositionBuilderAtEnd(builder, typeIsNotNullBlock);

            // Get RTTI pointer
            var indices = new[]
            {
                LLVM.ConstInt(int32Type, 0, false), // Pointer indirection
                LLVM.ConstInt(int32Type, (int)ObjectFields.RuntimeTypeInfo, false), // Access RTTI
            };

            var rttiPointer = LLVM.BuildInBoundsGEP(builder, obj.Value, indices, string.Empty);
            rttiPointer = LLVM.BuildLoad(builder, rttiPointer, string.Empty);

            // castedPointerObject is valid only from typeCheckBlock
            var castedPointerType = LLVM.PointerType(@class.Type.ObjectType, 0);
            ValueRef castedPointerObject;

            BasicBlockRef typeCheckBlock;

            if (@class.Type.TypeReference.Resolve().IsInterface)
            {
                // Cast as appropriate pointer type (for next PHI incoming if success)
                castedPointerObject = LLVM.BuildPointerCast(builder, obj.Value, castedPointerType, string.Empty);

                var inlineRuntimeTypeInfoType = LLVM.TypeOf(LLVM.GetParam(isInstInterfaceFunction, 0));
                var isInstInterfaceResult = LLVM.BuildCall(builder, isInstInterfaceFunction, new[]
                {
                    LLVM.BuildPointerCast(builder, rttiPointer, inlineRuntimeTypeInfoType, string.Empty),
                    LLVM.BuildPointerCast(builder, @class.GeneratedRuntimeTypeInfoGlobal, inlineRuntimeTypeInfoType, string.Empty),
                }, string.Empty);

                LLVM.BuildCondBr(builder, isInstInterfaceResult, typeCheckDoneBlock, typeNotMatchBlock);

                typeCheckBlock = LLVM.GetInsertBlock(builder);
            }
            else
            {
                // TODO: Probably better to rewrite this in C, but need to make sure depth will be inlined as constant
                // Get super type count
                // Get method stored in IMT slot
                indices = new[]
                {
                    LLVM.ConstInt(int32Type, 0, false), // Pointer indirection
                    LLVM.ConstInt(int32Type, (int)RuntimeTypeInfoFields.SuperTypeCount, false), // Super type count
                };

                typeCheckBlock = LLVM.AppendBasicBlockInContext(context, functionGlobal, string.Format("L_{0:x4}_type_check", instructionOffset));
                LLVM.MoveBasicBlockBefore(typeCheckBlock, typeNotMatchBlock);

                var superTypeCount = LLVM.BuildInBoundsGEP(builder, rttiPointer, indices, string.Empty);
                superTypeCount = LLVM.BuildLoad(builder, superTypeCount, string.Empty);

                var depthCompareResult = LLVM.BuildICmp(builder, IntPredicate.IntSGE, superTypeCount, LLVM.ConstInt(int32Type, (ulong)@class.Depth, false), string.Empty);
                LLVM.BuildCondBr(builder, depthCompareResult, typeCheckBlock, typeNotMatchBlock);

                // Start new typeCheckBlock
                LLVM.PositionBuilderAtEnd(builder, typeCheckBlock);

                // Get super types
                indices = new[]
                {
                    LLVM.ConstInt(int32Type, 0, false), // Pointer indirection
                    LLVM.ConstInt(int32Type, (int)RuntimeTypeInfoFields.SuperTypes, false), // Super types
                };

                var superTypes = LLVM.BuildInBoundsGEP(builder, rttiPointer, indices, string.Empty);
                superTypes = LLVM.BuildLoad(builder, superTypes, string.Empty);

                // Get actual super type
                indices = new[]
                {
                    LLVM.ConstInt(int32Type, (ulong)@class.Depth, false), // Pointer indirection
                };
                var superType = LLVM.BuildGEP(builder, superTypes, indices, string.Empty);
                superType = LLVM.BuildLoad(builder, superType, string.Empty);

                // Cast as appropriate pointer type (for next PHI incoming if success)
                castedPointerObject = LLVM.BuildPointerCast(builder, obj.Value, castedPointerType, string.Empty);

                // Compare super type in array at given depth with expected one
                var typeCompareResult = LLVM.BuildICmp(builder, IntPredicate.IntEQ, superType, LLVM.ConstPointerCast(@class.GeneratedRuntimeTypeInfoGlobal, intPtrType), string.Empty);
                LLVM.BuildCondBr(builder, typeCompareResult, typeCheckDoneBlock, typeNotMatchBlock);
            }

            // Start new typeNotMatchBlock: set object to null and jump to typeCheckDoneBlock
            LLVM.PositionBuilderAtEnd(builder, typeNotMatchBlock);
            if (opcode == Code.Castclass)
            {
                // Create InvalidCastException object
                var invalidCastExceptionClass = GetClass(corlib.MainModule.GetType(typeof(InvalidCastException).FullName));
                EmitNewobj(functionContext, invalidCastExceptionClass.Type, invalidCastExceptionClass.Functions.Single(x => x.MethodReference.Name == ".ctor" && x.MethodReference.Parameters.Count == 0));
                var invalidCastException = stack.Pop();
                GenerateInvoke(functionContext, throwExceptionFunction, new[] {LLVM.BuildPointerCast(builder, invalidCastException.Value, LLVM.TypeOf(LLVM.GetParam(throwExceptionFunction, 0)), string.Empty)});
                LLVM.BuildUnreachable(builder);
            }
            else
            {
                LLVM.BuildBr(builder, typeCheckDoneBlock);
            }

            // Start new typeCheckDoneBlock
            LLVM.PositionBuilderAtEnd(builder, typeCheckDoneBlock);
            functionContext.BasicBlock = typeCheckDoneBlock;

            // Put back with appropriate type at end of stack
            ValueRef mergedVariable;
            if (opcode == Code.Castclass)
            {
                mergedVariable = castedPointerObject;
            }
            else
            {
                mergedVariable = LLVM.BuildPhi(builder, castedPointerType, string.Empty);
                LLVM.AddIncoming(mergedVariable,
                    new[] {castedPointerObject, LLVM.ConstPointerNull(castedPointerType)},
                    new[] {typeCheckBlock, typeNotMatchBlock});
            }
            stack.Add(new StackValue(obj.StackType, @class.Type, mergedVariable));
        }

        private void EmitBoxValueType(List<StackValue> stack, Class @class)
        {
            var valueType = stack.Pop();

            var allocatedObject = BoxValueType(@class, valueType);

            // Add created object on the stack
            stack.Add(new StackValue(StackValueType.Object, @class.Type, allocatedObject));
        }

        private void EmitUnboxAnyValueType(List<StackValue> stack, Class @class)
        {
            var obj = stack.Pop();

            // TODO: check type?
            var objCast = LLVM.BuildPointerCast(builder, obj.Value, LLVM.PointerType(@class.Type.ObjectType, 0), string.Empty);

            var dataPointer = GetDataPointer(objCast);

            var expectedPointerType = LLVM.PointerType(@class.Type.DataType, 0);
            if (expectedPointerType != LLVM.TypeOf(dataPointer))
                dataPointer = LLVM.BuildPointerCast(builder, dataPointer, expectedPointerType, string.Empty);
            var data = LLVM.BuildLoad(builder, dataPointer, string.Empty);

            data = ConvertFromLocalToStack(@class.Type, data);

            stack.Add(new StackValue(@class.Type.StackType, @class.Type, data));
        }

        private void EmitUnaryOperation(List<StackValue> stack, Code opcode)
        {
            var operand1 = stack.Pop();

            var value1 = operand1.Value;

            // Check stack type (and convert if necessary)
            switch (operand1.StackType)
            {
                case StackValueType.Float:
                    if (opcode == Code.Not)
                        throw new InvalidOperationException("Not opcode doesn't work with float");
                    break;
                case StackValueType.NativeInt:
                    value1 = LLVM.BuildPtrToInt(builder, value1, nativeIntType, string.Empty);
                    break;
                case StackValueType.Int32:
                case StackValueType.Int64:
                    break;
                default:
                    throw new InvalidOperationException(string.Format("Opcode {0} not supported with stack type {1}", opcode, operand1.StackType));
            }

            // Perform neg or not operation
            switch (opcode)
            {
                case Code.Neg:
                    if (operand1.StackType == StackValueType.Float)
                        value1 = LLVM.BuildFNeg(builder, value1, string.Empty);
                    else
                        value1 = LLVM.BuildNeg(builder, value1, string.Empty);
                    break;
                case Code.Not:
                    value1 = LLVM.BuildNot(builder, value1, string.Empty);
                    break;
            }

            if (operand1.StackType == StackValueType.NativeInt)
                value1 = LLVM.BuildIntToPtr(builder, value1, intPtrType, string.Empty);

            // Add back to stack (with same type as before)
            stack.Add(new StackValue(operand1.StackType, operand1.Type, value1));
        }
        
        private void EmitBinaryOperation(FunctionCompilerContext functionContext, List<StackValue> stack, Code opcode)
        {
            var functionGlobal = functionContext.Function.GeneratedValue;

            var operand2 = stack.Pop();
            var operand1 = stack.Pop();

            var value1 = operand1.Value;
            var value2 = operand2.Value;

            StackValue outputOperandType;

            bool isShiftOperation = false;
            bool isIntegerOperation = false;

            // Detect shift and integer operations
            switch (opcode)
            {
                case Code.Shl:
                case Code.Shr:
                case Code.Shr_Un:
                    isShiftOperation = true;
                    break;
                case Code.Xor:
                case Code.Or:
                case Code.And:
                case Code.Div_Un:
                case Code.Not:
                    isIntegerOperation = true;
                    break;
            }

            if (isShiftOperation) // Shift operations are specials
            {
                switch (operand2.StackType)
                {
                    case StackValueType.Int32:
                    case StackValueType.NativeInt:
                        value2 = LLVM.BuildPtrToInt(builder, value2, nativeIntType, string.Empty);
                        break;
                    default:
                        goto InvalidBinaryOperation;
                }

                // Check first operand, and convert second operand to match first one
                switch (operand1.StackType)
                {
                    case StackValueType.Int32:
                        value2 = LLVM.BuildIntCast(builder, value2, int32Type, string.Empty);
                        break;
                    case StackValueType.Int64:
                        value2 = LLVM.BuildIntCast(builder, value2, int64Type, string.Empty);
                        break;
                    case StackValueType.NativeInt:
                        value1 = LLVM.BuildPtrToInt(builder, value1, nativeIntType, string.Empty);
                        value2 = LLVM.BuildIntCast(builder, value2, nativeIntType, string.Empty);
                        break;
                    default:
                        goto InvalidBinaryOperation;
                }

                // Output type is determined by first operand
                outputOperandType = operand1;
            }
            else if (operand1.StackType == operand2.StackType) // Diagonal
            {
                // Check type
                switch (operand1.StackType)
                {
                    case StackValueType.Int32:
                    case StackValueType.Int64:
                    case StackValueType.Float:
                        outputOperandType = operand1;
                        break;
                    case StackValueType.NativeInt:
                        value1 = LLVM.BuildPtrToInt(builder, value1, nativeIntType, string.Empty);
                        value2 = LLVM.BuildPtrToInt(builder, value2, nativeIntType, string.Empty);
                        outputOperandType = operand1;
                        break;
                    case StackValueType.Reference:
                        if (opcode != Code.Sub && opcode != Code.Sub_Ovf_Un)
                            goto InvalidBinaryOperation;
                        value1 = LLVM.BuildPtrToInt(builder, value1, nativeIntType, string.Empty);
                        value2 = LLVM.BuildPtrToInt(builder, value2, nativeIntType, string.Empty);
                        outputOperandType = new StackValue(StackValueType.NativeInt, intPtr, ValueRef.Empty);
                        break;
                    default:
                        throw new InvalidOperationException(string.Format("Binary operations are not allowed on {0}.", operand1.StackType));
                }
            }
            else if (operand1.StackType == StackValueType.NativeInt && operand2.StackType == StackValueType.Int32)
            {
                value1 = LLVM.BuildPtrToInt(builder, value1, nativeIntType, string.Empty);
                outputOperandType = operand1;
            }
            else if (operand1.StackType == StackValueType.Int32 && operand2.StackType == StackValueType.NativeInt)
            {
                value2 = LLVM.BuildPtrToInt(builder, value2, nativeIntType, string.Empty);
                outputOperandType = operand2;
            }
            else if (!isIntegerOperation
                     && (operand1.StackType == StackValueType.Reference || operand2.StackType == StackValueType.Reference)) // ref + [i32, nativeint] or [i32, nativeint] + ref
            {
                StackValue operandRef, operandInt;
                ValueRef valueRef, valueInt;

                if (operand2.StackType == StackValueType.Reference)
                {
                    operandRef = operand2;
                    operandInt = operand1;
                    valueRef = value2;
                    valueInt = value1;
                }
                else
                {
                    operandRef = operand1;
                    operandInt = operand2;
                    valueRef = value1;
                    valueInt = value2;
                }

                switch (operandInt.StackType)
                {
                    case StackValueType.Int32:
                        break;
                    case StackValueType.NativeInt:
                        valueInt = LLVM.BuildPtrToInt(builder, valueInt, nativeIntType, string.Empty);
                        break;
                    default:
                        goto InvalidBinaryOperation;
                }

                switch (opcode)
                {
                    case Code.Add:
                    case Code.Add_Ovf_Un:
                        break;
                    case Code.Sub:
                    case Code.Sub_Ovf:
                        if (operand2.StackType == StackValueType.Reference)
                            goto InvalidBinaryOperation;

                        valueInt = LLVM.BuildNeg(builder, valueInt, string.Empty);
                        break;
                    default:
                        goto InvalidBinaryOperation;
                }

                // If necessary, cast to i8*
                var valueRefType = LLVM.TypeOf(valueRef);
                if (valueRefType != intPtrType)
                    valueRef = LLVM.BuildPointerCast(builder, valueRef, intPtrType, string.Empty);

                valueRef = LLVM.BuildGEP(builder, valueRef, new[] {valueInt}, string.Empty);

                // Cast back to original type
                if (valueRefType != intPtrType)
                    valueRef = LLVM.BuildPointerCast(builder, valueRef, valueRefType, string.Empty);

                stack.Add(new StackValue(StackValueType.Reference, operandRef.Type, valueRef));

                // Early exit
                return;
            }
            else
            {
                goto InvalidBinaryOperation;
            }

            ValueRef result;

            // Perform binary operation
            if (operand1.StackType == StackValueType.Float)
            {
                switch (opcode)
                {
                    case Code.Add: result = LLVM.BuildFAdd(builder, value1, value2, string.Empty); break;
                    case Code.Sub: result = LLVM.BuildFSub(builder, value1, value2, string.Empty); break;
                    case Code.Mul: result = LLVM.BuildFMul(builder, value1, value2, string.Empty); break;
                    case Code.Div: result = LLVM.BuildFDiv(builder, value1, value2, string.Empty); break;
                    case Code.Rem: result = LLVM.BuildFRem(builder, value1, value2, string.Empty); break;
                    default:
                        goto InvalidBinaryOperation;
                }
            }
            else
            {
                // Special case: char is size 1, not 2!
                if (CharUsesUTF8)
                {
                    if (opcode == Code.Add && operand1.Type.TypeReference.FullName == typeof(char*).FullName)
                    {
                        value2 = LLVM.BuildLShr(builder, value2, LLVM.ConstInt(int32Type, 1, false), string.Empty);
                    }
                    else if (opcode == Code.Add && operand2.Type.TypeReference.FullName == typeof(char*).FullName)
                    {
                        value1 = LLVM.BuildLShr(builder, value1, LLVM.ConstInt(int32Type, 1, false), string.Empty);
                    }
                }

                switch (opcode)
                {
                    case Code.Add:          result = LLVM.BuildAdd(builder, value1, value2, string.Empty); break;
                    case Code.Sub:          result = LLVM.BuildSub(builder, value1, value2, string.Empty); break;
                    case Code.Mul:          result = LLVM.BuildMul(builder, value1, value2, string.Empty); break;
                    case Code.Div:          result = LLVM.BuildSDiv(builder, value1, value2, string.Empty); break;
                    case Code.Div_Un:       result = LLVM.BuildUDiv(builder, value1, value2, string.Empty); break;
                    case Code.Rem:          result = LLVM.BuildSRem(builder, value1, value2, string.Empty); break;
                    case Code.Rem_Un:       result = LLVM.BuildURem(builder, value1, value2, string.Empty); break;
                    case Code.Shl:          result = LLVM.BuildShl(builder, value1, value2, string.Empty); break;
                    case Code.Shr:          result = LLVM.BuildAShr(builder, value1, value2, string.Empty); break;
                    case Code.Shr_Un:       result = LLVM.BuildLShr(builder, value1, value2, string.Empty); break;
                    case Code.And:          result = LLVM.BuildAnd(builder, value1, value2, string.Empty); break;
                    case Code.Or:           result = LLVM.BuildOr(builder, value1, value2, string.Empty); break;
                    case Code.Xor:          result = LLVM.BuildXor(builder, value1, value2, string.Empty); break;
                    case Code.Add_Ovf:
                    case Code.Add_Ovf_Un:
                    case Code.Sub_Ovf:
                    case Code.Sub_Ovf_Un:
                    case Code.Mul_Ovf:
                    case Code.Mul_Ovf_Un:
                    {
                        Intrinsics intrinsicId;
                        switch (opcode)
                        {
                            case Code.Add_Ovf:
                                intrinsicId = Intrinsics.sadd_with_overflow;
                                break;
                            case Code.Add_Ovf_Un:
                                intrinsicId = Intrinsics.uadd_with_overflow;
                                break;
                            case Code.Sub_Ovf:
                                intrinsicId = Intrinsics.ssub_with_overflow;
                                break;
                            case Code.Sub_Ovf_Un:
                                intrinsicId = Intrinsics.usub_with_overflow;
                                break;
                            case Code.Mul_Ovf:
                                intrinsicId = Intrinsics.smul_with_overflow;
                                break;
                            case Code.Mul_Ovf_Un:
                                intrinsicId = Intrinsics.umul_with_overflow;
                                break;
                            default:
                                throw new ArgumentOutOfRangeException();
                        }
                        var intrinsic = LLVM.IntrinsicGetDeclaration(module, (uint)intrinsicId, new[] {LLVM.TypeOf(value1)});
                        var overflowResult = LLVM.BuildCall(builder, intrinsic, new[] {value1, value2}, string.Empty);
                        var hasOverflow = LLVM.BuildExtractValue(builder, overflowResult, 1, string.Empty);

                        var nextBlock = LLVM.AppendBasicBlockInContext(context, functionGlobal, string.Empty);
                        var overflowBlock = LLVM.AppendBasicBlockInContext(context, functionGlobal, "overflow");
                        LLVM.MoveBasicBlockAfter(overflowBlock, LLVM.GetInsertBlock(builder));
                        LLVM.MoveBasicBlockAfter(nextBlock, overflowBlock);

                        LLVM.BuildCondBr(builder, hasOverflow, overflowBlock, nextBlock);

                        LLVM.PositionBuilderAtEnd(builder, overflowBlock);

                        // Create OverflowException object
                        var overflowExceptionClass = GetClass(corlib.MainModule.GetType(typeof(OverflowException).FullName));
                        EmitNewobj(functionContext, overflowExceptionClass.Type, overflowExceptionClass.Functions.Single(x => x.MethodReference.Name == ".ctor" && x.MethodReference.Parameters.Count == 0));
                        var overflowException = stack.Pop();
                        GenerateInvoke(functionContext, throwExceptionFunction, new[] {LLVM.BuildPointerCast(builder, overflowException.Value, LLVM.TypeOf(LLVM.GetParam(throwExceptionFunction, 0)), string.Empty)});
                        LLVM.BuildUnreachable(builder);

                        functionContext.BasicBlock = nextBlock;
                        LLVM.PositionBuilderAtEnd(builder, nextBlock);
                        result = LLVM.BuildExtractValue(builder, overflowResult, 0, string.Empty);

                        break;
                    }
                    default:
                        goto InvalidBinaryOperation;
                }

                if (CharUsesUTF8)
                {
                    if (opcode == Code.Sub
                        && operand1.Type.TypeReference.FullName == typeof(char*).FullName
                        && operand2.Type.TypeReference.FullName == typeof(char*).FullName)
                    {
                        result = LLVM.BuildLShr(builder, result, LLVM.ConstInt(int32Type, 1, false), string.Empty);
                    }
                }
            }

            Type outputType;

            switch (outputOperandType.StackType)
            {
                case StackValueType.Int32:
                case StackValueType.Int64:
                case StackValueType.Float:
                    // No output conversion required, as it could only have been from same input types (non-shift) or operand 1 (shift)
                    outputType = operand1.Type;
                    break;
                case StackValueType.NativeInt:
                    outputType = intPtr;
                    result = LLVM.BuildIntToPtr(builder, result, intPtrType, string.Empty);
                    break;
                case StackValueType.Reference:
                    result = LLVM.BuildIntToPtr(builder, result, intPtrType, string.Empty);

                    // Get type from one of its operand (if output is reference type, one of the two operand must be too)
                    if (operand1.StackType == StackValueType.Reference)
                        outputType = operand1.Type;
                    else if (operand2.StackType == StackValueType.Reference)
                        outputType = operand2.Type;
                    else
                        goto InvalidBinaryOperation;
                    break;
                default:
                    goto InvalidBinaryOperation;
            }

            stack.Add(new StackValue(outputOperandType.StackType, outputOperandType.Type, result));

            return;

            InvalidBinaryOperation:
            throw new InvalidOperationException(string.Format("Binary operation {0} between {1} and {2} is not supported.", opcode, operand1.StackType, operand2.StackType));
        }

        private void EmitComparison(List<StackValue> stack, Code opcode)
        {
            var operand2 = stack.Pop();
            var operand1 = stack.Pop();

            ValueRef value1;
            ValueRef value2;
            GenerateComparableOperands(operand1, operand2, out value1, out value2);

            ValueRef compareResult;
            if (operand1.StackType == StackValueType.Float)
            {
                RealPredicate predicate;
                switch (opcode)
                {
                    case Code.Ceq:      predicate = RealPredicate.RealOEQ; break;
                    case Code.Cgt:      predicate = RealPredicate.RealOGT; break;
                    case Code.Cgt_Un:   predicate = RealPredicate.RealUGT; break;
                    case Code.Clt:      predicate = RealPredicate.RealOLT; break;
                    case Code.Clt_Un:   predicate = RealPredicate.RealULT; break;
                    default:
                        throw new NotSupportedException();
                }
                compareResult = LLVM.BuildFCmp(builder, predicate, value1, value2, string.Empty);
            }
            else
            {
                IntPredicate predicate;
                switch (opcode)
                {
                    case Code.Ceq:      predicate = IntPredicate.IntEQ; break;
                    case Code.Cgt:      predicate = IntPredicate.IntSGT; break;
                    case Code.Cgt_Un:   predicate = IntPredicate.IntUGT; break;
                    case Code.Clt:      predicate = IntPredicate.IntSLT; break;
                    case Code.Clt_Un:   predicate = IntPredicate.IntULT; break;
                    default:
                        throw new NotSupportedException();
                }
                compareResult = LLVM.BuildICmp(builder, predicate, value1, value2, string.Empty);
            }


            // Extends to int32
            compareResult = LLVM.BuildZExt(builder, compareResult, int32Type, string.Empty);

            // Push result back on the stack
            stack.Add(new StackValue(StackValueType.Int32, int32, compareResult));
        }

        private void GenerateComparableOperands(StackValue operand1, StackValue operand2, out ValueRef value1, out ValueRef value2)
        {
            value1 = operand1.Value;
            value2 = operand2.Value;

            // Downcast objects to typeof(object) so that they are comparables
            if (operand1.StackType == StackValueType.Object)
                value1 = ConvertFromStackToLocal(@object, operand1);
            if (operand2.StackType == StackValueType.Object)
                value2 = ConvertFromStackToLocal(@object, operand2);

            if ((operand1.StackType == StackValueType.NativeInt && operand2.StackType != StackValueType.NativeInt)
                || (operand1.StackType != StackValueType.NativeInt && operand2.StackType == StackValueType.NativeInt))
                throw new NotImplementedException("Comparison between native int and int types.");

            if (operand1.StackType == StackValueType.NativeInt
                && LLVM.TypeOf(value1) != LLVM.TypeOf(value2))
            {
                // NativeInt types should have same types to be comparable
                value2 = LLVM.BuildPointerCast(builder, value2, LLVM.TypeOf(value1), string.Empty);
            }

            // Different object types: cast everything to object
            if (operand1.StackType == StackValueType.Object
                && operand2.StackType == StackValueType.Object
                && operand1.Type != operand2.Type)
            {
                value1 = LLVM.BuildPointerCast(builder, value1, @object.DefaultType, string.Empty);
                value2 = LLVM.BuildPointerCast(builder, value2, @object.DefaultType, string.Empty);
            }

            if (operand1.StackType != operand2.StackType
                || LLVM.TypeOf(value1) != LLVM.TypeOf(value2))
                throw new InvalidOperationException(string.Format("Comparison between operands of different types, {0} and {1}.", operand1.Type, operand2.Type));
        }

        private void EmitConditionalBranch(FunctionCompilerContext functionContext, List<StackValue> stack, int nextInstructionOffset, int targetInstructionOffset, Code opcode)
        {
            var operand2 = stack.Pop();
            var operand1 = stack.Pop();

            ValueRef value1;
            ValueRef value2;
            GenerateComparableOperands(operand1, operand2, out value1, out value2);

            ValueRef compareResult;
            if (operand1.StackType == StackValueType.Float)
            {
                RealPredicate predicate;
                switch (opcode)
                {
                    case Code.Beq:
                    case Code.Beq_S:    predicate = RealPredicate.RealOEQ; break;
                    case Code.Bge:
                    case Code.Bge_S:    predicate = RealPredicate.RealOGE; break;
                    case Code.Bgt:
                    case Code.Bgt_S:    predicate = RealPredicate.RealOGT; break;
                    case Code.Ble:
                    case Code.Ble_S:    predicate = RealPredicate.RealOLE; break;
                    case Code.Blt:
                    case Code.Blt_S:    predicate = RealPredicate.RealOLT; break;
                    case Code.Bne_Un:
                    case Code.Bne_Un_S: predicate = RealPredicate.RealUNE; break;
                    case Code.Bge_Un:
                    case Code.Bge_Un_S: predicate = RealPredicate.RealUGE; break;
                    case Code.Bgt_Un:
                    case Code.Bgt_Un_S: predicate = RealPredicate.RealUGT; break;
                    case Code.Ble_Un:
                    case Code.Ble_Un_S: predicate = RealPredicate.RealULE; break;
                    case Code.Blt_Un:
                    case Code.Blt_Un_S: predicate = RealPredicate.RealULT; break;
                    default:
                        throw new NotSupportedException();
                }
                compareResult = LLVM.BuildFCmp(builder, predicate, value1, value2, string.Empty);
            }
            else
            {
                IntPredicate predicate;
                switch (opcode)
                {
                    case Code.Beq:
                    case Code.Beq_S:    predicate = IntPredicate.IntEQ; break;
                    case Code.Bge:
                    case Code.Bge_S:    predicate = IntPredicate.IntSGE; break;
                    case Code.Bgt:
                    case Code.Bgt_S:    predicate = IntPredicate.IntSGT; break;
                    case Code.Ble:
                    case Code.Ble_S:    predicate = IntPredicate.IntSLE; break;
                    case Code.Blt:
                    case Code.Blt_S:    predicate = IntPredicate.IntSLT; break;
                    case Code.Bne_Un:
                    case Code.Bne_Un_S: predicate = IntPredicate.IntNE; break;
                    case Code.Bge_Un:
                    case Code.Bge_Un_S: predicate = IntPredicate.IntUGE; break;
                    case Code.Bgt_Un:
                    case Code.Bgt_Un_S: predicate = IntPredicate.IntUGT; break;
                    case Code.Ble_Un:
                    case Code.Ble_Un_S: predicate = IntPredicate.IntULE; break;
                    case Code.Blt_Un:
                    case Code.Blt_Un_S: predicate = IntPredicate.IntULT; break;
                    default:
                        throw new NotSupportedException();
                }
                compareResult = LLVM.BuildICmp(builder, predicate, value1, value2, string.Empty);
            }

            // Branch depending on previous test
            LLVM.BuildCondBr(builder, compareResult, functionContext.BasicBlocks[targetInstructionOffset], functionContext.BasicBlocks[nextInstructionOffset]);

            functionContext.FlowingNextInstructionMode = FlowingNextInstructionMode.Explicit;
        }

        private void EmitLocalloc(List<StackValue> stack)
        {
            var numElements = stack.Pop();

            ValueRef numElementsCasted;
            if (numElements.StackType == StackValueType.NativeInt)
            {
                numElementsCasted = LLVM.BuildPtrToInt(builder, numElements.Value, int32Type, string.Empty);
            }
            else
            {
                numElementsCasted = LLVM.BuildIntCast(builder, numElements.Value, int32Type, string.Empty);
            }

            var alloca = LLVM.BuildArrayAlloca(builder, LLVM.Int8TypeInContext(context), numElementsCasted, string.Empty);
            alloca = LLVM.BuildPointerCast(builder, alloca, intPtr.DataType, string.Empty);

            stack.Add(new StackValue(StackValueType.NativeInt, intPtr, alloca));
        }

        private void EmitStind(FunctionCompilerContext functionContext, List<StackValue> stack, Code opcode)
        {
            var value = stack.Pop();
            var address = stack.Pop();

            // Determine type
            Type type;
            switch (opcode)
            {
                case Code.Stind_I: type = intPtr; break;
                case Code.Stind_I1: type = int8; break;
                case Code.Stind_I2: type = int16; break;
                case Code.Stind_I4: type = int32; break;
                case Code.Stind_I8: type = int64; break;
                case Code.Stind_R4: type = @float; break;
                case Code.Stind_R8: type = @double; break;
                case Code.Stind_Ref:
                    type = value.Type;
                    break;
                default:
                    throw new ArgumentException("opcode");
            }

            if (CharUsesUTF8)
            {
                if (opcode == Code.Stind_I2 && address.Type.TypeReference.FullName == typeof(char*).FullName)
                {
                    type = int8;
                }
            }

            // Convert to local type
            var sourceValue = ConvertFromStackToLocal(type, value);

            // Store value at address
            var pointerCast = LLVM.BuildPointerCast(builder, address.Value, LLVM.PointerType(type.DefaultType, 0), string.Empty);
            var storeInst = LLVM.BuildStore(builder, sourceValue, pointerCast);
            SetInstructionFlags(storeInst, functionContext.InstructionFlags);
            functionContext.InstructionFlags = InstructionFlags.None;
        }

        private void EmitLdind(FunctionCompilerContext functionContext, List<StackValue> stack, Code opcode)
        {
            var address = stack.Pop();

            // Determine type
            Type type;
            switch (opcode)
            {
                case Code.Ldind_I: type = intPtr; break;
                case Code.Ldind_I1: type = int8; break;
                case Code.Ldind_I2: type = int16; break;
                case Code.Ldind_I4: type = int32; break;
                case Code.Ldind_I8: type = int64; break;
                case Code.Ldind_U1: type = int8; break;
                case Code.Ldind_U2: type = int16; break;
                case Code.Ldind_U4: type = int32; break;
                case Code.Ldind_R4: type = @float; break;
                case Code.Ldind_R8: type = @double; break;
                case Code.Ldind_Ref:
                    type = GetType(((ByReferenceType)address.Type.TypeReference).ElementType);
                    break;
                default:
                    throw new ArgumentException("opcode");
            }

            if (CharUsesUTF8)
            {
                if (opcode == Code.Ldind_I2 && address.Type.TypeReference.FullName == typeof(char*).FullName)
                {
                    type = int8;
                }
            }

            // Load value at address
            var pointerCast = LLVM.BuildPointerCast(builder, address.Value, LLVM.PointerType(type.DefaultType, 0), string.Empty);
            var loadInst = LLVM.BuildLoad(builder, pointerCast, string.Empty);
            SetInstructionFlags(loadInst, functionContext.InstructionFlags);
            functionContext.InstructionFlags = InstructionFlags.None;

            // Convert to stack type
            var value = ConvertFromLocalToStack(type, loadInst);

            // Add to stack
            stack.Add(new StackValue(type.StackType, type, value));
        }

        private void EmitConv(List<StackValue> stack, Code opcode)
        {
            var value = stack.Pop();

            // Special case: string contains an extra indirection to access its first character.
            // We resolve it on conv.i.
            if (stringSliceable
                && value.Type.TypeReference.FullName == typeof(string).FullName
                && (opcode == Code.Conv_I || opcode == Code.Conv_U))
            {
                // Prepare indices
                var indices = new[]
                {
                    LLVM.ConstInt(int32Type, 0, false),                         // Pointer indirection
                    LLVM.ConstInt(int32Type, (int)ObjectFields.Data, false),    // Data
                    LLVM.ConstInt(int32Type, 2, false),                         // Access string pointer
                };

                var charPointerLocation = LLVM.BuildInBoundsGEP(builder, value.Value, indices, string.Empty);
                var firstCharacterPointer = LLVM.BuildLoad(builder, charPointerLocation, string.Empty);

                stack.Add(new StackValue(StackValueType.NativeInt, intPtr, firstCharacterPointer));
                return;
            }

            uint intermediateWidth;
            bool isSigned;
            bool isOverflow = false;

            switch (opcode)
            {
                case Code.Conv_U: isSigned = false; intermediateWidth = (uint)intPtrSize * 8; break;
                case Code.Conv_I: isSigned = true; intermediateWidth = (uint)intPtrSize * 8; break;
                case Code.Conv_U1: isSigned = false; intermediateWidth = 8; break;
                case Code.Conv_I1: isSigned = true; intermediateWidth = 8; break;
                case Code.Conv_U2: isSigned = false; intermediateWidth = 16; break;
                case Code.Conv_I2: isSigned = true; intermediateWidth = 16; break;
                case Code.Conv_U4: isSigned = false; intermediateWidth = 32; break;
                case Code.Conv_I4: isSigned = true; intermediateWidth = 32; break;
                case Code.Conv_U8: isSigned = false; intermediateWidth = 64; break;
                case Code.Conv_I8: isSigned = true; intermediateWidth = 64; break;
                case Code.Conv_Ovf_U:  isOverflow = true; goto case Code.Conv_U;
                case Code.Conv_Ovf_I:  isOverflow = true; goto case Code.Conv_I;
                case Code.Conv_Ovf_U1: isOverflow = true; goto case Code.Conv_U1;
                case Code.Conv_Ovf_I1: isOverflow = true; goto case Code.Conv_I1;
                case Code.Conv_Ovf_U2: isOverflow = true; goto case Code.Conv_U2;
                case Code.Conv_Ovf_I2: isOverflow = true; goto case Code.Conv_I2;
                case Code.Conv_Ovf_U4: isOverflow = true; goto case Code.Conv_U4;
                case Code.Conv_Ovf_I4: isOverflow = true; goto case Code.Conv_I4;
                case Code.Conv_Ovf_U8: isOverflow = true; goto case Code.Conv_U8;
                case Code.Conv_Ovf_I8: isOverflow = true; goto case Code.Conv_I8;
                case Code.Conv_Ovf_U_Un:  isOverflow = true; goto case Code.Conv_U;
                case Code.Conv_Ovf_I_Un:  isOverflow = true; goto case Code.Conv_I;
                case Code.Conv_Ovf_U1_Un: isOverflow = true; goto case Code.Conv_U1;
                case Code.Conv_Ovf_I1_Un: isOverflow = true; goto case Code.Conv_I1;
                case Code.Conv_Ovf_U2_Un: isOverflow = true; goto case Code.Conv_U2;
                case Code.Conv_Ovf_I2_Un: isOverflow = true; goto case Code.Conv_I2;
                case Code.Conv_Ovf_U4_Un: isOverflow = true; goto case Code.Conv_U4;
                case Code.Conv_Ovf_I4_Un: isOverflow = true; goto case Code.Conv_I4;
                case Code.Conv_Ovf_U8_Un: isOverflow = true; goto case Code.Conv_U8;
                case Code.Conv_Ovf_I8_Un: isOverflow = true; goto case Code.Conv_I8;
                case Code.Conv_R4:
                case Code.Conv_R8:
                    var inputTypeFullName = value.Type.TypeReference.FullName;
                    isSigned = inputTypeFullName == typeof(int).FullName
                        || inputTypeFullName == typeof(short).FullName
                        || inputTypeFullName == typeof(sbyte).FullName
                        || inputTypeFullName == typeof(IntPtr).FullName;
                    intermediateWidth = 0; // unknown yet, depends on input
                    break;
                case Code.Conv_R_Un:
                    // TODO: Not sure if this is exactly what Conv_R_Un should do...
                    isSigned = false;
                    intermediateWidth = 0;
                    break;
                default:
                    throw new InvalidOperationException();
            }


            var currentValue = value.Value;

            if (value.StackType == StackValueType.NativeInt)
            {
                // Convert to integer
                currentValue = LLVM.BuildPtrToInt(builder, currentValue, nativeIntType, string.Empty);
            }
            else if (value.StackType == StackValueType.Reference
                || value.StackType == StackValueType.Object)
            {
                if (opcode != Code.Conv_U8 && opcode != Code.Conv_U
                    && opcode != Code.Conv_I8 && opcode != Code.Conv_I)
                    throw new InvalidOperationException();

                // Convert to integer
                currentValue = LLVM.BuildPtrToInt(builder, currentValue, nativeIntType, string.Empty);
            }
            else if (value.StackType == StackValueType.Float)
            {
                if (opcode == Code.Conv_R4 || opcode == Code.Conv_R8)
                {
                    // Special case: float to float, avoid usual case that goes through an intermediary integer.
                    var outputType = opcode == Code.Conv_R8 ? @double : @float;
                    currentValue = LLVM.BuildFPCast(builder, currentValue, outputType.DataType, string.Empty);
                    stack.Add(new StackValue(StackValueType.Float, outputType, currentValue));
                    return;
                }

                // TODO: Float conversions
                currentValue = isSigned
                    ? LLVM.BuildFPToSI(builder, currentValue, LLVM.IntTypeInContext(context, intermediateWidth), string.Empty)
                    : LLVM.BuildFPToUI(builder, currentValue, LLVM.IntTypeInContext(context, intermediateWidth), string.Empty);
            }

            var inputType = LLVM.TypeOf(currentValue);
            var inputWidth = LLVM.GetIntTypeWidth(inputType);

            // Auto-adapt intermediate width for floats
            if (opcode == Code.Conv_R4 || opcode == Code.Conv_R8 || opcode == Code.Conv_R_Un)
            {
                intermediateWidth = inputWidth;
            }

            var smallestWidth = Math.Min(intermediateWidth, inputWidth);
            var smallestType = LLVM.IntTypeInContext(context, smallestWidth);
            var outputWidth = Math.Max(intermediateWidth, 32);

            // Truncate (if necessary)
            if (smallestWidth < inputWidth)
                currentValue = LLVM.BuildTrunc(builder, currentValue, smallestType, string.Empty);

            if (isOverflow)
            {
                // TODO: Compare currentValue with pre-trunc value?
            }

            // Reextend to appropriate type (if necessary)
            if (outputWidth > smallestWidth)
            {
                var outputIntType = LLVM.IntTypeInContext(context, outputWidth);
                if (isSigned)
                    currentValue = LLVM.BuildSExt(builder, currentValue, outputIntType, string.Empty);
                else
                    currentValue = LLVM.BuildZExt(builder, currentValue, outputIntType, string.Empty);
            }

            // Add constant integer value to stack
            switch (opcode)
            {
                case Code.Conv_U:
                case Code.Conv_I:
                case Code.Conv_Ovf_U:
                case Code.Conv_Ovf_I:
                case Code.Conv_Ovf_U_Un:
                case Code.Conv_Ovf_I_Un:
                    // Convert to native int (if necessary)
                    currentValue = LLVM.BuildIntToPtr(builder, currentValue, intPtrType, string.Empty);
                    stack.Add(new StackValue(StackValueType.NativeInt, intPtr, currentValue));
                    break;
                case Code.Conv_U1:
                case Code.Conv_I1:
                case Code.Conv_U2:
                case Code.Conv_I2:
                case Code.Conv_U4:
                case Code.Conv_I4:
                case Code.Conv_Ovf_U1:
                case Code.Conv_Ovf_I1:
                case Code.Conv_Ovf_U2:
                case Code.Conv_Ovf_I2:
                case Code.Conv_Ovf_U4:
                case Code.Conv_Ovf_I4:
                case Code.Conv_Ovf_U1_Un:
                case Code.Conv_Ovf_I1_Un:
                case Code.Conv_Ovf_U2_Un:
                case Code.Conv_Ovf_I2_Un:
                case Code.Conv_Ovf_U4_Un:
                case Code.Conv_Ovf_I4_Un:
                    stack.Add(new StackValue(StackValueType.Int32, int32, currentValue));
                    break;
                case Code.Conv_U8:
                case Code.Conv_I8:
                case Code.Conv_Ovf_U8:
                case Code.Conv_Ovf_I8:
                case Code.Conv_Ovf_U8_Un:
                case Code.Conv_Ovf_I8_Un:
                    stack.Add(new StackValue(StackValueType.Int64, int64, currentValue));
                    break;
                case Code.Conv_R4:
                case Code.Conv_R8:
                case Code.Conv_R_Un:
                    var outputType = opcode == Code.Conv_R8 || opcode == Code.Conv_R_Un ? @double : @float;
                    if (isSigned)
                        currentValue = LLVM.BuildSIToFP(builder, currentValue, outputType.DataType, string.Empty);
                    else
                        currentValue = LLVM.BuildUIToFP(builder, currentValue, outputType.DataType, string.Empty);
                    stack.Add(new StackValue(StackValueType.Float, outputType, currentValue));
                    break;
                default:
                    throw new InvalidOperationException();
            }
        }

        private void EmitEndfinally(FunctionCompilerContext functionContext, ExceptionHandlerInfo currentFinallyClause)
        {
            if (currentFinallyClause.Source.HandlerType == ExceptionHandlerType.Finally)
            {
                // Basic block to continue exception handling (if endfinally.jumptarget is -1, but we simply set it on undefined cases)
                var activeTryHandlers = functionContext.ActiveTryHandlers;
                var nextActiveTryHandler = activeTryHandlers.Count > 0 ? activeTryHandlers[activeTryHandlers.Count - 1].CatchDispatch : functionContext.ResumeExceptionBlock;

                // Generate dispatch code (with a switch/case)
                var @switch = LLVM.BuildSwitch(builder, LLVM.BuildLoad(builder, functionContext.EndfinallyJumpTarget, string.Empty), nextActiveTryHandler, (uint)currentFinallyClause.LeaveTargets.Count);
                for (int index = 0; index < currentFinallyClause.LeaveTargets.Count; index++)
                {
                    var leaveTarget = currentFinallyClause.LeaveTargets[index];

                    LLVM.AddCase(@switch, LLVM.ConstInt(int32Type, (ulong)index, false), functionContext.BasicBlocks[leaveTarget.Offset]);
                }
            }
            else if (currentFinallyClause.Source.HandlerType == ExceptionHandlerType.Fault)
            {
                var exceptionObject = LLVM.BuildLoad(builder, functionContext.ExceptionSlot, string.Empty);

                // Rethrow exception
                GenerateInvoke(functionContext, throwExceptionFunction, new[] {LLVM.BuildPointerCast(builder, exceptionObject, LLVM.TypeOf(LLVM.GetParam(throwExceptionFunction, 0)), string.Empty)});
                LLVM.BuildUnreachable(builder);
            }
            else
            {
                throw new InvalidOperationException("Exception clause containing a endfinally/endfault is not a finally or fault clause.");
            }

            // Default is not to flow to next instruction, previous Leave instructions already tagged what was necessary
            functionContext.FlowingNextInstructionMode = FlowingNextInstructionMode.None;
        }
    }
}