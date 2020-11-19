using HarmonyLib;
using OpenMod.Core.Commands.Events;
using System.Threading.Tasks;
using RocketCommands = Rocket.Unturned.Commands;

namespace Cooldowns.Rocket
{
    public class RocketPatches
    {
        public delegate void ExecutingCommand(CommandExecutedEvent @event, ref Task task);
        public static event ExecutingCommand OnExecutingCommand;

        [HarmonyPatch]
        private class Patches
        {
            [HarmonyPatch(typeof(RocketCommands.CommandEventListener), "HandleEventAsync")]
            [HarmonyPostfix]
            private static void ExecutingCommand(CommandExecutedEvent @event, ref Task __result)
            {
                OnExecutingCommand?.Invoke(@event, ref __result);
            }
        }
    }
}
