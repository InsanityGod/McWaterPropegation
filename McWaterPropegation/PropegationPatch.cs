using HarmonyLib;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;

namespace McWaterPropegation;

[HarmonyPatch]
public static class PropegationPatch
{
    [HarmonyTranspiler]
    [HarmonyPatch(typeof(BlockBehaviorFiniteSpreadingLiquid), "SpreadAndUpdateLiquidLevels")]
    public static IEnumerable<CodeInstruction> SpreadAndUpdateLiquidLevels_Transpiler(IEnumerable<CodeInstruction> instructions, ILGenerator generator)
    {
        var matcher = new CodeMatcher(instructions, generator);

        matcher.Start()
            .MatchEndForward(
                new CodeMatch(opcode => opcode.operand is MethodInfo method && typeof(Block).IsAssignableFrom(method.ReturnParameter.ParameterType)),
                CodeMatch.StoresLocal()
            );
        
        var localIndex = matcher.Instruction.LocalIndex();
        matcher.DeclareLocal(typeof(int), out var requiredSourceCountLocal);
        

        matcher.InsertAfter(
            CodeInstruction.LoadLocal(localIndex),
            new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(PropegationPatch), nameof(GetRequiredSourceCount))),
            CodeInstruction.StoreLocal(requiredSourceCountLocal.LocalIndex)
        );

        var loadRequiredSourceCountLocal = CodeInstruction.LoadLocal(requiredSourceCountLocal.LocalIndex);
        while (matcher.IsValid)
        {
            matcher.MatchStartForward(
                CodeMatch.LoadsConstant(3)
            );

            if(!matcher.IsValid) break;
            matcher.Opcode = loadRequiredSourceCountLocal.opcode;
            matcher.Operand = loadRequiredSourceCountLocal.operand;
        }

        //Base game half uses the BulkBlockAccesor and half doesn't for some reason, that however causes issues with mass propegation so fixing that while at it.
        matcher.Start();
        var accesor = AccessTools.PropertyGetter(typeof(IWorldAccessor), nameof(IWorldAccessor.BlockAccessor));
        var bulkAccesor = AccessTools.PropertyGetter(typeof(IWorldAccessor), nameof(IWorldAccessor.BulkBlockAccessor));
        while (matcher.IsValid)
        {
            matcher.MatchStartForward(
                CodeMatch.Calls(accesor)
            );

            if(!matcher.IsValid) break;
            matcher.Operand = bulkAccesor;
        }

        return matcher.InstructionEnumeration();
    }

    public static int GetRequiredSourceCount(Block block) => block?.BlockMaterial == EnumBlockMaterial.Water ? 2 : 3;

    [HarmonyTranspiler]
    [HarmonyPatch(typeof(BlockBehaviorFiniteSpreadingLiquid), nameof(BlockBehaviorFiniteSpreadingLiquid.CanSpreadIntoBlock), typeof(Block), typeof(Block), typeof(BlockPos), typeof(BlockPos), typeof(BlockFacing), typeof(IWorldAccessor))]
    public static IEnumerable<CodeInstruction> CanSpreadIntoBlock_Transpiler(IEnumerable<CodeInstruction> instructions, ILGenerator generator)
    {
        var matcher = new CodeMatcher(instructions, generator);

        var isSourceBlockMethod = AccessTools.Method(typeof(BlockBehaviorFiniteSpreadingLiquid), "IsLiquidSourceBlock");

        matcher.Start()
            .MatchEndForward(
                CodeMatch.Calls(isSourceBlockMethod),
                CodeMatch.WithOpcodes([OpCodes.Brtrue, OpCodes.Brtrue_S])
            );

        matcher.DefineLabel(out var originalPath);
        matcher.InsertAfterAndAdvance(
            CodeInstruction.LoadArgument(0),
            CodeInstruction.LoadLocal(0),
            new CodeInstruction(OpCodes.Call, isSourceBlockMethod),
            new CodeInstruction(OpCodes.Brfalse_S, originalPath),
            new CodeInstruction(OpCodes.Ldc_I4_0),
            new CodeInstruction(OpCodes.Ret)
        );
        matcher.Advance();
        matcher.Labels.Add(originalPath);

        return matcher.InstructionEnumeration();
    }
}
