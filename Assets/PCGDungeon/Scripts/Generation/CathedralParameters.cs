// The inputs to the cathedral predungeon generator. Defaults follow Diablo's
// cathedral. They are plain data so a policy can fill them in the same way it
// fills PcgGenerationParameters; PcgGenerationParameters.Cathedral overrides
// the CathedralSettings asset when set.

using System;
using UnityEngine;

public enum SpineAxis
{
    Random,
    X,
    Y
}

[Serializable]
public class CathedralParameters
{
    // The default dumbbell spine (10 + 16 + 10 tiles) plus the wall border needs more than 40.
    public int GridWidth = 50;
    public int GridHeight = 50;

    public SpineAxis SpineAxis = SpineAxis.Random;
    // The spine is a dumbbell: two square rooms joined by a long, narrower hall.
    public int SpineRoomSize = 10;
    public int SpineCorridorLength = 16;
    public int SpineCorridorWidth = 6;

    // Each budded room picks its width and height from these.
    public int[] BudSizes = { 2, 4, 6 };
    // Four columns stand in each end room, this many tiles in from its walls.
    public int ColumnInset = 3;
    // Arches line both sides of the hall, each spanning this many tiles and standing
    // this many tiles in from the hall's walls.
    public int ColonnadeSpacing = 2;
    public int ColonnadeInset = 1;

    // Budded rooms are separated from their parent by a wall with a doorway this wide.
    public int DoorwayWidth = 1;
    // Chance, each time a room buds, of switching the axis it buds along.
    [Range(0f, 1f)] public float AxisSwitchChance = 0.25f;

    // The layout is scrapped and regenerated until it has this many floor tiles.
    public int MinWalkableTiles = 500;
    public int MaxAttempts = 100;
}
