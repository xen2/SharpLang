using System;
using System.Collections.Generic;
using Mono.Cecil;
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

            // Create string data global
            var stringConstantData = LLVM.ConstStringInContext(context, operand, (uint)operand.Length, true);
            var stringConstantDataGlobal = LLVM.AddGlobal(module, LLVM.TypeOf(stringConstantData), string.Empty);

            // Cast from i8-array to i8*
            LLVM.SetInitializer(stringConstantDataGlobal, stringConstantData);
            var zero = LLVM.ConstInt(int32Type, 0, false);
            stringConstantDataGlobal = LLVM.ConstInBoundsGEP(stringConstantDataGlobal, new[] { zero, zero });

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
            // TODO: 64bit support
            ValueRef numElementsCasted;
            if (numElements.StackType == StackValueType.NativeInt)
            {
                numElementsCasted = LLVM.BuildPtrToInt(builder, numElements.Value, int32Type, string.Empty);
            }
            else
            {
                numElementsCasted = LLVM.BuildIntCast(builder, numElements.Value, int32Type, string.Empty);
            }
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

            var refType = GetType(elementType.TypeReference.MakeByReferenceType());

            // Load array data pointer
            var arrayFirstElement = LoadArrayDataPointer(array);

            // Find pointer of element at requested index
            var arrayElementPointer = LLVM.BuildGEP(builder, arrayFirstElement, new[] { index.Value }, string.Empty);

            // Convert
            arrayElementPointer = ConvertFromLocalToStack(refType, arrayElementPointer);

            // Push loaded element address onto the stack
            stack.Add(new StackValue(refType.StackType, refType, arrayElementPointer));
        }

        private void EmitLdelem(List<StackValue> stack)
        {
            var index = stack.Pop();
            var array = stack.Pop();

            // Get element type
            var elementType = GetType(((ArrayType)array.Type.TypeReference).ElementType);

            // Load array data pointer
            var arrayFirstElement = LoadArrayDataPointer(array);

            // Find pointer of element at requested index
            var arrayElementPointer = LLVM.BuildGEP(builder, arrayFirstElement, new[] { index.Value }, string.Empty);

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

            // Get element type
            var elementType = GetType(((ArrayType)array.Type.TypeReference).ElementType);

            // Load array data pointer
            var arrayFirstElement = LoadArrayDataPointer(array);

            // Find pointer of element at requested index
            var arrayElementPointer = LLVM.BuildGEP(builder, arrayFirstElement, new[] { index.Value }, string.Empty);

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
    }
}