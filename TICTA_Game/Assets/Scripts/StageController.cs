using UnityEngine;

[DefaultExecutionOrder(-100)]
public class StageController : MonoBehaviour
{
    private static StageController activeStage;

    [Header("Player Stage")]
    [SerializeField] private PlayerMovement player;
    [SerializeField] private Transform playerSpawnPoint;

    [Header("Enemy Stage")]
    [SerializeField] private EnemyBrain enemy;
    [SerializeField] private Transform enemySpawnPoint;

    public static StageController ActiveStage => activeStage;
    public PlayerMovement Player => player;
    public EnemyBrain Enemy => enemy;
    public Transform PlayerTransform => player != null ? player.transform : null;
    public Transform EnemyTransform => enemy != null ? enemy.transform : null;

    public static StageController GetOrCreateActiveStage()
    {
        if (activeStage != null)
        {
            return activeStage;
        }

        activeStage = FindFirstObjectByType<StageController>();
        if (activeStage != null)
        {
            return activeStage;
        }

        GameObject stageObject = new GameObject("StageController");
        activeStage = stageObject.AddComponent<StageController>();
        return activeStage;
    }

    private void Awake()
    {
        if (activeStage != null && activeStage != this)
        {
            Debug.LogWarning("[StageController] More than one StageController found. Using the first active stage.");
            return;
        }

        activeStage = this;
        ResolveStageActors();
        ApplySpawnPoints();
    }

    private void OnDestroy()
    {
        if (activeStage == this)
        {
            activeStage = null;
        }
    }

    public void RegisterPlayer(PlayerMovement stagePlayer)
    {
        if (stagePlayer == null) return;
        player = stagePlayer;
        ApplyPlayerSpawnPoint();
    }

    public void RegisterEnemy(EnemyBrain stageEnemy)
    {
        if (stageEnemy == null) return;
        enemy = stageEnemy;
        ApplyEnemySpawnPoint();
    }

    private void ResolveStageActors()
    {
        if (player == null)
        {
            player = FindFirstObjectByType<PlayerMovement>();
        }

        if (enemy == null)
        {
            enemy = FindFirstObjectByType<EnemyBrain>();
        }
    }

    private void ApplySpawnPoints()
    {
        ApplyPlayerSpawnPoint();
        ApplyEnemySpawnPoint();
    }

    private void ApplyPlayerSpawnPoint()
    {
        if (player == null || playerSpawnPoint == null) return;

        player.transform.SetPositionAndRotation(playerSpawnPoint.position, playerSpawnPoint.rotation);
    }

    private void ApplyEnemySpawnPoint()
    {
        if (enemy == null || enemySpawnPoint == null) return;

        enemy.transform.SetPositionAndRotation(enemySpawnPoint.position, enemySpawnPoint.rotation);
    }
}
