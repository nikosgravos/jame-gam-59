using UnityEngine;

/// <summary>
/// Physics-driven erratic movement. Picks a behaviour (wander, dash, feint, curve),
/// steers the Rigidbody2D's velocity toward it, and bounces off whatever it hits.
/// </summary>
public class ErraticMover : MonoBehaviour
{
    enum State { Wander, Dash, Feint, Curve }

    [Header("Speed")]
    public float baseSpeed = 8f;
    public float dashSpeed = 24f;
    public float acceleration = 45f;   // lower = floaty, higher = snappy
    public float pulseAmount = 0.3f;   // speed rhythmically rises and falls
    public float pulseRate = 3f;

    [Header("Speed ramp")]
    [Range(0f, 1f)] public float startSpeedScale = 0.25f;   // fraction of full speed at the start
    public float endSpeedScale = 1f;                        // fraction of full speed once the ramp is done
    public float rampTime = 30f;                            // seconds to go from start to end

    [Header("Steering")]
    public float wanderTurnSpeed = 180f;  // max degrees/sec of smooth noise steering
    public float noiseScale = 1.5f;       // higher = twitchier wandering
    public float curveTurnSpeed = 220f;   // degrees/sec while carving a curve

    [Header("Behaviour duration (seconds, min/max)")]
    public Vector2 wanderTime = new Vector2(0.6f, 1.6f);
    public Vector2 dashTime = new Vector2(0.15f, 0.35f);
    public Vector2 feintTime = new Vector2(0.2f, 0.45f);
    public Vector2 curveTime = new Vector2(0.8f, 1.6f);

    [Header("Behaviour weights (how often each is picked)")]
    public float wanderWeight = 3f;
    public float dashWeight = 2f;
    public float feintWeight = 1f;
    public float curveWeight = 1.5f;

    [Header("Bounces")]
    public float bounceJitter = 25f;   // random degrees added to each bounce
    [Range(0f, 1f)] public float dashOnBounceChance = 0.25f;
    public bool logHits = false;

    Rigidbody2D rb;
    State state;
    Vector2 heading;
    float stateTimer, noiseSeed, pulseOffset, curveDir, elapsed;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        noiseSeed = Random.value * 100f;
        pulseOffset = Random.value * 10f;
        heading = Random.insideUnitCircle.normalized;
        Begin(PickWeighted());
    }

    void OnEnable() => elapsed = 0f;   // the ramp restarts whenever the object is (re)activated

    void FixedUpdate()
    {
        float dt = Time.fixedDeltaTime;

        stateTimer -= dt;
        if (stateTimer <= 0f)
            Begin(state == State.Feint ? State.Dash : PickWeighted()); // a feint always ends in a dash

        float speed = baseSpeed * (1f + pulseAmount * Mathf.Sin(Time.time * pulseRate + pulseOffset));
        float accel = acceleration;
        float turn = 0f;

        switch (state)
        {
            case State.Wander:
                turn = (Mathf.PerlinNoise(noiseSeed, Time.time * noiseScale) - 0.5f) * 2f * wanderTurnSpeed;
                break;
            case State.Curve:
                turn = curveDir * curveTurnSpeed;
                break;
            case State.Dash:
                speed = dashSpeed;
                accel *= 3f;               // snap up to speed
                break;
            case State.Feint:
                speed = baseSpeed * 0.1f;  // brake almost to a stop
                break;
        }

        // starts slow and gradually speeds up
        elapsed += dt;
        float ramp = Mathf.Lerp(startSpeedScale, endSpeedScale, Mathf.Clamp01(elapsed / rampTime));
        speed *= ramp;

        heading = Rotate(heading, turn * dt);

        Vector2 v = Vector2.MoveTowards(rb.linearVelocity, heading * speed, accel * dt);
        rb.linearVelocity = Vector2.ClampMagnitude(v, dashSpeed * Mathf.Max(ramp, 1f) * 1.5f); // stops collisions adding runaway energy
    }

    void Begin(State next, bool newHeading = true)
    {
        state = next;

        Vector2 time = next switch
        {
            State.Wander => wanderTime,
            State.Dash => dashTime,
            State.Feint => feintTime,
            _ => curveTime,
        };
        stateTimer = Random.Range(time.x, time.y);

        if (next == State.Curve) curveDir = Random.value < 0.5f ? -1f : 1f;
        if (next == State.Dash && newHeading) heading = Random.insideUnitCircle.normalized; // sharp new direction
    }

    State PickWeighted()
    {
        float r = Random.value * (wanderWeight + dashWeight + feintWeight + curveWeight);
        if ((r -= wanderWeight) < 0f) return State.Wander;
        if ((r -= dashWeight) < 0f) return State.Dash;
        if ((r -= feintWeight) < 0f) return State.Feint;
        return State.Curve;
    }

    void OnCollisionEnter2D(Collision2D c)
    {
        if (logHits) Debug.Log(name + " hit " + c.gameObject.name);

        // steer along the bounce so it doesn't turn straight back into the wall
        heading = Reflect(heading, c.GetContact(0).normal);
        heading = Rotate(heading, Random.Range(-bounceJitter, bounceJitter));

        if (Random.value < dashOnBounceChance)
            Begin(State.Dash, newHeading: false);
    }

    static Vector2 Reflect(Vector2 v, Vector2 n) => (v - 2f * Vector2.Dot(v, n) * n).normalized;

    static Vector2 Rotate(Vector2 v, float degrees)
    {
        float r = degrees * Mathf.Deg2Rad, cos = Mathf.Cos(r), sin = Mathf.Sin(r);
        return new Vector2(v.x * cos - v.y * sin, v.x * sin + v.y * cos);
    }
}
