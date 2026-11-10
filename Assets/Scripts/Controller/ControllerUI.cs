// Assets/Scripts/Controller/ControllerUI.cs
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;

// Left stick: throttle (up/down) + yaw (left/right)
// Right stick: pitch (up/down) + roll (left/right)
public class ControllerUI : MonoBehaviour
{
    PlaceOnPlane placer;
    DroneController drone;
    GameObject controlsRoot;
    Text hint, stateText, altText, warnText;
    VirtualStick leftStick, rightStick;
    float warnUntil;
    Sprite circle;
    Font font;
    GameObject handled;
    string dbg = "starting";

    void Start()
    {
        try
        {
            font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (font == null) font = Font.CreateDynamicFontFromOSFont("Arial", 16);
            circle = MakeCircle();
            EnsureEventSystem();
            BuildUI();
            dbg = "UI built";
        }
        catch (System.Exception e)
        {
            dbg = "ERR: " + e.Message;
            Debug.LogException(e);
            return;
        }

        placer = FindAnyObjectByType<PlaceOnPlane>();
        if (placer != null)
        {
            placer.OnPlaced += HandlePlaced;
            placer.OnCleared += HandleCleared;
        }
        else Debug.LogWarning("ControllerUI: no PlaceOnPlane found in scene");
    }

    void OnDestroy()
    {
        if (placer != null)
        {
            placer.OnPlaced -= HandlePlaced;
            placer.OnCleared -= HandleCleared;
        }
        if (drone != null) drone.OnInvalidAction -= ShowWarning;
    }

    void HandlePlaced(GameObject go)
    {
        handled = go;
        if (drone != null) drone.OnInvalidAction -= ShowWarning;
        drone = go.GetComponent<DroneController>();
        if (drone == null) { dbg = "placed object has NO DroneController"; Debug.LogWarning("ControllerUI: placed object has no DroneController"); return; }
        drone.OnInvalidAction += ShowWarning;
        controlsRoot.SetActive(true);
        hint.gameObject.SetActive(false);
        warnText.text = "";
    }

    void HandleCleared()
    {
        handled = null;
        if (drone != null) drone.OnInvalidAction -= ShowWarning;
        drone = null;
        controlsRoot.SetActive(false);
        hint.gameObject.SetActive(true);
    }

    void ShowWarning(string msg)
    {
        warnText.text = msg;
        warnUntil = Time.time + 2.5f;
    }

    void Update()
    {
        if (controlsRoot == null) return;
        if (placer == null) placer = FindAnyObjectByType<PlaceOnPlane>();
        if (placer == null) { dbg = "no PlaceOnPlane"; return; }

        // Poll as well as listen, so a missed event can't hide the controls
        if (placer.Spawned != null && placer.Spawned != handled) HandlePlaced(placer.Spawned);
        if (placer.Spawned == null && handled != null) HandleCleared();

        if (drone == null) return;

        var c = new ControlInput
        {
            throttle = leftStick.Value.y,
            yaw      = leftStick.Value.x,
            pitch    = rightStick.Value.y,
            roll     = rightStick.Value.x
        };
        drone.SetInput(c);

        stateText.text = "State: " + drone.State;
        altText.text = "Altitude: " + drone.Altitude.ToString("0.00") + " m";
        if (Time.time > warnUntil) warnText.text = "";
    }


    // ---------------- UI building ----------------
    void EnsureEventSystem()
    {
        var existing = FindAnyObjectByType<EventSystem>();
        GameObject go;
        if (existing == null)
        {
            go = new GameObject("EventSystem");
            go.SetActive(false);
            go.AddComponent<EventSystem>();
        }
        else go = existing.gameObject;

        foreach (var m in go.GetComponents<BaseInputModule>())
            if (!(m is InputSystemUIInputModule)) { m.enabled = false; Destroy(m); }

        if (go.GetComponent<InputSystemUIInputModule>() == null)
        {
            bool wasActive = go.activeSelf;
            go.SetActive(false);
            var module = go.AddComponent<InputSystemUIInputModule>();
            module.AssignDefaultActions();
            go.SetActive(true);
            if (!wasActive && existing != null) go.SetActive(false);
        }
        else if (!go.activeSelf) go.SetActive(true);
    }

    void BuildUI()
    {
        var canvasGo = new GameObject("ControllerCanvas");
        var canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        var scaler = canvasGo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;
        canvasGo.AddComponent<GraphicRaycaster>();
        Transform root = canvasGo.transform;

        hint = MakeText(root, "Hint", "Move the phone slowly over the floor, then tap to place the drone",
                        40, Color.white, new Vector2(0.5f, 0f), new Vector2(0, 160), new Vector2(1500, 80));

        controlsRoot = new GameObject("Controls", typeof(RectTransform));
        controlsRoot.transform.SetParent(root, false);
        var rt = (RectTransform)controlsRoot.transform;
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
        Transform c = controlsRoot.transform;

        stateText = MakeText(c, "State", "State: Disarmed", 44, Color.white, new Vector2(0.5f, 1f), new Vector2(0, -40), new Vector2(900, 60));
        altText   = MakeText(c, "Altitude", "Altitude: 0.00 m", 36, new Color(0.7f, 0.9f, 1f), new Vector2(0.5f, 1f), new Vector2(0, -95), new Vector2(900, 50));
        warnText  = MakeText(c, "Warning", "", 40, new Color(1f, 0.4f, 0.3f), new Vector2(0.5f, 1f), new Vector2(0, -155), new Vector2(1400, 60));

        leftStick  = MakeStick(c, "LeftStick",  new Vector2(0f, 0f), new Vector2(260, 260));
        rightStick = MakeStick(c, "RightStick", new Vector2(1f, 0f), new Vector2(-260, 260));

        var green = new Color(0.15f, 0.6f, 0.3f, 0.85f);
        var red   = new Color(0.75f, 0.2f, 0.2f, 0.85f);
        var blue  = new Color(0.15f, 0.45f, 0.85f, 0.85f);
        var grey  = new Color(0.4f, 0.4f, 0.45f, 0.85f);
        var bottom = new Vector2(0.5f, 0f);
        var btnSize = new Vector2(200, 90);
        MakeButton(c, "ARM",      bottom, new Vector2(-330, 120), btnSize, green, () => { if (drone) drone.Arm(); });
        MakeButton(c, "DISARM",   bottom, new Vector2(-110, 120), btnSize, grey,  () => { if (drone) drone.Disarm(); });
        MakeButton(c, "TAKE OFF", bottom, new Vector2( 110, 120), btnSize, blue,  () => { if (drone) drone.TakeOff(); });
        MakeButton(c, "LAND",     bottom, new Vector2( 330, 120), btnSize, red,   () => { if (drone) drone.Land(); });

        MakeButton(c, "RESET", new Vector2(1f, 1f), new Vector2(-130, -60), new Vector2(200, 80), grey,
                   () => { if (placer != null) placer.Reposition(); });

        controlsRoot.SetActive(false);
    }

    VirtualStick MakeStick(Transform parent, string name, Vector2 anchor, Vector2 pos)
    {
        var bgGo = new GameObject(name, typeof(RectTransform));
        bgGo.transform.SetParent(parent, false);
        var bg = (RectTransform)bgGo.transform;
        bg.anchorMin = bg.anchorMax = anchor;
        bg.pivot = new Vector2(0.5f, 0.5f);
        bg.anchoredPosition = pos;
        bg.sizeDelta = new Vector2(300, 300);
        var bgImg = bgGo.AddComponent<Image>();
        bgImg.sprite = circle;
        bgImg.color = new Color(1f, 1f, 1f, 0.25f);

        var hGo = new GameObject("Handle", typeof(RectTransform));
        hGo.transform.SetParent(bgGo.transform, false);
        var h = (RectTransform)hGo.transform;
        h.anchorMin = h.anchorMax = new Vector2(0.5f, 0.5f);
        h.sizeDelta = new Vector2(120, 120);
        var hImg = hGo.AddComponent<Image>();
        hImg.sprite = circle;
        hImg.color = new Color(0.2f, 0.6f, 1f, 0.8f);
        hImg.raycastTarget = false;

        var stick = bgGo.AddComponent<VirtualStick>();
        stick.Setup(bg, h, 100f);
        return stick;
    }

    void MakeButton(Transform parent, string label, Vector2 anchor, Vector2 pos, Vector2 size,
                    Color color, UnityEngine.Events.UnityAction onClick)
    {
        var go = new GameObject(label + "Button", typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var rt = (RectTransform)go.transform;
        rt.anchorMin = rt.anchorMax = anchor;
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;
        var img = go.AddComponent<Image>();
        img.color = color;
        var btn = go.AddComponent<Button>();
        btn.targetGraphic = img;
        btn.onClick.AddListener(onClick);

        var t = MakeText(go.transform, "Label", label, 34, Color.white, new Vector2(0.5f, 0.5f), Vector2.zero, size);
        t.raycastTarget = false;
    }

    Text MakeText(Transform parent, string name, string text, int size, Color color, Vector2 anchor, Vector2 pos, Vector2 box)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var rt = (RectTransform)go.transform;
        rt.anchorMin = rt.anchorMax = anchor;
        rt.pivot = new Vector2(0.5f, anchor.y > 0.9f ? 1f : 0.5f);
        rt.anchoredPosition = pos;
        rt.sizeDelta = box;
        var t = go.AddComponent<Text>();
        t.font = font;
        t.text = text;
        t.fontSize = size;
        t.color = color;
        t.alignment = TextAnchor.MiddleCenter;
        t.raycastTarget = false;
        var sh = go.AddComponent<Shadow>();
        sh.effectColor = new Color(0, 0, 0, 0.8f);
        return t;
    }

    static Sprite MakeCircle()
    {
        int s = 128;
        var tex = new Texture2D(s, s, TextureFormat.RGBA32, false);
        var px = new Color[s * s];
        float r = s / 2f;
        for (int y = 0; y < s; y++)
            for (int x = 0; x < s; x++)
            {
                float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(r, r));
                px[y * s + x] = new Color(1, 1, 1, Mathf.Clamp01(r - d));
            }
        tex.SetPixels(px);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, s, s), new Vector2(0.5f, 0.5f));
    }
}

// One on-screen stick. Each stick tracks its own finger, so both work at once.
public class VirtualStick : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
{
    RectTransform background, handle;
    float radius = 100f;
    int pointerId = int.MinValue;
    bool active;

    public Vector2 Value { get; private set; }

    public void Setup(RectTransform bg, RectTransform h, float r)
    {
        background = bg; handle = h; radius = r;
    }

    public void OnPointerDown(PointerEventData e)
    {
        if (active) return;
        active = true;
        pointerId = e.pointerId;
        Move(e);
    }

    public void OnDrag(PointerEventData e)
    {
        if (!active || e.pointerId != pointerId) return;
        Move(e);
    }

    public void OnPointerUp(PointerEventData e)
    {
        if (!active || e.pointerId != pointerId) return;
        Release();
    }

    void OnDisable() { Release(); }

    void Release()
    {
        active = false;
        pointerId = int.MinValue;
        Value = Vector2.zero;
        if (handle != null) handle.anchoredPosition = Vector2.zero;
    }

    void Move(PointerEventData e)
    {
        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(background, e.position, e.pressEventCamera, out Vector2 local))
        {
            Value = Vector2.ClampMagnitude(local / radius, 1f);
            handle.anchoredPosition = Value * radius;
        }
    }
}