using System.Text;
using UnityEngine;

// Minimal on-screen readout for playtesting: health, equipment, active powerups
// and a count of awake enemies. Installs itself in any scene that has a
// PcgDungeonManager, so it never needs to be added by hand.
public sealed class PlayerHud : MonoBehaviour
{
    private const float PickupMessageSeconds = 3f;
    private const float EnemyRefreshSeconds = 0.5f;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Install()
    {
        if (FindFirstObjectByType<PcgDungeonManager>() != null && FindFirstObjectByType<PlayerHud>() == null)
            new GameObject("PlayerHud").AddComponent<PlayerHud>();
    }

    private QaPlayerController player;
    private QaExitZone exit;
    private GUIStyle style;
    private EnemyBrain[] enemies = new EnemyBrain[0];
    private float nextEnemyRefresh;

    private void OnGUI()
    {
        if (style == null)
        {
            style = new GUIStyle(GUI.skin.label) { fontSize = 18, wordWrap = false };
            style.normal.textColor = Color.white;
        }

        if (player == null)
            player = FindFirstObjectByType<QaPlayerController>();

        string text = player == null ? "HUD: no QaPlayerController in the scene (press R)" : Describe();
        int lines = text.Split('\n').Length;

        GUI.Box(new Rect(10, 10, 520, 14 + lines * 26), GUIContent.none);
        GUI.Label(new Rect(20, 16, 500, lines * 26), text, style);
    }

    private string Describe()
    {
        var text = new StringBuilder();

        string status = player.IsDead ? "   DEAD - press R"
            : player.Metrics.Completed ? "   LEVEL CLEARED - press R"
            : player.EpisodeActive ? "" : "   (no QA episode - press R)";
        text.AppendLine($"HP {player.Health:0.#} / {player.MaxHealth:0.#}{status}");

        PlayerEquipment equipment = player.Equipment;
        WeaponDefinition weapon = equipment.Weapon;
        text.AppendLine(weapon != null
            ? $"Weapon: {weapon.DisplayName} (dmg {weapon.Damage:0.#}, range {weapon.Range:0.#}, {weapon.CooldownSeconds:0.#}s)"
            : "Weapon: none");

        ShieldDefinition shield = equipment.Shield;
        text.AppendLine(shield != null
            ? $"Shield: {shield.DisplayName} (-{shield.DamageReduction * 100f:0}% damage)"
            : "Shield: none");

        string powerups = string.Join(", ", equipment.DescribeActivePowerups());
        text.AppendLine($"Powerups: {(powerups.Length > 0 ? powerups : "none")}");

        if (Time.time - player.LastPickupTime < PickupMessageSeconds && !string.IsNullOrEmpty(player.LastPickupName))
            text.AppendLine($"Picked up: {player.LastPickupName}");

        text.AppendLine(DescribeEnemies());
        text.Append(DescribeExit());
        return text.ToString();
    }

    private string DescribeExit()
    {
        if (exit == null)
            exit = FindFirstObjectByType<QaExitZone>();
        if (exit == null)
            return "Exit: none";

        float distance = Vector3.Distance(player.transform.position, exit.transform.position);
        return exit.RequireInteract ? $"Exit: {distance:0} m (press E on it)" : $"Exit: {distance:0} m";
    }

    // Shows how many enemies exist and how many have noticed the player, to spot enemies that never wake up.
    private string DescribeEnemies()
    {
        if (Time.unscaledTime >= nextEnemyRefresh)
        {
            enemies = FindObjectsByType<EnemyBrain>(FindObjectsSortMode.None);
            nextEnemyRefresh = Time.unscaledTime + EnemyRefreshSeconds;
        }

        int awake = 0;
        foreach (EnemyBrain enemy in enemies)
        {
            if (enemy != null && enemy.StateName != "Idle")
                awake++;
        }

        return $"Enemies: {enemies.Length} ({awake} chasing or attacking)";
    }
}
