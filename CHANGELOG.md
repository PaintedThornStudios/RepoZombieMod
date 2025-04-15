# Changelog

## [1.2.1] - 2025-04-15
### Global Limit
- added a global limit to zombie spawns. configurable, defaults to 10
- fixed issue with "horde on hurt by anything"

## [1.2.0] - 2025-04-15
### Animation Fix
- fixed animation sync across multiple clients
- fixed targeting across multiple clients
- fixed issues involving rotten flesh not being removed when eaten from all clients
- disabled sick effect on rotten flesh. may return in future

## [1.1.0] - 2025-04-12
### Horde Logic Update
- Adjusted Horde spawning logic to only have a chance on death or when hurt by an object the player is holding. Items do not trigger this, only valueables.
  - Has toggle option to enable all damage giving chance for horde ability. Defaulted to false.
- Reduced horde spawn chance default from 20 to 10
- Created new config option for "Max Number of Zombies a Zombie can Spawn". This means with horde enabled, this is the max TOTAL amount that the specific zombie can spawn. Spawned zombies can still spawn more zombies. Defaults to 2. 

## [1.0.0] - 2025-04-11
### Initial Release
- First stable release of the project
