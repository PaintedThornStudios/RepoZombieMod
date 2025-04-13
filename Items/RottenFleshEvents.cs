using UnityEngine;
using REPOLib.Objects;

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
            if (localGrabber != null && rottenFlesh != null)
            {
                rottenFlesh.EatFlesh(localGrabber.gameObject);
            }
        }
    }
} 
