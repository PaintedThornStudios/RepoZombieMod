using System;
using Unity.VisualScripting;
using UnityEngine;
using System.Collections.Generic;
using UnityEngine.Animations;
using PaintedThornStudios.PaintedUtils;
using Photon.Pun;
namespace MCZombieMod.AI;

public class EnemyMCZombieAnim : MonoBehaviour
{
    // Animation value types that can be displayed
    public enum AnimValueType
    {
        Trigger,
        Float,
        Int,
        Bool
    }

    // List of animation values to display
    [System.Serializable]
    public class AnimValueDisplay
    {
        public string name;
        public AnimValueType type;
        public bool enabled = true;
    }

    [Header("References")]
    public EnemyMCZombie controller;
    public Animator animator;
    public HurtCollider hurtCollider;
    public Rigidbody rigidbody;

    [Header("Particles")]
    public ParticleSystem[] hurtParticles;
    public ParticleSystem[] deathParticles;
    public ParticleSystem[] spawnParticles;

    [Header("Sounds")]
    public Sound roamSounds;
    public Sound curiousSounds;
    public Sound visionSounds;
    public Sound hurtSounds;
    public Sound deathSounds;
    public Sound attackSounds;
    public Sound lookForPlayerSounds;
    public Sound chasePlayerSounds;
    public Sound stunnedSounds;
    public Sound unstunnedSounds;
    public Sound footstepSounds;
    public Sound spawnSounds;
    public Sound playerHitSounds;

    [Header("Constraints")]
    public ParentConstraint parentConstraint;    

    // List of available animation values for debugging
    [SerializeField] private List<AnimValueDisplay> availableAnimValues = new List<AnimValueDisplay>
    {
        new AnimValueDisplay { name = "Walking", type = AnimValueType.Float },
        new AnimValueDisplay { name = "isWalking", type = AnimValueType.Bool },
        new AnimValueDisplay { name = "isStunned", type = AnimValueType.Bool },
        new AnimValueDisplay { name = "isAttacking", type = AnimValueType.Bool }
    };

    public List<AnimValueDisplay> AvailableAnimValues => availableAnimValues;

    private void Awake()
    {
        animator = GetComponent<Animator>();
        if (hurtCollider == null)
        {
            hurtCollider = GetComponentInChildren<HurtCollider>();
        }
        if (hurtCollider != null)
        {
            hurtCollider.gameObject.SetActive(false);
        }
        if (parentConstraint == null)
        {
            parentConstraint = GetComponent<ParentConstraint>();
        }
        rigidbody = transform.parent.Find("Rigidbody")?.GetComponent<Rigidbody>();
        if (rigidbody == null)
        {
            Debug.LogError("Rigidbody not found");
        }
    }
    
    private void Update()
    {
        if (controller.DeathImpulse)
        {
            controller.DeathImpulse = false;
            animator.SetTrigger("Death");
        }
    }

    public void SetDespawn()
    {
        EnemyReflectionUtil.GetEnemyParent(controller.Enemy).Despawn();
    }

    public void DeathParticlesImpulse()
    {
        foreach (var particle in deathParticles)
        {
            particle.gameObject.SetActive(true);
            particle.Play();
        }
    }

    public void HurtParticlesImpulse()
    {
        foreach (var particle in hurtParticles)
        {
            particle.gameObject.SetActive(true);
            particle.Play();
        }
    }

    public void SpawnParticlesImpulse()
    {
        foreach (var particle in spawnParticles)
        {
            particle.gameObject.SetActive(true);
            particle.Play();
        }
    }

    public void PlayRoamSound()
    {
        roamSounds.Play(controller.Enemy.CenterTransform.position);
    }

    public void PlayVisionSound()
    {
        visionSounds.Play(controller.Enemy.CenterTransform.position);
    }

    public void PlayCuriousSound()
    {
        curiousSounds.Play(controller.Enemy.CenterTransform.position);
    }

    public void PlayHurtSound()
    {
        hurtSounds.Play(controller.Enemy.CenterTransform.position);
    }

    public void PlayDeathSound()
    {
        deathSounds.Play(controller.Enemy.CenterTransform.position);
    }

    public void PlayAttackSound()
    {
        attackSounds.Play(controller.Enemy.CenterTransform.position);
    }
    
    public void AttackPlayer()
    {
        animator.SetTrigger("Attack");
    }

    public void PlayLookForPlayerSound()
    {
        lookForPlayerSounds.Play(controller.Enemy.CenterTransform.position);
    }

    public void PlayChasePlayerSound()
    {
        chasePlayerSounds.Play(controller.Enemy.CenterTransform.position);
    }

    public void PlaySpawnSound()
    {
        spawnSounds.Play(controller.Enemy.CenterTransform.position);
    }

    public void PlayPlayerHitSound()
    {
        playerHitSounds.Play(controller.Enemy.CenterTransform.position);
    }

    public void OnFootstep()
    {
        footstepSounds.Play(controller.Enemy.CenterTransform.position);
    }

    public void OnStun()
    {
        animator.SetBool("isStunned", true);
        stunnedSounds.Play(controller.Enemy.CenterTransform.position);
    }

    public void OnUnstun()
    {
        animator.SetBool("isStunned", false);
        unstunnedSounds.Play(controller.Enemy.CenterTransform.position);
    }

    public void OnSpawnComplete()
    {
        controller.OnSpawnComplete();
        parentConstraint.enabled = true;
        rigidbody.isKinematic = false;
    }

    public void OnSpawnsStart()
    {
        parentConstraint.enabled = false;
        rigidbody.isKinematic = true;
    }
}
