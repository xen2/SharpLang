// Copyright (c) 2014 SharpLang - Virgile Bello

using System.Text;
using SharpLLVM;

namespace SharpLang.CompilerServices
{
    public partial class Compiler
    {
        private const int PInvokeThunkCount = 4096;

        private StringBuilder GeneratePInvokeThunks()
        {
            // TODO: Not sure which register can actually be modified (need something that works on every calling convention)
            // Seems %eax is best choice, but need to check more carefully
            // Another way is to generate full code for each thunk, but this would take much more executable space (about 30+ bytes per thunk instead of 10)

            // Emit PInvoke thunks (x86 only for now)
            var pinvokeThunks = new StringBuilder();
            pinvokeThunks.Append(".section .text\n.global _ThunkTargets\n");
            pinvokeThunks.AppendLine("_thunkHelper:");
            // Save registers
            pinvokeThunks.AppendLine("    pushl %edx");
            pinvokeThunks.AppendLine("    pushl %ecx");
            // ThunkCurrentId = index (note: ThunkCurrentId is a TLS variable)
            pinvokeThunks.AppendLine("    movl __tls_index, %edx");
            pinvokeThunks.AppendLine("    movl %fs:2Ch, %ecx");
            pinvokeThunks.AppendLine("    movl (%ecx,%edx,4), %ecx");
            pinvokeThunks.AppendLine("    movl %eax, _ThunkCurrentId@SECREL32(%ecx)");
            // Load ThunkTargets[index] in eax
            pinvokeThunks.AppendLine("    leal _ThunkTargets, %ecx");
            pinvokeThunks.AppendLine("    movl (%ecx,%eax,4), %eax");
            // Restore registers
            pinvokeThunks.AppendLine("    popl %ecx");
            pinvokeThunks.AppendLine("    popl %edx");
            // Jump to eax = ThunkTargets[index]
            pinvokeThunks.AppendLine("    jmp *%eax");

            for (int i = 0; i < PInvokeThunkCount; ++i)
            {
                // Jump to appropriate thunk
                pinvokeThunks.AppendFormat("_thunk{0}:\n", i);
                pinvokeThunks.AppendFormat("    movl ${0}, %eax\n", i);
                pinvokeThunks.AppendFormat("    jmp _thunkHelper\n", i * intPtrSize);
            }
            return pinvokeThunks;
        }

        private void EmitPInvokeGlobals()
        {
            // Add ThunkPointers global, which contains the addresses of our thunk functions
            var thunkPointers = LLVM.AddGlobal(module, LLVM.ArrayType(intPtrLLVM, 4096), "ThunkPointers");

            if (TestMode)
            {
                LLVM.SetInitializer(thunkPointers, LLVM.ConstNull(LLVM.GetElementType(LLVM.TypeOf(thunkPointers))));
                return;
            }

            ValueRef[] thunkPointersData = new ValueRef[PInvokeThunkCount];
            for (int i = 0; i < PInvokeThunkCount; ++i)
            {
                thunkPointersData[i] = LLVM.AddGlobal(module, LLVM.GetElementType(intPtrLLVM), string.Format("thunk{0}", i));
            }
            LLVM.SetInitializer(thunkPointers, LLVM.ConstArray(intPtrLLVM, thunkPointersData));

            // Add TLS variable for storing current thunk ID
            var thunkCurrentId = LLVM.AddGlobal(module, int32LLVM, "ThunkCurrentId");
            LLVM.SetThreadLocal(thunkCurrentId, true);

            var pinvokeThunks = GeneratePInvokeThunks();
            LLVM.SetModuleInlineAsm(module, pinvokeThunks.ToString());
        }
    }
}