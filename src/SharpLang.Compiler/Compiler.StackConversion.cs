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
                && localType.GeneratedType == stack.Type.GeneratedType)
            {
                return stackValue;
            }

            // Spec: Storing into locals that hold an integer value smaller than 4 bytes long truncates the value as it moves from the stack to the local variable.
            if ((stack.StackType == StackValueType.Int32 || stack.StackType == StackValueType.Int64)
                && LLVM.GetTypeKind(localType.GeneratedType) == TypeKind.IntegerTypeKind)
            {
                return LLVM.BuildIntCast(builder, stackValue, localType.GeneratedType, string.Empty);
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
                    if (localType.GeneratedType != int32Type)
                    {
                        if (LLVM.GetTypeKind(localType.GeneratedType) != TypeKind.IntegerTypeKind)
                            throw new InvalidOperationException();

                        if (LLVM.GetIntTypeWidth(localType.GeneratedType) < 32)
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
                default:
                    throw new NotImplementedException();
            }
            return stack;
        }
    }
}