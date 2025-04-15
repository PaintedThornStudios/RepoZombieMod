using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using UnityEngine;
using System.IO;
using System.Linq;
using REPOLib;
using REPOLib.Commands;
using System;
using PaintedThornStudios.PaintedUtils;

namespace MCZombieMod
{
    [BepInDependency("PaintedThornStudios.PaintedUtils")]
    [BepInPlugin("CarsonJF.MCZombieMod", "MCZombieMod", "1.2.1")]
    [BepInDependency(REPOLib.MyPluginInfo.PLUGIN_GUID, BepInDependency.DependencyFlags.HardDependency)]
    public class MCZombieMod : BaseUnityPlugin
    {
        private static string GetModName()
        {
            var attribute = typeof(MCZombieMod).GetCustomAttributes(typeof(BepInPlugin), false)[0] as BepInPlugin;
            return attribute?.Name ?? "MCZombieMod";
        }

        private static readonly string BundleName = GetModName();
        internal static MCZombieMod Instance { get; private set; } = null!;
        internal new static ManualLogSource Logger => Instance._logger;
        private ManualLogSource _logger => base.Logger;
        internal Harmony? Harmony { get; set; }
        private AssetBundle? _assetBundle;
        private bool _hasFixedAudioMixerGroups = false;

        internal new MCZombieConfig Config { get; private set; }

        private void LoadAssetBundle()
        {
            if (_assetBundle != null) return;
            _assetBundle = AssetBundleUtil.LoadAssetBundle(this, BundleName, Logger);
        }

        private void LoadEnemiesFromResources()
        {
            if (_assetBundle == null) return;

            var enemyAssets = _assetBundle.GetAllAssetNames()
                .Where(name => name.Contains("/enemies/") && name.EndsWith(".asset"))
                .Select(name => _assetBundle.LoadAsset<EnemySetup>(name))
                .ToList();

            foreach (var enemy in enemyAssets)
            {
                if (enemy != null)
                {
                    REPOLib.Modules.Enemies.RegisterEnemy(enemy);
                }
                else
                {
                    Logger.LogWarning($"Failed to load enemy asset");
                }
            }
            if (enemyAssets.Count > 0)
            {
                Logger.LogInfo($"Successfully registered {enemyAssets.Count} enemies through REPOLib");
            }
        }

        private void LoadNetworkPrefabsFromResources()
        {
            if (_assetBundle == null) return;
            NetworkPrefabUtil.RegisterNetworkPrefabs(_assetBundle, Logger);
        }

        private void FixAllPrefabAudioMixerGroups()
        {
            if (_assetBundle == null || _hasFixedAudioMixerGroups) return;
            AssetBundleUtil.FixAudioMixerGroups(_assetBundle, Logger);
            _hasFixedAudioMixerGroups = true;
        }

        private void Awake()
        {
            Instance = this;
            Config = new MCZombieConfig(base.Config);
            
            this.gameObject.transform.parent = null;
            this.gameObject.hideFlags = HideFlags.HideAndDontSave;

            LoadAssetBundle();
            LoadEnemiesFromResources();
            LoadNetworkPrefabsFromResources();
            FixAllPrefabAudioMixerGroups();

            Harmony = new Harmony(Info.Metadata.GUID);
            Patch();

            Logger.LogInfo($"{Info.Metadata.GUID} v{Info.Metadata.Version} has loaded!");
        }

        internal void Patch()
        {
            Harmony?.PatchAll();
        }

        internal void Unpatch()
        {
            Harmony?.UnpatchSelf();
        }

        private void Update()
        {
            if (!_hasFixedAudioMixerGroups)
            {
                FixAllPrefabAudioMixerGroups();
            }
        }
    }
} 
