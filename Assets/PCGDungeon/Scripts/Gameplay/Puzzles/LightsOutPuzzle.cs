using UnityEngine;

// Three lights in a line: a switch flips its own light and its immediate neighbors.
// Goal: all lights on. The initial states require two or three distinct presses.
public sealed class LightsOutPuzzle : RoomPuzzle
{
    private readonly bool[] lit = new bool[3];
    private readonly Renderer[] lamps = new Renderer[3];
    private readonly Light[] glow = new Light[3];
    private Material material;
    private TextMesh instructions;
    private bool initialized;
    public int LitCount => (lit[0] ? 1 : 0) + (lit[1] ? 1 : 0) + (lit[2] ? 1 : 0);
    public bool IsLit(int index) => index >= 0 && index < 3 && lit[index];

    public void Initialize(int variation)
    {
        if (initialized) return;
        initialized = true;
        Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
        material = new Material(shader);
        material.EnableKeyword("_EMISSION");
        for (int i = 0; i < 3; i++)
        {
            Vector3 position = new Vector3((i - 1) * 1.3f, 0.35f, 0f);
            GameObject pedestal = Part("Switch " + (i + 1), PrimitiveType.Cylinder, position,
                new Vector3(0.65f, 0.35f, 0.65f), new Color(0.19f, 0.22f, 0.3f), true);
            pedestal.AddComponent<LightsOutSwitch>().Configure(this, i);
            lamps[i] = Part("Light " + (i + 1), PrimitiveType.Sphere, position + Vector3.up * 0.55f,
                Vector3.one * 0.48f, Color.white, false).GetComponent<Renderer>();
            var lamp = new GameObject("Glow");
            lamp.transform.SetParent(lamps[i].transform, false);
            glow[i] = lamp.AddComponent<Light>();
            glow[i].type = LightType.Point;
            glow[i].range = 2f;
            glow[i].color = new Color(0.3f, 1f, 0.65f);
            lit[i] = true;
        }
        // Every starting board is derived from a solved board using legal moves.
        int[] scramble = { 3, 5, 6, 7 };
        int mask = scramble[Mathf.Abs(variation % 4)];
        for (int i = 0; i < 3; i++) if ((mask & (1 << i)) != 0) Flip(i);
        var label = new GameObject("Puzzle instructions");
        label.transform.SetParent(transform, false);
        label.transform.localPosition = Vector3.up * 2f;
        instructions = label.AddComponent<TextMesh>();
        instructions.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        label.GetComponent<Renderer>().sharedMaterial = instructions.font.material;
        instructions.anchor = TextAnchor.MiddleCenter;
        instructions.alignment = TextAlignment.Center;
        instructions.fontSize = 48;
        instructions.characterSize = 0.045f;
        Refresh();
    }

    public bool ToggleSwitch(int index)
    {
        if (!initialized || IsSolved || index < 0 || index >= 3) return false;
        Flip(index);
        if (LitCount == 3) MarkSolved();
        Refresh();
        return true;
    }

    private void Flip(int index)
    {
        for (int i = Mathf.Max(0, index - 1); i <= Mathf.Min(2, index + 1); i++) lit[i] = !lit[i];
    }

    private void Refresh()
    {
        Color on = new Color(0.25f, 1f, 0.6f);
        Color off = new Color(0.16f, 0.19f, 0.25f);
        for (int i = 0; i < 3; i++)
        {
            SetColor(lamps[i], lit[i] ? on : off, lit[i] ? on * 2f : Color.black);
            glow[i].intensity = lit[i] ? 1.5f : 0f;
        }
        instructions.text = IsSolved ? "SOLVED!\nDoors unlocked" : $"LIGHTS OUT  {LitCount}/3\nLight all three to open the doors\nE: nearest switch + neighboring lights";
        instructions.color = IsSolved ? on : Color.white;
    }

    private GameObject Part(string partName, PrimitiveType shape, Vector3 position, Vector3 scale, Color color, bool collider)
    {
        GameObject part = GameObject.CreatePrimitive(shape);
        part.name = partName;
        part.transform.SetParent(transform, false);
        part.transform.localPosition = position;
        part.transform.localScale = scale;
        if (!collider)
        {
            part.GetComponent<Collider>().enabled = false;
            Destroy(part.GetComponent<Collider>());
        }
        part.GetComponent<Renderer>().sharedMaterial = material;
        SetColor(part.GetComponent<Renderer>(), color, Color.black);
        return part;
    }

    private static void SetColor(Renderer renderer, Color color, Color emission)
    {
        var block = new MaterialPropertyBlock();
        block.SetColor("_BaseColor", color);
        block.SetColor("_Color", color);
        block.SetColor("_EmissionColor", emission);
        renderer.SetPropertyBlock(block);
    }

    private void LateUpdate()
    {
        if (instructions != null && Camera.main != null) instructions.transform.rotation = Camera.main.transform.rotation;
    }

    private void OnDestroy()
    {
        if (material != null) Destroy(material);
    }
}
