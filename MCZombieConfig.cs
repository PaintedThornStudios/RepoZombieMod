using BepInEx.Configuration;
using UnityEngine;
using PaintedUtils;

namespace MCZombieMod
{
    public class MCZombieConfig : BaseEnemyConfig
    {
        // Configuration entries for HurtCollider
        public ConfigEntry<int> ZombiePlayerDamage { get; private set; }
        public ConfigEntry<float> ZombiePlayerDamageCooldown { get; private set; }
        public ConfigEntry<float> ZombiePlayerTumbleForce { get; private set; }
        
        // Physics object interaction settings
        public ConfigEntry<float> ZombiePhysHitForce { get; private set; }
        public ConfigEntry<float> ZombiePhysHitTorque { get; private set; }
        public ConfigEntry<bool> ZombiePhysDestroy { get; private set; }

        // Zombie Health Settings
        public ConfigEntry<int> ZombieHealth { get; private set; }

        // Zombie Spawn Settings
        public ConfigEntry<float> ZombieSpawnChance { get; private set; }

        // Zombie Speed Settings
        public ConfigEntry<float> ZombieSpeedMultiplier { get; private set; }

        // Additional zombie-specific configuration options
        public ConfigEntry<float> SpawnChance { get; private set; }
        public ConfigEntry<int> MaxHordeSpawn { get; private set; }
        public ConfigEntry<bool> HordeOnHurt { get; private set; }
        public ConfigEntry<float> FleshDropChance { get; private set; }
        public ConfigEntry<int> FleshDropAmountMin { get; private set; }
        public ConfigEntry<int> FleshDropAmountMax { get; private set; }
        public ConfigEntry<bool> CanEatFlesh { get; private set; }
        public ConfigEntry<int> FleshHealAmount { get; private set; }
        public ConfigEntry<int> GlobalHordeLimit { get; private set; }

        public MCZombieConfig(ConfigFile config)
        {
            InitializeConfig(config);
        }

        protected override void InitializeConfig(ConfigFile config)
        {
            // Initialize base configurations
            Health = config.Bind("Zombie", "Health", 200f, "Base health of zombies");
            SpeedMultiplier = config.Bind("Zombie", "Speed Multiplier", 1f, "Multiplier for zombie movement speed");
            PlayerDamage = config.Bind("Zombie", "Player Damage", 45, "Damage dealt to players");
            PlayerDamageCooldown = config.Bind("Zombie", "Player Damage Cooldown", 3f, "Cooldown between player damage");
            PlayerTumbleForce = config.Bind("Zombie", "Player Tumble Force", 5f, "Force applied to players when hit");
            PhysHitForce = config.Bind("Zombie", "Phys Hit Force", 10f, "Force applied to physics objects when hit");
            PhysHitTorque = config.Bind("Zombie", "Phys Hit Torque", 5f, "Torque applied to physics objects when hit");
            PhysDestroy = config.Bind("Zombie", "Phys Destroy", false, "Whether zombies can instantly destroy physics objects");

            // Initialize zombie-specific configurations
            SpawnChance = config.Bind("Zombie", "Horde Spawn Chance", 10f, "Chance for a zombie to spawn when hurt or killed (percentage)");
            MaxHordeSpawn = config.Bind("Zombie", "Max Number of Zombies a Zombie can spawn", 2, "Maximum number of zombies a zombie can spawn. if set to 2, each zombie can spawn 2 zombies when horde is triggered.");
            HordeOnHurt = config.Bind("Zombie", "Horde on Hurt by Anything", false, "Whether zombies can spawn a horde when hurt by ANYTHING.");
            FleshDropChance = config.Bind("Zombie", "Flesh Drop Chance", 100f, "Chance for a zombie to drop flesh on death");
            FleshDropAmountMin = config.Bind("Zombie", "Flesh Drop Amount Min", 1, "Minimum amount of flesh dropped on death");
            FleshDropAmountMax = config.Bind("Zombie", "Flesh Drop Amount Max", 3, "Maximum amount of flesh dropped on death");
            CanEatFlesh = config.Bind("Zombie", "Can Eat Flesh", true, "Whether Rotten Flesh is edible");
            FleshHealAmount = config.Bind("Zombie", "Flesh Heal Amount", 10, "Amount of health to heal the player for when eating flesh");
            GlobalHordeLimit = config.Bind("Zombie", "Global Horde Limit", 10, "Maximum total number of zombies allowed in the game at once");
        }
    }
} 