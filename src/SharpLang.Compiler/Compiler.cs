using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Mono.Cecil;
using Mono.Cecil.Cil;
using SharpLang.CompilerServices.Cecil;
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
        private TypeRef imtEntryType;

        // Builder that is used for PHI nodes
        private BuilderRef builderPhi;

        private AssemblyDefinition assembly;
        private AssemblyDefinition corlib;

        private Dictionary<TypeReference, Type> types = new Dictionary<TypeReference, Type>(MemberEqualityComparer.Default);
        private Dictionary<TypeReference, Class> classes = new Dictionary<TypeReference, Class>(MemberEqualityComparer.Default);
        private Dictionary<MethodReference, Function> functions = new Dictionary<MethodReference, Function>(MemberEqualityComparer.Default);

        private Queue<KeyValuePair<MethodReference, Function>> methodsToCompile = new Queue<KeyValuePair<MethodReference, Function>>();

        public ModuleRef CompileAssembly(AssemblyDefinition assembly)
        {
            this.assembly = assembly;
            corlib = assembly.MainModule.Import(typeof (void)).Resolve().Module.Assembly;
            module = LLVM.ModuleCreateWithName(assembly.Name.Name);

            allocObjectFunction = RuntimeInline.Runtime.define_allocObject(module);

            context = LLVM.GetModuleContext(module);
            builder = LLVM.CreateBuilderInContext(context);
            intPtrType = LLVM.PointerType(LLVM.Int8TypeInContext(context), 0);
            builderPhi = LLVM.CreateBuilderInContext(context);

            // struct IMTSlot { i8* functionPtr, i32 functionId, IMTEntry* nextEntry }
            imtEntryType = LLVM.StructCreateNamed(context, "IMTEntry");
            LLVM.StructSetBody(imtEntryType, new[] { intPtrType, LLVM.Int32TypeInContext(context), LLVM.PointerType(imtEntryType, 0) }, false);

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
                    if (member.DeclaringType.ContainsGenericParameter())
                        continue;
                    CreateType(member.DeclaringType);
                    if (method != null)
                    {
                        if (!method.HasGenericParameters)
                            CreateFunction(method);
                    }
                }

                foreach (var type in assemblyModule.Types)
                {
                    if (!type.HasGenericParameters && type.FullName != typeof(void).FullName)
                        GetClass(type);

                    foreach (var nestedType in type.NestedTypes)
                    {
                        if (!nestedType.HasGenericParameters)
                            GetClass(nestedType);
                    }
                }
            }

            // Process methods
            foreach (var @class in classes)
            {
                CompileClassMethods(@class.Value);
            }

            // Generate code
            while (methodsToCompile.Count > 0)
            {
                var methodToCompile = methodsToCompile.Dequeue();
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

	            var parameters = (entryPoint.ParameterTypes.Length > 0)
	                ? new[] { LLVM.ConstPointerNull(entryPoint.ParameterTypes[0].DefaultType) }
	                : new ValueRef[0];

                LLVM.BuildCall(builder, entryPoint.GeneratedValue, parameters, string.Empty);
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
            // Already processed?
            if (@class.MethodCompiled)
                return;

            @class.MethodCompiled = true;

            var typeDefinition = @class.TypeReference.Resolve();

            bool isInterface = typeDefinition.IsInterface;

            bool isExternal = typeDefinition.Module.Assembly != assembly;
            if (!isExternal)
            {
                // Process methods
                foreach (var method in typeDefinition.Methods)
                {
                    // If a method contains generic parameters, skip it
                    // Its closed instantiations (with generic arguments) is what needs to be generated.
                    if (method.ContainsGenericParameter())
                        continue;

                    var methodReference = ResolveGenericMethod(@class.TypeReference, method);
                    var function = CreateFunction(methodReference);

                    if (method.IsVirtual)
                    {
                        if (isInterface)
                        {
                            // Store IMT slot
                            function.VirtualSlot = (int)(GetMethodId(methodReference) % InterfaceMethodTableSize);
                        }
                        else if (method.IsNewSlot)
                        {
                            // New slot
                            function.VirtualSlot = @class.VirtualTable.Count;
                            @class.VirtualTable.Add(function);
                        }
                        else
                        {
                            // Find slot in base types
                            var baseType = @class.BaseType;
                            Function matchedMethod = null;
                            while (baseType != null)
                            {
                                matchedMethod = CecilExtensions.TryMatchMethod(baseType, method);
                                if (matchedMethod != null)
                                    break;
                                baseType = baseType.BaseType;
                            }

                            if (matchedMethod == null)
                                throw new InvalidOperationException(string.Format("Could not find a slot for virtual function {0} in parents of class {1}", method, @class.TypeReference));

                            function.VirtualSlot = matchedMethod.VirtualSlot;
                            @class.VirtualTable[function.VirtualSlot] = function;
                        }
                    }
                }
            }
        }

        private static MethodReference ResolveGenericMethod(TypeReference typeReference, MethodReference method)
        {
            var genericInstanceType = typeReference as GenericInstanceType;
            if (genericInstanceType != null)
                return method.MakeGeneric(genericInstanceType.GenericArguments.ToArray());

            return method;
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