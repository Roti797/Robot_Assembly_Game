using System;
using System.Collections;
using UnityEngine;

/// <summary>
/// One loose robot part. Input-agnostic: the desktop, VR and AR rigs all drive it through
/// BeginGrab / MoveTo / EndGrab (or NotifyExternalGrab for XR Grab Interactable), and the
/// StepManager decides what a release means.
/// partID / isAssembled / targetSocket are kept from the original prototype.
/// </summary>
[DisallowMultipleComponent]
public class RobotPart : MonoBehaviour
{
    public enum PartState { Locked, Available, Current, Assembled }

    [Header("Assembly")]
    public string partID;
    public bool isAssembled;
    public Transform targetSocket;

    [Header("Behaviour")]
    [Tooltip("Seconds for the ease-in when the part snaps onto its socket.")]
    public float snapDuration = 0.25f;

    [Tooltip("Small or thin parts (internal components) stay where they are put instead of falling under gravity. " +
             "Off for the original armour parts, which settle on the bench.")]
    public bool keepKinematic;

    public PartState State { get; private set; } = PartState.Available;
    public bool IsHeld { get; private set; }
    public Vector3 HomePosition { get; private set; }
    public Quaternion HomeRotation { get; private set; }

    public event Action<RobotPart> Grabbed;
    public event Action<RobotPart> Released;
    public event Action<RobotPart> Snapped;
    public event Action<RobotPart> StateChanged;

    Rigidbody body;
    Collider[] colliders;
    Renderer[] renderers;
    MaterialPropertyBlock block;
    Coroutine easeRoutine;

    static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

    public Renderer[] Renderers { get { Init(); return renderers; } }
    public bool CanGrab => State == PartState.Current && !isAssembled;

    void Awake() { Init(); }

    // Lazy and null-checked: Awake may not have run yet (inactive parent in AR) and the editor
    // builder also calls into this, so never rely on a one-shot flag.
    void Init()
    {
        if (renderers != null && colliders != null && block != null) return;
        bool first = renderers == null;
        body = GetComponent<Rigidbody>();
        colliders = GetComponentsInChildren<Collider>(true);
        renderers = GetComponentsInChildren<Renderer>(true);
        if (block == null) block = new MaterialPropertyBlock();
        if (first)
        {
            HomePosition = transform.position;
            HomeRotation = transform.rotation;
        }
    }

    /// <summary>Call after the layout is placed so "home" is where the part rests on the tray.</summary>
    public void CaptureHome()
    {
        Init();
        HomePosition = transform.position;
        HomeRotation = transform.rotation;
    }

    public void SetState(PartState state)
    {
        Init();
        State = state;
        bool dim = state == PartState.Locked;
        foreach (var r in renderers)
        {
            if (r == null) continue;
            r.GetPropertyBlock(block);
            block.SetColor(BaseColorId, dim ? new Color(0.55f, 0.55f, 0.58f, 1f) : Color.white);
            r.SetPropertyBlock(block);
        }
        // Only the current part is pickable; everything else is invisible to rays and overlaps.
        foreach (var c in colliders) if (c != null) c.enabled = state == PartState.Current;
        SetDynamic(state == PartState.Current);
        StateChanged?.Invoke(this);
    }

    /// <summary>Dynamic = rests on the bench under gravity. Kinematic = held, locked or snapped.</summary>
    void SetDynamic(bool dynamic)
    {
        if (body == null) return;
        if (keepKinematic) dynamic = false;
        if (dynamic)
        {
            body.isKinematic = false;
            body.useGravity = true;
        }
        else
        {
            if (!body.isKinematic) { body.linearVelocity = Vector3.zero; body.angularVelocity = Vector3.zero; }
            body.isKinematic = true;
            body.useGravity = false;
        }
    }

    // ---- input-facing API ------------------------------------------------------------------

    public void BeginGrab()
    {
        if (!CanGrab || IsHeld) return;
        IsHeld = true;
        SetDynamic(false);
        Grabbed?.Invoke(this);
    }

    public void MoveTo(Vector3 position, Quaternion rotation)
    {
        if (!IsHeld) return;
        transform.SetPositionAndRotation(position, rotation);
    }

    public void EndGrab()
    {
        if (!IsHeld) return;
        IsHeld = false;
        if (!isAssembled) SetDynamic(true);
        Released?.Invoke(this);
    }

    /// <summary>XR Grab Interactable already moves the object and manages its Rigidbody.</summary>
    public void NotifyExternalGrab(bool grabbed)
    {
        if (grabbed)
        {
            if (!CanGrab || IsHeld) return;
            IsHeld = true;
            Grabbed?.Invoke(this);
        }
        else if (IsHeld)
        {
            IsHeld = false;
            Released?.Invoke(this);
        }
    }

    /// <summary>Part dropped somewhere it can't be reached: ease it back onto the tray.</summary>
    public void ReturnHome()
    {
        if (isAssembled) return;
        if (easeRoutine != null) StopCoroutine(easeRoutine);
        SetDynamic(false);
        easeRoutine = StartCoroutine(EaseTo(HomePosition, HomeRotation, 0.4f, () => SetDynamic(State == PartState.Current)));
    }

    /// <summary>README: attached when part.position = socket.position and part.rotation = socket.rotation.</summary>
    public void SnapIntoSocket()
    {
        if (targetSocket == null || isAssembled) return;
        isAssembled = true;
        IsHeld = false;
        SetDynamic(false);
        foreach (var c in colliders) if (c != null) c.enabled = false;
        if (easeRoutine != null) StopCoroutine(easeRoutine);
        easeRoutine = StartCoroutine(EaseTo(targetSocket.position, targetSocket.rotation, snapDuration, () =>
        {
            transform.SetParent(targetSocket, true);
            transform.SetPositionAndRotation(targetSocket.position, targetSocket.rotation);
            State = PartState.Assembled;
            StateChanged?.Invoke(this);
            Snapped?.Invoke(this);
        }));
    }

    IEnumerator EaseTo(Vector3 pos, Quaternion rot, float seconds, Action done)
    {
        Vector3 p0 = transform.position;
        Quaternion r0 = transform.rotation;
        float t = 0f;
        while (t < seconds)
        {
            t += Time.deltaTime;
            float k = Mathf.Clamp01(t / Mathf.Max(0.0001f, seconds));
            k = k * k * (3f - 2f * k); // smoothstep
            transform.SetPositionAndRotation(Vector3.LerpUnclamped(p0, pos, k), Quaternion.Slerp(r0, rot, k));
            yield return null;
        }
        transform.SetPositionAndRotation(pos, rot);
        easeRoutine = null;
        done?.Invoke();
    }
}
