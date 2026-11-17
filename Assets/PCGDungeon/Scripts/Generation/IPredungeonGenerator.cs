// Stage one of dungeon generation: decides which tiles are walkable and where
// the rooms are, without creating any GameObjects. Generator.BuildTiles turns
// the result into tiles, so every dungeon type shares the stages after it.
public interface IPredungeonGenerator
{
    // The result needs at least one room, a valid BossRoom index, and floor kept
    // Predungeon.Border tiles from the grid edge. Null if no dungeon could be built.
    Predungeon Generate();
}
