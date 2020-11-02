using OpenMod.API.Commands;
using OpenMod.API.Ioc;
using System;
using System.Threading.Tasks;

namespace Cooldowns.Services
{
    [Service]
    public interface ICooldownManager
    {
        Task<DateTime?> LastExecuted(ICommandActor actor, string command);

        Task RecordExecution(ICommandActor actor, string command, DateTime time);
    }
}
