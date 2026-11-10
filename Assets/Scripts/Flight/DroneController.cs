// Assets/Scripts/Flight/DroneController.cs
using System;
using UnityEngine;

public class DroneController : MonoBehaviour
{
    [Header("Visual (the child that tilts)")]
    [SerializeField] Transform model;

    [Header("Flight model (tunable)")]
    [SerializeField] float maxHorizontalSpeed = 1.0f;  // m/s
    [SerializeField] float maxClimbSpeed = 0.8f;       // m/s
    [SerializeField] float yawRate = 90f;              // deg/s
    [SerializeField] float maxTiltDegrees = 20f;
    [SerializeField] float maxAltitude = 2.0f;         // m above ground
    [SerializeField] float minFlyAltitude = 0.1f;      // floor while flying
    [SerializeField] float takeoffAltitude = 1.0f;     // m
    [SerializeField] float landSpeed = 0.4f;           // m/s
    [SerializeField] float maxRadius = 2.0f;           // fly area around the spawn point
    [SerializeField] float responsiveness = 4f;        // higher = snappier
    [SerializeField] float tiltSmoothing = 6f;
    [SerializeField] float inputDeadzone = 0.05f;

    // ---- Public interface (as agreed) ----
    public FlightState State { get; private set; } = FlightState.Disarmed;
    public float Altitude => transform.position.y - groundY;
    public Vector3 Position => transform.position;
    public Vector3 Velocity => velocity;

    public event Action<FlightState> OnStateChanged;
    public event Action OnCollision;
    public event Action<string> OnInvalidAction;

    // ---- Private ----
    ControlInput input;
    Vector3 velocity;
    float groundY;
    Vector3 origin;
    Quaternion modelBaseRotation = Quaternion.identity;

    void Awake()
    {
        groundY = transform.position.y;   // spawn point = ground level
        origin = transform.position;

        if (model == null && transform.childCount > 0)
            model = transform.GetChild(0);
        if (model != null)
            modelBaseRotation = model.localRotation;   // keeps the (90,0,0) import rotation
    }

    // ================= Commands (the "buttons") =================
    public void Arm()
    {
        if (State != FlightState.Disarmed) { Invalid("Drone is already armed"); return; }
        SetState(FlightState.Armed);
    }

    public void Disarm()
    {
        switch (State)
        {
            case FlightState.Armed:
            case FlightState.Landed:
                SetState(FlightState.Disarmed); break;
            case FlightState.Disarmed:
                Invalid("Drone is already disarmed"); break;
            default:
                Invalid("Land the drone before disarming"); break;
        }
    }

    public void TakeOff()
    {
        switch (State)
        {
            case FlightState.Armed:
            case FlightState.Landed:
                SetState(FlightState.TakingOff); break;
            case FlightState.Disarmed:
                Invalid("Arm the drone before takeoff"); break;
            default:
                Invalid("Drone is already in the air"); break;
        }
    }

    public void Land()
    {
        switch (State)
        {
            case FlightState.TakingOff:
            case FlightState.Flying:
            case FlightState.Hovering:
                SetState(FlightState.Landing); break;
            case FlightState.Landing:
                Invalid("Drone is already landing"); break;
            default:
                Invalid("Drone is already on the ground"); break;
        }
    }

    // Joysticks call this every frame. Values are clamped to -1..1.
    public void SetInput(ControlInput newInput)
    {
        input.throttle = Mathf.Clamp(newInput.throttle, -1f, 1f);
        input.yaw      = Mathf.Clamp(newInput.yaw,      -1f, 1f);
        input.pitch    = Mathf.Clamp(newInput.pitch,    -1f, 1f);
        input.roll     = Mathf.Clamp(newInput.roll,     -1f, 1f);
    }

    // ADDITION to the agreed interface: obstacles will call this later.
    public void ReportCollision() => OnCollision?.Invoke();

    // ================= Simulation =================
    void Update()
    {
        float dt = Time.deltaTime;

        switch (State)
        {
            case FlightState.TakingOff: UpdateTakeOff(dt); break;
            case FlightState.Flying:
            case FlightState.Hovering:  UpdateFlight(dt);  break;
            case FlightState.Landing:   UpdateLanding(dt); break;
            default:                    Settle(dt);        break; // on the ground
        }

        UpdateTilt(dt);
    }

    void UpdateTakeOff(float dt)
    {
        float remaining = takeoffAltitude - Altitude;
        if (remaining <= 0.05f)
        {
            SetState(FlightState.Hovering);
            return;
        }
        float climb = Mathf.Clamp(remaining * 2f, 0.15f, maxClimbSpeed); // slows near the target
        Move(Vector3.zero, climb, 0f, 0f, dt);
    }

    void UpdateFlight(float dt)
    {
        Vector3 planar = new Vector3(input.roll, 0f, input.pitch);
        if (planar.magnitude > 1f) planar.Normalize();
        Vector3 horizontal = (transform.right * planar.x + transform.forward * planar.z) * maxHorizontalSpeed;

        Move(horizontal, input.throttle * maxClimbSpeed, input.yaw, minFlyAltitude, dt);

        bool moving = Mathf.Abs(input.throttle) > inputDeadzone || Mathf.Abs(input.yaw) > inputDeadzone ||
                      Mathf.Abs(input.pitch) > inputDeadzone || Mathf.Abs(input.roll) > inputDeadzone;
        SetState(moving ? FlightState.Flying : FlightState.Hovering);
    }

    void UpdateLanding(float dt)
    {
        Vector3 planar = new Vector3(input.roll, 0f, input.pitch);
        if (planar.magnitude > 1f) planar.Normalize();
        Vector3 horizontal = (transform.right * planar.x + transform.forward * planar.z) * maxHorizontalSpeed;

        float descend = -Mathf.Clamp(Altitude * 2f, 0.1f, landSpeed);   // slows near the floor
        Move(horizontal, descend, input.yaw, 0f, dt);

        if (Altitude <= 0.02f)
        {
            Vector3 p = transform.position;
            p.y = groundY;
            transform.position = p;
            velocity = Vector3.zero;
            SetState(FlightState.Landed);
        }
    }

    void Settle(float dt)
    {
        velocity = Vector3.Lerp(velocity, Vector3.zero, 1f - Mathf.Exp(-responsiveness * dt));
    }

    void Move(Vector3 horizontalTarget, float verticalTarget, float yawInput, float minAlt, float dt)
    {
        transform.Rotate(0f, yawInput * yawRate * dt, 0f, Space.World);

        Vector3 target = horizontalTarget + Vector3.up * verticalTarget;
        velocity = Vector3.Lerp(velocity, target, 1f - Mathf.Exp(-responsiveness * dt));
        transform.position += velocity * dt;

        Vector3 pos = transform.position;
        float low = groundY + minAlt;
        float high = groundY + maxAltitude;
        if (pos.y < low)  { pos.y = low;  if (velocity.y < 0f) velocity.y = 0f; }
        if (pos.y > high) { pos.y = high; if (velocity.y > 0f) velocity.y = 0f; }

        Vector3 offset = new Vector3(pos.x - origin.x, 0f, pos.z - origin.z);
        if (offset.magnitude > maxRadius)
        {
            Vector3 n = offset.normalized;
            pos.x = origin.x + n.x * maxRadius;
            pos.z = origin.z + n.z * maxRadius;
            float outward = Vector3.Dot(velocity, n);
            if (outward > 0f) velocity -= n * outward;
        }
        transform.position = pos;
    }

    // Only the Model child leans; the root moves and yaws.
    void UpdateTilt(float dt)
    {
        if (model == null) return;
        Vector3 local = transform.InverseTransformDirection(velocity);
        float pitchTilt = (local.z / maxHorizontalSpeed) * maxTiltDegrees;
        float rollTilt = -(local.x / maxHorizontalSpeed) * maxTiltDegrees;
        pitchTilt = Mathf.Clamp(pitchTilt, -maxTiltDegrees, maxTiltDegrees);
        rollTilt = Mathf.Clamp(rollTilt, -maxTiltDegrees, maxTiltDegrees);

        Quaternion target = Quaternion.Euler(pitchTilt, 0f, rollTilt) * modelBaseRotation;
        model.localRotation = Quaternion.Slerp(model.localRotation, target, 1f - Mathf.Exp(-tiltSmoothing * dt));
    }

    void SetState(FlightState next)
    {
        if (next == State) return;
        State = next;
        OnStateChanged?.Invoke(State);
    }

    void Invalid(string message)
    {
        Debug.Log("[Drone] Blocked: " + message);
        OnInvalidAction?.Invoke(message);
    }
}