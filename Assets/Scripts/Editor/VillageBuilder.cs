using UnityEngine;
using UnityEditor;

public class VillageBuilder : EditorWindow
{
    [MenuItem("Village/Build Simple Village")]
    public static void BuildVillage()
    {
        GameObject villageRoot = new GameObject("Simple Village");
        villageRoot.transform.position = new Vector3(30, 0, 30);

        Material buildingMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Building.mat");

        // Define assets to use
        string[] buildingPaths = {
            "Assets/Objects/Barn/Barn.obj",
            "Assets/Objects/Big Barn/BigBarn.obj",
            "Assets/Objects/Small Barn/SmallBarn.obj",
            "Assets/Objects/Silo House/Silo_House.obj",
            "Assets/Objects/Tower Windmill/TowerWindmill.obj"
        };

        // Layout parameters
        float radius = 20f;
        int buildingCount = 6;

        for (int i = 0; i < buildingCount; i++)
        {
            float angle = i * Mathf.PI * 2 / buildingCount;
            Vector3 pos = new Vector3(Mathf.Cos(angle) * radius, 0, Mathf.Sin(angle) * radius);
            
            // Randomly pick a building
            string path = buildingPaths[Random.Range(0, buildingPaths.Length)];
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);

            if (prefab != null)
            {
                GameObject building = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
                building.name = prefab.name + "_" + i;
                building.transform.SetParent(villageRoot.transform);
                building.transform.localPosition = pos;
                building.transform.localRotation = Quaternion.LookRotation(-pos.normalized);

                // Apply material to all mesh renderers
                MeshRenderer[] renderers = building.GetComponentsInChildren<MeshRenderer>();
                foreach (var renderer in renderers)
                {
                    renderer.sharedMaterial = buildingMat;
                }

                // Add MeshCollider if not present
                if (building.GetComponentInChildren<Collider>() == null)
                {
                    foreach (var filter in building.GetComponentsInChildren<MeshFilter>())
                    {
                        var collider = filter.gameObject.AddComponent<MeshCollider>();
                        collider.sharedMesh = filter.sharedMesh;
                    }
                }
            }
            else
            {
                Debug.LogWarning("Could not find building at: " + path);
            }
        }

        // Add a Windmill on a "hill" (just slightly higher and further)
        string windmillPath = "Assets/Objects/Tower Windmill/TowerWindmill.obj";
        GameObject windmillPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(windmillPath);
        if (windmillPrefab != null)
        {
            GameObject windmill = (GameObject)PrefabUtility.InstantiatePrefab(windmillPrefab);
            windmill.name = "Village_Windmill";
            windmill.transform.SetParent(villageRoot.transform);
            windmill.transform.localPosition = new Vector3(-30, 5, -30);
            
            MeshRenderer[] renderers = windmill.GetComponentsInChildren<MeshRenderer>();
            foreach (var renderer in renderers)
            {
                renderer.sharedMaterial = buildingMat;
            }

            foreach (var filter in windmill.GetComponentsInChildren<MeshFilter>())
            {
                var collider = filter.gameObject.AddComponent<MeshCollider>();
                collider.sharedMesh = filter.sharedMesh;
            }
        }

        Selection.activeGameObject = villageRoot;
        Debug.Log("Village created at (30, 0, 30)");
    }
}
