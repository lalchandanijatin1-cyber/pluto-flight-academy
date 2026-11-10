// Assets/Scripts/Game/GameSystems.cs
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

// Creates itself at startup. Hooks the tutorial + checkpoint course to the placed drone,
// builds the extra HUD and restyles the existing ControllerUI (no changes to its code).
public class GameSystems : MonoBehaviour
{
    const float GaugeMax = 2f;   // altitude gauge full-scale (m)

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Boot()
    {
        if (FindAnyObjectByType<GameSystems>() != null) return;
        var go = new GameObject("GameSystems");
        go.AddComponent<CheckpointCourse>();
        go.AddComponent<TutorialCloud>();
        go.AddComponent<GameSystems>();
    }

    PlaceOnPlane placer;
    DroneController drone;
    CheckpointCourse course;
    TutorialCloud tutorial;
    bool bound, skinned;
    GameObject boundObject;

    GameObject hudRoot, ringPanel;
    Text stateText, altText, warnText;
    Text speedText, ringTitle, ringSub, courseBtnLabel, popupText;
    Image gaugeFill;
    float popupStart, popupUntil;

    void Start()
    {
        course = GetComponent<CheckpointCourse>();
        tutorial = GetComponent<TutorialCloud>();
        BuildHud();
        hudRoot.SetActive(false);

        course.OnRingCleared += n =>
        {
            if (n < CheckpointCourse.Total) Popup("RING " + n + " / " + CheckpointCourse.Total, new Color(0.3f, 1f, 0.5f), 1.0f);
        };
        course.OnFinished += () =>
            Popup("COURSE COMPLETE!  " + Fmt(course.Elapsed), new Color(1f, 0.85f, 0.2f), 4f);
        course.OnMessage += m => Popup(m, new Color(1f, 0.6f, 0.25f), 1.6f);
    }
    
    void Update()
    {
        if (placer == null) placer = FindAnyObjectByType<PlaceOnPlane>();
        if (!skinned) skinned = TrySkin();
        if (placer == null) return;

        var spawned = placer.Spawned;
        if (spawned == null)
        {
            if (bound)
            {
                bound = false; drone = null; boundObject = null;
                course.Unbind(); tutorial.Unbind();
                hudRoot.SetActive(false);
            }
        }
        else if (!bound || boundObject != spawned)
        {
            drone = spawned.GetComponent<DroneController>();
            if (drone != null)
            {
                bound = true; boundObject = spawned;
                course.Bind(drone); tutorial.Bind(drone);
                hudRoot.SetActive(true);
            }
        }

        if (drone != null) UpdateHud();
    }

    // ---------------- HUD ----------------
    void BuildHud()
    {
        var canvas = UIKit.MakeCanvas("GameHudCanvas", -1);   // behind the controller canvas
        hudRoot = canvas.gameObject;
        Transform t = canvas.transform;
        var TL = UIKit.V(0, 1);

        var badge = UIKit.Panel("Badge", t, UIKit.Rounded, new Color(0.03f, 0.07f, 0.14f, 0.62f), TL, TL,
                                new Vector2(20, -16), new Vector2(500, 170));
        UIKit.Glow(badge, new Color(0.25f, 0.9f, 1f, 0.7f));
        speedText = UIKit.Label("Speed", t, "SPD 0.00 m/s", 30, new Color(0.8f, 1f, 0.8f), TL, TL,
                                new Vector2(40, -126), new Vector2(460, 40), TextAnchor.MiddleLeft);

        // altitude gauge (left side)
        var L = UIKit.V(0, 0.5f);
        var gbg = UIKit.Panel("GaugeBg", t, UIKit.Rounded, new Color(0.03f, 0.07f, 0.14f, 0.6f), L, L,
                              new Vector2(24, -20), new Vector2(40, 360));
        UIKit.Glow(gbg, new Color(0.25f, 0.9f, 1f, 0.6f));
        gaugeFill = UIKit.Panel("GaugeFill", t, UIKit.Square, new Color(0.25f, 0.9f, 1f, 0.9f), L, L,
                                new Vector2(30, -20), new Vector2(28, 346));
        gaugeFill.type = Image.Type.Filled;
        gaugeFill.fillMethod = Image.FillMethod.Vertical;
        gaugeFill.fillOrigin = (int)Image.OriginVertical.Bottom;
        gaugeFill.fillAmount = 0f;
        UIKit.Label("GaugeTag", t, "ALT", 24, new Color(0.7f, 0.95f, 1f), L, UIKit.V(0.5f, 0),
                    new Vector2(44, 168), new Vector2(80, 30), TextAnchor.MiddleCenter);

        // checkpoint button (bottom centre, below the ARM/LAND row)
        courseBtnLabel = MakeButton(t, "START CHECKPOINTS", UIKit.V(0.5f, 0), UIKit.V(0.5f, 0),
                                    new Vector2(0, 8), new Vector2(420, 60), new Color(0.55f, 0.25f, 0.85f, 0.92f), ToggleCourse);

        // ring counter (top right, under RESET)
        var TR = UIKit.V(1, 1);
        var rp = UIKit.Panel("RingPanel", t, UIKit.Rounded, new Color(0.03f, 0.07f, 0.14f, 0.62f), TR, TR,
                             new Vector2(-30, -112), new Vector2(400, 100));
        UIKit.Glow(rp, new Color(1f, 0.8f, 0.1f, 0.8f));
        ringPanel = rp.gameObject;
        ringTitle = UIKit.Label("RingTitle", rp.transform, "", 40, new Color(1f, 0.85f, 0.2f), UIKit.V(0.5f, 0.5f), UIKit.V(0.5f, 0.5f),
                                new Vector2(0, 18), new Vector2(380, 50), TextAnchor.MiddleCenter);
        ringSub = UIKit.Label("RingSub", rp.transform, "", 26, Color.white, UIKit.V(0.5f, 0.5f), UIKit.V(0.5f, 0.5f),
                              new Vector2(0, -24), new Vector2(380, 36), TextAnchor.MiddleCenter);
        ringPanel.SetActive(false);

        popupText = UIKit.Label("Popup", t, "", 64, Color.yellow, UIKit.V(0.5f, 0.5f), UIKit.V(0.5f, 0.5f),
                                new Vector2(0, 60), new Vector2(1400, 100), TextAnchor.MiddleCenter);
    }

    Text MakeButton(Transform parent, string label, Vector2 anchor, Vector2 pivot, Vector2 pos, Vector2 size,
                    Color color, UnityEngine.Events.UnityAction onClick)
    {
        var img = UIKit.Panel(label + "Btn", parent, UIKit.Rounded, color, anchor, pivot, pos, size);
        img.raycastTarget = true;
        var b = img.gameObject.AddComponent<Button>();
        b.targetGraphic = img;
        b.onClick.AddListener(onClick);
        UIKit.Glow(img, new Color(1f, 1f, 1f, 0.55f));
        img.gameObject.AddComponent<PressScale>();
        return UIKit.Label("Label", img.transform, label, 32, Color.white, UIKit.V(0.5f, 0.5f), UIKit.V(0.5f, 0.5f),
                           Vector2.zero, size, TextAnchor.MiddleCenter);
    }

    void ToggleCourse()
    {
        if (drone == null) return;
        if (tutorial.Stage == 5 && !tutorial.Finished)
        {
            Popup("Finish the tutorial step first", new Color(1f, 0.6f, 0.25f), 1.5f);
            return;
        }
        if (course.Running) course.Stop();
        else course.Begin();
    }

    void Popup(string msg, Color col, float seconds)
    {
        if (popupText == null) return;
        popupText.text = msg;
        popupText.color = col;
        popupStart = Time.time;
        popupUntil = Time.time + seconds;
    }

    static string Fmt(float t)
    {
        int m = (int)(t / 60f);
        float s = t - m * 60f;
        return m + ":" + s.ToString("00.0");
    }

    static Color StateColor(FlightState s)
    {
        switch (s)
        {
            case FlightState.Disarmed: return new Color(1f, 0.45f, 0.4f);
            case FlightState.Armed: return new Color(1f, 0.8f, 0.2f);
            case FlightState.TakingOff: return new Color(0.4f, 0.9f, 1f);
            case FlightState.Flying: return new Color(0.4f, 1f, 0.5f);
            case FlightState.Hovering: return new Color(0.3f, 1f, 0.8f);
            case FlightState.Landing: return new Color(1f, 0.65f, 0.25f);
            default: return new Color(0.6f, 0.75f, 1f);
        }
    }

    void UpdateHud()
    {
        speedText.text = "SPD " + drone.Velocity.magnitude.ToString("0.00") + " m/s";
        gaugeFill.fillAmount = Mathf.Clamp01(drone.Altitude / GaugeMax);
        if (stateText != null) stateText.color = StateColor(drone.State);

        bool show = course.Running || course.Finished;
        ringPanel.SetActive(show);
        if (show)
        {
            ringTitle.text = "RINGS " + course.Cleared + " / " + CheckpointCourse.Total;
            ringSub.text = "TIME " + Fmt(course.Elapsed) + "    MISS " + course.Misses;
        }
        courseBtnLabel.text = course.Running ? "STOP CHECKPOINTS" : "START CHECKPOINTS";

        if (Time.time < popupUntil)
        {
            float k = Mathf.Clamp01((Time.time - popupStart) / 0.2f);
            popupText.rectTransform.localScale = Vector3.one * Mathf.Lerp(1.4f, 1f, k);
            var c = popupText.color; c.a = Mathf.Clamp01((popupUntil - Time.time) / 0.4f);
            popupText.color = c;
        }
        else if (popupText.text != "") popupText.text = "";
    }

    // ---------------- Skin for the existing ControllerUI ----------------
    bool TrySkin()
    {
        var canvasGo = GameObject.Find("ControllerCanvas");
        if (canvasGo == null) return false;
        var controls = canvasGo.transform.Find("Controls");
        if (controls == null) return false;

        foreach (var b in controls.GetComponentsInChildren<Button>(true)) StyleButton(b);
        foreach (var s in controls.GetComponentsInChildren<VirtualStick>(true)) StyleStick(s);

        stateText = FindText(controls, "State");
        altText = FindText(controls, "Altitude");
        warnText = FindText(controls, "Warning");
        var TL = UIKit.V(0, 1);
        Place(stateText, TL, TL, new Vector2(40, -26), new Vector2(460, 56), 46, TextAnchor.MiddleLeft);
        Place(altText, TL, TL, new Vector2(40, -80), new Vector2(460, 44), 34, TextAnchor.MiddleLeft);
        Place(warnText, UIKit.V(0.5f, 0.5f), UIKit.V(0.5f, 0.5f), new Vector2(0, 170), new Vector2(1400, 70), 46, TextAnchor.MiddleCenter);
        return true;
    }

    static Text FindText(Transform root, string n)
    {
        var t = root.Find(n);
        return t != null ? t.GetComponent<Text>() : null;
    }

    static void Place(Text t, Vector2 anchor, Vector2 pivot, Vector2 pos, Vector2 size, int fontSize, TextAnchor a)
    {
        if (t == null) return;
        var rt = t.rectTransform;
        rt.anchorMin = rt.anchorMax = anchor;
        rt.pivot = pivot;
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;
        t.fontSize = fontSize;
        t.alignment = a;
        t.fontStyle = FontStyle.Bold;
    }

    static void StyleButton(Button b)
    {
        var img = b.GetComponent<Image>();
        if (img == null) return;
        img.sprite = UIKit.Rounded;
        img.type = Image.Type.Sliced;
        var c = img.color;
        img.color = new Color(c.r * 0.9f, c.g * 0.9f, c.b * 0.9f, 0.92f);
        UIKit.Glow(img, new Color(1f, 1f, 1f, 0.55f));
        b.gameObject.AddComponent<PressScale>();
        var t = b.GetComponentInChildren<Text>();
        if (t != null) t.fontStyle = FontStyle.Bold;
    }

    static void StyleStick(VirtualStick s)
    {
        var bg = s.GetComponent<Image>();
        if (bg != null)
        {
            bg.color = new Color(0.05f, 0.15f, 0.25f, 0.35f);
            UIKit.Glow(bg, new Color(0.25f, 0.9f, 1f, 0.8f));
        }
        var inner = UIKit.Panel("Inner", s.transform, UIKit.Circle, new Color(0.3f, 0.9f, 1f, 0.12f),
                                UIKit.V(0.5f, 0.5f), UIKit.V(0.5f, 0.5f), Vector2.zero, new Vector2(190, 190));
        inner.transform.SetAsFirstSibling();

        var h = s.transform.Find("Handle");
        if (h != null)
        {
            var hi = h.GetComponent<Image>();
            if (hi != null)
            {
                hi.color = new Color(0.2f, 0.9f, 1f, 0.9f);
                UIKit.Glow(hi, new Color(1f, 1f, 1f, 0.6f));
            }
        }
        string lbl = s.name == "LeftStick" ? "THROTTLE / YAW" : "PITCH / ROLL";
        UIKit.Label("Tag", s.transform, lbl, 26, new Color(0.7f, 0.95f, 1f), UIKit.V(0.5f, 1f), UIKit.V(0.5f, 0f),
                    new Vector2(0, 6), new Vector2(300, 34), TextAnchor.MiddleCenter);
    }
}

public class PressScale : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
{
    Vector3 baseScale = Vector3.one;
    void Awake() { baseScale = transform.localScale; }
    public void OnPointerDown(PointerEventData e) { transform.localScale = baseScale * 0.92f; }
    public void OnPointerUp(PointerEventData e) { transform.localScale = baseScale; }
    public void OnPointerExit(PointerEventData e) { transform.localScale = baseScale; }
    void OnDisable() { transform.localScale = baseScale; }
}

public static class UIKit
{
    static Sprite rounded, circle, square;
    static Font font;

    public static Vector2 V(float x, float y) { return new Vector2(x, y); }

    public static Font Font
    {
        get
        {
            if (font == null) font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (font == null) font = Font.CreateDynamicFontFromOSFont("Arial", 16);
            return font;
        }
    }

    public static Sprite Rounded { get { if (rounded == null) rounded = MakeSprite(64, 22f, true); return rounded; } }
    public static Sprite Circle { get { if (circle == null) circle = MakeSprite(128, 64f, false); return circle; } }
    public static Sprite Square { get { if (square == null) square = MakeSprite(4, 0f, false); return square; } }

    static Sprite MakeSprite(int s, float r, bool slice)
    {
        var tex = new Texture2D(s, s, TextureFormat.RGBA32, false);
        tex.wrapMode = TextureWrapMode.Clamp;
        var px = new Color[s * s];
        float half = s / 2f;
        for (int y = 0; y < s; y++)
            for (int x = 0; x < s; x++)
            {
                float qx = Mathf.Abs(x + 0.5f - half) - (half - r);
                float qy = Mathf.Abs(y + 0.5f - half) - (half - r);
                float d = new Vector2(Mathf.Max(qx, 0f), Mathf.Max(qy, 0f)).magnitude + Mathf.Min(Mathf.Max(qx, qy), 0f) - r;
                px[y * s + x] = new Color(1f, 1f, 1f, Mathf.Clamp01(0.5f - d));
            }
        tex.SetPixels(px);
        tex.Apply();
        Vector4 border = slice ? new Vector4(r, r, r, r) : Vector4.zero;
        return Sprite.Create(tex, new Rect(0, 0, s, s), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, border);
    }

    public static Canvas MakeCanvas(string name, int order)
    {
        var go = new GameObject(name);
        var c = go.AddComponent<Canvas>();
        c.renderMode = RenderMode.ScreenSpaceOverlay;
        c.sortingOrder = order;
        var sc = go.AddComponent<CanvasScaler>();
        sc.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        sc.referenceResolution = new Vector2(1920, 1080);
        sc.matchWidthOrHeight = 0.5f;
        go.AddComponent<GraphicRaycaster>();
        return c;
    }

    public static RectTransform Rect(string name, Transform parent, Vector2 anchor, Vector2 pivot, Vector2 pos, Vector2 size)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var rt = (RectTransform)go.transform;
        rt.anchorMin = rt.anchorMax = anchor;
        rt.pivot = pivot;
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;
        return rt;
    }

    public static Image Panel(string name, Transform parent, Sprite sprite, Color color,
                              Vector2 anchor, Vector2 pivot, Vector2 pos, Vector2 size)
    {
        var rt = Rect(name, parent, anchor, pivot, pos, size);
        var img = rt.gameObject.AddComponent<Image>();
        img.sprite = sprite;
        img.color = color;
        img.raycastTarget = false;
        if (sprite == rounded) img.type = Image.Type.Sliced;
        return img;
    }

    public static Text Label(string name, Transform parent, string text, int size, Color color,
                             Vector2 anchor, Vector2 pivot, Vector2 pos, Vector2 box, TextAnchor align)
    {
        var rt = Rect(name, parent, anchor, pivot, pos, box);
        var t = rt.gameObject.AddComponent<Text>();
        t.font = Font;
        t.text = text;
        t.fontSize = size;
        t.color = color;
        t.alignment = align;
        t.fontStyle = FontStyle.Bold;
        t.horizontalOverflow = HorizontalWrapMode.Wrap;
        t.verticalOverflow = VerticalWrapMode.Overflow;
        t.raycastTarget = false;
        var o = rt.gameObject.AddComponent<Outline>();
        o.effectColor = new Color(0f, 0f, 0f, 0.9f);
        o.effectDistance = new Vector2(2, -2);
        return t;
    }

    public static void Glow(Graphic g, Color c)
    {
        var o = g.gameObject.AddComponent<Outline>();
        o.effectColor = c;
        o.effectDistance = new Vector2(3, -3);
    }
}