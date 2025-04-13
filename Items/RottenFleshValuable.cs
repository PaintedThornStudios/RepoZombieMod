using UnityEngine;
using System.Collections;
using REPOLib.Modules;
using PaintedUtils;
using PaintedThornStudios.PaintedUtils;
using Photon.Pun;
namespace MCZombieMod.Items
{
    public class RottenFleshValuable : MonoBehaviour
    {
        // Static field to store the heal amount that can be modified by config
        public static int GlobalFleshHealAmount = 25;

        /*
        [Header("Sickness Settings")]
        [Tooltip("Base duration the player stays sick (in seconds). Actual duration will be this value ± 10 seconds")]
        public float sicknessDuration = 60f;
        [Tooltip("Minimum time between random puke events while sick (in seconds)")]
        public float minPukeInterval = 10f;
        [Tooltip("Maximum time between random puke events while sick (in seconds)")]
        public float maxPukeInterval = 30f;
        [Tooltip("How long each puke event lasts (in seconds)")]
        public float pukeDuration = 3f;

        [Header("Prefab References")]
        [Tooltip("Prefab containing the SemiPuke component and its particles")]
        public GameObject pukePrefab;
        */

        [Header("Prefab References")]
        [Tooltip("Sounds to play when eating the flesh (randomly selected)")]
        [SerializeField] private AudioClip[] eatSoundClips;
        [Tooltip("Sound to play when done eating the flesh")]
        [SerializeField] private AudioClip eatBurpSound;
        [Tooltip("Particle system to play when eating the flesh")]
        public ParticleSystem eatParticles;
        [Tooltip("How long to play the eat effects before destroying the item")]
        public float eatEffectDuration = 2f;
        [Tooltip("How many times to play the eat effects")]
        public int eatEffectPlays = 9;
        [Tooltip("Time between each effect play (in seconds)")]
        public float effectPlayInterval = 0.07f;

        private PlayerAvatar playerAvatar;
        private bool canEatFlesh = true;
        private AudioSource audioSource;
        private int fleshHealAmount = GlobalFleshHealAmount;

        private void Awake()
        {
            audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.spatialBlend = 1f; // Make it 3D sound
            audioSource.playOnAwake = false;
            // Initialize the heal amount from the static value
            fleshHealAmount = GlobalFleshHealAmount;
        }

        private void PlayRandomEatSound()
        {
            if (eatSoundClips != null && eatSoundClips.Length > 0)
            {
                int randomIndex = Random.Range(0, eatSoundClips.Length);
                audioSource.PlayOneShot(eatSoundClips[randomIndex]);
                GameDirector.instance.CameraImpact.ShakeDistance(5f, 1f, 6f, base.transform.position, 0.2f);
            }
        }

        public void EatFlesh(GameObject player)
        {
            Debug.Log(player.name + " is eating flesh");
            PlayerAvatar avatar = player.GetComponent<PlayerAvatar>();
            if (avatar != null)
            {
                playerAvatar = avatar;
                /*
                PlayerSickness sickness = playerAvatar.GetComponent<PlayerSickness>();

                if (canEatFlesh && (sickness == null || !sickness.IsSick()))  // Only eat if not already sick
                {
                */
                if (canEatFlesh)
                {
                    // Play eat sound and particles
                    PlayRandomEatSound();

                    if (eatParticles != null)
                    {
                        eatParticles.Play();
                    }

                    // Start effects and sickness after delay
                    StartCoroutine(StartSicknessAfterEffects());
                }
            }
            else
            {
                Debug.LogError("EatFlesh failed: PlayerAvatar component missing on player GameObject");
            }
        }

        private IEnumerator StartSicknessAfterEffects()
        {
            // Play effects multiple times with fixed interval
            for (int i = 0; i < eatEffectPlays; i++)
            {
                PlayRandomEatSound();
                
                if (eatParticles != null)
                {
                    eatParticles.Play();
                }
                
                // Wait fixed interval before next play
                yield return new WaitForSeconds(effectPlayInterval);
            }
            
            // Wait remaining time if needed
            float remainingTime = eatEffectDuration - (eatEffectPlays * effectPlayInterval);
            if (remainingTime > 0)
            {
                yield return new WaitForSeconds(remainingTime);
            }
            
            /*
            // Get or add PlayerSickness component
            // Try to get PlayerSickness on the player avatar
            // 🔍 Find the local player's own PlayerAvatar
            PlayerAvatar localAvatar = GameDirector.instance.PlayerList.Find(p => p.isLocal);
            if (localAvatar == null)
            {
                Debug.LogError("Failed to find local PlayerAvatar to apply sickness.");
                yield break;
            }

            // 🔍 Get or add PlayerSickness on the local player
            PlayerSickness sickness = localAvatar.GetComponent<PlayerSickness>();
            if (sickness == null)
            {
                sickness = localAvatar.gameObject.AddComponent<PlayerSickness>();
                sickness.sicknessDuration = sicknessDuration;
                sickness.minPukeInterval = minPukeInterval;
                sickness.maxPukeInterval = maxPukeInterval;
                sickness.pukeDuration = pukeDuration;
                sickness.pukePrefab = pukePrefab;
            }
            sickness.AssignPlayerAvatar(localAvatar);
            sickness.BeginSickness();
            */

            audioSource.PlayOneShot(eatBurpSound);
            playerAvatar.playerHealth.HealOther(fleshHealAmount, effect: true);
            PhotonView view = GetComponent<PhotonView>();
            if (PhotonNetwork.IsConnected && view != null)
            {
                view.RPC("RPC_DestroyFlesh", RpcTarget.All);
            }
            else
            {
                Destroy(gameObject); // fallback for offline
            }


        }

        [PunRPC]
        public void RPC_DestroyFlesh()
        {
            Destroy(gameObject);
        }
    }

} 
