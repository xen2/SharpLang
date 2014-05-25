using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Mono.Cecil;
using Mono.Cecil.Cil;
using SharpLLVM;

namespace SharpLang.CompilerServices
{
    /// <summary>
    /// Provides access to C# LLVM code generator.
    /// </summary>
    public sealed partial class Compiler
    {
        private ModuleRef module;
        private ContextRef context;

        private ValueRef allocObjectFunction;

        // Builder for normal instructions
        private BuilderRef builder;
        private TypeRef intPtrType;

        // Builder that is used for PHI nodes
        private BuilderRef builderPhi;

        private AssemblyDefinition assembly;
        private AssemblyDefinition corlib;

        private Dictionary<TypeReference, Type> types = new Dictionary<TypeReference, Type>(MemberEqualityComparer.Default);
        private Dictionary<TypeDefinition, Class> classes = new Dictionary<TypeDefinition, Class>(MemberEqualityComparer.Default);
        private Dictionary<MethodReference, Function> functions = new Dictionary<MethodReference, Function>(MemberEqualityComparer.Default);

        private List<KeyValuePair<MethodDefinition, Function>> methodsToCompile = new List<KeyValuePair<MethodDefinition, Function>>();

        public ModuleRef CompileAssembly(AssemblyDefinition assembly)
        {
            this.assembly = assembly;
            corlib = assembly.MainModule.Import(typeof (void)).Resolve().Module.Assembly;
            module = LLVM.ModuleCreateWithName(assembly.Name.Name);

            allocObjectFunction = RuntimeInline.Runtime.define_allocObject(module);

            context = LLVM.GetModuleContext(module);
            builder = LLVM.CreateBuilderInContext(context);
            intPtrType = LLVM.Int32TypeInContext(context);
            builderPhi = LLVM.CreateBuilderInContext(context);

            // Process types
            foreach (var assemblyModule in assembly.Modules)
            {
                foreach (var type in assemblyModule.GetTypeReferences())
                {
                    CreateType(type);
                }

                foreach (var member in assemblyModule.GetMemberReferences())
                {
                    var method = member as MethodReference;
                    CreateType(member.DeclaringType);
                    if (method != null)
                    {
                        CreateFunction(method);
                    }
                }

                foreach (var type in assemblyModule.Types)
                {
                    CreateType(type);

                    foreach (var nestedType in type.NestedTypes)
                    {
                        CreateType(nestedType);
                    }
                }
            }

            // Process methods
            foreach (var @class in classes)
            {
                CompileClassMethods(@class.Value);
            }

            // Generate code
            foreach (var methodToCompile in methodsToCompile)
            {
                CompileFunction(methodToCompile.Key, methodToCompile.Value);
            }

            // Emit "main" which will call the assembly entry point (if any)
            Function entryPoint;
	        if (assembly.EntryPoint != null && functions.TryGetValue(assembly.EntryPoint, out entryPoint))
	        {
	            var mainFunctionType = LLVM.FunctionType(LLVM.Int32TypeInContext(context), new TypeRef[0], false);
	            var mainFunction = LLVM.AddFunction(module, "main", mainFunctionType);
                LLVM.SetLinkage(mainFunction, Linkage.ExternalLinkage);
                LLVM.PositionBuilderAtEnd(builder, LLVM.AppendBasicBlockInContext(context, mainFunction, string.Empty));

                LLVM.BuildCall(builder, entryPoint.GeneratedValue, new ValueRef[0], string.Empty);
	            LLVM.BuildRet(builder, LLVM.ConstInt(LLVM.Int32TypeInContext(context), 0, false));
	        }

            LLVM.DisposeBuilder(builder);

            var code = LLVM.PrintModuleToString(module);

            // Verify module
            string message;
            if (LLVM.VerifyModule(module, VerifierFailureAction.PrintMessageAction, out message))
            {
                throw new InvalidOperationException(message);
            }
            
            return module;
        }

        void CompileClassMethods(Class @class)
        {
            bool isExternal = @class.TypeDefinition.Module.Assembly != assembly;
            if (!isExternal)
            {
                // Process methods
                foreach (var method in @class.TypeDefinition.Methods)
                {
                    var function = CreateFunction(method);

                    if (method.IsVirtual)
                    {
                        function.VirtualSlot = @class.VirtualTable.Count;
                        @class.VirtualTable.Add(function);
                    }
                }

                // Create VTable
                var vtableMethodType = LLVM.PointerType(LLVM.Int8TypeInContext(context), 0);
                var vtable = LLVM.ConstArray(vtableMethodType, @class.VirtualTable.Select(virtualMethod => LLVM.ConstPointerCast(virtualMethod.GeneratedValue, vtableMethodType)).ToArray());
            }
        }

        class MemberEqualityComparer : IEqualityComparer<MemberReference>
        {
            public static readonly MemberEqualityComparer Default = new MemberEqualityComparer();

            public bool Equals(MemberReference x, MemberReference y)
            {
                return x.FullName == y.FullName;
            }

            public int GetHashCode(MemberReference obj)
            {
                return obj.FullName.GetHashCode();
            }
        }
    }
}