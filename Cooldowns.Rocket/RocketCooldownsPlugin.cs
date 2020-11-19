using Cooldowns.Services;
using Cysharp.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using OpenMod.API.Commands;
using OpenMod.API.Plugins;
using OpenMod.Core.Commands;
using OpenMod.Core.Commands.Events;
using OpenMod.Unturned.Plugins;
using OpenMod.Unturned.Users;
using Rocket.API;
using Rocket.Core;
using Rocket.Unturned;
using Rocket.Unturned.Player;
using System;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

[assembly: PluginMetadata("Cooldowns.Rocket", DisplayName = "Rocket Cooldowns", Author = "SilK")]
namespace Cooldowns.Rocket
{
    public class RocketCooldownsPlugin : OpenModUnturnedPlugin
    {
        private readonly IConfiguration m_Configuration;
        private readonly IStringLocalizer m_StringLocalizer;
        private readonly ILogger<RocketCooldownsPlugin> m_Logger;
        private readonly IPluginAccessor<CooldownsPlugin> m_CooldownsPluginAccessor;
        private readonly IPluginAccessor<RocketUnturnedOpenModPlugin> m_RocketUnturnedOpenModPluginAccessor;
        private readonly ICooldownManager m_CooldownManager;

        private static readonly Regex s_CommandRegex = new Regex("^\\S*");

        public const string RocketCooldownsFormat = "Rocket.{0}";

        public RocketCooldownsPlugin(
            IConfiguration configuration, 
            IStringLocalizer stringLocalizer,
            ILogger<RocketCooldownsPlugin> logger,
            IPluginAccessor<CooldownsPlugin> cooldownsPluginAccessor,
            IPluginAccessor<RocketUnturnedOpenModPlugin> rocketUnturnedOpenModPluginAccessor,
            ICooldownManager cooldownManager,
            IServiceProvider serviceProvider) : base(serviceProvider)
        {
            m_Configuration = configuration;
            m_StringLocalizer = stringLocalizer;
            m_Logger = logger;
            m_CooldownsPluginAccessor = cooldownsPluginAccessor;
            m_RocketUnturnedOpenModPluginAccessor = rocketUnturnedOpenModPluginAccessor;
            m_CooldownManager = cooldownManager;
        }

        protected override async UniTask OnLoadAsync()
        {
            RocketPatches.OnExecutingCommand += OnExecutingCommand;
        }

        protected override async UniTask OnUnloadAsync()
        {
            RocketPatches.OnExecutingCommand -= OnExecutingCommand;
        }

        private void OnExecutingCommand(CommandExecutedEvent @event, ref Task task)
        {
            var original = task;

            // Rocket's attempt at executing commands is hooked rather than using
            // it's native event as it allows us to run asynchronous code

            async Task CheckCooldown()
            {
                if (!(@event.CommandContext.Exception is CommandNotFoundException) || R.Commands == null) return;

                const string rocketPrefix = "rocket:";

                var commandAlias = @event.CommandContext.CommandAlias;
                if (string.IsNullOrEmpty(commandAlias))
                {
                    return;
                }

                if (commandAlias.StartsWith(rocketPrefix))
                {
                    commandAlias = commandAlias.Replace(rocketPrefix, string.Empty);
                }

                if (@event.Actor is UnturnedUser user)
                {
                    var steamPlayer = user.Player.SteamPlayer;
                    IRocketPlayer rocketPlayer = UnturnedPlayer.FromSteamPlayer(steamPlayer);

                    IRocketCommand command = R.Commands.GetCommand(commandAlias.ToLower());

                    if (command != null && R.Permissions.HasPermission(rocketPlayer, command))
                    {
                        string commandId = string.Format(RocketCooldownsFormat, command.Name);

                        var cooldownSpan = await m_CooldownsPluginAccessor.Instance.GetCooldownSpan(@event.Actor, commandId);

                        if (cooldownSpan.HasValue)
                        {
                            var lastExecuted = await m_CooldownManager.LastExecuted(@event.Actor, commandId);

                            if (lastExecuted.HasValue)
                            {
                                var spanSinceLast = DateTime.Now - lastExecuted.Value;

                                if (spanSinceLast < cooldownSpan)
                                {
                                    @event.CommandContext.Exception = new UserFriendlyException(
                                        m_CooldownsPluginAccessor.Instance.StringLocalizer["cooldown:command",
                                            new {TimeLeft = cooldownSpan - spanSinceLast}]);

                                    @event.ExceptionHandled = false;

                                    return;
                                }
                            }

                            await m_CooldownManager.RecordExecution(@event.Actor, commandId, DateTime.Now);
                        }
                    }
                }

                await original;
            }

            task = CheckCooldown();
        }
    }
}
