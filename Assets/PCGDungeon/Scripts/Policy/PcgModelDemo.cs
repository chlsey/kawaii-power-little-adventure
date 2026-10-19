using UnityEngine;

// Small controls for trying the attached policy against the real dungeon generator.
public sealed class PcgModelDemo : MonoBehaviour
{
    [SerializeField] private PcgDungeonManager dungeonManager;
    [SerializeField] private PcgPolicyRunner policyRunner;

    private void OnGUI()
    {
        int panelHeight = 230;
        if (policyRunner.UsesLayoutPolicy)
            panelHeight = 370;
        GUILayout.BeginArea(new Rect(16, 16, 310, panelHeight), GUI.skin.box);
        GUILayout.Label("PCG Model Demo");
        GUILayout.Label("Requested rooms: " + policyRunner.TargetRoomCount);
        GUILayout.BeginHorizontal();
        if (GUILayout.Button("Fewer rooms"))
        {
            ChangeTarget(-1);
        }
        if (GUILayout.Button("More rooms"))
        {
            ChangeTarget(1);
        }
        GUILayout.EndHorizontal();

        GUILayout.Label("Model chose: " + policyRunner.Parameters.RoomCount);
        GUILayout.Label("Generated rooms: " + dungeonManager.GeneratedRoomCount);
        if (policyRunner.UsesLayoutPolicy)
        {
            GUILayout.Label("Requested size: " + policyRunner.TargetRoomWidth + " x " + policyRunner.TargetRoomHeight);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Narrower")) ChangeSize(-1, 0);
            if (GUILayout.Button("Wider")) ChangeSize(1, 0);
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Shorter")) ChangeSize(0, -1);
            if (GUILayout.Button("Taller")) ChangeSize(0, 1);
            GUILayout.EndHorizontal();
            GUILayout.Label("Model size: " + policyRunner.Parameters.MinRoomWidth + " x " + policyRunner.Parameters.MinRoomHeight);
            Vector2 size = dungeonManager.GeneratedRegularRoomSize;
            GUILayout.Label("Generated size: " + size.x + " x " + size.y);
        }
        GUILayout.Label("Seed: " + policyRunner.Parameters.Seed);
        if (GUILayout.Button("Generate dungeon (R)"))
        {
            dungeonManager.StartEpisode();
        }
        GUILayout.EndArea();
    }

    private void ChangeTarget(int difference)
    {
        policyRunner.TargetRoomCount += difference;
        dungeonManager.StartEpisode();
    }

    private void ChangeSize(int width, int height)
    {
        policyRunner.TargetRoomWidth += width;
        policyRunner.TargetRoomHeight += height;
        dungeonManager.StartEpisode();
    }
}
