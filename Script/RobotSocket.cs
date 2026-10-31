using UnityEngine;

/// <summary>
/// A Socket_* empty on the torso. Accepts only the part whose partID matches acceptedPartID.
/// Per the handoff README the socket transform IS the attach pose (part pivot == attach point).
/// </summary>
public class RobotSocket : MonoBehaviour
{
    public string acceptedPartID = "Torso";

    [Tooltip("How close (metres) a released part must be for the socket to react to it.")]
    public float snapRadius = 0.22f;

    public bool Occupied { get; private set; }

    public bool Accepts(RobotPart part)
    {
        return part != null && !Occupied && !part.isAssembled && part.partID == acceptedPartID;
    }

    public float DistanceTo(RobotPart part)
    {
        return Vector3.Distance(part.transform.position, transform.position);
    }

    public bool TryPlacePart(RobotPart part)
    {
        if (!Accepts(part)) return false;
        Occupied = true;
        part.targetSocket = transform;
        part.SnapIntoSocket();
        return true;
    }
}
