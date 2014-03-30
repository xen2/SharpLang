using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using Mono.Cecil;
using Mono.Cecil.Cil;
using SharpLLVM;

namespace SharpLang.CompilerServices
{
    public sealed class Compiler
    {
        private ModuleRef module;
        private ContextRef context;
        private BuilderRef builder;

        private IAssemblyResolver assemblyResolver;
        private AssemblyDefinition assembly;
        private AssemblyDefinition corlib;

        private Dictionary<TypeReference, Type> types = new Dictionary<TypeReference, Type>();
        private Dictionary<TypeDefinition, Class> classes = new Dictionary<TypeDefinition, Class>();
        private Dictionary<MethodReference, Function> functions = new Dictionary<MethodReference, Function>();

        private List<KeyValuePair<MethodDefinition, ValueRef>> methodsToCompile = new List<KeyValuePair<MethodDefinition, ValueRef>>();

        public ModuleRef CompileAssembly(IAssemblyResolver assemblyResolver, AssemblyDefinition assembly)
        {
            this.assemblyResolver = assemblyResolver;
            this.assembly = assembly;
            corlib = assembly.MainModule.Import(typeof (void)).Resolve().Module.Assembly;
            module = LLVM.ModuleCreateWithName(assembly.Name.Name);
            context = LLVM.GetModuleContext(module);
            builder = LLVM.CreateBuilderInContext(context);

            // Process types
            foreach (var assemblyModule in assembly.Modules)
            {
                foreach (var type in assemblyModule.GetTypeReferences())
                {
                    CompileType(type);
                }

                foreach (var member in assemblyModule.GetMemberReferences())
                {
                    var method = member as MethodReference;
                    CompileType(member.DeclaringType);
                    if (method != null)
                    {
                        CompileMethod(method);
                    }
                }

                foreach (var type in assemblyModule.Types)
                {
                    CompileType(type);
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
                CompileMethodImpl(methodToCompile.Key, methodToCompile.Value);
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

            // Verify module
            string message;
            if (LLVM.VerifyModule(module, VerifierFailureAction.PrintMessageAction, out message))
            {
                throw new InvalidOperationException(message);
            }
            
            return module;
        }

        Class CompileClass(TypeDefinition typeDefinition)
        {
            Class @class;
            if (classes.TryGetValue(typeDefinition, out @class))
                return @class;

            TypeRef dataType;
            bool processFields = false;

            switch (typeDefinition.MetadataType)
            {
                case MetadataType.Void:
                    dataType = LLVM.VoidTypeInContext(context);
                    break;
                case MetadataType.Boolean:
                    dataType = LLVM.Int1TypeInContext(context);
                    break;
                case MetadataType.Char:
                case MetadataType.Byte:
                case MetadataType.SByte:
                    dataType = LLVM.Int8TypeInContext(context);
                    break;
                case MetadataType.Int16:
                case MetadataType.UInt16:
                    dataType = LLVM.Int16TypeInContext(context);
                    break;
                case MetadataType.Int32:
                case MetadataType.UInt32:
                    dataType = LLVM.Int32TypeInContext(context);
                    break;
                case MetadataType.Int64:
                case MetadataType.UInt64:
                    dataType = LLVM.Int64TypeInContext(context);
                    break;
                case MetadataType.String:
                    // String: 32 bit length + char pointer
                    dataType = LLVM.StructCreateNamed(context, typeDefinition.FullName);
                    LLVM.StructSetBody(dataType, new[] { LLVM.Int32TypeInContext(context), LLVM.PointerType(LLVM.Int8TypeInContext(context), 0) }, false);
                    break;
                default:
                    // Process non-static fields
                    dataType = LLVM.StructCreateNamed(context, typeDefinition.FullName);
                    processFields = true;
                    break;
            }

            @class = new Class(typeDefinition, dataType);
            classes.Add(typeDefinition, @class);

            if (processFields)
            {
                var fieldTypes = new List<TypeRef>(typeDefinition.Fields.Count);

                foreach (var field in typeDefinition.Fields)
                {
                    if (field.IsStatic)
                        continue;

                    fieldTypes.Add(CompileType(assembly.MainModule.Import(field.FieldType)).GeneratedType);
                }

                LLVM.StructSetBody(dataType, fieldTypes.ToArray(), false);
            }

            return @class;
        }

        void CompileClassMethods(Class @class)
        {
            bool isExternal = @class.TypeDefinition.Module.Assembly != assembly;
            if (!isExternal)
            {
                // Process methods
                foreach (var method in @class.TypeDefinition.Methods)
                {
                    var function = CompileMethod(method);
                }
            }
        }

        Type CompileType(TypeReference typeReference)
        {
            Type type;
            if (types.TryGetValue(typeReference, out type))
                return type;

            if (typeReference.MetadataType != MetadataType.Void)
            {
                var typeDefinition = typeReference.Resolve();
                var @class = CompileClass(typeDefinition);

                type = new Type(typeReference, @class.Type);
            }
            else
            {
                type = new Type(typeReference, LLVM.VoidTypeInContext(context));
            }

            types.Add(typeReference, type);

            if (type == null)
            {
                throw new NotImplementedException();
            }

            return type;
        }

        Function CompileMethod(MethodReference method)
        {
            Function function;
            if (functions.TryGetValue(method, out function))
                return function;

            var numParams = method.Parameters.Count;
            var parameterTypes = new TypeRef[numParams];
            for (int index = 0; index < numParams; index++)
            {
                var parameter = method.Parameters[index];
                var parameterType = CompileType(parameter.ParameterType).GeneratedType;
                if (parameterType.Value == IntPtr.Zero)
                    throw new InvalidOperationException();
                parameterTypes[index] = parameterType;
            }

            // Generate function global
            var methodDefinition = method.Resolve();
            bool isExternal = methodDefinition.Module.Assembly != assembly;
            var methodMangledName = Regex.Replace(method.FullName, @"(\W)", "_");
            var functionType = LLVM.FunctionType(CompileType(method.ReturnType).GeneratedType, parameterTypes, false);
            var functionGlobal = LLVM.AddFunction(module, methodMangledName, functionType);

            function = new Function(functionGlobal);
            functions.Add(method, function);

            if (isExternal)
            {
                // External weak linkage
                LLVM.SetLinkage(functionGlobal, Linkage.ExternalWeakLinkage);
            }
            else
            {
                // Need to compile
                LLVM.SetLinkage(functionGlobal, Linkage.ExternalLinkage);
                methodsToCompile.Add(new KeyValuePair<MethodDefinition, ValueRef>(methodDefinition, functionGlobal));
            }
            
            return function;
        }

        private void CompileMethodImpl(MethodDefinition method, ValueRef functionGlobal)
        {
            var numParams = method.Parameters.Count;
            var body = method.Body;

            LLVM.PositionBuilderAtEnd(builder, LLVM.AppendBasicBlockInContext(context, functionGlobal, string.Empty));

            var stack = new List<StackValue>(body.MaxStackSize);

            foreach (var instruction in body.Instructions)
            {
                switch (instruction.OpCode.Code)
                {
                    case Code.Ret:
                    {
                        if (method.ReturnType.MetadataType == MetadataType.Void)
                            LLVM.BuildRetVoid(builder);
                        else
                            throw new NotImplementedException("Opcode not implemented.");
                        break;
                    }
                    case Code.Call:
                    {
                        var targetMethodReference = (MethodReference)instruction.Operand;
                        //var targetMethodClass = CompileClass(targetMethodReference.DeclaringType);
                        var targetMethod = CompileMethod(targetMethodReference);

                        //targetMethodReference.FullName

                        // Build argument list
                        var targetNumParams = targetMethodReference.Parameters.Count;
                        var args = new ValueRef[targetNumParams];
                        for (int index = 0; index < targetNumParams; index++)
                        {
                            var parameter = targetMethodReference.Parameters[index];
                            var stackItem = stack[stack.Count - targetNumParams + index];

                            args[index] = stackItem.Value;
                        }

                        // Remove arguments from stack
                        stack.RemoveRange(stack.Count - targetNumParams, numParams);

                        // Invoke method
                        LLVM.BuildCall(builder, targetMethod.GeneratedValue, args, string.Empty);
                        
                        // Mark method as needed
                        LLVM.SetLinkage(targetMethod.GeneratedValue, Linkage.ExternalLinkage);

                        // Push return result on stack
                        if (targetMethodReference.ReturnType.MetadataType != MetadataType.Void)
                        {
                            throw new NotImplementedException();
                        }

                        break;
                    }
                    case Code.Ldstr:
                    {
                        var stringType = CompileClass(corlib.MainModule.GetType(typeof (string).FullName));
                        var operand = (string)instruction.Operand;

                        // Create string data global
                        var stringConstantData = LLVM.ConstStringInContext(context, operand, (uint)operand.Length, true);
                        var stringConstantDataGlobal = LLVM.AddGlobal(module, LLVM.TypeOf(stringConstantData), string.Empty);

                        // Cast from i8-array to i8*
                        LLVM.SetInitializer(stringConstantDataGlobal, stringConstantData);
                        var zero = LLVM.ConstInt(LLVM.Int32TypeInContext(context), 0, false);
                        stringConstantDataGlobal = LLVM.ConstInBoundsGEP(stringConstantDataGlobal, new[] { zero, zero });

                        // Create string
                        var stringConstant = LLVM.ConstNamedStruct(stringType.DataType,
                            new[] { LLVM.ConstInt(LLVM.Int32TypeInContext(context), (ulong)operand.Length, false), stringConstantDataGlobal });

                        // Push on stack
                        stack.Add(new StackValue(StackValueType.Value, stringConstant));

                        break;
                    }
                    default:
                        throw new NotImplementedException("Opcode not implemented.");
                }
            }
        }
    }
}