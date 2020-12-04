using Cooldowns.Data;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OpenMod.API.Commands;
using OpenMod.API.Ioc;
using OpenMod.API.Permissions;
using OpenMod.API.Persistence;
using OpenMod.API.Plugins;
using OpenMod.API.Prioritization;
using OpenMod.Core.Users;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Cooldowns.Services
{
    [ServiceImplementation(Lifetime = ServiceLifetime.Singleton, Priority = Priority.Lowest)]
    public class CooldownManager : ICooldownManager
    {
        private readonly IPluginAccessor<CooldownsPlugin> m_PluginAccessor;
        private readonly IPermissionChecker m_PermissionChecker;
        private readonly Dictionary<string, List<CooldownRecord>> m_Records;
        private readonly HashSet<string> m_LoadedStore;

        public CooldownManager(IPluginAccessor<CooldownsPlugin> pluginAccessor,
            IPermissionChecker permissionChecker)
        {
            m_PluginAccessor = pluginAccessor;
            m_PermissionChecker = permissionChecker;
            m_Records = new Dictionary<string, List<CooldownRecord>>();
            m_LoadedStore = new HashSet<string>();
        }

        private IConfiguration Configuration => m_PluginAccessor.Instance?.Configuration;

        private IDataStore DataStore => m_PluginAccessor.Instance?.CooldownDataStore;

        private string GetFullId(ICommandActor actor) => actor.Type + "." + actor.Id;

        private async Task<List<CooldownRecord>> GetPersistedRecords(string actorId, bool force = false)
        {
            if (Configuration == null || DataStore == null) return null;

            if (!Configuration.GetValue<bool>("reloadPersistence:enabled")) return null;

            // Have already attempted to retrieve persisted records
            if (m_LoadedStore.Contains(actorId) && !force) return null;

            m_LoadedStore.Add(actorId);

            if (!await DataStore.ExistsAsync(actorId)) return null;

            var records = await DataStore.LoadAsync<List<CooldownRecord>>(actorId);

            if (records == null) return null;

            // Keep results loaded
            if (m_Records.TryGetValue(actorId, out var existingRecords))
            {
                foreach (var record in records)
                {
                    // Already existing records will be more recent
                    if (existingRecords.All(x => x.Command != record.Command))
                    {
                        existingRecords.Add(record);
                    }
                }

                existingRecords.AddRange(records);
            }
            else
            {
                m_Records.Add(actorId, records);
            }

            return records;
        }

        private async Task SavePersistedRecord(string actorId, string command, DateTime time)
        {
            if (Configuration == null || DataStore == null) return;

            if (!Configuration.GetValue<bool>("reloadPersistence:enabled")) return;

            var records = await GetPersistedRecords(actorId, true);

            if (records != null)
            {
                var record = records.FirstOrDefault(x => x.Command == command);

                if (record == null)
                {
                    records.Add(new CooldownRecord(command, time));
                }
                else
                {
                    record.Executed = time;
                }
            }
            else
            {
                records = new List<CooldownRecord>()
                {
                    new CooldownRecord(command, time)
                };
            }

            await DataStore.SaveAsync(actorId, records);
        }

        public async Task<DateTime?> LastExecuted(ICommandActor actor, string command)
        {
            if (actor.Type == KnownActorTypes.Console) return null;

            if (await m_PermissionChecker.CheckPermissionAsync(actor,
                $"{m_PluginAccessor.Instance.OpenModComponentId}:immune") == PermissionGrantResult.Grant)
                return null;

            string actorId = GetFullId(actor);
            
            if (m_Records.TryGetValue(actorId, out List<CooldownRecord> records))
            {
                var record = records.FirstOrDefault(x => x.Command == command);

                if (record != null) return record.Executed;
            }

            return (await GetPersistedRecords(actorId))?.FirstOrDefault(x => x.Command == command)?.Executed;
        }

        public async Task RecordExecution(ICommandActor actor, string command, DateTime time)
        {
            if (actor.Type == KnownActorTypes.Console) return;

            string actorId = GetFullId(actor);

            if (m_Records.TryGetValue(actorId, out var records))
            {
                var record = records.FirstOrDefault(x => x.Command == command);

                if (record == null)
                {
                    records.Add(new CooldownRecord(command, time));
                }
                else
                {
                    record.Executed = time;
                }
            }
            else
            {
                m_Records.Add(actorId, new List<CooldownRecord>()
                {
                    new CooldownRecord(command, time)
                });
            }

            await SavePersistedRecord(actorId, command, time);
        }
    }
}
