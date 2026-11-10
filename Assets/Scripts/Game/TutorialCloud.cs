// Assets/Scripts/Game/TutorialCloud.cs
using UnityEngine;
using UnityEngine.UI;

// 7-stage tutorial. Each stage advances ONLY when the real action is detected.
public class TutorialCloud : MonoBehaviour
{
    const int StageCount = 7;
    const float HoverRadius = 0.35f, HoverSeconds = 5f, LandRadius = 0.6f, StuckSeconds = 15f;

    static readonly string[] Titles =
        { "CONTROLLER", "ARM", "TAKE OFF", "HOVER", "MOVE", "NAVIGATE", "LAND" };
    static readonly string[] Info =
    {
        "Drag the LEFT stick (throttle / yaw) and the RIGHT stick (pitch / roll).",
        "Press the green ARM button to start the motors.",
        "Press TAKE OFF and climb to 1 metre.",
        "Hold the drone steady inside the green circle for 5 seconds.",
        "Use the right stick to fly forward, back, left and right.",
        "Fly through 3 glowing rings. The gold ring is next.",
        "Land inside the green circle on the floor, then press DISARM."
    };
    static readonly string[] Hints =
    {
        "Touch and drag inside each circle.",
        "ARM is the green button, bottom centre.",
        "TAKE OFF climbs by itself.",
        "Let go of both sticks to hover.",
        "Push the RIGHT stick in every direction.",
        "Aim at the centre of the gold ring.",
        "Move over the circle, press LAND."
    };

    public int Stage { get; private set; }
    public bool Finished { get { return Stage >= StageCount; } }

    DroneController drone;
    CheckpointCourse course;
    GameObject root;
    RectTransform cloud;
    Text titleText, bodyText, statusText, hintText;
    readonly Image[] dots = new Image[StageCount];
    VirtualStick left, right;

    float stageStart, popStart, flashUntil, hoverHold;
    bool leftSeen, rightSeen, fwd, back, lft, rgt, landedOk;
    Vector3 spawnPos, hoverCenter;
    GameObject zoneRing;

    static readonly Color Cyan = new Color(0.25f, 0.9f, 1f);
    static readonly Color Green = new Color(0.3f, 1f, 0.5f);
    static readonly Color Grey = new Color(1f, 1f, 1f, 0.25f);

    void Awake()
    {
        BuildUI();
        root.SetActive(false);
    }

    public void Bind(DroneController d)
    {
        ClearZone();
        drone = d;
        course = FindAnyObjectByType<CheckpointCourse>();
        spawnPos = d.Position;
        root.SetActive(true);
        StartStage(0);
    }

    public void Unbind()
    {
        ClearZone();
        drone = null;
        if (root != null) root.SetActive(false);
    }

    void OnDestroy() { ClearZone(); }

    void ClearZone()
    {
        if (zoneRing != null) Destroy(zoneRing);
        zoneRing = null;
    }

    GameObject MakeFlatRing(Vector3 pos, float radius, Color col)
    {
        var go = new GameObject("TutorialZone");
        go.transform.position = pos;
        go.transform.rotation = Quaternion.LookRotation(Vector3.up, Vector3.forward);
        RingFactory.Build(go.transform, radius, 40, 0.03f, RingFactory.Mat(col));
        return go;
    }

    void StartStage(int s)
    {
        Stage = s;
        stageStart = Time.time;
        popStart = Time.time;
        flashUntil = 0f;
        ClearZone();
        switch (s)
        {
            case 0: leftSeen = rightSeen = false; break;
            case 3:
                hoverCenter = drone.Position;
                hoverHold = 0f;
                zoneRing = MakeFlatRing(hoverCenter, HoverRadius, Green);
                break;
            case 4: fwd = back = lft = rgt = false; break;
            case 5: if (course != null) course.Begin(); break;
            case 6:
                if (course != null) course.Stop();
                landedOk = false;
                zoneRing = MakeFlatRing(new Vector3(spawnPos.x, spawnPos.y + 0.02f, spawnPos.z), LandRadius, Green);
                break;
        }
        RefreshStatic();
    }

    void RefreshStatic()
    {
        if (Finished)
        {
            titleText.text = "TUTORIAL COMPLETE";
            titleText.color = Green;
            bodyText.text = "Great flying! Now try START CHECKPOINTS at the bottom: 10 rings, beat your time.";
        }
        else
        {
            titleText.text = "STEP " + (Stage + 1) + "/" + StageCount + "  -  " + Titles[Stage];
            titleText.color = Cyan;
            bodyText.text = Info[Stage];
        }
        statusText.text = "";
        hintText.text = "";
        for (int i = 0; i < StageCount; i++)
            dots[i].color = i < Stage ? Green : (i == Stage ? Cyan : Grey);
    }

    void FindSticks()
    {
        if (left != null && right != null) return;
        foreach (var s in FindObjectsByType<VirtualStick>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (s.name == "LeftStick") left = s;
            else if (s.name == "RightStick") right = s;
        }
    }

    void Update()
    {
        if (drone == null) return;
        FindSticks();

        float k = Mathf.Clamp01((Time.time - popStart) / 0.25f);
        cloud.localScale = Vector3.one * Mathf.Lerp(0.92f, 1f, k);

        if (Finished) return;

        if (flashUntil > 0f)
        {
            if (Time.time < flashUntil) return;
            StartStage(Stage + 1);
            return;
        }

        string status = "";
        bool done = false;
        switch (Stage)
        {
            case 0: done = CheckSticks(out status); break;
            case 1: done = CheckArm(out status); break;
            case 2: done = CheckTakeoff(out status); break;
            case 3: done = CheckHover(out status); break;
            case 4: done = CheckMove(out status); break;
            case 5: done = CheckNav(out status); break;
            case 6: done = CheckLand(out status); break;
        }
        statusText.text = status;
        hintText.text = (Time.time - stageStart > StuckSeconds) ? "HINT: " + Hints[Stage] : "";

        if (done) Complete();
    }

    void Complete()
    {
        titleText.text = "STEP " + (Stage + 1) + " COMPLETE!";
        titleText.color = Green;
        statusText.text = "";
        hintText.text = "";
        dots[Stage].color = Green;
        flashUntil = Time.time + 1.1f;
    }

    static string Box(bool b) { return b ? "[x]" : "[ ]"; }

    bool CheckSticks(out string status)
    {
        if (left != null && left.Value.magnitude > 0.5f) leftSeen = true;
        if (right != null && right.Value.magnitude > 0.5f) rightSeen = true;
        status = "Left stick " + Box(leftSeen) + "    Right stick " + Box(rightSeen);
        return leftSeen && rightSeen;
    }

    bool CheckArm(out string status)
    {
        status = "State: " + drone.State;
        return drone.State != FlightState.Disarmed;
    }

    bool CheckTakeoff(out string status)
    {
        status = "Altitude " + drone.Altitude.ToString("0.00") + " / 1.00 m";
        bool air = drone.State == FlightState.Flying || drone.State == FlightState.Hovering;
        return air && drone.Altitude >= 0.85f;
    }

    bool CheckHover(out string status)
    {
        float drift = Vector3.Distance(drone.Position, hoverCenter);
        bool air = drone.State == FlightState.Flying || drone.State == FlightState.Hovering;
        if (!air)
        {
            hoverHold = 0f;
            status = "The drone must stay in the air - take off again";
            return false;
        }
        if (drift <= HoverRadius) hoverHold += Time.deltaTime;
        else hoverHold = 0f;

        status = drift <= HoverRadius
            ? "Hold " + hoverHold.ToString("0.0") + " / " + HoverSeconds.ToString("0.0") + " s    Drift " + drift.ToString("0.00") + " m"
            : "DRIFTING! " + drift.ToString("0.00") + " m - return to the green circle";
        return hoverHold >= HoverSeconds;
    }

    bool CheckMove(out string status)
    {
        Vector3 v = drone.transform.InverseTransformDirection(drone.Velocity);
        if (v.z > 0.35f) fwd = true;
        if (v.z < -0.35f) back = true;
        if (v.x > 0.35f) rgt = true;
        if (v.x < -0.35f) lft = true;
        status = "FORWARD " + Box(fwd) + "  BACK " + Box(back) + "  LEFT " + Box(lft) + "  RIGHT " + Box(rgt);
        return fwd && back && lft && rgt;
    }

    bool CheckNav(out string status)
    {
        if (course == null) { status = "Checkpoint course missing"; return false; }
        if (!course.Running && course.Cleared < 3) course.Begin();
        status = "Rings " + course.Cleared + " / 3    Missed " + course.Misses;
        return course.Cleared >= 3;
    }

    bool CheckLand(out string status)
    {
        Vector3 d = drone.Position - spawnPos;
        d.y = 0f;
        float dist = d.magnitude;

        if (drone.State == FlightState.TakingOff) landedOk = false;
        if (drone.State == FlightState.Landed && dist <= LandRadius) landedOk = true;

        if (drone.State == FlightState.Landed && !landedOk)
            status = "Landed outside the circle - take off and land inside it";
        else if (landedOk && drone.State != FlightState.Disarmed)
            status = "Landed in the circle! Now press DISARM";
        else if (drone.State == FlightState.Disarmed && !landedOk)
            status = "ARM, take off, then land inside the circle";
        else
            status = "Distance to circle centre " + dist.ToString("0.00") + " m";

        return drone.State == FlightState.Disarmed && landedOk;
    }

    // ---------------- UI ----------------
    void BuildUI()
    {
        var canvas = UIKit.MakeCanvas("TutorialCanvas", 6);
        root = canvas.gameObject;
        Transform t = canvas.transform;

        cloud = UIKit.Rect("Cloud", t, UIKit.V(0.5f, 1f), UIKit.V(0.5f, 1f), new Vector2(0, -12), new Vector2(1000, 200));
        var body = UIKit.Panel("Body", cloud, UIKit.Rounded, new Color(0.04f, 0.09f, 0.18f, 0.78f),
                               UIKit.V(0.5f, 0.5f), UIKit.V(0.5f, 0.5f), Vector2.zero, new Vector2(1000, 200));
        UIKit.Glow(body, new Color(0.25f, 0.9f, 1f, 0.8f));
        UIKit.Panel("Accent", cloud, UIKit.Square, Cyan, UIKit.V(0f, 0.5f), UIKit.V(0f, 0.5f),
                    new Vector2(16, 0), new Vector2(6, 150));

        titleText = UIKit.Label("Title", cloud, "", 38, Cyan, UIKit.V(0, 1), UIKit.V(0, 1),
                                new Vector2(36, -10), new Vector2(930, 48), TextAnchor.MiddleLeft);
        bodyText = UIKit.Label("Info", cloud, "", 30, Color.white, UIKit.V(0, 1), UIKit.V(0, 1),
                               new Vector2(36, -58), new Vector2(930, 74), TextAnchor.UpperLeft);
        statusText = UIKit.Label("Status", cloud, "", 28, new Color(1f, 0.9f, 0.4f), UIKit.V(0, 1), UIKit.V(0, 1),
                                 new Vector2(36, -134), new Vector2(930, 36), TextAnchor.MiddleLeft);
        hintText = UIKit.Label("Hint", cloud, "", 24, new Color(1f, 0.6f, 0.25f), UIKit.V(1, 0), UIKit.V(1, 0),
                               new Vector2(-30, 6), new Vector2(700, 32), TextAnchor.MiddleRight);

        for (int i = 0; i < StageCount; i++)
            dots[i] = UIKit.Panel("Dot" + i, cloud, UIKit.Circle, Grey, UIKit.V(0, 0), UIKit.V(0, 0),
                                  new Vector2(36 + i * 30, 10), new Vector2(16, 16));
    }
}