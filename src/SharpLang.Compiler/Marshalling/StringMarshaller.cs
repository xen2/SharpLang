using System;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Mono.Cecil;
using Mono.Cecil.Cil;

namespace SharpLang.CompilerServices.Marshalling
{
    /// <summary>
    /// Marshaller for <see cref="string"/>.
    /// </summary>
    class StringMarshaller : Marshaller
    {
        private readonly NativeType nativeType;

        public StringMarshaller(MarshalInfo marshalInfo)
        {
            nativeType = marshalInfo != null ? marshalInfo.NativeType : NativeType.LPTStr;
        }

        public override void EmitConvertManagedToNative(MarshalCodeContext context)
        {
            var corlib = context.Assembly.MainModule.Import(typeof(void)).Resolve().Module.Assembly;

            // Note: we consider LPTStr (platform dependent) to be unicode (not valid on Win98/WinME, but well...)
            if (context.IsCleanupInlined && (nativeType == NativeType.LPTStr || nativeType == NativeType.LPWStr))
            {
                // fixed (char* c = str)
                var charPtr = new PointerType(context.Assembly.MainModule.Import(typeof(char)));
                var @string = context.Assembly.MainModule.Import(typeof(string));

                var runtimeHelpers = corlib.MainModule.GetType(typeof(RuntimeHelpers).FullName);

                var pinnedStringVariable = new VariableDefinition(new PinnedType(@string));
                var charPtrVariable = new VariableDefinition(charPtr);

                context.Method.Body.Variables.Add(charPtrVariable);
                context.Method.Body.Variables.Add(pinnedStringVariable);

                // Pin string
                context.ManagedEmitters.Peek().Emit(context.ILProcessor);
                context.ILProcessor.Emit(OpCodes.Stloc, pinnedStringVariable);
                context.ILProcessor.Emit(OpCodes.Ldloc, pinnedStringVariable);

                // Load character start
                var storeCharPtrInst = Instruction.Create(OpCodes.Stloc, charPtrVariable);
                context.ILProcessor.Emit(OpCodes.Conv_I);
                context.ILProcessor.Emit(OpCodes.Dup);
                context.ILProcessor.Emit(OpCodes.Brfalse, storeCharPtrInst);
                context.ILProcessor.Emit(OpCodes.Call, context.Assembly.MainModule.Import(runtimeHelpers.Methods.First(x => x.Name == "get_OffsetToStringData")));
                context.ILProcessor.Emit(OpCodes.Add);

                // Optional: Store it in a variable (for easier debugging?)
                context.ILProcessor.Append(storeCharPtrInst);
                context.ILProcessor.Emit(OpCodes.Ldloc, charPtrVariable);
            }
            else
            {
                string stringToHGlobalName;
                switch (nativeType)
                {
                    case NativeType.LPTStr: // Let's ignore Win98/WinME
                    case NativeType.LPWStr:
                        stringToHGlobalName = "StringToHGlobalUni";
                        break;
                    case NativeType.LPStr:
                        stringToHGlobalName = "StringToHGlobalAnsi";
                        break;
                    case NativeType.BStr:
                    case NativeType.TBStr:
                    case NativeType.ANSIBStr:
                        throw new NotImplementedException("BSTR is not supported in String Marshaller");
                    default:
                        throw new ArgumentOutOfRangeException("nativeType");
                }

                // Call StringToHGlobalUni(str)
                var marshal = corlib.MainModule.GetType(typeof(Marshal).FullName);
                var stringToHGlobal = context.Assembly.MainModule.Import(marshal.Methods.First(x => x.Name == stringToHGlobalName));

                context.ManagedEmitters.Peek().Emit(context.ILProcessor);
                context.ILProcessor.Emit(OpCodes.Call, stringToHGlobal);
            }
        }

        public override void EmitConvertNativeToManaged(MarshalCodeContext context)
        {
            var corlib = context.Assembly.MainModule.Import(typeof(void)).Resolve().Module.Assembly;
            var @string = corlib.MainModule.GetType(typeof(string).FullName);
            var stringCtor = @string.Methods.First(x => x.IsConstructor && x.Parameters.Count == 1 && x.Parameters[0].ParameterType.FullName == typeof(char*).FullName);

            context.NativeEmitters.Peek().Emit(context.ILProcessor);
            context.ILProcessor.Emit(OpCodes.Newobj, context.Assembly.MainModule.Import(stringCtor));
        }

        public override TypeReference GetNativeType(MarshalCodeContext context)
        {
            return new PointerType(context.Assembly.MainModule.Import(typeof(char)));
        }

        //public override void EmitConvertToNativeCleanup(MarshalCodeContext context)
        //{
        //    // Unpin string
        //}
    }
}