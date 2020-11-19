using HarmonyLib;
using OpenMod.Core.Commands.Events;
using Rocket.Unturned.Commands;
using System.Threading.Tasks;

namespace Cooldowns.Rocket
{
    public class RocketPatches
    {
        public delegate void ExecutingCommand(CommandExecutedEvent @event, ref Task task);
        public static event ExecutingCommand OnExecutingCommand;

        [HarmonyPatch]
        private class Patches
        {
            [HarmonyPatch(typeof(CommandEventListener), "HandleEventAsync")]
            [HarmonyPostfix]
            private static void ExecutingCommand(CommandExecutedEvent @event, ref Task __result)
            {
                OnExecutingCommand?.Invoke(@event, ref __result);
            }
        }
    }
}
