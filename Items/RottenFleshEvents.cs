using UnityEngine;
using REPOLib.Objects;
using Unity.VisualScripting;

namespace MCZombieMod.Items
{
    public class RottenFleshEvents : MonoBehaviour
    {
        private RottenFleshValuable rottenFlesh;
        private PhysGrabber localGrabber;

        private void Awake()
        {
            rottenFlesh = GetComponent<RottenFleshValuable>();
            localGrabber = FindObjectOfType<PhysGrabber>();
        }

        public void OnUse()
        {
            var grabbers = FindObjectsOfType<PhysGrabber>();
            foreach (var grabber in grabbers)
            {
                var avatar = grabber.GetComponent<PlayerAvatar>();
                if (avatar != null && avatar.isLocal)
                {
                    rottenFlesh.EatFlesh(grabber.gameObject);
                    return;
                }
            }

            Debug.LogError("No local player found to eat the flesh.");
        }
    }
} 
