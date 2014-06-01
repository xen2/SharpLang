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
            if (stack.StackType == StackValueType.Value
                && localType.DefaultType == stack.Type.DefaultType)
            {
                return stackValue;
            }

            // Object: allow upcast as well
            if (stack.StackType == StackValueType.Object)
            {
                if (localType.DefaultType == stack.Type.DefaultType)
                {
                    return stackValue;
                }

                if (localType.TypeReference.Resolve().IsInterface)
                {
                    // Interface upcast
                    var stackClass = GetClass(stack.Type.TypeReference);
                    foreach (var @interface in stackClass.Interfaces)
                    {
                        if (MemberEqualityComparer.Default.Equals(@interface.TypeReference, localType.TypeReference))
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
            }

            // Spec: Storing into locals that hold an integer value smaller than 4 bytes long truncates the value as it moves from the stack to the local variable.
            if ((stack.StackType == StackValueType.Int32 || stack.StackType == StackValueType.Int64)
                && LLVM.GetTypeKind(localType.DefaultType) == TypeKind.IntegerTypeKind)
            {
                return LLVM.BuildIntCast(builder, stackValue, localType.DefaultType, string.Empty);
            }

            // TODO: Other cases
            throw new NotImplementedException();
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
                    var int32Type = LLVM.Int32TypeInContext(context);
                    if (localType.DefaultType != int32Type)
                    {
                        if (LLVM.GetTypeKind(localType.DefaultType) != TypeKind.IntegerTypeKind)
                            throw new InvalidOperationException();

                        if (LLVM.GetIntTypeWidth(localType.DefaultType) < 32)
                        {
                            // Extend sign if needed
                            // TODO: Need a way to handle unsigned int. Unfortunately it seems that
                            // LLVMBuildIntCast doesn't have CastInst::CreateIntegerCast isSigned parameter.
                            // Probably need to directly create ZExt/SExt.
                            return LLVM.BuildIntCast(builder, stack, int32Type, string.Empty);
                        }
                    }
                    break;
                case StackValueType.Value:
                    // Value type, no conversion should be needed
                    break;
                case StackValueType.Object:
                    // TODO: Check type conversions (upcasts, etc...)
                    break;
                default:
                    throw new NotImplementedException();
            }
            return stack;
        }
    }
}