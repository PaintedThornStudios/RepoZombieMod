using UnityEngine;
using System;
using PaintedUtils;
using MCZombieMod.AI;
using System.Collections.Generic;
using MCZombieMod.Items;

namespace MCZombieMod
{
    public class MCZombieConfigController : EnemyConfigController
    {
        private MCZombieConfig zombieConfig;
        private DropTable dropTable;
        protected override void Awake()
        {
            // Get the zombie-specific config
            zombieConfig = MCZombieMod.Instance.Config;
            config = zombieConfig; // Set the base config reference

            // Get the drop table from ItemDropper component in children
            var itemDropper = GetComponentInChildren<ItemDropper>();
            if (itemDropper != null)
            {
                dropTable = itemDropper.DropTable;
            }

            base.Awake();
        }

        protected override void HandleDeath()
        {
            // Intentionally not calling base.HandleDeath() to prevent double drops
            // The ItemDropper component already handles drops through the EnemyHealth onDeath event

            // Check if we should spawn another zombie on death
            
        }

        protected override void OnDestroy()
        {
            base.OnDestroy();
        }

        protected override void ApplyConfigurations()
        {
            if (config == null) return;

            base.ApplyConfigurations();

            // Apply zombie-specific configurations
            if (zombieConfig != null)
            {
                // Update drop table configuration
                if (dropTable != null && dropTable.drops != null && dropTable.drops.Count > 0)
                {
                    // Find the Rotten Flesh drop (assuming it's the first one)
                    var fleshDrop = dropTable.drops.Find(d => d.guaranteed);
                    if (fleshDrop != null)
                    {
                        // If it's not guaranteed, update its chance
                        if (zombieConfig.FleshDropChance.Value < 100f)
                        {
                            fleshDrop.guaranteed = false;
                            fleshDrop.dropChanceType = ItemDropper.DropChanceType.Percentage;
                            fleshDrop.chance = zombieConfig.FleshDropChance.Value;
                        }
                        fleshDrop.minQuantity = zombieConfig.FleshDropAmountMin.Value;
                        fleshDrop.maxQuantity = zombieConfig.FleshDropAmountMax.Value;
                    }
                }
                EnemyMCZombie.spawnHordeChance = zombieConfig.SpawnChance.Value;
                EnemyMCZombie.HordeOnHurt = zombieConfig.HordeOnHurt.Value;
                // Update the max horde spawn value
                var enemyMCZombie = GetComponent<EnemyMCZombie>();
                if (enemyMCZombie != null)
                {
                    enemyMCZombie.MaxHordeSpawn = zombieConfig.MaxHordeSpawn.Value;
                }
                // Update the global heal amount that will be used by all RottenFleshValuable instances
                RottenFleshValuable.GlobalFleshHealAmount = zombieConfig.FleshHealAmount.Value;
            }
        }

        public override void RefreshConfigurations()
        {
            base.RefreshConfigurations();
        }
    }
} 