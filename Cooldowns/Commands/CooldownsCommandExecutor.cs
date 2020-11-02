using Autofac;
using Cooldowns.Data;
using Cooldowns.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OpenMod.API;
using OpenMod.API.Commands;
using OpenMod.API.Eventing;
using OpenMod.API.Ioc;
using OpenMod.API.Localization;
using OpenMod.API.Permissions;
using OpenMod.API.Plugins;
using OpenMod.API.Prioritization;
using OpenMod.Core.Commands;
using OpenMod.Core.Commands.Events;
using OpenMod.Core.Helpers;
using OpenMod.Core.Permissions;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Threading.Tasks;

namespace Cooldowns.Commands
{
    [ServiceImplementation(Lifetime = ServiceLifetime.Transient, Priority = Priority.Normal)]
    public class CooldownsCommandExecutor : ICommandExecutor
    {
        private readonly IRuntime m_Runtime;
        private readonly ILifetimeScope m_LifetimeScope;
        private readonly ICommandStore m_CommandStore;
        private readonly ICommandPermissionBuilder m_CommandPermissionBuilder;
        private readonly IEventBus m_EventBus;
        private readonly ILogger<CooldownsCommandExecutor> m_Logger;
        private readonly ICooldownManager m_CooldownManager;
        private readonly IPermissionRoleStore m_PermissionRoleStore;
        private readonly IPermissionRolesDataStore m_PermissionRolesDataStore;
        private readonly IPluginAccessor<CooldownsPlugin> m_PluginAccessor;

        public CooldownsCommandExecutor(
            IRuntime runtime,
            ILifetimeScope lifetimeScope,
            ICommandStore commandStore,
            ICommandPermissionBuilder commandPermissionBuilder,
            IEventBus eventBus,
            ILogger<CooldownsCommandExecutor> logger,
            ICooldownManager cooldownManager,
            IPermissionRoleStore permissionRoleStore,
            IPermissionRolesDataStore permissionRolesDataStore,
            IPluginAccessor<CooldownsPlugin> pluginAccessor)
        {
            m_Runtime = runtime;
            m_LifetimeScope = lifetimeScope;
            m_CommandStore = commandStore;
            m_CommandPermissionBuilder = commandPermissionBuilder;
            m_EventBus = eventBus;
            m_Logger = logger;
            m_CooldownManager = cooldownManager;
            m_PermissionRoleStore = permissionRoleStore;
            m_PermissionRolesDataStore = permissionRolesDataStore;
            m_PluginAccessor = pluginAccessor;
        }

        private async Task<TimeSpan?> GetCooldownSpan(ICommandActor actor, ICommandRegistration command)
        {
            var roles = await m_PermissionRoleStore.GetRolesAsync(actor);

            if (roles == null || roles.Count == 0) return null;

            TimeSpan? span = null;
            int priority = 0;

            foreach (var role in roles)
            {
                try
                {
                    // Skip as result won't matter
                    if (span.HasValue && priority >= role.Priority) continue;

                    var data =
                        (await m_PermissionRolesDataStore.GetRoleDataAsync<List<object>>(role.Id,
                            "cooldowns"))?.OfType<Dictionary<object, object>>();

                    if (data == null) continue;
                    
                    foreach (var dict in data)
                    {
                        var currentSpan = dict.ToObject<CooldownSpan>();

                        if (currentSpan.Command.Equals(command.Id, StringComparison.OrdinalIgnoreCase))
                        {
                            span = currentSpan.GetParsedCooldown();
                            priority = role.Priority;
                        }
                    }
                }
                catch (Exception ex)
                {
                    m_Logger.LogError(ex, "Error occurred while parsing command cooldown");
                    throw;
                }
            }

            return span;
        }

        public async Task<ICommandContext> ExecuteAsync(ICommandActor actor, string[] args, string prefix)
        {
            if (args == null || args.Length == 0)
            {
                throw new Exception("Can not execute command with null or empty args");
            }

            m_Logger.LogInformation($"Actor {actor.Type}/{actor.DisplayName} ({actor.Id}) has executed command \"{string.Join(" ", args)}\".");

            var currentCommandAccessor = m_LifetimeScope.Resolve<ICurrentCommandContextAccessor>();
            var commandContextBuilder = m_LifetimeScope.Resolve<ICommandContextBuilder>();
            var stringLocalizer = m_LifetimeScope.Resolve<IOpenModStringLocalizer>();

            var commandsRegistrations = await m_CommandStore.GetCommandsAsync();
            var commandContext = commandContextBuilder.CreateContext(actor, args, prefix, commandsRegistrations);
            var commandExecutingEvent = new CommandExecutingEvent(actor, commandContext);
            await m_EventBus.EmitAsync(m_Runtime, this, commandExecutingEvent);
            
            if (commandExecutingEvent.IsCancelled)
            {
                return commandExecutingEvent.CommandContext;
            }

            try
            {
                if (commandContext.Exception != null)
                {
                    throw commandContext.Exception;
                }

                currentCommandAccessor.Context = commandContext;

                var permission = m_CommandPermissionBuilder.GetPermission(commandContext.CommandRegistration);
                var permissionChecker = m_Runtime.Host.Services.GetRequiredService<IPermissionChecker>();

                if (!string.IsNullOrWhiteSpace(permission) && await permissionChecker.CheckPermissionAsync(actor, permission) != PermissionGrantResult.Grant)
                {
                    throw new NotEnoughPermissionException(stringLocalizer, permission);
                }

                var cooldownSpan = await GetCooldownSpan(actor, commandContext.CommandRegistration);

                if (cooldownSpan.HasValue)
                {
                    var lastExecuted = await m_CooldownManager.LastExecuted(actor, commandContext.CommandRegistration.Id);

                    if (lastExecuted.HasValue)
                    {
                        var spanSinceLast = DateTime.Now - lastExecuted.Value;

                        if (spanSinceLast < cooldownSpan)
                        {
                            throw new UserFriendlyException(m_PluginAccessor.Instance.StringLocalizer["cooldown:command", new { TimeLeft = cooldownSpan - spanSinceLast }]);
                        }
                    }
                }

                Stopwatch sw = new Stopwatch();
                sw.Start();
                var command = commandContext.CommandRegistration.Instantiate(commandContext.ServiceProvider);
                await command.ExecuteAsync();
                m_Logger.LogDebug($"Command \"{string.Join(" ", args)}\" executed in {sw.ElapsedMilliseconds}ms");
                sw.Reset();

                currentCommandAccessor.Context = null;

                if (cooldownSpan.HasValue)
                {
                    await m_CooldownManager.RecordExecution(actor, commandContext.CommandRegistration.Id, DateTime.Now);
                }
            }
            catch (UserFriendlyException ex)
            {
                commandContext.Exception = ex;
            }
            catch (Exception ex)
            {
                commandContext.Exception = ex;
            }
            finally
            {
                var commandExecutedEvent = new CommandExecutedEvent(actor, commandContext);
                await m_EventBus.EmitAsync(m_Runtime, this, commandExecutedEvent);

                if (commandContext.Exception != null && !commandExecutedEvent.ExceptionHandled)
                {
                    if (commandContext.Exception is UserFriendlyException)
                    {
                        await actor.PrintMessageAsync(commandContext.Exception.Message, Color.DarkRed);
                    }
                    else
                    {
                        await actor.PrintMessageAsync("An internal error occured during the command execution.", Color.DarkRed);
                        m_Logger.LogError(commandContext.Exception, $"Exception occured on command \"{string.Join(" ", args)}\" by actor {actor.Type}/{actor.DisplayName} ({actor.Id})");
                    }
                }

                await commandContext.DisposeAsync();
            }

            return commandContext;
        }
    }
}