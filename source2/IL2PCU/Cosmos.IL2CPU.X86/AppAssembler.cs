﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Reflection;
using Cosmos.Compiler.Assembler;
using Cosmos.Compiler.Assembler.X86;
using CPU = Cosmos.Compiler.Assembler;
using CPUx86 = Cosmos.Compiler.Assembler.X86;


namespace Cosmos.IL2CPU.X86
{
    public abstract class AppAssembler: IL2CPU.AppAssembler
    {
      public const string EndOfMethodLabelNameNormal = ".END__OF__METHOD_NORMAL";
      public const string EndOfMethodLabelNameException = ".END__OF__METHOD_EXCEPTION";
      public const string EntryPointName = "__ENGINE_ENTRYPOINT__";


        public AppAssembler(byte comportNumber)
            : base(new AssemblerNasm(comportNumber))
        {
        }

        protected override void Move(string aDestLabelName, int aValue)
        {
            new CPUx86.Move
            {
                DestinationRef = CPU.ElementReference.New(aDestLabelName),
                DestinationIsIndirect = true,
                SourceValue = (uint)aValue
            };
        }

        protected override void Push(uint aValue)
        {
            new CPUx86.Push
            {
                DestinationValue = aValue
            };
        }

        protected override void Pop()
        {
            new CPUx86.Add { DestinationReg = CPUx86.Registers.ESP, SourceValue = (uint)mAssembler.Stack.Pop().Size };
        }

        protected override void Push(string aLabelName)
        {
            new CPUx86.Push
            {
                DestinationRef = CPU.ElementReference.New(aLabelName)
            };
        }

        protected override void Call(MethodBase aMethod)
        {
            new Compiler.Assembler.X86.Call
            {
                DestinationLabel = CPU.MethodInfoLabelGenerator.GenerateLabelName(aMethod)
            };
        }

        protected override void Jump(string aLabelName)
        {
            new Compiler.Assembler.X86.Jump
            {
                DestinationLabel = aLabelName
            };
        }

        protected override int GetVTableEntrySize()
        {
            return 16; // todo: retrieve from actual type info
        }

        private const string InitStringIDsLabel = "___INIT__STRINGS_TYPE_ID_S___";


        public override void EmitEntrypoint(MethodBase aEntrypoint, IEnumerable<MethodBase> aMethods)
        {
            #region Literal strings fixup code
            // at the time the datamembers for literal strings are created, the type id for string is not yet determined. 
            // for now, we fix this at runtime.
            new Label(InitStringIDsLabel);
            new Push { DestinationReg = Registers.EBP };
            new Move { DestinationReg = Registers.EBP, SourceReg = Registers.ESP };
            new Move { DestinationReg = Registers.EAX, SourceRef = ElementReference.New(ILOp.GetTypeIDLabel(typeof(String))), SourceIsIndirect = true };
            foreach (var xDataMember in mAssembler.DataMembers)
            {
                if (!xDataMember.Name.StartsWith("StringLiteral"))
                {
                    continue;
                }
                if (xDataMember.Name.EndsWith("__Contents"))
                {
                    continue;
                }
                new Move { DestinationRef = ElementReference.New(xDataMember.Name), DestinationIsIndirect = true, SourceReg = Registers.EAX };
            }
            new Pop { DestinationReg = Registers.EBP };
            new Return();
            #endregion
            new Label(EntryPointName);
            new Push { DestinationReg = Registers.EBP };
            new Move { DestinationReg = Registers.EBP, SourceReg = Registers.ESP };
            new Call { DestinationLabel = InitVMTCodeLabel };
            new Call { DestinationLabel = InitStringIDsLabel };

            foreach (var xCctor in aMethods)
            {
                if (xCctor.Name == ".cctor"
                  && xCctor.IsStatic
                  && xCctor is ConstructorInfo)
                {
                    Call(xCctor);
                }
            }
            Call(aEntrypoint);
            new Pop { DestinationReg = Registers.EBP };
            new Return();
        }

        protected override void Ldarg(MethodInfo aMethod, int aIndex)
        {
            IL.Ldarg.DoExecute(mAssembler, aMethod, (ushort)aIndex);
        }

        protected override void Call(MethodInfo aMethod, MethodInfo aTargetMethod)
        {
            var xSize = IL.Call.GetStackSizeToReservate(aTargetMethod.MethodBase);
            if (xSize > 0)
            {
                new CPUx86.Sub { DestinationReg = Registers.ESP, SourceValue = xSize };
            }
            new CPUx86.Call { DestinationLabel = ILOp.GetMethodLabel(aTargetMethod) };
        }

        protected override void Ldflda(MethodInfo aMethod, string aFieldId)
        {
            IL.Ldflda.DoExecute(mAssembler, aMethod, aMethod.MethodBase.DeclaringType, aFieldId, false);
        }

        //// todo: remove when everything goes fine
        //protected override void AfterOp(MethodInfo aMethod, ILOpCode aOpCode) {
        //  base.AfterOp(aMethod, aOpCode);
        //  new Move { DestinationReg = Registers.EAX, SourceReg = Registers.EBP };
        //  var xTotalTransitionalStackSize = (from item in Stack
        //                                     select (int)ILOp.Align((uint)item.Size, 4)).Sum();
        //  // include locals too
        //  if (aMethod.MethodBase.DeclaringType.Name == "GCImplementationImpl"
        //    && aMethod.MethodBase.Name == "AllocNewObject") {
        //    Console.Write("");
        //  }
        //  var xLocalsValue = (from item in aMethod.MethodBase.GetMethodBody().LocalVariables
        //                      select (int)ILOp.Align(ILOp.SizeOfType(item.LocalType), 4)).Sum();
        //  var xExtraSize = xLocalsValue + xTotalTransitionalStackSize;

        //  new Sub { DestinationReg = Registers.EAX, SourceValue = (uint)xExtraSize };
        //  new Compare { DestinationReg = Registers.EAX, SourceReg = Registers.ESP };
        //  var xLabel = ILOp.GetLabel(aMethod, aOpCode) + "___TEMP__STACK_CHECK";
        //  new ConditionalJump { Condition = ConditionalTestEnum.Equal, DestinationLabel = xLabel };
        //  new Xchg { DestinationReg = Registers.BX, SourceReg = Registers.BX, Size = 16 };
        //  new Halt();

        //  new Label(xLabel);

        //}

        public override uint GetSizeOfType(Type aType)
        {
            return ILOp.SizeOfType(aType);
        }
    }
}