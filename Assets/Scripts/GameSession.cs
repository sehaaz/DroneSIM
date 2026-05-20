using UnityEngine;

/// <summary>
/// Static data container for passing selections between the DroneSelection scene
/// and the main simulation scene.
/// </summary>
public static class GameSession
{
    public static DroneConfig SelectedDroneConfig;
    public static DroneController.InputSource SelectedInputSource = DroneController.InputSource.Keyboard;
    public static bool HasSelection = false;

    public static void Clear()
    {
        SelectedDroneConfig = null;
        SelectedInputSource = DroneController.InputSource.Keyboard;
        HasSelection = false;
    }
}
