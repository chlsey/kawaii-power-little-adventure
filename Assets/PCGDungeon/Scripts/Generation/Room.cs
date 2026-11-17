using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;


public enum RoomType
{
    Normal,
    Spawn,
    Boss,
    Puzzle
}

public class Room : MonoBehaviour
{
    //Use this vectors to make sure there's none connected
    private Vector2Int TLCorner;
    private Vector2Int BLCorner;
    private Vector2Int TRCorner;
    private Vector2Int BRCorner;

    private List<GameObject> corners = new List<GameObject>();
    private List<GameObject> midPoints = new List<GameObject>();

    private bool isPlayerInside;

    public Vector2Int GeneratedSize { get; set; }
    public RoomType Type { get; set; }
    public List<GameObject> MidPoints { get => midPoints; }
    public List<GameObject> Corners { get => corners; set => corners = value; }
    public bool IsPlayerInside { get => isPlayerInside; set => isPlayerInside = value; }
}
