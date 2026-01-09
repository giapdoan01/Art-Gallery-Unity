using UnityEngine;
using System;

/// <summary>
/// Runtime Transform Gizmo using LineRenderer
/// Hiển thị trong Game View và cho phép drag object
/// ✅ FIXED: Added IsActive property for external checking
/// </summary>
public class RuntimeTransformGizmo : MonoBehaviour
{
    [Header("Gizmo Settings")]
    [SerializeField] private float gizmoSize = 2f;
    [SerializeField] private float lineWidth = 0.05f;
    [SerializeField] private float arrowSize = 0.2f;
    [SerializeField] private float hoverRadius = 0.15f;
    [SerializeField] private bool scaleWithDistance = true;

    [Header("Colors")]
    [SerializeField] private Color xAxisColor = new Color(1f, 0.2f, 0.2f, 1f);
    [SerializeField] private Color yAxisColor = new Color(0.2f, 1f, 0.2f, 1f);
    [SerializeField] private Color zAxisColor = new Color(0.2f, 0.2f, 1f, 1f);
    [SerializeField] private Color selectedColor = Color.yellow;
    [SerializeField] private Color hoverColor = Color.white;

    [Header("Interaction")]
    [SerializeField] private float dragSensitivity = 1f;
    [SerializeField] private float rotateSensitivity = 180f;
    [SerializeField] private bool snapEnabled = false;
    [SerializeField] private float moveSnapValue = 0.5f;
    [SerializeField] private float rotateSnapValue = 15f;

    [Header("References")]
    [SerializeField] private Camera renderCamera;
    [SerializeField] private Material lineMaterial;

    [Header("Debug")]
    [SerializeField] private bool showDebug = false;

    // State
    private TransformMode currentMode = TransformMode.Move;
    private TransformSpace currentSpace = TransformSpace.Local;
    private GizmoAxis hoveredAxis = GizmoAxis.None;
    private GizmoAxis selectedAxis = GizmoAxis.None;
    private bool isDragging = false;
    private bool isActive = false;

    // LineRenderers
    private LineRenderer xAxisLine;
    private LineRenderer yAxisLine;
    private LineRenderer zAxisLine;
    private GameObject linesContainer;

    // Drag state
    private Vector3 dragStartMousePos;
    private Vector3 dragStartPosition;
    private Vector3 dragStartRotation;
    private Plane dragPlane;

    // ✅ Camera control reference
    private CameraFollow cameraFollow;

    // Callbacks
    public event Action<Vector3, Vector3> OnTransformChanged;

    // ✅ Public property to check if gizmo is active
    public bool IsActive => isActive;

    #region Unity Lifecycle

    private void Awake()
    {
        if (renderCamera == null)
            renderCamera = Camera.main;

        // ✅ Tìm CameraFollow component
        if (renderCamera != null)
        {
            cameraFollow = renderCamera.GetComponent<CameraFollow>();
            if (cameraFollow == null && showDebug)
            {
                Debug.LogWarning("[RuntimeTransformGizmo] CameraFollow component not found on camera!");
            }
        }

        // Create line material if not assigned
        if (lineMaterial == null)
        {
            lineMaterial = new Material(Shader.Find("Sprites/Default"));
            lineMaterial.color = Color.white;
        }

        // Create line renderers
        CreateLineRenderers();

        // Disable by default
        isActive = false;
        enabled = false;
        HideLines();

        if (showDebug)
            Debug.Log($"[RuntimeTransformGizmo] Awake on {gameObject.name}");
    }

    private void Update()
    {
        if (!isActive)
            return;

        // ✅ Kiểm tra camera
        if (renderCamera == null)
        {
            renderCamera = Camera.main;
            if (renderCamera == null)
            {
                Debug.LogError($"[RuntimeTransformGizmo] ❌ Cannot find Camera.main!");
                return;
            }
        }

        // Update line positions
        UpdateLinePositions();

        // Update hover (chỉ khi không drag)
        if (!isDragging)
        {
            UpdateHover();
        }

        // Handle input
        HandleInput();

        // Update line colors
        UpdateLineColors();
    }

    private void OnDestroy()
    {
        // Cleanup
        if (linesContainer != null)
            Destroy(linesContainer);
    }

    // Debug UI
    private void OnGUI()
    {
        if (!isActive || !showDebug) return;

        GUI.color = Color.white;
        GUI.Label(new Rect(10, 10, 300, 20), $"Gizmo Active: {isActive}");
        GUI.Label(new Rect(10, 30, 300, 20), $"Mode: {currentMode}");
        GUI.Label(new Rect(10, 50, 300, 20), $"Hovered: {hoveredAxis}");
        GUI.Label(new Rect(10, 70, 300, 20), $"Selected: {selectedAxis}");
        GUI.Label(new Rect(10, 90, 300, 20), $"Dragging: {isDragging}");
        GUI.Label(new Rect(10, 110, 300, 20), $"Position: {transform.position}");
    }

    #endregion

    #region LineRenderer Setup

    private void CreateLineRenderers()
    {
        // Create container
        linesContainer = new GameObject("GizmoLines");
        linesContainer.transform.SetParent(transform);
        linesContainer.transform.localPosition = Vector3.zero;
        linesContainer.transform.localRotation = Quaternion.identity;

        // Create X axis line
        xAxisLine = CreateAxisLine("XAxis", xAxisColor);

        // Create Y axis line
        yAxisLine = CreateAxisLine("YAxis", yAxisColor);

        // Create Z axis line
        zAxisLine = CreateAxisLine("ZAxis", zAxisColor);

        if (showDebug)
            Debug.Log($"[RuntimeTransformGizmo] ✅ LineRenderers created");
    }

    private LineRenderer CreateAxisLine(string name, Color color)
    {
        GameObject lineObj = new GameObject(name);
        lineObj.transform.SetParent(linesContainer.transform);
        lineObj.transform.localPosition = Vector3.zero;

        LineRenderer line = lineObj.AddComponent<LineRenderer>();
        line.material = lineMaterial;
        line.startColor = color;
        line.endColor = color;
        line.startWidth = lineWidth;
        line.endWidth = lineWidth;
        line.positionCount = 2;
        line.useWorldSpace = true;

        // Disable shadows
        line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        line.receiveShadows = false;

        return line;
    }

    private void UpdateLinePositions()
    {
        if (xAxisLine == null || yAxisLine == null || zAxisLine == null)
            return;

        float size = CalculateGizmoSize();
        Vector3 origin = transform.position;
        Quaternion rotation = currentSpace == TransformSpace.Local ? transform.rotation : Quaternion.identity;

        // X axis (Red) - Right
        Vector3 xEnd = origin + rotation * Vector3.right * size;
        xAxisLine.SetPosition(0, origin);
        xAxisLine.SetPosition(1, xEnd);

        // Y axis (Green) - Up
        Vector3 yEnd = origin + rotation * Vector3.up * size;
        yAxisLine.SetPosition(0, origin);
        yAxisLine.SetPosition(1, yEnd);

        // Z axis (Blue) - Forward
        Vector3 zEnd = origin + rotation * Vector3.forward * size;
        zAxisLine.SetPosition(0, origin);
        zAxisLine.SetPosition(1, zEnd);
    }

    private void UpdateLineColors()
    {
        if (xAxisLine == null || yAxisLine == null || zAxisLine == null)
            return;

        // X axis (Red)
        Color xColor = GetAxisColor(GizmoAxis.X, xAxisColor);
        xAxisLine.startColor = xColor;
        xAxisLine.endColor = xColor;

        if (selectedAxis == GizmoAxis.X)
            xAxisLine.startWidth = lineWidth * 4f;
        else if (hoveredAxis == GizmoAxis.X)
            xAxisLine.startWidth = lineWidth * 3f;
        else
            xAxisLine.startWidth = lineWidth;

        xAxisLine.endWidth = xAxisLine.startWidth;

        // Y axis (Green)
        Color yColor = GetAxisColor(GizmoAxis.Y, yAxisColor);
        yAxisLine.startColor = yColor;
        yAxisLine.endColor = yColor;

        if (selectedAxis == GizmoAxis.Y)
            yAxisLine.startWidth = lineWidth * 4f;
        else if (hoveredAxis == GizmoAxis.Y)
            yAxisLine.startWidth = lineWidth * 3f;
        else
            yAxisLine.startWidth = lineWidth;

        yAxisLine.endWidth = yAxisLine.startWidth;

        // Z axis (Blue)
        Color zColor = GetAxisColor(GizmoAxis.Z, zAxisColor);
        zAxisLine.startColor = zColor;
        zAxisLine.endColor = zColor;

        if (selectedAxis == GizmoAxis.Z)
            zAxisLine.startWidth = lineWidth * 4f;
        else if (hoveredAxis == GizmoAxis.Z)
            zAxisLine.startWidth = lineWidth * 3f;
        else
            zAxisLine.startWidth = lineWidth;

        zAxisLine.endWidth = zAxisLine.startWidth;
    }

    private void ShowLines()
    {
        if (linesContainer != null)
            linesContainer.SetActive(true);
    }

    private void HideLines()
    {
        if (linesContainer != null)
            linesContainer.SetActive(false);
    }

    #endregion

    #region Public Methods

    /// <summary>
    /// ✅ Activate gizmo - Shows lines and enables interaction
    /// Does NOT use GameObject.SetActive!
    /// </summary>
    public void Activate()
    {
        isActive = true;
        enabled = true;
        isDragging = false;
        selectedAxis = GizmoAxis.None;
        hoveredAxis = GizmoAxis.None;

        ShowLines();

        if (showDebug)
            Debug.Log($"[RuntimeTransformGizmo] ✅ ACTIVATED for: {gameObject.name}");
    }

    /// <summary>
    /// ✅ Deactivate gizmo - Hides lines and disables interaction
    /// Does NOT use GameObject.SetActive!
    /// </summary>
    public void Deactivate()
    {
        isActive = false;
        enabled = false;
        isDragging = false;
        selectedAxis = GizmoAxis.None;
        hoveredAxis = GizmoAxis.None;

        HideLines();

        if (showDebug)
            Debug.Log($"[RuntimeTransformGizmo] ❌ DEACTIVATED for: {gameObject.name}");
    }

    public void SetMode(TransformMode mode)
    {
        currentMode = mode;
        selectedAxis = GizmoAxis.None;
        hoveredAxis = GizmoAxis.None;

        if (showDebug)
            Debug.Log($"[RuntimeTransformGizmo] Mode: {mode}");
    }

    public void SetSpace(TransformSpace space)
    {
        currentSpace = space;

        if (showDebug)
            Debug.Log($"[RuntimeTransformGizmo] Space: {space}");
    }

    public void SetSnapEnabled(bool enabled)
    {
        snapEnabled = enabled;
    }

    #endregion

    #region Input Handling

    private void HandleInput()
    {
        // ✅ Ignore if pointer over UI input field
        if (UnityEngine.EventSystems.EventSystem.current != null)
        {
            var currentSelected = UnityEngine.EventSystems.EventSystem.current.currentSelectedGameObject;
            if (currentSelected != null && currentSelected.GetComponent<TMPro.TMP_InputField>() != null)
            {
                return;
            }
        }

        // Start drag
        if (Input.GetMouseButtonDown(0))
        {
            if (hoveredAxis != GizmoAxis.None)
            {
                StartDrag();
            }
        }

        // Update drag
        if (isDragging)
        {
            UpdateDrag();
        }

        // End drag
        if (Input.GetMouseButtonUp(0) && isDragging)
        {
            EndDrag();
        }

        // Keyboard shortcuts
        if (Input.GetKeyDown(KeyCode.W))
            SetMode(TransformMode.Move);
        else if (Input.GetKeyDown(KeyCode.E))
            SetMode(TransformMode.Rotate);
    }

    private void StartDrag()
    {
        isDragging = true;
        selectedAxis = hoveredAxis;
        dragStartMousePos = Input.mousePosition;
        dragStartPosition = transform.position;
        dragStartRotation = transform.eulerAngles;

        Vector3 normal = GetDragPlaneNormal();
        dragPlane = new Plane(normal, transform.position);

        // ✅ Disable camera control khi bắt đầu drag
        if (cameraFollow != null)
        {
            cameraFollow.enabled = false;
            if (showDebug)
                Debug.Log("[RuntimeTransformGizmo] CameraFollow disabled");
        }

        if (showDebug)
        {
            Debug.Log($"[RuntimeTransformGizmo] ========== START DRAG ==========");
            Debug.Log($"[RuntimeTransformGizmo] Selected axis: {selectedAxis}");
            Debug.Log($"[RuntimeTransformGizmo] Start position: {dragStartPosition}");
            Debug.Log($"[RuntimeTransformGizmo] =====================================");
        }
    }

    private void UpdateDrag()
    {
        switch (currentMode)
        {
            case TransformMode.Move:
                UpdateMoveDrag();
                break;
            case TransformMode.Rotate:
                UpdateRotateDrag();
                break;
        }
    }

    private void EndDrag()
    {
        if (showDebug)
        {
            Debug.Log($"[RuntimeTransformGizmo] ========== END DRAG ==========");
            Debug.Log($"[RuntimeTransformGizmo] Final position: {transform.position}");
            Debug.Log($"[RuntimeTransformGizmo] =====================================");
        }

        isDragging = false;
        selectedAxis = GizmoAxis.None;

        // ✅ Enable lại camera control khi kết thúc drag
        if (cameraFollow != null)
        {
            cameraFollow.enabled = true;
            if (showDebug)
                Debug.Log("[RuntimeTransformGizmo] CameraFollow enabled");
        }
    }

    #endregion

    #region Hover Detection

    private void UpdateHover()
    {
        // Ignore if dragging
        if (isDragging)
            return;

        // ✅ Ignore if pointer over UI input field
        if (UnityEngine.EventSystems.EventSystem.current != null)
        {
            var currentSelected = UnityEngine.EventSystems.EventSystem.current.currentSelectedGameObject;
            if (currentSelected != null && currentSelected.GetComponent<TMPro.TMP_InputField>() != null)
            {
                hoveredAxis = GizmoAxis.None;
                return;
            }
        }

        if (renderCamera == null)
        {
            hoveredAxis = GizmoAxis.None;
            return;
        }

        Ray ray = renderCamera.ScreenPointToRay(Input.mousePosition);
        float size = CalculateGizmoSize();

        Vector3 origin = transform.position;
        Quaternion rotation = currentSpace == TransformSpace.Local ? transform.rotation : Quaternion.identity;

        // ✅ Sử dụng screen-space distance
        float screenHoverRadius = hoverRadius * 100f; // pixels

        // ✅ Tìm axis gần nhất trên màn hình
        GizmoAxis closestAxis = GizmoAxis.None;
        float closestScreenDistance = float.MaxValue;

        // Check X axis
        float screenDistX = GetScreenDistanceToAxis(ray, origin, rotation * Vector3.right, size);
        if (screenDistX < screenHoverRadius && screenDistX < closestScreenDistance)
        {
            closestScreenDistance = screenDistX;
            closestAxis = GizmoAxis.X;
        }

        // Check Y axis
        float screenDistY = GetScreenDistanceToAxis(ray, origin, rotation * Vector3.up, size);
        if (screenDistY < screenHoverRadius && screenDistY < closestScreenDistance)
        {
            closestScreenDistance = screenDistY;
            closestAxis = GizmoAxis.Y;
        }

        // Check Z axis
        float screenDistZ = GetScreenDistanceToAxis(ray, origin, rotation * Vector3.forward, size);
        if (screenDistZ < screenHoverRadius && screenDistZ < closestScreenDistance)
        {
            closestScreenDistance = screenDistZ;
            closestAxis = GizmoAxis.Z;
        }

        // Update hovered axis
        hoveredAxis = closestAxis;
    }

    /// <summary>
    /// ✅ Tính khoảng cách trên màn hình (pixels) từ chuột đến axis line
    /// </summary>
    private float GetScreenDistanceToAxis(Ray ray, Vector3 origin, Vector3 direction, float length)
    {
        Vector3 dir = direction.normalized;

        // Sample nhiều điểm trên axis
        int samples = 20;
        float minScreenDistance = float.MaxValue;

        for (int i = 0; i <= samples; i++)
        {
            float t = (float)i / samples;
            Vector3 pointOnAxis = origin + dir * (t * length);

            // Convert to screen space
            Vector3 screenPoint = renderCamera.WorldToScreenPoint(pointOnAxis);

            // Check if behind camera
            if (screenPoint.z < 0)
                continue;

            // Calculate screen distance
            Vector2 screenPos = new Vector2(screenPoint.x, screenPoint.y);
            Vector2 mousePos = Input.mousePosition;
            float screenDist = Vector2.Distance(screenPos, mousePos);

            if (screenDist < minScreenDistance)
            {
                minScreenDistance = screenDist;
            }
        }

        return minScreenDistance;
    }

    #endregion

    #region Move Drag

    private void UpdateMoveDrag()
    {
        if (renderCamera == null)
        {
            Debug.LogError($"[RuntimeTransformGizmo] renderCamera is null!");
            return;
        }

        Ray ray = renderCamera.ScreenPointToRay(Input.mousePosition);

        if (!dragPlane.Raycast(ray, out float enter))
        {
            return;
        }

        Vector3 hitPoint = ray.GetPoint(enter);
        Vector3 dragStartHitPoint = GetDragStartHitPoint();
        Vector3 delta = hitPoint - dragStartHitPoint;

        Quaternion rotation = currentSpace == TransformSpace.Local ? transform.rotation : Quaternion.identity;
        Vector3 constrainedDelta = ConstrainToAxis(delta, rotation);

        if (snapEnabled)
        {
            constrainedDelta = SnapVector(constrainedDelta, moveSnapValue);
        }

        Vector3 newPosition = dragStartPosition + constrainedDelta;

        // ✅ Apply position to THIS transform
        transform.position = newPosition;

        // ✅ Trigger event
        OnTransformChanged?.Invoke(newPosition, transform.eulerAngles);
    }

    private Vector3 GetDragStartHitPoint()
    {
        Ray ray = renderCamera.ScreenPointToRay(dragStartMousePos);
        dragPlane.Raycast(ray, out float enter);
        return ray.GetPoint(enter);
    }

    private Vector3 ConstrainToAxis(Vector3 delta, Quaternion rotation)
    {
        switch (selectedAxis)
        {
            case GizmoAxis.X:
                Vector3 xAxis = rotation * Vector3.right;
                return xAxis * Vector3.Dot(delta, xAxis);
            case GizmoAxis.Y:
                Vector3 yAxis = rotation * Vector3.up;
                return yAxis * Vector3.Dot(delta, yAxis);
            case GizmoAxis.Z:
                Vector3 zAxis = rotation * Vector3.forward;
                return zAxis * Vector3.Dot(delta, zAxis);
            default:
                return delta;
        }
    }

    private Vector3 SnapVector(Vector3 vector, float snapValue)
    {
        return new Vector3(
            Mathf.Round(vector.x / snapValue) * snapValue,
            Mathf.Round(vector.y / snapValue) * snapValue,
            Mathf.Round(vector.z / snapValue) * snapValue
        );
    }

    #endregion

    #region Rotate Drag

    private void UpdateRotateDrag()
    {
        Vector2 mouseDelta = (Vector2)Input.mousePosition - (Vector2)dragStartMousePos;
        float angle = (mouseDelta.x / Screen.width) * rotateSensitivity;

        if (snapEnabled)
        {
            angle = Mathf.Round(angle / rotateSnapValue) * rotateSnapValue;
        }

        Vector3 rotationAxis = GetRotationAxis();
        Quaternion rotation = Quaternion.AngleAxis(angle, rotationAxis);

        if (currentSpace == TransformSpace.Local)
        {
            transform.localRotation = Quaternion.Euler(dragStartRotation) * rotation;
        }
        else
        {
            transform.rotation = rotation * Quaternion.Euler(dragStartRotation);
        }

        OnTransformChanged?.Invoke(transform.position, transform.eulerAngles);
    }

    private Vector3 GetRotationAxis()
    {
        Quaternion rotation = currentSpace == TransformSpace.Local ? transform.rotation : Quaternion.identity;

        switch (selectedAxis)
        {
            case GizmoAxis.X:
                return rotation * Vector3.right;
            case GizmoAxis.Y:
                return rotation * Vector3.up;
            case GizmoAxis.Z:
                return rotation * Vector3.forward;
            default:
                return Vector3.up;
        }
    }

    #endregion

    #region Utilities

    private Color GetAxisColor(GizmoAxis axis, Color baseColor)
    {
        if (selectedAxis == axis)
            return selectedColor;

        if (hoveredAxis == axis)
            return hoverColor;

        return baseColor;
    }

    private float CalculateGizmoSize()
    {
        if (!scaleWithDistance || renderCamera == null)
            return gizmoSize;

        float distance = Vector3.Distance(renderCamera.transform.position, transform.position);
        return gizmoSize * (distance / 10f);
    }

    private Vector3 GetDragPlaneNormal()
    {
        if (renderCamera == null)
            return Vector3.up;

        Quaternion rotation = currentSpace == TransformSpace.Local ? transform.rotation : Quaternion.identity;
        Vector3 cameraForward = renderCamera.transform.forward;

        switch (selectedAxis)
        {
            case GizmoAxis.X:
                Vector3 xAxis = rotation * Vector3.right;
                return Vector3.Cross(xAxis, cameraForward).normalized;
            case GizmoAxis.Y:
                Vector3 yAxis = rotation * Vector3.up;
                return Vector3.Cross(yAxis, cameraForward).normalized;
            case GizmoAxis.Z:
                Vector3 zAxis = rotation * Vector3.forward;
                return Vector3.Cross(zAxis, cameraForward).normalized;
            default:
                return cameraForward;
        }
    }

    #endregion
}

public enum TransformMode { Move, Rotate, Scale }
public enum TransformSpace { Local, World }
public enum GizmoAxis { None, X, Y, Z }
