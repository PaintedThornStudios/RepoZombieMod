using Photon.Pun;
using UnityEngine;
using UnityEngine.AI;
using System;
using Random = UnityEngine.Random;
using System.Reflection;
using SingularityGroup.HotReload;
using MCZombieMod;
using PaintedThornStudios.PaintedUtils;

namespace MCZombieMod.AI;

public class EnemyMCZombie : MonoBehaviour
{
    public enum State {
        Spawn,
        Idle,
        Roam,
        Investigate,
        Chase,
        LookForPlayer,
        Attack,
        Leave,
        Stun
    }

    private Enemy _enemy;
    public Enemy Enemy => _enemy;
    private PhotonView _photonView;
    public PhotonView PhotonView => _photonView;
    public PlayerAvatar _targetPlayer;

    private EnemyNavMeshAgent _navMeshAgent => EnemyReflectionUtil.GetEnemyNavMeshAgent(_enemy);
    private EnemyRigidbody _rigidbody => EnemyReflectionUtil.GetEnemyRigidbody(_enemy);
    private EnemyParent _enemyParent => EnemyReflectionUtil.GetEnemyParent(_enemy);
    private EnemyVision _vision => EnemyReflectionUtil.GetEnemyVision(_enemy);
    private EnemyStateInvestigate _investigate => EnemyReflectionUtil.GetEnemyStateInvestigate(_enemy);

    private bool _stateImpulse;
    private bool _deathImpulse;
    private bool _attackImpulse;
    public bool DeathImpulse {
        get => _deathImpulse;
        set => _deathImpulse = value;
    }

    [Header("State")]
    [SerializeField] public State currentState;
    [SerializeField] public float stateTimer;
    public static float spawnHordeChance = 0.2f; // Chance to spawn another enemy
    public float RandomSpawnChance { get; set; }
    private Quaternion _horizontalRotationTarget = Quaternion.identity;
    private Vector3 _agentDestination;
    private Vector3 _targetPosition;
    private bool _hurtImpulse;
    private float hurtLerp;
    private int _hurtAmount;
    private Material _hurtableMaterial;
    private float _pitCheckTimer;
    private bool _pitCheck;
    private float _ambientTimer;
    private bool _ambientImpulse;
    private bool _attackWindowOpen = false;
    private float _attackWindowTimer = 0f;

    private Vector3 _lastSeenPlayerPosition;
    private float _attackCooldown;
    private float _lookForPlayerTimer;
    private int _unsuccessfulAttackCount;  // Counter for unsuccessful attacks
    private float _interestTimer;  // Timer for losing interest
    private float _maxInterestTime = 30f;  // Maximum time to stay interested in a player
    private float _hurtInterestLoss = 0.5f;  // How much interest is lost when hurt (0-1)
    private float _currentInterest = 1f;  // Current interest level (0-1)
    public float CurrentInterest => _currentInterest;  // Public property to access current interest

    [Header("Animation")]
    [SerializeField] private EnemyMCZombieAnim animator;
    [SerializeField] private SkinnedMeshRenderer _renderer;
    [SerializeField] private AnimationCurve hurtCurve;

    [Header("Rotation and LookAt")]
    public SpringQuaternion horizontalRotationSpring;
    public SpringQuaternion headLookAtSpring;
    public Transform headLookAtTarget;
    public Transform headLookAtSource;
    
    private float _spawnCooldown = 0f;
    private const float SPAWN_COOLDOWN_TIME = 5f;
    private int _spawnedZombies = 0;  // Counter for spawned zombies
    private int _maxHordeSpawn = 2;  // Maximum number of zombies that can be spawned
    private const int DEFAULT_MAX_SPAWNED_ZOMBIES = 2;  // Default maximum number of zombies that can be spawned
    private int MAX_SPAWNED_ZOMBIES => _maxHordeSpawn;  // Current maximum based on config
    private static bool _hordeOnHurt = false;
    public static bool HordeOnHurt { get => _hordeOnHurt; set => _hordeOnHurt = value; }
    public int MaxHordeSpawn { get => _maxHordeSpawn; set => _maxHordeSpawn = value; }

    private void Awake()
    {
        _enemy = GetComponent<Enemy>();
        _photonView = GetComponent<PhotonView>();
        _hurtAmount = Shader.PropertyToID("_ColorOverlayAmount");

        if (_renderer != null) 
        {
            _hurtableMaterial = _renderer.material;
        }

        hurtCurve = AssetManager.instance.animationCurveImpact;

        // Configure NavMeshAgent to prevent jumping on players
        if (_navMeshAgent != null)
        {
            Type agentType = _navMeshAgent.GetType();
            
            // Set radius
            var radiusField = agentType.GetField("Radius", BindingFlags.NonPublic | BindingFlags.Instance);
            if (radiusField != null)
            {
                radiusField.SetValue(_navMeshAgent, 0.5f);
            }

            // Set height
            var heightField = agentType.GetField("Height", BindingFlags.NonPublic | BindingFlags.Instance);
            if (heightField != null)
            {
                heightField.SetValue(_navMeshAgent, 2f);
            }

            // Set obstacle avoidance type
            var avoidanceField = agentType.GetField("ObstacleAvoidanceType", BindingFlags.NonPublic | BindingFlags.Instance);
            if (avoidanceField != null)
            {
                avoidanceField.SetValue(_navMeshAgent, ObstacleAvoidanceType.HighQualityObstacleAvoidance);
            }

            // Set avoidance priority
            var priorityField = agentType.GetField("AvoidancePriority", BindingFlags.NonPublic | BindingFlags.Instance);
            if (priorityField != null)
            {
                priorityField.SetValue(_navMeshAgent, Random.Range(0, 100));
            }
        }
    }

    private void OnDestroy()
    {

    }

    public void OnPlayerHit()
    {
        _unsuccessfulAttackCount = 0;
        _attackWindowOpen = false;
        if (SemiFunc.IsMultiplayer())
        {
            _photonView.RPC(nameof(RPC_PlayPlayerHit), RpcTarget.All);
        }
        else
        {
            animator.PlayPlayerHitSound();
        }
    }

    private void Update()
    {
        if ((!GameManager.Multiplayer() || PhotonNetwork.IsMasterClient) && LevelGenerator.Instance.Generated) 
        {
            if (!_enemy.IsStunned())
            {
                if (_enemy.IsStunned())
                {
                    UpdateState(State.Stun);
                }

                switch (currentState)
                {
                    case State.Spawn: StateSpawn(); break;
                    case State.Idle: StateIdle(); break;
                    case State.Roam: StateRoam(); break;
                    case State.Investigate: StateInvestigate(); break;
                    case State.Chase: StateChase(); break;
                    case State.LookForPlayer: StateLookForPlayer(); break;
                    case State.Attack: StateAttack(); break;
                    case State.Leave: StateLeave(); break;
                    case State.Stun: StateStun(); break;
                    default: throw new ArgumentOutOfRangeException();
                }

                RotationLogic();
                TargetingLogic();
            }
            HurtEffect();

            if (_ambientTimer > 0f)
            {
                _ambientTimer -= Time.deltaTime;
            }
            else
            {
                _ambientImpulse = true;
            }

            if (_attackCooldown > 0f)
            {
                _attackCooldown -= Time.deltaTime;
            }

            if (_spawnCooldown > 0f)
            {
                _spawnCooldown -= Time.deltaTime;
            }

            // Handle animation parameter syncing
            if (!_enemy.IsStunned())
            {
                Vector3 velocity = EnemyReflectionUtil.GetAgentVelocity(_navMeshAgent);
                float speed = velocity.magnitude;

                float targetWalkingValue = (speed / 2f) + 1f;
                float currentWalkingValue = animator.animator.GetFloat("Walking");
                float newWalkingValue = Mathf.MoveTowards(currentWalkingValue, targetWalkingValue, Time.deltaTime * 10f);

                if (Mathf.Abs(currentWalkingValue - newWalkingValue) > 0.01f)
                {
                    if (SemiFunc.IsMultiplayer())
                    {
                        _photonView.RPC(nameof(RPC_SetFloat), RpcTarget.All, "Walking", newWalkingValue);
                    }
                    else
                    {
                        animator.animator.SetFloat("Walking", newWalkingValue);
                    }
                }

                bool isMoving = speed > 0.1f;
                if (SemiFunc.IsMultiplayer())
                {
                    _photonView.RPC(nameof(RPC_SetBool), RpcTarget.All, "isWalking", isMoving);
                }
                else
                {
                    animator.animator.SetBool("isWalking", isMoving);
                }
            }
        }

        // Synchronize position with other clients
        if (GameManager.Multiplayer() && !PhotonNetwork.IsMasterClient)
        {
            _navMeshAgent.Warp(_rigidbody.transform.position);
        }
    }

    private void LateUpdate()
    {
        HeadLookAtLogic();
    }

    private void UpdateState(State newState) {
        // Debug.Log("State -> " + newState);
        if (newState != currentState) {
            currentState = newState;
            stateTimer = 0f;
            _stateImpulse = true;

            if (GameManager.Multiplayer()) {
                _photonView.RPC(nameof(UpdateStateRPC), RpcTarget.All, currentState);
            } else {
                UpdateStateRPC(currentState);
            }
        }        
    }

    [PunRPC]
    private void UpdateStateRPC(State _state)
    {
        currentState = _state;
        // Debug.Log("State -> " + currentState);
    }

    [PunRPC]
    private void UpdatePlayerTargetRPC(int viewID)
    {
        foreach (PlayerAvatar item in SemiFunc.PlayerGetList())
        {
            if (item.photonView.ViewID == viewID)
            {
                _targetPlayer = item;
                break;
            }
        }
    }


    private void TargetingLogic()
    {
        if ((currentState == State.Chase) && _targetPlayer)
        {
            Vector3 vector = _targetPlayer.transform.position + _targetPlayer.transform.forward * 1.5f;
            if (_pitCheckTimer <= 0f)
            {
                _pitCheckTimer = 0.1f;
                _pitCheck = !Physics.Raycast(vector + Vector3.up, Vector3.down, 4f, LayerMask.GetMask("Default"));
            }
            else
            {
                _pitCheckTimer -= Time.deltaTime;
            }

            if (_pitCheck)
            {
                vector = _targetPlayer.transform.position;
            }
            
            _targetPosition = Vector3.Lerp(_targetPosition, vector, 20f * Time.deltaTime);
        }
    }

    private void RotationLogic()
    {
        if (EnemyReflectionUtil.GetAgentVelocity(_navMeshAgent).normalized.magnitude > 0.01f)
        {
            _horizontalRotationTarget = Quaternion.LookRotation(EnemyReflectionUtil.GetAgentVelocity(_navMeshAgent).normalized);
            _horizontalRotationTarget.eulerAngles = new Vector3(0f, _horizontalRotationTarget.eulerAngles.y, 0f);
        }
        if (currentState == State.Spawn || currentState == State.Idle || currentState == State.Roam || currentState == State.Investigate || currentState == State.Leave)
        {
            horizontalRotationSpring.speed = 5f;
            horizontalRotationSpring.damping = 0.7f;
        }
        else
        {
            horizontalRotationSpring.speed = 10f;
            horizontalRotationSpring.damping = 0.8f;
        }
        base.transform.rotation = SemiFunc.SpringQuaternionGet(horizontalRotationSpring, _horizontalRotationTarget);
    }

    private void HeadLookAtLogic() {
        if (currentState == State.Chase && !_enemy.IsStunned() && _targetPlayer && !EnemyReflectionUtil.IsPlayerDisabled(_targetPlayer))
        {
            Vector3 direction = _targetPlayer.PlayerVisionTarget.VisionTransform.position - headLookAtTarget.position;
            direction = SemiFunc.ClampDirection(direction, headLookAtTarget.forward, 60f);
            headLookAtSource.rotation = SemiFunc.SpringQuaternionGet(headLookAtSpring, Quaternion.LookRotation(direction));
        }
        else
        {
            headLookAtSource.rotation = SemiFunc.SpringQuaternionGet(headLookAtSpring, headLookAtTarget.rotation);
        }
    }

    private void HurtEffect() {
        if (_hurtImpulse)
        {
            hurtLerp += 2.5f * Time.deltaTime;
            hurtLerp = Mathf.Clamp01(hurtLerp);

            if (_hurtableMaterial != null)
            {
                _hurtableMaterial.SetFloat(_hurtAmount, hurtCurve.Evaluate(hurtLerp));
            }

            if (hurtLerp > 1f)
            {
                _hurtImpulse = false;
                if (_hurtableMaterial != null)
                {
                    _hurtableMaterial.SetFloat(_hurtAmount, 0f);
                }
            }
        }
    }
    
    /*================================================================
     *
     * States
     *
     *===============================================================*/

    private void StateSpawn() {
        if (_stateImpulse) {
            _navMeshAgent.Warp(_rigidbody.transform.position);
            _navMeshAgent.ResetPath();
            _stateImpulse = false;
        }
    }

    // Mob is just standing around deciding what to do next
    private void StateIdle() {
        if (_stateImpulse) {
            _stateImpulse = false;
            stateTimer = Random.Range(4f, 8f);
            _navMeshAgent.Warp(_rigidbody.transform.position);
            _navMeshAgent.ResetPath();
        }
        
        if (!SemiFunc.EnemySpawnIdlePause()) {
            stateTimer -= Time.deltaTime;
            
            if (stateTimer <= 0f) {
                UpdateState(State.Roam);
            }
            
            if (SemiFunc.EnemyForceLeave(_enemy)) {
                UpdateState(State.Leave);
            } 
        }
    }

    // Mob is just wandering around looking for someone
    private void StateRoam() {
        if (_stateImpulse) {
            bool foundTarget = false;

            _ambientTimer = Random.Range(5f, 15f);
            stateTimer = Random.Range(4f, 8f);
            LevelPoint point = SemiFunc.LevelPointGet(base.transform.position, 10f, 25f);

            if (!point)
            {
                point = SemiFunc.LevelPointGet(base.transform.position, 0f, 999f);
            }

            if (point &&
                NavMesh.SamplePosition(point.transform.position + Random.insideUnitSphere * 3f, out var hit, 5f, -1) &&
                Physics.Raycast(hit.position, Vector3.down, 5f, LayerMask.GetMask("Default")))
            {
                _agentDestination = hit.position;
                foundTarget = true;
            }

            if (!foundTarget) {
                return;
            }

            EnemyReflectionUtil.SetNotMovingTimer(_rigidbody, 0f);
            _stateImpulse = false;
        } else {
            _navMeshAgent.SetDestination(_agentDestination);

            if (EnemyReflectionUtil.GetNotMovingTimer(_rigidbody) > 2f) {
                UpdateState(State.Idle);
            }

            if (stateTimer <= 0f) {
                UpdateState(State.Idle);
            }
        }

        if (SemiFunc.EnemyForceLeave(_enemy)) {
            UpdateState(State.Leave);
        }

        if (_ambientImpulse) {
            _ambientImpulse = false;
            if (SemiFunc.IsMultiplayer())
            {
                _photonView.RPC(nameof(RPC_PlayRoam), RpcTarget.All);
            }
            else
            {
                animator.PlayRoamSound();
            }
            _ambientTimer = Random.Range(15f, 20f);
        }
    }

    // Mob noticed something and is moving to investigate
    private void StateInvestigate() {
        if (_stateImpulse) { // first time state is called
            _stateImpulse = false;
            stateTimer = Random.Range(24f, 30f);
            _ambientTimer = Random.Range(5f, 15f);
        }

        _navMeshAgent.SetDestination(_targetPosition);
        stateTimer -= Time.deltaTime;
        _vision.StandOverride(0.25f);

        if (_targetPlayer && !EnemyReflectionUtil.IsPlayerDisabled(_targetPlayer)) {
            float distanceToPlayer = Vector3.Distance(_rigidbody.transform.position, _targetPlayer.transform.position);
            
            if (distanceToPlayer <= 5f) {
                _lastSeenPlayerPosition = _targetPlayer.transform.position;
                UpdateState(State.Chase);
                return;
            }
        }

        if (stateTimer <= 0f) {
            UpdateState(State.Idle);
            return;
        }
        
        if (_ambientImpulse) {
            _ambientImpulse = false;
            if (SemiFunc.IsMultiplayer())
            {
                _photonView.RPC(nameof(RPC_PlayCurious), RpcTarget.All);
            }
            else
            {
                animator.PlayCuriousSound();
            }
            _ambientTimer = Random.Range(15f, 20f);
        }

        if (_navMeshAgent.CanReach(_targetPosition, 1f) &&
            Vector3.Distance(_rigidbody.transform.position, _navMeshAgent.GetPoint()) < 2f &&
            !NavMesh.SamplePosition(_targetPosition, out var _, 0.5f, -1))
        {
            UpdateState(State.Roam);
            return;
        }        
    }

    // Mob is aggro'd on a player and chasing them
    private void StateChase() {
        if (_stateImpulse) {
            _stateImpulse = false;
            _ambientTimer = Random.Range(5f, 15f);
            _interestTimer = _maxInterestTime;
        }

        if (!_targetPlayer || EnemyReflectionUtil.IsPlayerDisabled(_targetPlayer))
        {
            _lastSeenPlayerPosition = _targetPlayer ? _targetPlayer.transform.position : _lastSeenPlayerPosition;
            if (SemiFunc.IsMultiplayer())
            {
                _photonView.RPC(nameof(RPC_PlayLookForPlayer), RpcTarget.All);
            }
            else
            {
                animator.PlayLookForPlayerSound();
            }
            UpdateState(State.LookForPlayer);
            return;
        }

        // Update interest timer
        _interestTimer -= Time.deltaTime;
        if (_interestTimer <= 0f) {
            _currentInterest -= 0.1f * Time.deltaTime;
            if (_currentInterest <= 0f) {
                _lastSeenPlayerPosition = _targetPlayer ? _targetPlayer.transform.position : _lastSeenPlayerPosition;
                UpdateState(State.LookForPlayer);
                return;
            }
        }

        float distanceToPlayer = Vector3.Distance(_rigidbody.transform.position, _targetPlayer.transform.position);
        if (distanceToPlayer <= 2f && _attackCooldown <= 0f) {
            _attackImpulse = true;
            _attackCooldown = 4f;
            UpdateState(State.Attack);
            return;
        }

        // Vertical overlap/stuck check
        if (_targetPlayer && Vector3.Distance(_targetPlayer.transform.position, transform.position) < 1.5f)
        {
            float heightDifference = transform.position.y - _targetPlayer.transform.position.y;
            if (heightDifference > 1f && EnemyReflectionUtil.GetNotMovingTimer(_rigidbody) > 1f)
            {
                _navMeshAgent.Warp(transform.position + Vector3.back * 2f);
                _lastSeenPlayerPosition = _targetPlayer ? _targetPlayer.transform.position : _lastSeenPlayerPosition;
                UpdateState(State.LookForPlayer);
                OnStun();
                return;
            }
        }

        if (!hasTargetLineOfSight()) {
            _lastSeenPlayerPosition = _targetPlayer.transform.position;
            UpdateState(State.LookForPlayer);
            return;
        }

        // Reset interest timer when we have line of sight
        _interestTimer = _maxInterestTime;

        // Lose interest if crawling or tumbling
        if (distanceToPlayer > 6) {
            if (_targetPlayer.isCrawling || _targetPlayer.isTumbling) {
                _currentInterest -= Time.deltaTime * 0.2f;
                if (_currentInterest <= 0f)
                {
                    _lastSeenPlayerPosition =
                        _targetPlayer ? _targetPlayer.transform.position : _lastSeenPlayerPosition;
                    UpdateState(State.LookForPlayer);
                    return;
                }
            }
        }

        _targetPosition = _targetPlayer.transform.position;
        _navMeshAgent.SetDestination(_targetPosition);

        Vector3 origin = _rigidbody.transform.position + Vector3.up * 1.5f;
        Vector3 direction = (_targetPlayer.transform.position + Vector3.up * 1.0f) - origin;
        
        _horizontalRotationTarget = Quaternion.LookRotation(direction);
        _horizontalRotationTarget.eulerAngles = new Vector3(0f, _horizontalRotationTarget.eulerAngles.y, 0f);
        base.transform.rotation = SemiFunc.SpringQuaternionGet(horizontalRotationSpring, _horizontalRotationTarget);

        if (_ambientImpulse) {
            _ambientImpulse = false;
            if (SemiFunc.IsMultiplayer())
            {
                _photonView.RPC(nameof(RPC_PlayChasePlayer), RpcTarget.All);
            }
            else
            {
                animator.PlayChasePlayerSound();
            }
            _ambientTimer = Random.Range(15f, 20f);
        }
    }

    // Mob recently lost a player and is looking around for a new one
    private void StateLookForPlayer() {
        if (_stateImpulse) {
            _stateImpulse = false;
            _interestTimer = Random.Range(8f, 12f);
            _ambientTimer = Random.Range(5f, 15f);
            _lookForPlayerTimer = Random.Range(2f, 5f);
        }

        _interestTimer -= Time.deltaTime;

        if (_targetPlayer && !EnemyReflectionUtil.IsPlayerDisabled(_targetPlayer)) {
            bool seesPlayer = true;
            
            float distanceToPlayer = Vector3.Distance(_rigidbody.transform.position, _targetPlayer.transform.position);
            if (distanceToPlayer > 8f || !hasTargetLineOfSight() || !NavMesh.SamplePosition(_targetPosition, out var _, 0.5f, -1)) {
                seesPlayer = false;
            }

            if (seesPlayer) {
                _lastSeenPlayerPosition = _targetPlayer.transform.position;
                UpdateState(State.Chase);
                return;
            }
        }

        if (_interestTimer <= 0f) {
            UpdateState(State.Leave);
            return;
        }

        if (_lookForPlayerTimer <= 0f) {
            _targetPosition = _lastSeenPlayerPosition + Random.insideUnitSphere * 3f;
            _navMeshAgent.SetDestination(_targetPosition);
            _lookForPlayerTimer = Random.Range(2f, 5f);
        }

        if (_ambientImpulse) {
            _ambientImpulse = false;
            if (SemiFunc.IsMultiplayer())
            {
                _photonView.RPC(nameof(RPC_PlayVision), RpcTarget.All);
            }
            else
            {
                animator.PlayVisionSound();
            }
            _ambientTimer = Random.Range(15f, 20f);
        }
    }

    // Mob is actively swinging at a player                                                                                                     
    private void StateAttack() {
        if (_stateImpulse) {
            _stateImpulse = false;
            _attackImpulse = false;
            _attackWindowOpen = true;
            _attackWindowTimer = 2.6f;
            if (SemiFunc.IsMultiplayer())
            {
                _photonView.RPC(nameof(RPC_PlayAttack), RpcTarget.All);
            }
            else
            {
                animator.AttackPlayer();
                animator.PlayAttackSound();
            }
            _navMeshAgent.ResetPath(); // Freeze movement
        }

        // Increment unsuccessful attack count at the start of each attack state
        _unsuccessfulAttackCount++;
        if (_unsuccessfulAttackCount >= 2)
        {
            _unsuccessfulAttackCount = 0;
            _currentInterest -= 0.6f;
            if (_currentInterest <= 0f)
            {
                UpdateState(State.Roam);
                _currentInterest = 0f;
                return;
            }
        }

        if (_attackWindowOpen) {
            _attackWindowTimer -= Time.deltaTime;
            if (_attackWindowTimer <= 0f) {
                _attackWindowOpen = false;
                
                // Only trigger attack if we're actually close to the player
                if (_targetPlayer && !EnemyReflectionUtil.IsPlayerDisabled(_targetPlayer))
                {
                    float distanceToPlayer = Vector3.Distance(_rigidbody.transform.position, _targetPlayer.transform.position);
                    if (distanceToPlayer <= 2f)
                    {
                        if (SemiFunc.IsMultiplayer())
                        {
                            _photonView.RPC(nameof(RPC_PlayAttack), RpcTarget.All);
                        }
                        else
                        {
                            animator.AttackPlayer();
                            animator.PlayAttackSound();
                        }
                    }
                }
                _navMeshAgent.SetDestination(_targetPosition); // Resume movement
            }
        } else {
            animator.animator.SetBool("isAttacking", false);
            _navMeshAgent.SetDestination(_targetPosition); // Resume movement
        }

        if (_targetPlayer && !EnemyReflectionUtil.IsPlayerDisabled(_targetPlayer))
        {
            Vector3 origin = _rigidbody.transform.position + Vector3.up * 1.5f;
            Vector3 direction = (_targetPlayer.transform.position + Vector3.up * 1.0f) - origin;
            float distance = direction.magnitude;

            bool hasLineOfSight = false;
            if (Physics.Raycast(origin, direction.normalized, out RaycastHit hit, distance, LayerMask.GetMask("Default")))
            {
                if (hit.transform.CompareTag("Player")) hasLineOfSight = true;
            }
            else
            {
                hasLineOfSight = true;
            }

            if (!hasLineOfSight)
            {
                UpdateState(State.LookForPlayer);
                return;
            }

            _horizontalRotationTarget = Quaternion.LookRotation(direction);
            _horizontalRotationTarget.eulerAngles = new Vector3(0f, _horizontalRotationTarget.eulerAngles.y, 0f);
            base.transform.rotation = SemiFunc.SpringQuaternionGet(horizontalRotationSpring, _horizontalRotationTarget);

            float distanceToPlayer = Vector3.Distance(_rigidbody.transform.position, _targetPlayer.transform.position);
            if (distanceToPlayer <= 10f)
            {
                _lastSeenPlayerPosition = _targetPlayer.transform.position;
                UpdateState(State.Chase);
                return;
            }
        }

        if (_targetPlayer != null && Vector3.Distance(_targetPlayer.transform.position, transform.position) < 1.5f) {
            float heightDifference = transform.position.y - _targetPlayer.transform.position.y;
            if (heightDifference > 1f && EnemyReflectionUtil.GetNotMovingTimer(_rigidbody) > 1f)
            {
                _navMeshAgent.Warp(transform.position + Vector3.back * 2f);
                UpdateState(State.Roam);
                OnStun();
                return;
            }
        }

        UpdateState(State.LookForPlayer);
    }

    // Mob is fucking off somewhere
    private void StateLeave()
    {
        if (_stateImpulse)
        {
            _ambientTimer = Random.Range(5f, 15f);
            stateTimer = 5f;
            bool flag = false;
            LevelPoint levelPoint = SemiFunc.LevelPointGetPlayerDistance(base.transform.position, 30f, 50f);
            if (!levelPoint)
            {
                levelPoint = SemiFunc.LevelPointGetFurthestFromPlayer(base.transform.position, 5f);
            } 
            if ((bool)levelPoint && NavMesh.SamplePosition(levelPoint.transform.position + Random.insideUnitSphere * 1f, out var hit, 5f, -1) && Physics.Raycast(hit.position, Vector3.down, 5f, LayerMask.GetMask("Default")))
            {
                _agentDestination = hit.position;
                flag = true;
            }
            if (flag)
            {
                _enemy.NavMeshAgent.SetDestination(_agentDestination);
                EnemyReflectionUtil.SetNotMovingTimer(_rigidbody, 0f);
                _stateImpulse = false;
            }
        }
        else
        {
            if (EnemyReflectionUtil.GetNotMovingTimer(_rigidbody) > 2f)
            {
                stateTimer -= Time.deltaTime;
            }
            SemiFunc.EnemyCartJump(_enemy);
            if (Vector3.Distance(base.transform.position, _agentDestination) < 1f || stateTimer <= 0f)
            {
                SemiFunc.EnemyCartJumpReset(_enemy);
                UpdateState(State.Idle);
            }            
        }

        if (_ambientImpulse)
        {
            _ambientImpulse = false;
            if (SemiFunc.IsMultiplayer())
            {
                _photonView.RPC(nameof(RPC_PlayRoam), RpcTarget.All);
            }
            else
            {
                animator.PlayRoamSound();
            }
            _ambientTimer = Random.Range(15f, 20f);
        }
    }

    // Mob is currently stunned
    private void StateStun() {
        if (_stateImpulse) {
            _stateImpulse = false;
        }
        if (!_enemy.IsStunned()) {
            UpdateState(State.Idle);
        }
    }
    
    /*================================================================
     *
     * Events
     * 
     *===============================================================*/

    // Called when the mob spawns
    public void OnSpawn()
    {
        if (SemiFunc.IsMasterClientOrSingleplayer() && SemiFunc.EnemySpawn(_enemy))
        {
            UpdateState(State.Spawn);
            if (SemiFunc.IsMultiplayer())
            {
                _photonView.RPC(nameof(RPC_SetTrigger), RpcTarget.All, "Spawn");
                _photonView.RPC(nameof(RPC_PlaySpawn), RpcTarget.All);
                _photonView.RPC(nameof(RPC_PlaySpawnParticles), RpcTarget.All);
            }
            else
            {
                animator.animator.SetTrigger("Spawn");
                animator.PlaySpawnSound();
                animator.SpawnParticlesImpulse();
            }
        }
    }


    [PunRPC]
    private void PlaySpawnEffectsRPC()
    {
        _photonView.RPC("RPC_SetTrigger", RpcTarget.All, "Attack");
        animator.SpawnParticlesImpulse();
        animator.PlaySpawnSound();
    }

    // Called when the mob sees a player
    public void OnVision() {
        if ((currentState == State.Idle || currentState == State.Roam || currentState == State.Investigate) && !_enemy.IsStunned()) {
            if (SemiFunc.IsMasterClientOrSingleplayer()) {
                _targetPlayer = EnemyReflectionUtil.GetVisionTriggeredPlayer(_vision);
                if (SemiFunc.IsMultiplayer()) {
                    _photonView.RPC("UpdatePlayerTargetRPC", RpcTarget.All, _photonView.ViewID);
                }
                UpdateState(State.Investigate); 
                animator.PlayVisionSound();
            }
            return;
        }

        if (currentState == State.Investigate || currentState == State.LookForPlayer) {
            if(!_targetPlayer) {
                _targetPlayer = EnemyReflectionUtil.GetVisionTriggeredPlayer(_vision);
            }

            if (Enemy.Vision.onVisionTriggeredCulled && !Enemy.Vision.onVisionTriggeredNear) {
                _targetPosition = _targetPlayer.transform.position;
            } else if (Enemy.Vision.onVisionTriggeredDistance < 5f) {
                UpdateState(State.Chase);
            }
        }
    }
    
    // Called when the mob hears something
    public void OnInvestigate() {
        if(SemiFunc.IsMasterClientOrSingleplayer() && (currentState == State.Idle || currentState == State.Roam || currentState == State.Investigate || currentState == State.LookForPlayer)) {
            _targetPosition = EnemyReflectionUtil.GetOnInvestigateTriggeredPosition(_investigate);
            UpdateState(State.Investigate);
        }
    }
    
    // Called when the mob is damaged
    public void OnHurt() {
        _hurtImpulse = true;
        _unsuccessfulAttackCount = 0;
        _currentInterest -= _hurtInterestLoss;
        if (_currentInterest <= 0f)
        {
            UpdateState(State.Roam);
        }

        if (SemiFunc.IsMultiplayer())
        {
            _photonView.RPC(nameof(RPC_PlayHurt), RpcTarget.All);
        }
        else
        {
            animator.PlayHurtSound();
            animator.HurtParticlesImpulse();
        }
    }

    [PunRPC]
    private void PlayHurtEffectsRPC()
    {
        animator.PlayHurtSound();
        animator.HurtParticlesImpulse();
    }

    // Called when the mob is damaged by a player-held object
    public void OnObjectHurt() {
        if (SemiFunc.IsMasterClientOrSingleplayer() && 
            _enemy.Health.onObjectHurtPlayer != null && 
            _spawnedZombies < MAX_SPAWNED_ZOMBIES && 
            _spawnCooldown <= 0f) 
        {
            RandomSpawnChance = Random.Range(0f, 100f);
            if (RandomSpawnChance <= spawnHordeChance) {
                ZombieHordeSpawn();
                _spawnCooldown = SPAWN_COOLDOWN_TIME;
                _spawnedZombies++;
            }
        }
        _hurtImpulse = true;
        _unsuccessfulAttackCount = 0; 
        _currentInterest -= _hurtInterestLoss;
        if (_currentInterest <= 0f)
        {
            UpdateState(State.Roam);
        }
        animator.PlayHurtSound();
        animator.HurtParticlesImpulse();
    }

    public void ZombieHordeSpawn() {
        if (!SemiFunc.IsMasterClientOrSingleplayer()) return; // Only spawn on host/master client
        
        var enemy = REPOLib.Modules.Enemies.GetEnemyByName("mczombie");
            
        var spawnPosition = _lastSeenPlayerPosition + Random.insideUnitSphere * 3f;
            
        if (NavMesh.SamplePosition(spawnPosition + Random.insideUnitSphere * 3f, out var hit, 5f, -1) &&
            Physics.Raycast(hit.position, Vector3.down, 5f, LayerMask.GetMask("Default")))
        {
            if (SemiFunc.IsMultiplayer())
            {
                _photonView.RPC(nameof(SpawnEnemyRPC), RpcTarget.All, hit.position, _enemy.transform.rotation);
            }
            else
            {
                REPOLib.Modules.Enemies.SpawnEnemy(enemy, hit.position, _enemy.transform.rotation, false);
            }
        }
    }

    [PunRPC]
    private void SpawnEnemyRPC(Vector3 position, Quaternion rotation)
    {
        var enemy = REPOLib.Modules.Enemies.GetEnemyByName("mczombie");
        REPOLib.Modules.Enemies.SpawnEnemy(enemy, position, rotation, false);
    }

    // Called when the mob dies
    public void OnDeath() {
        _deathImpulse = true;
        if (SemiFunc.IsMultiplayer())
        {
            _photonView.RPC(nameof(RPC_PlayDeath), RpcTarget.All);
        }
        else
        {
            animator.PlayDeathSound();
            animator.DeathParticlesImpulse();
        }
        GameDirector.instance.CameraShake.ShakeDistance(3f, 3f, 10f, base.transform.position, 0.5f);
        GameDirector.instance.CameraImpact.ShakeDistance(3f, 3f, 10f, base.transform.position, 0.05f);
        
        if (SemiFunc.IsMasterClientOrSingleplayer())
        {
            _enemyParent.Despawn();
            RandomSpawnChance = Random.Range(0f, 100f);
            if (_spawnCooldown <= 0f && RandomSpawnChance <= spawnHordeChance && _spawnedZombies < MAX_SPAWNED_ZOMBIES)
            {
                ZombieHordeSpawn();
                _spawnCooldown = SPAWN_COOLDOWN_TIME;
                _spawnedZombies++;
            }
        }
    }

    // Called when the mob is stunned
    public void OnStun() {
        if (SemiFunc.IsMultiplayer())
        {
            _photonView.RPC(nameof(RPC_PlayStunned), RpcTarget.All);
        }
        else
        {
            animator.OnStun();
        }
    }

    // Called when the mob becomes unstunned
    public void OnUnstun() {
        if (SemiFunc.IsMultiplayer())
        {
            _photonView.RPC(nameof(RPC_PlayUnstunned), RpcTarget.All);
        }
        else
        {
            animator.OnUnstun();
        }
    }

    // Called when the mob is grabbed
    public void OnGrabbed() {
        if (SemiFunc.IsMasterClientOrSingleplayer() && 
            currentState != State.Attack && 
            currentState != State.Chase && 
            currentState != State.Stun) {
            _targetPlayer = _enemy.Vision.onVisionTriggeredPlayer;
            if (SemiFunc.IsMultiplayer()) {
                _photonView.RPC("UpdatePlayerTargetRPC", RpcTarget.All, _targetPlayer.photonView.ViewID);
            }
            UpdateState(State.Chase);
        }
    }

    // Called via animation event after mob spawns
    public void OnSpawnComplete() {
        UpdateState(State.Idle);
    }
    
    /*================================================================
     *
     * Helper Stuff
     *
     *===============================================================*/

    public bool hasTargetLineOfSight() {
        if (!_targetPlayer || EnemyReflectionUtil.IsPlayerDisabled(_targetPlayer)) {
            return false;
        }
        
        Vector3 origin = _rigidbody.transform.position + Vector3.up * 1.5f;
        Vector3 direction = (_targetPlayer.transform.position + Vector3.up * 1.0f) - origin;
        float distance = direction.magnitude;
        
        // Only check line of sight if we're within a reasonable distance
        if (distance > 10f) {
            return false;
        }
        
        bool hasLineOfSight = false;
        if (Physics.Raycast(origin, direction.normalized, out RaycastHit hit, distance, LayerMask.GetMask("Default"))) {
            if (hit.transform.CompareTag("Player")) {
                hasLineOfSight = true;
            }
        } else {
            hasLineOfSight = true;
        }

        return hasLineOfSight;
    }

    [PunRPC]
    private void PlayAttackEffectsRPC()
    {
        animator.PlayAttackSound();
        animator.AttackPlayer();
        animator.animator.SetBool("isAttacking", true);
    }

    [PunRPC]
    private void TriggerAttackRPC()
    {
        _photonView.RPC("RPC_SetTrigger", RpcTarget.All, "Attack");

    }
    [PunRPC]
    public void RPC_SetTrigger(string trigger) => animator.animator.SetTrigger(trigger);

    [PunRPC]
    public void RPC_SetBool(string param, bool value) => animator.animator.SetBool(param, value);

    [PunRPC]
    public void RPC_SetFloat(string param, float value) => animator.animator.SetFloat(param, value);

    [PunRPC]
    public void RPC_PlayAttack()
    {
        animator.animator.SetTrigger("Attack");
        animator.attackSounds.Play(Enemy.CenterTransform.position);
    }

    [PunRPC]
    public void RPC_PlayHurt()
    {
        animator.hurtSounds.Play(Enemy.CenterTransform.position);
        foreach (var particle in animator.hurtParticles)
        {
            particle.gameObject.SetActive(true);
            particle.Play();
        }
    }

    [PunRPC]
    public void RPC_PlayDeath()
    {
        animator.deathSounds.Play(Enemy.CenterTransform.position);
        foreach (var particle in animator.deathParticles)
        {
            particle.gameObject.SetActive(true);
            particle.Play();
        }
    }

    [PunRPC]
    public void RPC_PlaySpawn()
    {
        animator.spawnSounds.Play(Enemy.CenterTransform.position);
        foreach (var particle in animator.spawnParticles)
        {
            particle.gameObject.SetActive(true);
            particle.Play();
        }
    }

    [PunRPC]
    public void RPC_SetDespawn() => EnemyReflectionUtil.GetEnemyParent(Enemy).Despawn();

    [PunRPC]
    public void RPC_PlayRoam() => animator.roamSounds.Play(Enemy.CenterTransform.position);

    [PunRPC]
    public void RPC_PlayVision() => animator.visionSounds.Play(Enemy.CenterTransform.position);

    [PunRPC]
    public void RPC_PlayCurious() => animator.curiousSounds.Play(Enemy.CenterTransform.position);

    [PunRPC]
    public void RPC_PlayLookForPlayer() => animator.lookForPlayerSounds.Play(Enemy.CenterTransform.position);

    [PunRPC]
    public void RPC_PlayChasePlayer() => animator.chasePlayerSounds.Play(Enemy.CenterTransform.position);

    [PunRPC]
    public void RPC_PlayPlayerHit() => animator.playerHitSounds.Play(Enemy.CenterTransform.position);

    [PunRPC]
    public void RPC_PlayStunned()
    {
        animator.animator.SetBool("isStunned", true);
        animator.stunnedSounds.Play(Enemy.CenterTransform.position);
    }

    [PunRPC]
    public void RPC_PlayUnstunned()
    {
        animator.animator.SetBool("isStunned", false);
        animator.unstunnedSounds.Play(Enemy.CenterTransform.position);
    }

    [PunRPC]
    public void RPC_PlayFootstep() => animator.footstepSounds.Play(Enemy.CenterTransform.position);

    [PunRPC]
    public void RPC_PlayDeathParticles()
    {
        foreach (var particle in animator.deathParticles)
        {
            particle.gameObject.SetActive(true);
            particle.Play();
        }
    }

    [PunRPC]
    public void RPC_PlayHurtParticles()
    {
        foreach (var particle in animator.hurtParticles)
        {
            particle.gameObject.SetActive(true);
            particle.Play();
        }
    }

    [PunRPC]
    public void RPC_PlaySpawnParticles()
    {
        foreach (var particle in animator.spawnParticles)
        {
            particle.gameObject.SetActive(true);
            particle.Play();
        }
    }
}