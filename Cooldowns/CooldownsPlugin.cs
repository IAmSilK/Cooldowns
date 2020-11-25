using Cooldowns.Data;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using OpenMod.API.Commands;
using OpenMod.API.Permissions;
using OpenMod.API.Persistence;
using OpenMod.API.Plugins;
using OpenMod.Core.Helpers;
using OpenMod.Core.Permissions;
using OpenMod.Core.Plugins;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

[assembly: PluginMetadata("OpenMod.Cooldowns", DisplayName = "Cooldowns", Author = "SilK")]
namespace Cooldowns
{
    public class CooldownsPlugin : OpenModUniversalPlugin
    {
        private readonly ILogger<CooldownsPlugin> m_Logger;
        private readonly IDataStoreFactory m_DataStoreFactory;
        private readonly IPermissionRegistry m_PermissionRegistry;
        private readonly IPermissionRoleStore m_PermissionRoleStore;
        private readonly IPermissionRolesDataStore m_PermissionRolesDataStore;

        public IDataStore CooldownDataStore { get; private set; }
        public IStringLocalizer StringLocalizer;

        public CooldownsPlugin(ILogger<CooldownsPlugin> logger,
            IDataStoreFactory dataStoreFactory,
            IPermissionRegistry permissionRegistry,
            IStringLocalizer stringLocalizer,
            IPermissionRoleStore permissionRoleStore,
            IPermissionRolesDataStore permissionRolesDataStore,
            IServiceProvider serviceProvider) : base(serviceProvider)
        {
            m_Logger = logger;
            m_DataStoreFactory = dataStoreFactory;
            m_PermissionRegistry = permissionRegistry;
            m_PermissionRoleStore = permissionRoleStore;
            m_PermissionRolesDataStore = permissionRolesDataStore;

            StringLocalizer = stringLocalizer;
        }

        protected override Task OnLoadAsync()
        {
            m_PermissionRegistry.RegisterPermission(this, "immune", "Grants immunity to cooldowns.");

            CooldownDataStore = m_DataStoreFactory.CreateDataStore(new DataStoreCreationParameters()
            {
                ComponentId = OpenModComponentId,
                WorkingDirectory = Path.Combine(WorkingDirectory, "Records")
            });

            return Task.CompletedTask;
        }

        protected override Task OnUnloadAsync()
        {
            return Task.CompletedTask;
        }

        public async Task<TimeSpan?> GetCooldownSpan(ICommandActor actor, string commandId)
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

                        if (currentSpan.Command.Equals(commandId, StringComparison.OrdinalIgnoreCase))
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
    }
}
