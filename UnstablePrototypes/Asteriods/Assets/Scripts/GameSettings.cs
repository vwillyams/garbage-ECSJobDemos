using UnityEngine;

public static class GameSettings
{
    public static int mapWidth;
    public static int mapHeight;
    static GameSettings()
    {
        // TODO: temp hack
        mapWidth = Screen.width;
        mapHeight = Screen.height;
    }
}
