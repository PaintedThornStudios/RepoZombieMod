using System;
using Unity.VisualScripting;
using UnityEngine;
using System.Collections.Generic;
using UnityEngine.Animations;
using PaintedThornStudios.PaintedUtils;

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
    [SerializeField] private Sound roamSounds;
    [SerializeField] private Sound curiousSounds;
    [SerializeField] private Sound visionSounds;
    [SerializeField] private Sound hurtSounds;
    [SerializeField] private Sound deathSounds;
    [SerializeField] private Sound attackSounds;
    [SerializeField] private Sound lookForPlayerSounds;
    [SerializeField] private Sound chasePlayerSounds;
    [SerializeField] private Sound stunnedSounds;
    [SerializeField] private Sound unstunnedSounds;
    [SerializeField] private Sound footstepSounds;
    [SerializeField] private Sound spawnSounds;
    [SerializeField] private Sound playerHitSounds;


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

        if (!controller.Enemy.IsStunned())
        {
            Vector3 velocity = EnemyReflectionUtil.GetAgentVelocity(EnemyReflectionUtil.GetEnemyNavMeshAgent(controller.Enemy));
            float speed = velocity.magnitude;
            
            // More responsive walking animation
            float targetWalkingValue = (speed / 2f) + 1f;
            float currentWalkingValue = animator.GetFloat("Walking");
            float newWalkingValue = Mathf.MoveTowards(currentWalkingValue, targetWalkingValue, Time.deltaTime * 10f);
            animator.SetFloat("Walking", newWalkingValue);
            
            // Only set walking state if actually moving
            bool isMoving = speed > 0.1f;
            animator.SetBool("isWalking", isMoving);

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

    // Called by animation events when foot hits ground
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
