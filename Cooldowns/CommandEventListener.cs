using Cooldowns.Services;
using OpenMod.API.Commands;
using OpenMod.API.Eventing;
using OpenMod.Core.Commands.Events;
using OpenMod.Core.Eventing;
using System;
using System.Threading.Tasks;

namespace Cooldowns
{
    public class CommandEventListener : IEventListener<CommandExecutingEvent>, IEventListener<CommandExecutedEvent>
    {
        private readonly CooldownsPlugin m_Plugin;
        private readonly ICooldownManager m_CooldownManager;

        public CommandEventListener(CooldownsPlugin plugin, ICooldownManager cooldownManager)
        {
            m_Plugin = plugin;
            m_CooldownManager = cooldownManager;
        }

        [EventListener(Priority = EventListenerPriority.High)]
        public async Task HandleEventAsync(object sender, CommandExecutingEvent @event)
        {
            string id = @event.CommandContext?.CommandRegistration?.Id;

            if (id == null) return;

            var cooldownSpan = await m_Plugin.GetCooldownSpan(@event.Actor, id);

            if (cooldownSpan.HasValue)
            {
                var lastExecuted = await m_CooldownManager.LastExecuted(@event.Actor, id);

                if (lastExecuted.HasValue)
                {
                    var spanSinceLast = DateTime.Now - lastExecuted.Value;

                    if (spanSinceLast < cooldownSpan)
                    {
                        @event.CommandContext.Exception = new UserFriendlyException(m_Plugin.StringLocalizer["cooldown:command", new { TimeLeft = cooldownSpan - spanSinceLast }]);
                    }
                }
            }
        }

        public async Task HandleEventAsync(object sender, CommandExecutedEvent @event)
        {
            if (@event.CommandContext.Exception == null || @event.ExceptionHandled)
            {
                // Command was successfully executed
                await m_CooldownManager.RecordExecution(@event.Actor, @event.CommandContext.CommandRegistration.Id, DateTime.Now);
            }
        }
    }
}
