// Assets/Scripts/Game/CheckpointCourse.cs
using System;
using System.Collections.Generic;
using UnityEngine;

public static class RingFactory
{
    static Material baseMat;

    public static Material Mat(Color c)
    {
        if (baseMat == null)
        {
            var tmp = GameObject.CreatePrimitive(PrimitiveType.Cube);
            baseMat = tmp.GetComponent<Renderer>().sharedMaterial;
            UnityEngine.Object.Destroy(tmp);
        }
        var m = new Material(baseMat);
        if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c);
        if (m.HasProperty("_Color")) m.SetColor("_Color", c);
        return m;
    }

    // Ring lies in the local XY plane (normal = local Z), made of small bars.
    public static Renderer[] Build(Transform parent, float radius, int count, float thickness, Material mat)
    {
        var list = new Renderer[count];
        float seg = 2f * Mathf.PI * radius / count;
        for (int i = 0; i < count; i++)
        {
            float a = i * 360f / count;
            float rad = a * Mathf.Deg2Rad;
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            UnityEngine.Object.Destroy(go.GetComponent<Collider>());
            go.name = "Seg";
            var t = go.transform;
            t.SetParent(parent, false);
            t.localPosition = new Vector3(Mathf.Cos(rad), Mathf.Sin(rad), 0f) * radius;
            t.localRotation = Quaternion.Euler(0f, 0f, a);
            t.localScale = new Vector3(thickness, seg * 1.1f, thickness);
            var r = go.GetComponent<Renderer>();
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            r.sharedMaterial = mat;
            list[i] = r;
        }
        return list;
    }

    public static void SetMat(Renderer[] rs, Material m)
    {
        foreach (var r in rs) r.sharedMaterial = m;
    }
}

// 10 rings on a circle through the spawn point. A ring counts only when the drone
// crosses its plane inside the ring. Rings are cleared in order.
public class CheckpointCourse : MonoBehaviour
{
    public const int Total = 10;

    [SerializeField] float circleRadius = 0.9f;   // course stays within 1.8 m of spawn
    [SerializeField] float ringRadius = 0.25f;
    [SerializeField] float passTolerance = 1.2f;  // x ring radius

    public int Cleared { get; private set; }
    public int Misses { get; private set; }
    public float Elapsed { get; private set; }
    public bool Running { get; private set; }
    public bool Finished { get; private set; }

    public event Action<int> OnRingCleared;
    public event Action OnFinished;
    public event Action<string> OnMessage;

    DroneController drone;
    Vector3 origin, forward;
    GameObject root;
    readonly List<Transform> ringRoots = new List<Transform>();
    readonly List<Renderer[]> ringSegs = new List<Renderer[]>();
    readonly List<Vector3> centers = new List<Vector3>();
    readonly List<Vector3> normals = new List<Vector3>();
    Material matIdle, matActive, matDone;
    float prevSide;

    public void Bind(DroneController d)
    {
        Stop();
        drone = d;
        origin = d.Position;
        forward = Vector3.ProjectOnPlane(d.transform.forward, Vector3.up);
        if (forward.sqrMagnitude < 0.001f) forward = Vector3.forward;
        forward.Normalize();
        Cleared = 0; Misses = 0; Elapsed = 0f; Finished = false;
    }

    public void Unbind()
    {
        Stop();
        drone = null;
        Cleared = 0; Misses = 0; Elapsed = 0f; Finished = false;
    }

    public void Begin()
    {
        if (drone == null) return;
        Stop();
        Build();
        Cleared = 0; Misses = 0; Elapsed = 0f; Finished = false;
        Running = true;
        Highlight(0);
        prevSide = Side(0);
        OnMessage?.Invoke("GO! Fly through the gold ring");
    }

    public void Stop()
    {
        Running = false;
        if (root != null) Destroy(root);
        root = null;
        ringRoots.Clear(); ringSegs.Clear(); centers.Clear(); normals.Clear();
    }

    void OnDestroy() { Stop(); }

    void Build()
    {
        root = new GameObject("CheckpointCourse");
        matIdle = RingFactory.Mat(new Color(0.1f, 0.85f, 1f));
        matActive = RingFactory.Mat(new Color(1f, 0.8f, 0.1f));
        matDone = RingFactory.Mat(new Color(0.2f, 1f, 0.4f));

        Vector3 right = Vector3.Cross(Vector3.up, forward);
        Vector3 center = origin + forward * circleRadius;

        for (int i = 0; i < Total; i++)
        {
            float th = (18f + 36f * i) * Mathf.Deg2Rad;
            Vector3 flat = -forward * Mathf.Cos(th) + right * Mathf.Sin(th);
            Vector3 tangent = (forward * Mathf.Sin(th) + right * Mathf.Cos(th)).normalized;
            Vector3 pos = center + flat * circleRadius;
            pos.y = origin.y + 0.9f + 0.3f * Mathf.Sin(i * 1.2f);

            var go = new GameObject("Ring " + (i + 1));
            go.transform.SetParent(root.transform, false);
            go.transform.position = pos;
            go.transform.rotation = Quaternion.LookRotation(tangent, Vector3.up);
            var segs = RingFactory.Build(go.transform, ringRadius, 28, 0.03f, matIdle);

            ringRoots.Add(go.transform);
            ringSegs.Add(segs);
            centers.Add(pos);
            normals.Add(tangent);
        }
    }

    void Highlight(int active)
    {
        for (int i = 0; i < ringSegs.Count; i++)
        {
            Material m = i < active ? matDone : (i == active ? matActive : matIdle);
            RingFactory.SetMat(ringSegs[i], m);
        }
    }

    float Side(int i)
    {
        return Vector3.Dot(drone.Position - centers[i], normals[i]);
    }

    void Update()
    {
        if (!Running || drone == null || Cleared >= ringRoots.Count) return;
        Elapsed += Time.deltaTime;

        int i = Cleared;
        ringRoots[i].localScale = Vector3.one * (1f + 0.08f * Mathf.Sin(Time.time * 6f));

        Vector3 rel = drone.Position - centers[i];
        float side = Vector3.Dot(rel, normals[i]);
        if (prevSide * side < 0f)
        {
            float lateral = (rel - normals[i] * side).magnitude;
            if (lateral <= ringRadius * passTolerance) Pass();
            else
            {
                Misses++;
                OnMessage?.Invoke("Missed ring " + (i + 1) + " - go through the centre");
            }
        }
        prevSide = side;
    }

    void Pass()
    {
        ringRoots[Cleared].localScale = Vector3.one;
        Cleared++;
        if (Cleared >= Total)
        {
            Running = false;
            Finished = true;
            Highlight(Total);
            OnRingCleared?.Invoke(Cleared);
            OnFinished?.Invoke();
            return;
        }
        Highlight(Cleared);
        prevSide = Side(Cleared);
        OnRingCleared?.Invoke(Cleared);
    }
}