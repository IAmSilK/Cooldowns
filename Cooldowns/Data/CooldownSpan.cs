using OpenMod.Core.Helpers;
using System;

namespace Cooldowns.Data
{
    public class CooldownSpan
    {
        public string Command { get; set; }

        public string Cooldown { get; set; }

        public TimeSpan GetParsedCooldown() => TimeSpanHelper.Parse(Cooldown);
    }
}
