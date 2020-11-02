using Microsoft.Extensions.Localization;
using OpenMod.API.Permissions;
using OpenMod.API.Persistence;
using OpenMod.API.Plugins;
using OpenMod.Core.Plugins;
using System;
using System.IO;
using System.Threading.Tasks;

[assembly: PluginMetadata("Cooldowns", DisplayName = "Cooldowns", Author = "SilK")]
namespace Cooldowns
{
    public class CooldownsPlugin : OpenModUniversalPlugin
    {
        private readonly IDataStoreFactory m_DataStoreFactory;
        private readonly IPermissionRegistry m_PermissionRegistry;

        public IDataStore CooldownDataStore { get; private set; }
        public IStringLocalizer StringLocalizer;

        public CooldownsPlugin(IDataStoreFactory dataStoreFactory,
            IPermissionRegistry permissionRegistry,
            IStringLocalizer stringLocalizer,
            IServiceProvider serviceProvider) : base(serviceProvider)
        {
            m_DataStoreFactory = dataStoreFactory;
            m_PermissionRegistry = permissionRegistry;

            StringLocalizer = stringLocalizer;
        }

        protected override Task OnLoadAsync()
        {
            m_PermissionRegistry.RegisterPermission(this, "immune", description: "Grants immunity to cooldowns.");

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
    }
}
