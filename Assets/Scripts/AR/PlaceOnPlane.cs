// Assets/Scripts/AR/PlaceOnPlane.cs
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;

public class PlaceOnPlane : MonoBehaviour
{
    [SerializeField] ARRaycastManager raycastManager;
    [SerializeField] ARPlaneManager planeManager;
    [SerializeField] GameObject objectPrefab;

    [Header("Tap filtering")]
    [SerializeField] float maxTapSeconds = 0.4f;
    [SerializeField] float maxTapMovePixels = 40f;
    [SerializeField, Range(0f, 0.2f)] float edgeIgnoreFraction = 0.06f;

    [Header("Debug aid (keep OFF; not anchored to a real surface)")]
    [SerializeField] bool allowFallbackPlacement = false;
    [SerializeField] float fallbackDistance = 1.2f;
    [SerializeField] float fallbackDrop = 1.3f;

    public GameObject Spawned { get; private set; }
    public event Action<GameObject> OnPlaced;
    public event Action OnCleared;   // fired after Reposition()

    static readonly List<ARRaycastHit> hits = new();
    string status = "";
    bool pressValid;
    float pressTime;
    Vector2 pressPos;
    float ignoreUntil;

    void Awake()
    {
        if (raycastManager == null) raycastManager = GetComponent<ARRaycastManager>();
        if (planeManager == null) planeManager = GetComponent<ARPlaneManager>();
    }

    void Update()
    {
        if (raycastManager == null || objectPrefab == null) return;
        if (Spawned != null) return;

        int planes = planeManager != null ? planeManager.trackables.count : 0;
        status = "AR: " + ARSession.state + "   planes found: " + planes +
                 (planes == 0 ? "\nMove the phone slowly side to side over a textured floor"
                              : "\nTap to place the drone");

        if (Time.unscaledTime < ignoreUntil) return;

        var screen = Touchscreen.current;
        if (screen == null) return;
        var touch = screen.primaryTouch;

        if (touch.press.wasPressedThisFrame)
        {
            Vector2 p = touch.position.ReadValue();
            bool onUI = EventSystem.current != null &&
                        EventSystem.current.IsPointerOverGameObject(touch.touchId.ReadValue());
            bool onEdge = p.x < Screen.width * edgeIgnoreFraction ||
                          p.x > Screen.width * (1f - edgeIgnoreFraction) ||
                          p.y < Screen.height * edgeIgnoreFraction ||
                          p.y > Screen.height * (1f - edgeIgnoreFraction);
            pressValid = !onUI && !onEdge;
            pressTime = Time.unscaledTime;
            pressPos = p;
            return;
        }

        if (!touch.press.wasReleasedThisFrame || !pressValid) return;
        pressValid = false;

        Vector2 end = touch.position.ReadValue();
        if (Time.unscaledTime - pressTime > maxTapSeconds) return;
        if ((end - pressPos).magnitude > maxTapMovePixels) return;

        if (raycastManager.Raycast(end, hits, TrackableType.PlaneWithinPolygon | TrackableType.FeaturePoint))
        {
            Pose pose = hits[0].pose;
            float yaw = Camera.main != null ? Camera.main.transform.eulerAngles.y : 0f;
            Place(pose.position, Quaternion.Euler(0f, yaw, 0f));
            Debug.Log("PlaceOnPlane: placed on AR hit (" + hits[0].hitType + ")");
        }
        else if (allowFallbackPlacement && Camera.main != null)
        {
            Transform cam = Camera.main.transform;
            Vector3 flatForward = Vector3.ProjectOnPlane(cam.forward, Vector3.up).normalized;
            Vector3 p = cam.position + flatForward * fallbackDistance;
            p.y = cam.position.y - fallbackDrop;
            Place(p, Quaternion.Euler(0f, cam.eulerAngles.y, 0f));
            Debug.LogWarning("PlaceOnPlane: FALLBACK placement (no AR surface hit). Not anchored.");
        }
    }

    void Place(Vector3 position, Quaternion rotation)
    {
        Spawned = Instantiate(objectPrefab, position, rotation);
        HidePlanes();
        OnPlaced?.Invoke(Spawned);
    }

    // Remove the drone and go back to scanning/placing.
    public void Reposition()
    {
        if (Spawned != null) Destroy(Spawned);
        Spawned = null;
        pressValid = false;
        ignoreUntil = Time.unscaledTime + 0.6f;   // don't re-place from the reset tap
        if (planeManager == null) planeManager = FindAnyObjectByType<ARPlaneManager>();
        if (planeManager != null) planeManager.enabled = true;
        OnCleared?.Invoke();
        Debug.Log("PlaceOnPlane: Reposition requested");
    }

    void HidePlanes()
    {
        if (planeManager == null) planeManager = FindAnyObjectByType<ARPlaneManager>();
        if (planeManager == null) return;
        foreach (var plane in planeManager.trackables)
            plane.gameObject.SetActive(false);
        planeManager.enabled = false;
    }

    // Status text while scanning (display only). RESET button now lives in ControllerUI.
    void OnGUI()
    {
        if (Spawned != null || string.IsNullOrEmpty(status)) return;
        var style = new GUIStyle(GUI.skin.label)
        {
            fontSize = Mathf.RoundToInt(Screen.height / 26f),
            alignment = TextAnchor.UpperCenter,
            normal = { textColor = Color.white }
        };
        GUI.Label(new Rect(0, Screen.height * 0.05f, Screen.width, Screen.height * 0.25f), status, style);
    }
}