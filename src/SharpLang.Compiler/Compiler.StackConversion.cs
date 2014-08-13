using System;
using SharpLLVM;

namespace SharpLang.CompilerServices
{
    public partial class Compiler
    {
        /// <summary>
        /// Helper function to convert variables from stack to local
        /// </summary>
        /// <param name="local">The local variable.</param>
        /// <param name="stack">The stack variable.</param>
        /// <returns></returns>
        /// <exception cref="System.NotImplementedException"></exception>
        private ValueRef ConvertFromStackToLocal(Type localType, StackValue stack)
        {
            var stackValue = stack.Value;

            // Same type, return as is
            if ((stack.StackType == StackValueType.Value
                    || stack.StackType == StackValueType.NativeInt)
                && localType.DefaultType == stack.Type.DefaultType)
            {
                return stackValue;
            }

            // NativeInt to NativeInt
            // Need pointer conversion
            if (stack.StackType == StackValueType.NativeInt && localType.StackType == StackValueType.NativeInt)
            {
                return LLVM.BuildPointerCast(builder, stackValue, localType.DefaultType, string.Empty);
            }

            // NativeInt to Reference
            // Used for example when casting+boxing primitive type pointers
            // TODO: Start GC tracking?
            if (stack.StackType == StackValueType.NativeInt && localType.StackType == StackValueType.Reference)
            {
                // Fallback: allow everything for now...
                return LLVM.BuildPointerCast(builder, stackValue, localType.DefaultType, string.Empty);
            }

            // Object: allow upcast as well
            if (stack.StackType == StackValueType.Reference
                || stack.StackType == StackValueType.Object)
            {
                if (localType.DefaultType == stack.Type.DefaultType)
                {
                    return stackValue;
                }

                if (localType.TypeReference.Resolve().IsInterface)
                {
                    // Interface upcast
                    var stackClass = GetClass(stack.Type);
                    foreach (var @interface in stackClass.Interfaces)
                    {
                        if (MemberEqualityComparer.Default.Equals(@interface.Type.TypeReference, localType.TypeReference))
                        {
                            return LLVM.BuildPointerCast(builder, stackValue, localType.DefaultType, string.Empty);
                        }
                    }
                }
                else
                {
                    // Class upcast
                    // Check upcast in hierarchy
                    // TODO: we could optimize by storing Depth
                    var stackType = stack.Type.TypeReference;
                    while (stackType != null)
                    {
                        if (MemberEqualityComparer.Default.Equals(stackType, localType.TypeReference))
                        {
                            // It's an upcast, do LLVM pointer cast
                            return LLVM.BuildPointerCast(builder, stackValue, localType.DefaultType, string.Empty);
                        }

                        stackType = stackType.Resolve().BaseType;
                    }
                }

                // Fallback: allow everything for now...
                return LLVM.BuildPointerCast(builder, stackValue, localType.DefaultType, string.Empty);
            }

            if (stack.StackType == StackValueType.Float && stack.Type == localType)
            {
                return stackValue;
            }

            // Spec: Storing into locals that hold an integer value smaller than 4 bytes long truncates the value as it moves from the stack to the local variable.
            if ((stack.StackType == StackValueType.Int32 || stack.StackType == StackValueType.Int64)
                && LLVM.GetTypeKind(localType.DefaultType) == TypeKind.IntegerTypeKind)
            {
                return LLVM.BuildIntCast(builder, stackValue, localType.DefaultType, string.Empty);
            }

            // TODO: Other cases
            throw new NotImplementedException(string.Format("Error converting from {0} to {1}", stack.Type, localType));
        }

        /// <summary>
        /// Helper function to convert variables from local to stack.
        /// </summary>
        /// <param name="local">The local variable.</param>
        /// <param name="stack">The stack variable.</param>
        /// <returns></returns>
        /// <exception cref="System.InvalidOperationException"></exception>
        /// <exception cref="System.NotImplementedException"></exception>
        private ValueRef ConvertFromLocalToStack(Type localType, ValueRef stack)
        {
            switch (localType.StackType)
            {
                case StackValueType.Int32:
                case StackValueType.Int64:
                    var expectedIntType = localType.StackType == StackValueType.Int32 ? int32Type : int64Type;
                    if (localType.DefaultType != expectedIntType)
                    {
                        if (LLVM.GetTypeKind(localType.DefaultType) != TypeKind.IntegerTypeKind)
                            throw new InvalidOperationException();

                        if (LLVM.GetIntTypeWidth(localType.DefaultType) < LLVM.GetIntTypeWidth(expectedIntType))
                        {
                            // Extend sign if needed
                            // TODO: Need a way to handle unsigned int. Unfortunately it seems that
                            // LLVMBuildIntCast doesn't have CastInst::CreateIntegerCast isSigned parameter.
                            // Probably need to directly create ZExt/SExt.
                            return LLVM.BuildIntCast(builder, stack, expectedIntType, string.Empty);
                        }
                    }
                    break;
                case StackValueType.NativeInt:
                    // NativeInt type, no conversion should be needed
                    break;
                case StackValueType.Value:
                    // Value type, no conversion should be needed
                    break;
                case StackValueType.Reference:
                case StackValueType.Object:
                    // TODO: Check type conversions (upcasts, etc...)
                    break;
                case StackValueType.Float:
                    // Float type, no conversion should be needed
                    break;
                default:
                    throw new NotImplementedException();
            }
            return stack;
        }
    }
}