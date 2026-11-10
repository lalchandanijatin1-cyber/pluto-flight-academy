// Assets/Scripts/AR/PlaneVisualizer.cs
using UnityEngine;
using UnityEngine.XR.ARFoundation;

// Draws a see-through yellow surface on every detected plane, built from code.
[RequireComponent(typeof(ARPlaneManager))]
public class PlaneVisualizer : MonoBehaviour
{
    ARPlaneManager planeManager;
    Material mat;

    void Awake()
    {
        planeManager = GetComponent<ARPlaneManager>();
        Shader sh = Shader.Find("Sprites/Default");
        if (sh == null) sh = Shader.Find("Universal Render Pipeline/Unlit");
        mat = new Material(sh);
        mat.color = new Color(1f, 0.9f, 0.1f, 0.35f);   // yellow, see-through
    }

    void OnEnable()  { planeManager.trackablesChanged.AddListener(OnChanged); }
    void OnDisable() { planeManager.trackablesChanged.RemoveListener(OnChanged); }

    void OnChanged(ARTrackablesChangedEventArgs<ARPlane> args)
    {
        foreach (var plane in args.added)
        {
            if (plane.GetComponent<ARPlaneMeshVisualizer>() == null)
                plane.gameObject.AddComponent<ARPlaneMeshVisualizer>();
            var mr = plane.GetComponent<MeshRenderer>();
            if (mr == null) mr = plane.gameObject.AddComponent<MeshRenderer>();
            mr.sharedMaterial = mat;
        }
    }
}