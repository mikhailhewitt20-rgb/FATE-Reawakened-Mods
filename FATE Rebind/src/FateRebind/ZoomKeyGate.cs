using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
namespace ZombieGhost.Rebind
{
 internal static class ZoomKeyGate
 {
  // The game's camera Update additionally requires physical W/S after reading
  // CameraZoom. Replace only those two calls so the caller can route held zoom
  // consistently; retain instruction labels and exception boundaries.
  internal static IEnumerable<CodeInstruction> Rewrite(IEnumerable<CodeInstruction> instructions,MethodInfo original,MethodInfo replacement)
  {
   var code=instructions.Select(i=>new CodeInstruction(i)).ToList();int count=0;
   for(int i=1;i<code.Count;i++)
   {
    if(!code[i].Calls(original))continue;
    var key=code[i-1];
    if(key.opcode!=OpCodes.Ldc_I4 && key.opcode!=OpCodes.Ldc_I4_S)continue;
    int value=Convert.ToInt32(key.operand);
    if(value!=119 && value!=115)continue;
    code[i].opcode=OpCodes.Call;code[i].operand=replacement;count++;
   }
   if(count!=2)throw new InvalidOperationException("Camera keyboard zoom gate changed; expected exactly the two W/S checks, found "+count);
   return code;
  }
 }
}
