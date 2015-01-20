// Copyright (c) 2014 SharpLang - Virgile Bello

using System;
using System.Text;
using SharpLLVM;

namespace SharpLang.CompilerServices
{
    public partial class Compiler
    {
        private const int PInvokeThunkCount = 4096;

        private StringBuilder PInvokeEmitThunks()
        {
            // Emit PInvoke thunks (x86 only for now)
            var pinvokeThunks = new StringBuilder();

            PInvokeTarget target;
            if (LLVM.GetTarget(runtimeModule).StartsWith("i686-w64-"))
            {
                target = PInvokeTarget.WindowsX86;
            }
            else if (LLVM.GetTarget(runtimeModule).StartsWith("x86_64-w64-"))
            {
                target = PInvokeTarget.WindowsX64;
            }
            else
            {
                throw new NotSupportedException("Unknown platform target.");
            }

            // Generate thunkHelper (which will read TLS ThunkCurrentId and dispatch to ThunkTargets)
            pinvokeThunks.AppendFormat(".section .text\n.global {0}\n", AssemblySymbolName(target, "ThunkTargets"));
            pinvokeThunks.AppendFormat("{0}:\n", AssemblySymbolName(target, "thunkHelper"));
            PInvokeEmitThunkHelper(target, pinvokeThunks);

            for (int i = 0; i < PInvokeThunkCount; ++i)
            {
                // Jump to appropriate thunk
                pinvokeThunks.AppendFormat("{0}{1}:\n", AssemblySymbolName(target, "thunk"), i);
                PInvokeEmitThunk(target, pinvokeThunks, i);
            }
            
            return pinvokeThunks;
        }

        private static string AssemblySymbolName(PInvokeTarget target, string symbolName)
        {
            if (target == PInvokeTarget.WindowsX86)
                return '_' + symbolName;

            return symbolName;
        }

        private void PInvokeEmitThunk(PInvokeTarget target, StringBuilder pinvokeThunks, int i)
        {
            if (target == PInvokeTarget.WindowsX86)
            {
                // Set eax to thunk id
                pinvokeThunks.AppendFormat("    movl ${0}, %eax\n", i);
                // Call thunkHelper dispatch function
                pinvokeThunks.AppendFormat("    jmp _thunkHelper\n", i*intPtrSize);
            }
        }

        private static void PInvokeEmitThunkHelper(PInvokeTarget target, StringBuilder pinvokeThunks)
        {
            if (target == PInvokeTarget.WindowsX86)
            {
                // TODO: Not sure which register can actually be modified (need something that works on every calling convention)
                // Seems %eax is best choice, but need to check more carefully

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
            }
        }

        private void PInvokeEmitGlobals()
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

            var pinvokeThunks = PInvokeEmitThunks();
            LLVM.SetModuleInlineAsm(module, pinvokeThunks.ToString());
        }

        // TODO: Real triplet support? And extend this generally to the compiler for more robust cross-compiler support (instead of using intPtrSize == 8)
        enum PInvokeTarget
        {
            WindowsX86,
            WindowsX64,
        }
    }
}