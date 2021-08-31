using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Numerics;
using UnityEngine;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine.UI;
using UnityEngine.XR;
using Matrix4x4 = UnityEngine.Matrix4x4;
using Quaternion = UnityEngine.Quaternion;
using Vector2 = UnityEngine.Vector2;
using Vector3 = UnityEngine.Vector3;
public class ScaleHandle
{
    internal static Color constrainProportionsScaleHandleColor = new Color( 190f / 255, 190f / 255, 190f / 255, 1f);
    
    internal static int s_xScaleHandleHash = "xScaleHandleHash".GetHashCode();
    internal static int s_yScaleHandleHash = "yScaleHandleHash".GetHashCode();
    internal static int s_zScaleHandleHash = "zScaleHandleHash".GetHashCode();
    internal static int s_xyzScaleHandleHash = "xyzScaleHandleHash".GetHashCode();
    
    
    static Vector3 s_DoScaleHandle_AxisHandlesOctant = Vector3.one;
    static int[] s_DoScaleHandle_AxisDrawOrder = { 0, 1, 2 };
    public static float s_CurrentMultiplier;
    public static Vector3 s_InitialScale;
    internal static float handleLength { get; set;}
    internal static bool proportionalScale { get; set; }
    
    internal static Vector3 DoScaleHandle(ScaleHandleIds ids, Vector3 scale, Vector3 position, Quaternion rotation, float handleSize, ScaleHandleParam param, bool isProportionalScale = false)
    {
        // Calculate the camera view vector in Handle draw space
        // this handle the case where the matrix is skewed
        var handlePosition = Handles.matrix.MultiplyPoint3x4(position);
        var drawToWorldMatrix = Handles.matrix * Matrix4x4.TRS(position, rotation, Vector3.one);
        var invDrawToWorldMatrix = drawToWorldMatrix.inverse;
        var viewVectorDrawSpace = (Vector3)HandleUtils.GetCameraViewFrom.Invoke(null, new object[]{handlePosition, invDrawToWorldMatrix});

        var isDisabled = !GUI.enabled;

        var isHot = ids.Has(GUIUtility.hotControl);

        var axisOffset = param.axisOffset;
        var axisLineScale = param.axisLineScale;
        // When an axis is hot, draw the line from the center to the handle
        // So ignore the offset
        if (isHot)
        {
            axisLineScale += axisOffset;
            axisOffset = Vector3.zero;
        }

        var isCenterIsHot = ids.xyz == GUIUtility.hotControl;

        switch (Event.current.type)
        {
            case EventType.MouseDown:
                s_InitialScale = scale == Vector3.zero ? Vector3.one : scale;
                s_CurrentMultiplier = 1.0f;
                break;
            case EventType.MouseDrag:
                if (isProportionalScale)
                    proportionalScale = true;
                break;
            case EventType.MouseUp:
                proportionalScale = false;
                break;
        }

        HandleUtils.CalcDrawOrder.Invoke(null, new object[]{viewVectorDrawSpace, s_DoScaleHandle_AxisDrawOrder});
        for (var ii = 0; ii < 3; ++ii)
        {
            int i = s_DoScaleHandle_AxisDrawOrder[ii];
            int axisIndex = i;
            if (!param.ShouldShow(i))
                continue;

            if (!PositionHandle.currentlyDragging)
            {
                switch (param.orientation)
                {
                    case ScaleHandleParam.Orientation.Signed:
                        s_DoScaleHandle_AxisHandlesOctant[i] = 1;
                        break;
                    case ScaleHandleParam.Orientation.Camera:
                        s_DoScaleHandle_AxisHandlesOctant[i] = viewVectorDrawSpace[i] > 0.01f ? -1 : 1;
                        break;
                }
            }


            var id = ids[i];
            var isThisAxisHot = isHot && id == GUIUtility.hotControl;

            var axisDir = PositionHandle.GetAxisVector(i);
            var axisColor = isProportionalScale ? constrainProportionsScaleHandleColor : (Color)HandleUtils.GetColorByAxis.Invoke(null, new object[]{i});
            var offset = axisOffset[i];
            var cameraLerp = id == GUIUtility.hotControl ? 0 : GetCameraViewLerpForWorldAxis(viewVectorDrawSpace, axisDir);
            // If we are here and is hot, then this axis is hot and must be opaque
            cameraLerp = isHot ? 0 : cameraLerp;
            Handles.color = isDisabled ? Color.Lerp(axisColor, HandleUtils.staticColor, HandleUtils.staticBlend) : axisColor;

            axisDir *= s_DoScaleHandle_AxisHandlesOctant[i];

            if (cameraLerp <= kCameraViewThreshold)
            {
                Handles.color =
                    (Color) HandleUtils.GetFadedAxisColor.Invoke(null, new object[] {Handles.color, cameraLerp, id});

                if (isHot && !isThisAxisHot)
                    Handles.color = isProportionalScale ? Handles.selectedColor : HandleUtils.s_DisabledHandleColor;

                if (isCenterIsHot)
                    Handles.color = Handles.selectedColor;

                Handles.color = (Color)HandleUtils.ToActiveColorSpace.Invoke(null, new object[] { Handles.color});
            
                if (isProportionalScale)
                    axisIndex = 0;

                scale = DoAxis(id, scale, axisIndex, position, rotation * axisDir, rotation, handleSize * param.axisSize[axisIndex], EditorSnapSettings.scale, offset, axisLineScale[axisIndex], s_InitialScale, isProportionalScale);
            }
        }

        if (param.ShouldShow(ScaleHandleParam.Handle.XYZ) && (ids.xyz == GUIUtility.hotControl || !isHot))
        {
            Handles.color = isProportionalScale ? constrainProportionsScaleHandleColor : (Color)HandleUtils.ToActiveColorSpace.Invoke(null, new object[]{HandleUtils.centerColor});
            proportionalScale = false;
            EditorGUI.BeginChangeCheck();
            s_CurrentMultiplier = Handles.ScaleValueHandle(ids.xyz, s_CurrentMultiplier, position, rotation, handleSize * param.xyzSize, Handles.CubeHandleCap, EditorSnapSettings.scale);
            if (EditorGUI.EndChangeCheck())
            {
                scale = s_InitialScale * s_CurrentMultiplier;
            }
        }

        return scale;
    }
    
    internal const float kCameraViewThreshold = 0.6f;
    
    // When axis is looking away from camera, fade it out along 25 -> 15 degrees range
    static readonly float kCameraViewLerpStart1 = Mathf.Cos(Mathf.Deg2Rad * 25.0f);
    static readonly float kCameraViewLerpEnd1 = Mathf.Cos(Mathf.Deg2Rad * 15.0f);
    // When axis is looking towards the camera, fade it out along 170 -> 175 degrees range
    static readonly float kCameraViewLerpStart2 = Mathf.Cos(Mathf.Deg2Rad * 170.0f);
    static readonly float kCameraViewLerpEnd2 = Mathf.Cos(Mathf.Deg2Rad * 175.0f);
    internal static float GetCameraViewLerpForWorldAxis(Vector3 viewVector, Vector3 axis)
    {
        var dot = Vector3.Dot(viewVector, axis);
        var l1 = Mathf.InverseLerp(kCameraViewLerpStart1, kCameraViewLerpEnd1, dot);
        var l2 = Mathf.InverseLerp(kCameraViewLerpStart2, kCameraViewLerpEnd2, dot);
        return Mathf.Max(l1, l2);
    }
    
    internal struct ScaleHandleIds
    {
        public static ScaleHandleIds @default
        {
            get
            {
                return new ScaleHandleIds(
                    GUIUtility.GetControlID(s_xScaleHandleHash, FocusType.Passive),
                    GUIUtility.GetControlID(s_yScaleHandleHash, FocusType.Passive),
                    GUIUtility.GetControlID(s_zScaleHandleHash, FocusType.Passive),
                    GUIUtility.GetControlID(s_xyzScaleHandleHash, FocusType.Passive)
                );
            }
        }

        public readonly int x, y, z, xyz;

        public int this[int index]
        {
            get
            {
                switch (index)
                {
                    case 0: return x;
                    case 1: return y;
                    case 2: return z;
                    case 3: return xyz;
                }
                return -1;
            }
        }

        public bool Has(int id)
        {
            return x == id
                || y == id
                || z == id
                || xyz == id;
        }

        public ScaleHandleIds(int x, int y, int z, int xyz)
        {
            this.x = x;
            this.y = y;
            this.z = z;
            this.xyz = xyz;
        }

        public override int GetHashCode()
        {
            return x ^ y ^ z ^ xyz;
        }

        public override bool Equals(object obj)
        {
            if (!(obj is ScaleHandleIds))
                return false;

            var o = (ScaleHandleIds)obj;
            return o.x == x && o.y == y && o.z == z
                && o.xyz == xyz;
        }
    }

    internal struct ScaleHandleParam
    {
        [Flags]
        public enum Handle
        {
            None = 0,
            X = 1 << 0,
            Y = 1 << 1,
            Z = 1 << 2,
            XYZ = 1 << 3,
            All = ~None
        }

        public enum Orientation
        {
            Signed,
            Camera
        }

        static ScaleHandleParam s_Default = new ScaleHandleParam((Handle)(-1), Vector3.zero, Vector3.one, Vector3.one, 1, Orientation.Signed);
        public static ScaleHandleParam Default { get { return s_Default; } set { s_Default = value; } }

        public readonly Vector3 axisOffset;
        public readonly Vector3 axisSize;
        public readonly Vector3 axisLineScale;
        public readonly float xyzSize;
        public readonly Handle handles;
        public readonly Orientation orientation;

        public bool ShouldShow(int axis)
        {
            return (handles & (Handle)(1 << axis)) != 0;
        }

        public bool ShouldShow(Handle handle)
        {
            return (handles & handle) != 0;
        }

        public ScaleHandleParam(Handle handles, Vector3 axisOffset, Vector3 axisSize, Vector3 axisLineScale, float xyzSize, Orientation orientation)
        {
            this.axisOffset = axisOffset;
            this.axisSize = axisSize;
            this.axisLineScale = axisLineScale;
            this.xyzSize = xyzSize;
            this.handles = handles;
            this.orientation = orientation;
        }
    }
    
    private static float s_StartScale, s_ScaleDrawLength = 1.0f;
    private static float s_ValueDrag;
    private static Vector2 s_StartMousePosition, s_CurrentMousePosition;
    internal static float s_HoverExtraScale = 1.05f;
    
    internal static Vector3 DoAxis(int id, Vector3 scale, int scaleItemIndex, Vector3 position, Vector3 direction, Quaternion rotation, float size, float snap, float handleOffset, float lineScale, Vector3 initialScale, bool constrainProportionsScaling)
    {
        // If constrainProportionsScaling enabled, transforms behave the same way as Cube Handle does
        if (constrainProportionsScaling)
        {
            var value = DoAxis(id, scale.x, position, direction, rotation, size, EditorSnapSettings.scale, handleOffset, lineScale);
            //return initialScale * DoCenter(id, value, position, rotation, size, Handles.CubeHandleCap, snap);
        }
        else
        {
            scale[scaleItemIndex] = DoAxis(id, scale[scaleItemIndex], position, direction, rotation, size, snap, handleOffset, lineScale);
        }

        return scale;
    }
    
    internal static float DoAxis(int id, float scale, Vector3 position, Vector3 direction, Quaternion rotation, float size, float snap, float handleOffset, float lineScale)
    {
        //if (GUIUtility.hotControl == id)
           // Handles.handleLength = size * scale / s_StartScale;
        var positionOffset = direction * size * handleOffset;
        var s = size;
        // var s = GUIUtility.hotControl == id || Handles.proportionalScale
        //     ? Handles.handleLength
        //     : size;
        var startPosition = position + positionOffset;
        var cubePosition = position + direction * (s * s_ScaleDrawLength * lineScale) + positionOffset;

        Event evt = Event.current;
        switch (evt.GetTypeForControl(id))
        {
            case EventType.MouseMove:
            case EventType.Layout:
                HandleUtility.AddControl(id, HandleUtility.DistanceToLine(startPosition, cubePosition));
                HandleUtility.AddControl(id, HandleUtility.DistanceToCube(cubePosition, rotation, size * .1f));
                break;

            case EventType.MouseDown:
                // am I closest to the thingy?
                if (HandleUtility.nearestControl == id && evt.button == 0 && !evt.alt)
                {
                    GUIUtility.hotControl = id;    // Grab mouse focus
                    s_CurrentMousePosition = s_StartMousePosition = evt.mousePosition;
                    s_StartScale = scale;
                    evt.Use();
                    EditorGUIUtility.SetWantsMouseJumping(1);
                }
                break;

            case EventType.MouseDrag:
                if (GUIUtility.hotControl == id)
                {
                    s_CurrentMousePosition += evt.delta;
                    var dist = 1 + HandleUtility.CalcLineTranslation(s_StartMousePosition, s_CurrentMousePosition, position, direction) / size;
                    dist = Handles.SnapValue(dist, snap);
                    scale = s_StartScale * dist;
                    GUI.changed = true;
                    evt.Use();
                }
                break;

            case EventType.MouseUp:
                if (GUIUtility.hotControl == id && (evt.button == 0 || evt.button == 2))
                {
                    GUIUtility.hotControl = 0;
                    evt.Use();
                    EditorGUIUtility.SetWantsMouseJumping(0);
                }
                break;

            case EventType.Repaint:
                Color prevColor = new Color();
                float thickness = 0.01f;
                object[] parameters = new object[] {id, evt, null, null};
                
                HandleUtils.SetupHandleColor.Invoke(null, parameters);
                float capSize = size * .1f;
                if ((bool)HandleUtils.IsHovering.Invoke(null, new object[]{id, evt}))
                    capSize *= s_HoverExtraScale;

                var camera = Camera.current;
                var viewDir = camera != null ? camera.transform.forward : -direction;
                var facingAway = Vector3.Dot(viewDir, direction) < 0.0f;
                // draw line vs cube in the appropriate order based on viewing
                // direction, for correct transparency sorting
                var lineEndPos = position + direction * (s * s_ScaleDrawLength * lineScale - capSize * 0.5f) + positionOffset;
                if (facingAway)
                {
                    Handles.DrawLine(startPosition, lineEndPos, thickness);
                    Handles.CubeHandleCap(id, cubePosition, rotation, capSize, EventType.Repaint);
                }
                else
                {
                    Handles.CubeHandleCap(id, cubePosition, rotation, capSize, EventType.Repaint);
                    Handles.DrawLine(startPosition, lineEndPos, thickness);
                }
                Handles.color = prevColor;
                break;
        }

        return scale;
    }
}
