
using UnityEngine;
using UnityEngine.InputSystem;

public class PCInput : MonoBehaviour
{
    public Camera playerCamera;
    public float maxPickDistance = 100f;

    private RobotPart selectedPart;
    private Rigidbody selectedBody;
    private bool wasKinematic;
    private bool wasUsingGravity;

    private Plane dragPlane;
    private Vector3 dragOffset;

    void Start()
    {
        if (playerCamera == null)
            playerCamera = Camera.main;
    }

    void Update()
    {
        if (Mouse.current == null || playerCamera == null)
            return;

        if (Mouse.current.leftButton.wasPressedThisFrame)
            TryPickPart();

        if (selectedPart != null &&
            Mouse.current.leftButton.isPressed)
            DragPart();

        if (selectedPart != null &&
            Mouse.current.leftButton.wasReleasedThisFrame)
            ReleasePart();
    }

    void TryPickPart()
    {
        Ray ray = playerCamera.ScreenPointToRay(
            Mouse.current.position.ReadValue()
        );

        RaycastHit[] hits = Physics.RaycastAll(ray, maxPickDistance);

        foreach (RaycastHit hit in hits)
        {
            RobotPart part =
                hit.collider.GetComponentInParent<RobotPart>();

            if (part == null || part.isAssembled)
                continue;

            selectedPart = part;
            selectedBody = part.GetComponent<Rigidbody>();

            if (selectedBody != null)
            {
                wasKinematic = selectedBody.isKinematic;
                wasUsingGravity = selectedBody.useGravity;
                selectedBody.isKinematic = true;
                selectedBody.useGravity = false;
            }

            dragPlane = new Plane(
                -playerCamera.transform.forward,
                part.transform.position
            );

            if (dragPlane.Raycast(ray, out float distance))
            {
                Vector3 hitPoint = ray.GetPoint(distance);
                dragOffset = part.transform.position - hitPoint;
            }
            else
            {
                dragOffset = Vector3.zero;
            }

            Debug.Log("Selected part: " + part.partID);
            return;
        }

        Debug.Log("No unassembled robot part was hit.");
    }

    void DragPart()
    {
        Ray ray = playerCamera.ScreenPointToRay(
            Mouse.current.position.ReadValue()
        );

        if (dragPlane.Raycast(ray, out float distance))
        {
            selectedPart.transform.position =
                ray.GetPoint(distance) + dragOffset;
        }
    }

    void ReleasePart()
    {
        if (selectedPart == null)
            return;

        Collider partCollider =
            selectedPart.GetComponent<Collider>();

        bool placed = false;

        if (partCollider != null)
        {
            RobotSocket[] sockets =
                FindObjectsByType<RobotSocket>(
                    FindObjectsSortMode.None
                );

            foreach (RobotSocket socket in sockets)
            {
                Collider socketCollider =
                    socket.GetComponent<Collider>();

                if (socketCollider == null)
                    continue;

                Debug.Log(
                    "Checking " + selectedPart.partID +
                    " against socket: " + socket.acceptedPartID
                );

                if (socketCollider.bounds.Intersects(
                    partCollider.bounds))
                {
                    Debug.Log("Socket overlap detected!");

                    if (socket.TryPlacePart(selectedPart))
                    {
                        placed = true;
                        break;
                    }
                }
            }
        }

        if (!placed)
            Debug.Log("Part not assembled. Check socket position.");

        if (selectedBody != null && !selectedPart.isAssembled)
        {
            selectedBody.isKinematic = wasKinematic;
            selectedBody.useGravity = wasUsingGravity;
        }

        selectedPart = null;
        selectedBody = null;
    }
}
