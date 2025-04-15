using UnityEngine;
using Photon.Pun;
using System.Collections;

public class PlayerSickness : MonoBehaviourPun
{
    [Header("Sickness Settings")]
    public float sicknessDuration = 60f;
    public float minPukeInterval = 10f;
    public float maxPukeInterval = 30f;
    public float pukeDuration = 3f;

    [Header("Prefab References")]
    public GameObject pukePrefab;

    private bool isSick = false;
    private bool isPuking = false;
    private float sickTimer = 0f;
    private float nextPukeTime = 0f;
    private PlayerAvatar playerAvatar;

    private void Awake()
    {
        if (playerAvatar == null)
            playerAvatar = GetComponentInParent<PlayerAvatar>();

        if (playerAvatar == null)
            Debug.LogWarning("[SICKNESS] PlayerAvatar not found on self or parent.");
    }

    private PhotonView GetAvatarView()
    {
        return playerAvatar != null ? playerAvatar.GetComponentInChildren<PhotonView>() : null;
    }

    public void AssignPlayerAvatar(PlayerAvatar avatar)
    {
        playerAvatar = avatar;
    }

    public void BeginSickness()
    {
        if (playerAvatar == null)
        {
            playerAvatar = GetComponentInParent<PlayerAvatar>();
            if (playerAvatar == null)
            {
                Debug.LogWarning("PlayerSickness: No PlayerAvatar found on self or parent!");
                return;
            }
        }

        if (playerAvatar.isLocal)
        {
            Debug.Log("[SICKNESS] Starting RPC_BeginSickness");
            playerAvatar.photonView.RPC("RPC_BeginSickness", RpcTarget.AllBuffered);
        }
        else
        {
            Debug.LogWarning("Tried to start sickness but playerAvatar is not local.");
        }
    }

    public void StartSicknessLocal()
    {
        if (isSick) return;

        isSick = true;
        sickTimer = sicknessDuration + Random.Range(-10f, 10f);
        nextPukeTime = Random.Range(2f, 5f);
        Debug.Log($"[SICKNESS] Local sickness started: {sickTimer:F1}s");
    }

    void Update()
    {
        if (!isSick) return;

        sickTimer -= Time.deltaTime;

        if (sickTimer <= 0f)
        {
            isSick = false;
            return;
        }

        if (!isPuking)
        {
            nextPukeTime -= Time.deltaTime;
            if (nextPukeTime <= 0f)
            {
                photonView.RPC("RPC_StartPukeEffect", RpcTarget.All);
            }
        }
    }


    [PunRPC]
    private void RPC_StartPukeEffect()
    {
        if (isPuking || pukePrefab == null || playerAvatar == null) return;

        isPuking = true;

        Transform attachPoint = playerAvatar.isLocal ?
            playerAvatar.localCameraTransform :
            playerAvatar.playerAvatarVisuals.transform;

        GameObject pukeEffect = Instantiate(pukePrefab, attachPoint);
        pukeEffect.transform.localPosition = new Vector3(0f, -0.2f, 0.3f);
        pukeEffect.transform.localRotation = Quaternion.identity;

        SemiPuke semiPuke = pukeEffect.GetComponentInChildren<SemiPuke>();
        if (semiPuke != null)
        {
            StartCoroutine(PukeActiveRoutine(pukeEffect, semiPuke));
        }
        else
        {
            Debug.LogError("No SemiPuke component found!");
        }
    }

    private IEnumerator PukeActiveRoutine(GameObject pukeEffect, SemiPuke semiPuke)
    {
        float timer = 0f;

        while (timer < pukeDuration)
        {
            semiPuke.PukeActive(pukeEffect.transform.position, transform.rotation);
            timer += Time.deltaTime;
            yield return null;
        }

        Destroy(pukeEffect, 1f);
        isPuking = false;

        if (isSick)
        {
            nextPukeTime = Random.Range(minPukeInterval, maxPukeInterval);
        }
    }

    public bool IsSick() => isSick;

    [PunRPC]
    public void RPC_BeginSickness()
    {
        if (!isSick)
        {
            isSick = true;
            float randomVariation = Random.Range(-10f, 10f);
            sickTimer = sicknessDuration + randomVariation;
            nextPukeTime = Random.Range(2f, 5f);
            Debug.Log($"Got sick! Will be sick for {sickTimer:F1} seconds. First puke in {nextPukeTime:F1} seconds");
        }
    }

    [PunRPC]
    public void RPC_EnsureSicknessOnTarget()
    {
        if (GetComponent<PlayerSickness>() == null)
        {
            var avatar = GetComponentInParent<PlayerAvatar>();
            if (avatar != null)
            {
                var sickness = gameObject.AddComponent<PlayerSickness>();
                sickness.AssignPlayerAvatar(avatar);
                Debug.Log("[SICKNESS] RPC-added PlayerSickness to: " + avatar.playerName);
            }
        }
    }


}
