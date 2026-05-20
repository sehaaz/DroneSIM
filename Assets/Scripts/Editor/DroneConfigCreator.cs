using UnityEngine;
using UnityEditor;
using System.IO;

/// <summary>
/// Editor utility to create all 5 drone config ScriptableObject assets.
/// Run from menu: DroneSIM > Create Drone Configs
/// </summary>
public class DroneConfigCreator
{
    [MenuItem("DroneSIM/Create Drone Configs")]
    public static void CreateAllDroneConfigs()
    {
        string folder = "Assets/ScriptableObjects/Drones";

        if (!AssetDatabase.IsValidFolder("Assets/ScriptableObjects"))
            AssetDatabase.CreateFolder("Assets", "ScriptableObjects");
        if (!AssetDatabase.IsValidFolder(folder))
            AssetDatabase.CreateFolder("Assets/ScriptableObjects", "Drones");

        CreateDrone(folder, "SkyLarkTrainer", "SkyLark Trainer",
            "Built for beginners. Heavy frame with aggressive stabilization makes this the most forgiving drone to fly. " +
            "Perfect for learning throttle management and basic maneuvers.",
            DifficultyRating.Easy,
            mass: 1.5f, maxThrustScale: 1.8f, maxTiltAngle: 30f,
            yawRate: 120f, acroRate: 200f,
            pidP: 8.0f, pidI: 0.2f, pidD: 1.0f,
            baseDrag: 0.8f, angleAngularDrag: 3.0f, acroAngularDrag: 1.0f,
            horizonTransitionPoint: 0.6f,
            defaultFlightMode: DronePhysics.FlightMode.Angle);

        CreateDrone(folder, "AeroScoutS2", "AeroScout S2",
            "A step up from the Trainer. Slightly lighter with moderate stabilization. " +
            "Good for pilots ready to push beyond the basics while still having a safety net.",
            DifficultyRating.Easy,
            mass: 1.3f, maxThrustScale: 2.0f, maxTiltAngle: 35f,
            yawRate: 150f, acroRate: 250f,
            pidP: 7.0f, pidI: 0.15f, pidD: 0.8f,
            baseDrag: 0.7f, angleAngularDrag: 2.5f, acroAngularDrag: 0.8f,
            horizonTransitionPoint: 0.55f,
            defaultFlightMode: DronePhysics.FlightMode.Angle);

        CreateDrone(folder, "Vortex5", "Vortex 5",
            "The all-rounder. Balanced weight, thrust, and stabilization provide a neutral flight experience. " +
            "Equally capable in steady cruising and moderate acrobatics.",
            DifficultyRating.Medium,
            mass: 1.0f, maxThrustScale: 2.0f, maxTiltAngle: 45f,
            yawRate: 180f, acroRate: 360f,
            pidP: 5.0f, pidI: 0.1f, pidD: 0.5f,
            baseDrag: 0.5f, angleAngularDrag: 2.0f, acroAngularDrag: 0.5f,
            horizonTransitionPoint: 0.5f,
            defaultFlightMode: DronePhysics.FlightMode.Angle);

        CreateDrone(folder, "PhantomFX", "Phantom FX",
            "Lightweight performance quad with boosted thrust. Reduced stabilization rewards precise stick control. " +
            "Starts in Horizon mode for aggressive yet recoverable flight.",
            DifficultyRating.Medium,
            mass: 0.85f, maxThrustScale: 2.5f, maxTiltAngle: 50f,
            yawRate: 220f, acroRate: 450f,
            pidP: 4.0f, pidI: 0.08f, pidD: 0.4f,
            baseDrag: 0.4f, angleAngularDrag: 1.5f, acroAngularDrag: 0.4f,
            horizonTransitionPoint: 0.45f,
            defaultFlightMode: DronePhysics.FlightMode.Horizon);

        CreateDrone(folder, "BansheeRS", "Banshee RS",
            "Competition racing quad. Extremely light with massive thrust-to-weight ratio and minimal stabilization. " +
            "Twitchy and unforgiving. For experienced pilots only.",
            DifficultyRating.Hard,
            mass: 0.6f, maxThrustScale: 3.0f, maxTiltAngle: 60f,
            yawRate: 300f, acroRate: 700f,
            pidP: 3.0f, pidI: 0.05f, pidD: 0.2f,
            baseDrag: 0.25f, angleAngularDrag: 1.0f, acroAngularDrag: 0.2f,
            horizonTransitionPoint: 0.3f,
            defaultFlightMode: DronePhysics.FlightMode.Acro);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("All 5 drone configs created in " + folder);
    }

    private static void CreateDrone(string folder, string fileName, string droneName,
        string description, DifficultyRating difficulty,
        float mass, float maxThrustScale, float maxTiltAngle,
        float yawRate, float acroRate,
        float pidP, float pidI, float pidD,
        float baseDrag, float angleAngularDrag, float acroAngularDrag,
        float horizonTransitionPoint,
        DronePhysics.FlightMode defaultFlightMode)
    {
        string path = $"{folder}/{fileName}.asset";

        // Don't overwrite existing configs
        if (AssetDatabase.LoadAssetAtPath<DroneConfig>(path) != null)
        {
            Debug.Log($"Skipping {fileName} - already exists at {path}");
            return;
        }

        DroneConfig config = ScriptableObject.CreateInstance<DroneConfig>();
        config.droneName = droneName;
        config.description = description;
        config.difficulty = difficulty;
        config.mass = mass;
        config.maxThrustScale = maxThrustScale;
        config.maxTiltAngle = maxTiltAngle;
        config.yawRate = yawRate;
        config.acroRate = acroRate;
        config.pidP = pidP;
        config.pidI = pidI;
        config.pidD = pidD;
        config.baseDrag = baseDrag;
        config.angleAngularDrag = angleAngularDrag;
        config.acroAngularDrag = acroAngularDrag;
        config.horizonTransitionPoint = horizonTransitionPoint;
        config.defaultFlightMode = defaultFlightMode;

        AssetDatabase.CreateAsset(config, path);
        Debug.Log($"Created drone config: {path}");
    }
}
