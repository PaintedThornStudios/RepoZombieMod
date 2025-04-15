using UnityEngine;
using System.Collections;
using Photon.Pun;
using REPOLib.Modules;
using PaintedUtils;
using PaintedThornStudios.PaintedUtils;

namespace MCZombieMod.Items
{
    public class RottenFleshValuable : MonoBehaviour
    {
        public static int GlobalFleshHealAmount = 25;

        [Header("Prefab References")]
        [SerializeField] private AudioClip[] eatSoundClips;
        [SerializeField] private AudioClip eatBurpSound;
        public ParticleSystem eatParticles;
        public float eatEffectDuration = 2f;
        public int eatEffectPlays = 9;
        public float effectPlayInterval = 0.07f;

        private PlayerAvatar playerAvatar;
        private AudioSource audioSource;
        private int fleshHealAmount = GlobalFleshHealAmount;

        private void Awake()
        {
            audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.spatialBlend = 1f;
            audioSource.playOnAwake = false;
            fleshHealAmount = GlobalFleshHealAmount;
        }

        private void PlayRandomEatSound()
        {
            if (eatSoundClips != null && eatSoundClips.Length > 0)
            {
                int randomIndex = Random.Range(0, eatSoundClips.Length);
                audioSource.PlayOneShot(eatSoundClips[randomIndex]);
                GameDirector.instance.CameraImpact.ShakeDistance(5f, 1f, 6f, transform.position, 0.2f);
            }
        }

        public void EatFlesh(GameObject player)
        {
            var avatar = player.GetComponent<PlayerAvatar>();
            if (avatar == null) 
            {
                Debug.LogError("EatFlesh failed: PlayerAvatar component missing.");
                return;
            }

            if (!avatar.isLocal) {
                Debug.LogError("EatFlesh failed: PlayerAvatar is not local.");
                return;
            }

            Debug.Log("Eating flesh for " + avatar.playerName);

            playerAvatar = avatar;
            StartCoroutine(StartSicknessAfterEffects());
        }

        private IEnumerator StartSicknessAfterEffects()
        {
            for (int i = 0; i < eatEffectPlays; i++)
            {
                PlayRandomEatSound();
                if (eatParticles != null) eatParticles.Play();
                yield return new WaitForSeconds(effectPlayInterval);
            }

            float remainingTime = eatEffectDuration - (eatEffectPlays * effectPlayInterval);
            if (remainingTime > 0)
                yield return new WaitForSeconds(remainingTime);

            audioSource.PlayOneShot(eatBurpSound);

            if (playerAvatar != null && playerAvatar.isLocal)
            {
                Debug.Log("Healed local player " + playerAvatar.playerName + " for " + fleshHealAmount + " health");
                playerAvatar.playerHealth.HealOther(fleshHealAmount, true); // HealOther is better for visual effect
                playerAvatar.HealedOther();
                
                PhotonView pview = GetComponent<PhotonView>();
                if (PhotonNetwork.IsConnected && pview != null && pview.IsMine)
                {
                    pview.RPC(nameof(RPC_DestroyFlesh), RpcTarget.All);
                }
                else
                {
                    Destroy(gameObject);
                }
            }

            // ✅ Safety check: If we're holding this item, release it before destroying.
            if (Inventory.instance != null &&
                Inventory.instance.physGrabber != null &&
                Inventory.instance.physGrabber.grabbedPhysGrabObject != null &&
                Inventory.instance.physGrabber.grabbedPhysGrabObject.gameObject == this.gameObject)
            {
                Inventory.instance.physGrabber.ReleaseObject();
            }

            PhotonView view = GetComponent<PhotonView>();
            if (PhotonNetwork.IsConnected && view != null)
            {
                view.RPC(nameof(RPC_DestroyFlesh), RpcTarget.All);
            }
            else
            {
                Destroy(gameObject);
            }
        }

        [PunRPC]
        public void RPC_DestroyFlesh()
        {
            Destroy(gameObject);
        }
    }
}
