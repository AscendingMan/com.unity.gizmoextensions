using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEditor;

public class RotationHandle
{
    public static Vector3[] s_AxisVector = { Vector3.right, Vector3.up, Vector3.forward };
    public static Color k_RotationPieColor = new Color(GizmoExtensions.Styles.planeColor.r, GizmoExtensions.Styles.planeColor.g, GizmoExtensions.Styles.planeColor.b, 1f);//new Color(246f / 255, 242f / 255, 50f / 255, .89f);
    public static int activeIndex = 0;
    
    internal static Quaternion DoRotationHandle(RotationHandleIds ids, Quaternion rotation, Vector3 position, RotationHandleParam param)
    {
        var evt = Event.current;
        var camForward = Handles.inverseMatrix.MultiplyVector(Camera.current != null ? Camera.current.transform.forward : Vector3.forward);
        
        var size = HandleUtility.GetHandleSize(position);
        var temp = Handles.color;
        bool isDisabled = !GUI.enabled;

        var isHot = ids.Has(GUIUtility.hotControl);

        // Draw free rotation first to give it the lowest priority
        if (!isDisabled
            && param.ShouldShow(RotationHandleParam.Handle.XYZ)
            && (ids.xyz == GUIUtility.hotControl || !isHot))
        {
            Handles.color = new Color(0, 0, 0, 0.3f);
            rotation = FreeRotate.Do(ids.xyz, rotation, position, size * param.xyzSize, param.displayXYZCircle);
        }

        for (var i = 0; i < 3; ++i)
        {
            if (!param.ShouldShow(i))
                continue;

            if(isHot && ids[i] != GUIUtility.hotControl)
                continue;

            var axisColor = (Color)HandleUtils.GetColorByAxis.Invoke(null, new object[]{i});
            Handles.color = isDisabled ? Color.Lerp(axisColor, HandleUtils.staticColor, HandleUtils.staticBlend) : axisColor;
            Handles.color = (Color)HandleUtils.ToActiveColorSpace.Invoke(null, new object[]{Handles.color});
            var axisDir = s_AxisVector[i];

            var radius = size * param.axisSize[i];

            if (ids[i] == GUIUtility.hotControl)
                activeIndex = i;
            
            rotation = Disc.Do(ids[i], rotation, position, rotation * axisDir, radius, true, EditorSnapSettings.rotate, param.enableRayDrag, true, k_RotationPieColor);
        }

        // while dragging any rotation handles, draw a gray disc outline
        if (isHot && evt.type == EventType.Repaint)
        {
            Handles.color = (Color)HandleUtils.ToActiveColorSpace.Invoke(null, new object[]{HandleUtils.s_DisabledHandleColor});
            Handles.DrawWireDisc(position, camForward, size * param.axisSize[0], Handles.lineThickness);
        }

        if (!isDisabled
            && param.ShouldShow(RotationHandleParam.Handle.CameraAxis)
            && (ids.cameraAxis == GUIUtility.hotControl || !isHot))
        {
            Handles.color = (Color)HandleUtils.ToActiveColorSpace.Invoke(null, new object[]{HandleUtils.centerColor});
            rotation = Disc.Do(ids.cameraAxis, rotation, position, camForward, size * param.cameraAxisSize, false, 0, param.enableRayDrag, true, k_RotationPieColor);
        }

        Handles.color = temp;
        return rotation;
    }
    
    
    internal static int s_xRotateHandleHash = "xRotateHandleHash".GetHashCode();
    internal static int s_yRotateHandleHash = "yRotateHandleHash".GetHashCode();
    internal static int s_zRotateHandleHash = "zRotateHandleHash".GetHashCode();
    internal static int s_cameraAxisRotateHandleHash = "cameraAxisRotateHandleHash".GetHashCode();
    internal static int s_xyzRotateHandleHash = "xyzRotateHandleHash".GetHashCode();
    internal static int s_xScaleHandleHash = "xScaleHandleHash".GetHashCode();
    internal static int s_yScaleHandleHash = "yScaleHandleHash".GetHashCode();
    internal static int s_zScaleHandleHash = "zScaleHandleHash".GetHashCode();
    internal static int s_xyzScaleHandleHash = "xyzScaleHandleHash".GetHashCode();
    
    internal struct RotationHandleParam
    {
        [Flags]
        public enum Handle
        {
            None = 0,
            X = 1 << 0,
            Y = 1 << 1,
            Z = 1 << 2,
            CameraAxis = 1 << 3,
            XYZ = 1 << 4,
            All = ~None
        }

        static RotationHandleParam s_Default = new RotationHandleParam((Handle)(-1), Vector3.one, 1f, 1.1f, true, true);
        public static RotationHandleParam Default { get { return s_Default; } set { s_Default = value; } }

        public readonly Vector3 axisSize;
        public readonly float cameraAxisSize;
        public readonly float xyzSize;
        public readonly Handle handles;
        public readonly bool enableRayDrag;
        public readonly bool displayXYZCircle;

        public bool ShouldShow(int axis)
        {
            return (handles & (Handle)(1 << axis)) != 0;
        }

        public bool ShouldShow(Handle handle)
        {
            return (handles & handle) != 0;
        }

        public RotationHandleParam(Handle handles, Vector3 axisSize, float xyzSize, float cameraAxisSize, bool enableRayDrag, bool displayXYZCircle)
        {
            this.axisSize = axisSize;
            this.xyzSize = xyzSize;
            this.handles = handles;
            this.cameraAxisSize = cameraAxisSize;
            this.enableRayDrag = enableRayDrag;
            this.displayXYZCircle = displayXYZCircle;
        }
    }
    
    internal struct RotationHandleIds
    {
        public static RotationHandleIds @default
        {
            get
            {
                return new RotationHandleIds(
                    GUIUtility.GetControlID(s_xRotateHandleHash, FocusType.Passive),
                    GUIUtility.GetControlID(s_yRotateHandleHash, FocusType.Passive),
                    GUIUtility.GetControlID(s_zRotateHandleHash, FocusType.Passive),
                    GUIUtility.GetControlID(s_cameraAxisRotateHandleHash, FocusType.Passive),
                    GUIUtility.GetControlID(s_xyzRotateHandleHash, FocusType.Passive)
                );
            }
        }

        public readonly int x, y, z, cameraAxis, xyz;

        public int this[int index]
        {
            get
            {
                switch (index)
                {
                    case 0: return x;
                    case 1: return y;
                    case 2: return z;
                    case 3: return cameraAxis;
                    case 4: return xyz;
                }
                return -1;
            }
        }

        public bool Has(int id)
        {
            return x == id
                || y == id
                || z == id
                || cameraAxis == id
                || xyz == id;
        }

        public RotationHandleIds(int x, int y, int z, int cameraAxis, int xyz)
        {
            this.x = x;
            this.y = y;
            this.z = z;
            this.cameraAxis = cameraAxis;
            this.xyz = xyz;
        }

        public override int GetHashCode()
        {
            return x ^ y ^ z ^ cameraAxis ^ xyz;
        }

        public override bool Equals(object obj)
        {
            if (!(obj is RotationHandleIds))
                return false;

            var o = (RotationHandleIds)obj;
            return o.x == x && o.y == y && o.z == z
                && o.cameraAxis == cameraAxis && o.xyz == xyz;
        }
    }
}

internal class Disc
{
    internal static MethodInfo LockHandlePosition;
    internal static MethodInfo UnlockHandlePosition;
    internal static MethodInfo SetupIgnoreRaySnapObjects;
    internal static PropertyInfo ignoreRaySnapObjects;
    internal static PropertyInfo incrementalSnapActive;
    
    const int k_MaxSnapMarkers = 360 / 5;
    const float k_RotationUnitSnapMajorMarkerStep = 45;
    const float k_RotationUnitSnapMarkerSize = 0.1f;
    const float k_RotationUnitSnapMajorMarkerSize = 0.2f;
    const float k_GrabZoneScale = 0.3f;

    static Vector2 s_StartMousePosition, s_CurrentMousePosition;
    public static Vector3 s_StartPosition, s_StartAxis;
    static Quaternion s_StartRotation;
    public static float s_RotationDist;

    static Disc()
    {
        var toolMethods = typeof(Tools).GetMethods(BindingFlags.NonPublic | BindingFlags.Static);
        foreach (var mi in toolMethods)
        {
            if (mi.Name == "LockHandlePosition" && mi.GetParameters().Length == 0)
                LockHandlePosition = mi;
            
            if (mi.Name == "UnlockHandlePosition" && mi.GetParameters().Length == 0)
                UnlockHandlePosition = mi;
        }    
        
        
        //LockHandlePosition = typeof(Tools).GetMethod("LockHandlePosition", BindingFlags.NonPublic | BindingFlags.Static); 
        //UnlockHandlePosition = typeof(Tools).GetMethod("UnlockHandlePosition", BindingFlags.NonPublic | BindingFlags.Static); 
        SetupIgnoreRaySnapObjects = typeof(Handles).GetMethod("SetupIgnoreRaySnapObjects", BindingFlags.NonPublic | BindingFlags.Static); 

        ignoreRaySnapObjects = typeof(HandleUtility).GetProperty("ignoreRaySnapObjects", BindingFlags.NonPublic | BindingFlags.Static);
        incrementalSnapActive = typeof(EditorSnapSettings).GetProperty("incrementalSnapActive", BindingFlags.NonPublic | BindingFlags.Static); 
    }
    
    public static Quaternion Do(int id, Quaternion rotation, Vector3 position, Vector3 axis, float size, bool cutoffPlane, float snap)
    {
        return Do(id, rotation, position, axis, size, cutoffPlane, snap, true, true, Handles.secondaryColor);
    }

    public static Quaternion Do(int id, Quaternion rotation, Vector3 position, Vector3 axis, float size, bool cutoffPlane, float snap, bool enableRayDrag, bool showHotArc, Color fillColor)
    {
        var cam = Handles.inverseMatrix.MultiplyVector(Camera.current != null ? Camera.current.transform.forward : Vector3.forward);

        if (Mathf.Abs(Vector3.Dot(cam, axis)) > .999f)
            cutoffPlane = false;

        Event evt = Event.current;
        switch (evt.GetTypeForControl(id))
        {
            case EventType.Layout:
            case EventType.MouseMove:
            {
                float d;
                if (cutoffPlane)
                {
                    Vector3 from = Vector3.Cross(axis, cam).normalized;
                    d = HandleUtility.DistanceToArc(position, axis, from, 180, size) * k_GrabZoneScale;
                }
                else
                {
                    d = HandleUtility.DistanceToDisc(position, axis, size) * k_GrabZoneScale;
                }

                HandleUtility.AddControl(id, d);
                break;
            }
            case EventType.MouseDown:
                // am I closest to the thingy?
                if (HandleUtility.nearestControl == id && evt.button == 0 && !evt.alt)
                {
                    GUIUtility.hotControl = id;    // Grab mouse focus
                    LockHandlePosition.Invoke(null, new object[]{});
                    if (cutoffPlane)
                    {
                        Vector3 from = Vector3.Cross(axis, cam).normalized;
                        s_StartPosition = HandleUtility.ClosestPointToArc(position, axis, from, 180, size);
                    }
                    else
                    {
                        s_StartPosition = HandleUtility.ClosestPointToDisc(position, axis, size);
                    }
                    s_RotationDist = 0;
                    s_StartRotation = rotation;
                    s_StartAxis = axis;
                    s_CurrentMousePosition = s_StartMousePosition = Event.current.mousePosition;
                    evt.Use();
                    EditorGUIUtility.SetWantsMouseJumping(1);
                }
                break;

            case EventType.MouseDrag:
                if (GUIUtility.hotControl == id)
                {
                    // handle look to point rotation
                    bool rayDrag = EditorGUI.actionKey && evt.shift && enableRayDrag;
                    if (rayDrag)
                    {
                        object values = new object();
                        ignoreRaySnapObjects.GetValue(values);
                        Transform[] ignoreSnapObjectsVal = (Transform[])values;
                        
                        if (ignoreSnapObjectsVal == null)
                            SetupIgnoreRaySnapObjects.Invoke(null, new object[]{});
                        object hit = HandleUtility.RaySnap(HandleUtility.GUIPointToWorldRay(evt.mousePosition));
                        if (hit != null && Vector3.Dot(axis.normalized, rotation * Vector3.forward) < 0.999)
                        {
                            RaycastHit rh = (RaycastHit)hit;
                            Vector3 lookPoint = rh.point - position;
                            Vector3 lookPointProjected = lookPoint - Vector3.Dot(lookPoint, axis.normalized) * axis.normalized;
                            rotation = Quaternion.LookRotation(lookPointProjected, rotation * Vector3.up);
                        }
                    }
                    else
                    {
                        Vector3 direction = Vector3.Cross(axis, position - s_StartPosition).normalized;
                        s_CurrentMousePosition += evt.delta;
                        s_RotationDist = HandleUtility.CalcLineTranslation(s_StartMousePosition, s_CurrentMousePosition, s_StartPosition, direction) / size * 30;
                        s_RotationDist = Handles.SnapValue(s_RotationDist, snap);
                        rotation = Quaternion.AngleAxis(s_RotationDist * -1, s_StartAxis) * s_StartRotation;
                    }

                    GUI.changed = true;
                    evt.Use();
                }
                break;

            case EventType.MouseUp:
                if (GUIUtility.hotControl == id && (evt.button == 0 || evt.button == 2))
                {
                    UnlockHandlePosition.Invoke(null, new object[]{});;
                    GUIUtility.hotControl = 0;
                    evt.Use();
                    EditorGUIUtility.SetWantsMouseJumping(0);
                }
                break;

            case EventType.KeyDown:
                if (evt.keyCode == KeyCode.Escape && GUIUtility.hotControl == id)
                {
                    // We do not use the event nor clear hotcontrol to ensure auto revert value kicks in from native side
                    UnlockHandlePosition.Invoke(null, new object[]{});
                    EditorGUIUtility.SetWantsMouseJumping(0);
                }
                break;

            case EventType.Repaint:
                Color prevColor = new Color();
                float thickness = 0.01f;
                object[] parameters = new object[] {id, evt, null, null};
                
                HandleUtils.SetupHandleColor.Invoke(null, parameters);
                prevColor = (Color)parameters[2];
                thickness = (float)parameters[3];
                
                // If we're dragging it, we'll go a bit further and draw a selection pie
                if (GUIUtility.hotControl == id)
                {
                    Color t = Handles.color;
                    Vector3 from = (s_StartPosition - position).normalized;
                    Handles.color = fillColor;
                    Handles.DrawLine(position, position + from * size);
                    var d = -Mathf.Sign(s_RotationDist) * Mathf.Repeat(Mathf.Abs(s_RotationDist), 360);
                    Vector3 to = Quaternion.AngleAxis(d, axis) * from;
                    Handles.DrawLine(position, position + to * size);

                    Handles.color = fillColor * new Color(1, 1, 1, .2f);
                    for (int i = 0, revolutions = (int)Mathf.Abs(s_RotationDist * 0.002777777778f); i < revolutions; ++i)
                        Handles.DrawSolidDisc(position, axis, size);
                    Handles.DrawSolidArc(position, axis, from, d, size);

                    //Draw snap markers
                    //if (EditorSnapSettings.incrementalSnapActive && snap > 0)
                    Handles.color = GizmoExtensions.Styles.activeAxisColor;

                    bool snappingActive = (bool)incrementalSnapActive.GetValue(typeof(EditorSnapSettings));
                    if (snappingActive && snap > 0)
                    {
                        DrawRotationUnitSnapMarkers(position, axis, size, k_RotationUnitSnapMarkerSize, snap, @from);
                        DrawRotationUnitSnapMarkers(position, axis, size, k_RotationUnitSnapMajorMarkerSize, k_RotationUnitSnapMajorMarkerStep, @from);
                    }
                }

                if (showHotArc && GUIUtility.hotControl == id || GUIUtility.hotControl != id && !cutoffPlane)
                    Handles.DrawWireDisc(position, axis, size, thickness);
                else if (GUIUtility.hotControl != id && cutoffPlane)
                {
                    Vector3 from = Vector3.Cross(axis, cam).normalized;
                    Handles.DrawWireArc(position, axis, from, 180, size, thickness);
                }

                Handles.color = prevColor;
                break;
        }

        return rotation;
    }

    static void DrawRotationUnitSnapMarkers(Vector3 position, Vector3 axis, float handleSize, float markerSize, float snap, Vector3 @from)
    {
        var iterationCount = Mathf.FloorToInt(360 / snap);
        var performFading = iterationCount > k_MaxSnapMarkers;
        var limitedIterationCount = Mathf.Min(iterationCount, k_MaxSnapMarkers);

        // center the markers around the current angle
        var count = Mathf.RoundToInt(limitedIterationCount * 0.5f);

        for (var i = -count; i < count; ++i)
        {
            var rot = Quaternion.AngleAxis(i * snap, axis);
            var u = rot * @from;
            var startPoint = position + (1 - markerSize) * handleSize * u;
            var endPoint = position + 1 * handleSize * u;
            //Handles.color = Handles.selectedColor;
            if (performFading)
            {
                var alpha = 1 - Mathf.SmoothStep(0, 1, Mathf.Abs(i / ((float)limitedIterationCount - 1) - 0.5f) * 2);
                Handles.color = new Color(Handles.color.r, Handles.color.g, Handles.color.b, alpha);
            }
            Handles.DrawLine(startPoint, endPoint);
        }
    }
}

internal class FreeRotate
{
    static readonly Color s_DimmingColor = new Color(0f, 0f, 0f, 0.078f);
    private static Vector2 s_CurrentMousePosition;

    public static Quaternion Do(int id, Quaternion rotation, Vector3 position, float size)
    {
        return Do(id, rotation, position, size, true);
    }

    internal static Quaternion Do(int id, Quaternion rotation, Vector3 position, float size, bool drawCircle)
    {
        Vector3 worldPosition = Handles.matrix.MultiplyPoint(position);
        Matrix4x4 origMatrix = Handles.matrix;

        Event evt = Event.current;
        switch (evt.GetTypeForControl(id))
        {
            case EventType.Layout:
            case EventType.MouseMove:
                // We only want the position to be affected by the Handles.matrix.
                Handles.matrix = Matrix4x4.identity;
                HandleUtility.AddControl(id, HandleUtility.DistanceToCircle(worldPosition, size) + 5f);//HandleUtility.kPickDistance);
                Handles.matrix = origMatrix;
                break;
            case EventType.MouseDown:
                // am I closest to the thingy?
                if (HandleUtility.nearestControl == id && evt.button == 0 && !evt.alt)
                {
                    GUIUtility.hotControl = id; // Grab mouse focus
                    Disc.LockHandlePosition.Invoke(null, new object[]{});;
                    s_CurrentMousePosition = evt.mousePosition;
                    //HandleUtility.ignoreRaySnapObjects = null;
                    evt.Use();
                    EditorGUIUtility.SetWantsMouseJumping(1);
                }
                break;
            case EventType.MouseDrag:
                if (GUIUtility.hotControl == id)
                {
                    // rayDrag rotates object to look at ray hit
                    bool rayDrag = EditorGUI.actionKey && evt.shift;
                    if (rayDrag)
                    {
                        if (Disc.ignoreRaySnapObjects == null)
                            Disc.SetupIgnoreRaySnapObjects.Invoke(null, new object[]{});;

                        object hit = HandleUtility.RaySnap(HandleUtility.GUIPointToWorldRay(evt.mousePosition));
                        if (hit != null)
                        {
                            RaycastHit rh = (RaycastHit)hit;
                            Quaternion newRotation = Quaternion.LookRotation(rh.point - position);
                            if (Tools.pivotRotation == PivotRotation.Global)
                            {
                                Transform t = Selection.activeTransform;
                                if (t)
                                {
                                    Quaternion delta = Quaternion.Inverse(t.rotation) * rotation;
                                    newRotation = newRotation * delta;
                                }
                            }
                            rotation = newRotation;
                        }
                    }
                    else
                    {
                        s_CurrentMousePosition += evt.delta;
                        Vector3 rotDir = Camera.current.transform.TransformDirection(new Vector3(-evt.delta.y, -evt.delta.x, 0));
                        rotation = Quaternion.AngleAxis(evt.delta.magnitude, rotDir.normalized) * rotation;
                    }
                    GUI.changed = true;
                    evt.Use();
                }
                break;
            case EventType.MouseUp:
                if (GUIUtility.hotControl == id && (evt.button == 0 || evt.button == 2))
                {
                    Disc.UnlockHandlePosition.Invoke(null, new object[]{});;
                    GUIUtility.hotControl = 0;
                    evt.Use();
                    EditorGUIUtility.SetWantsMouseJumping(0);
                }
                break;
            case EventType.KeyDown:
                if (evt.keyCode == KeyCode.Escape && GUIUtility.hotControl == id)
                {
                    // We do not use the event nor clear hotcontrol to ensure auto revert value kicks in from native side
                    Disc.UnlockHandlePosition.Invoke(null, new object[]{});
                    EditorGUIUtility.SetWantsMouseJumping(0);
                }
                break;
            case EventType.Repaint:
                Color prevColor = new Color();
                float thickness = 0.01f;
                object[] parameters = new object[] {id, evt, null, null};
                
                HandleUtils.SetupHandleColor.Invoke(null, parameters);
                
                var isHot = id == GUIUtility.hotControl;
                var isHover = (bool)HandleUtils.IsHovering.Invoke(null, new object[]{id, evt});

                // We only want the position to be affected by the Handles.matrix.
                Handles.matrix = Matrix4x4.identity;
                if (drawCircle)
                    Handles.DrawWireDisc(worldPosition, Camera.current.transform.forward, size, thickness);
                if (isHover || isHot)
                {
                    Handles.color = s_DimmingColor;
                    Handles.DrawSolidDisc(worldPosition, Camera.current.transform.forward, size);
                }
                Handles.matrix = origMatrix;
                Handles.color = prevColor;
                break;
        }
        return rotation;
    }
}