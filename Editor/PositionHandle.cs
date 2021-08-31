using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEditor;

public class PositionHandle
{
    internal static int s_xAxisMoveHandleHash  = "xAxisFreeMoveHandleHash".GetHashCode();
    internal static int s_yAxisMoveHandleHash  = "yAxisFreeMoveHandleHash".GetHashCode();
    internal static int s_zAxisMoveHandleHash  = "zAxisFreeMoveHandleHash".GetHashCode();
    internal static int s_FreeMoveHandleHash  = "FreeMoveHandleHash".GetHashCode();
    internal static int s_xzAxisMoveHandleHash = "xzAxisFreeMoveHandleHash".GetHashCode();
    internal static int s_xyAxisMoveHandleHash = "xyAxisFreeMoveHandleHash".GetHashCode();
    internal static int s_yzAxisMoveHandleHash = "yzAxisFreeMoveHandleHash".GetHashCode();

    const float kFreeMoveHandleSizeFactor = 0.15f;
    
    internal struct PositionHandleIds
    {
        public static PositionHandleIds @default
        {
            get
            {
                return new PositionHandleIds(
                    GUIUtility.GetControlID(s_xAxisMoveHandleHash, FocusType.Passive),
                    GUIUtility.GetControlID(s_yAxisMoveHandleHash, FocusType.Passive),
                    GUIUtility.GetControlID(s_zAxisMoveHandleHash, FocusType.Passive),
                    GUIUtility.GetControlID(s_xyAxisMoveHandleHash, FocusType.Passive),
                    GUIUtility.GetControlID(s_xzAxisMoveHandleHash, FocusType.Passive),
                    GUIUtility.GetControlID(s_yzAxisMoveHandleHash, FocusType.Passive),
                    GUIUtility.GetControlID(s_FreeMoveHandleHash, FocusType.Passive)
                );
            }
        }

        public readonly int x, y, z, xy, yz, xz, xyz;

        public int this[int index]
        {
            get
            {
                switch (index)
                {
                    case 0: return x;
                    case 1: return y;
                    case 2: return z;
                    case 3: return xy;
                    case 4: return yz;
                    case 5: return xz;
                    case 6: return xyz;
                }
                return -1;
            }
        }

        public bool Has(int id)
        {
            return x == id
                || y == id
                || z == id
                || xy == id
                || yz == id
                || xz == id
                || xyz == id;
        }

        public PositionHandleIds(int x, int y, int z, int xy, int xz, int yz, int xyz)
        {
            this.x = x;
            this.y = y;
            this.z = z;
            this.xy = xy;
            this.yz = yz;
            this.xz = xz;
            this.xyz = xyz;
        }

        public override int GetHashCode()
        {
            return x ^ y ^ z ^ xy ^ xz ^ yz ^ xyz;
        }

        public override bool Equals(object obj)
        {
            if (!(obj is PositionHandleIds))
                return false;

            var o = (PositionHandleIds)obj;
            return o.x == x && o.y == y && o.z == z
                && o.xy == xy && o.xz == xz && o.yz == yz
                && o.xyz == xyz;
        }
    }

    internal struct PositionHandleParam
    {
        public static PositionHandleParam DefaultHandle = new PositionHandleParam(
            Handle.X | Handle.Y | Handle.Z | Handle.XY | Handle.XZ | Handle.YZ,
            Vector3.zero, Vector3.one, Vector3.zero, Vector3.one * 0.25f,
            Orientation.Signed, Orientation.Camera);
        public static PositionHandleParam DefaultFreeMoveHandle = new PositionHandleParam(
            Handle.X | Handle.Y | Handle.Z | Handle.XYZ,
            Vector3.zero, Vector3.one, Vector3.zero, Vector3.one * 0.25f,
            Orientation.Signed, Orientation.Signed);

        [Flags]
        public enum Handle
        {
            None = 0,
            X = 1 << 0,
            Y = 1 << 1,
            Z = 1 << 2,
            XY = 1 << 3,
            YZ = 1 << 4,
            XZ = 1 << 5,
            XYZ = 1 << 6,
            All = ~None
        }

        public enum Orientation
        {
            Signed,
            Camera
        }

        public readonly Vector3 axisOffset;
        public readonly Vector3 axisSize;
        public readonly Vector3 planeOffset;
        public readonly Vector3 planeSize;
        public readonly Handle handles;
        public readonly Orientation axesOrientation;
        public readonly Orientation planeOrientation;

        public bool ShouldShow(int axis)
        {
            return (handles & (Handle)(1 << axis)) != 0;
        }

        public bool ShouldShow(Handle handle)
        {
            return (handles & handle) != 0;
        }

        public PositionHandleParam(
            Handle handles,
            Vector3 axisOffset,
            Vector3 axisSize,
            Vector3 planeOffset,
            Vector3 planeSize,
            Orientation axesOrientation,
            Orientation planeOrientation)
        {
            this.axisOffset = axisOffset;
            this.axisSize = axisSize;
            this.planeOffset = planeOffset;
            this.planeSize = planeSize;
            this.handles = handles;
            this.axesOrientation = axesOrientation;
            this.planeOrientation = planeOrientation;
        }
    }
    
    static float[] s_DoPositionHandle_Internal_CameraViewLerp = new float[6];
    static int[] s_DoPositionHandle_Internal_NextIndex = { 1, 2, 0 };
    static int[] s_DoPositionHandle_Internal_PrevIndex = { 2, 0, 1 };
    static int[] s_DoPositionHandle_Internal_PrevPlaneIndex = { 5, 3, 4 };
    static int[] s_DoPositionHandle_Internal_AxisDrawOrder = { 0, 1, 2 };
    static string[] s_DoPositionHandle_Internal_AxisNames = { "xAxis", "yAxis", "zAxis" };
    // Hide & disable axis if they have faded out more than 60%
    internal const float kCameraViewThreshold = 0.6f;
    
    // Which octant the planar move handles are in.
    static Vector3 s_PlanarHandlesOctant = Vector3.one;
    static Vector3 s_DoPositionHandle_AxisHandlesOctant = Vector3.one;
    
    public static Color centerColor = new Color(.8f, .8f, .8f, .93f);

    // If the user is currently mouse dragging then this value will be True
    // and will disallow toggling Free Move mode on or off, or changing the octant of the planar handles.
    internal static bool currentlyDragging { get { return EditorGUIUtility.hotControl != 0; } }

    static Vector3 s_DoPositionHandle_ArrowCapConeOffset = Vector3.zero;
    
    //public static MethodInfo DoPositionHandle_ArrowCap;
    
    static Vector3[] s_AxisVector = { Vector3.right, Vector3.up, Vector3.forward };
    
    public PositionHandle()
    {

    }
    
    internal static Vector3 GetAxisVector(int axis)
    {
        return s_AxisVector[axis];
    }
    
    public static Vector3 DoPositionHandle_Internal(Vector3 position, Quaternion rotation, Vector3 direction)
    {
        var ids = PositionHandleIds.@default;
        var param = PositionHandleParam.DefaultHandle;
        Color temp = Handles.color;
    
        bool isDisabled = !GUI.enabled;
    
        // Calculate the camera view vector in Handle draw space
        // this handle the case where the matrix is skewed
        var handlePosition = Handles.matrix.MultiplyPoint3x4(position);
        var drawToWorldMatrix = Handles.matrix * Matrix4x4.TRS(position, rotation, Vector3.one);
        var invDrawToWorldMatrix = drawToWorldMatrix.inverse;
        var viewVectorDrawSpace = (Vector3)HandleUtils.GetCameraViewFrom.Invoke(null, new object[] {handlePosition, (Matrix4x4)invDrawToWorldMatrix});
        
        var size = HandleUtility.GetHandleSize(position);
        
        // Calculate per axis camera lerp
        // for (var i = 0; i < 3; ++i)
        //     s_DoPositionHandle_Internal_CameraViewLerp[i] = ids[i] == GUIUtility.hotControl ? 0 : (float)GetCameraViewLerpForWorldAxis.Invoke(null, new object[] {viewVectorDrawSpace, GetAxisVector(i)});
        // // Calculate per plane camera lerp (xy, yz, xz)
        // for (var i = 0; i < 3; ++i)
        //     s_DoPositionHandle_Internal_CameraViewLerp[3 + i] = Mathf.Max(s_DoPositionHandle_Internal_CameraViewLerp[i], s_DoPositionHandle_Internal_CameraViewLerp[(i + 1) % 3]);
        
        var isHot = ids.Has(GUIUtility.hotControl);
        var axisOffset = param.axisOffset;
        var planeOffset = param.planeOffset;
        if (isHot)
        {
            axisOffset = Vector3.zero;
            planeOffset = Vector3.zero;
        }
        
        // Draw plane handles (xy, yz, xz)
        var planeSize = isHot ? param.planeSize + param.planeOffset : param.planeSize;
        for (var i = 0; i < 3; ++i)
        {
            if (!param.ShouldShow(3 + i) || isHot && ids[3 + i] != GUIUtility.hotControl)
                continue;
        
            var cameraLerp = isHot ? 0 : s_DoPositionHandle_Internal_CameraViewLerp[3 + i];
            if (cameraLerp <= kCameraViewThreshold)
            {
                var offset = planeOffset * size;
                offset[s_DoPositionHandle_Internal_PrevIndex[i]] = 0;
                var planarSize = Mathf.Max(planeSize[i], planeSize[s_DoPositionHandle_Internal_NextIndex[i]]);
                position = DoPlanarHandle(ids[3 + i], i, position, offset, rotation, size * planarSize, cameraLerp, viewVectorDrawSpace, param.planeOrientation);
            }
        }
        
        // Draw axis sliders last to have priority over the planes
        HandleUtils.CalcDrawOrder.Invoke(null, new object[] {viewVectorDrawSpace, s_DoPositionHandle_Internal_AxisDrawOrder});
        for (var ii = 0; ii < 3; ++ii)
        {
            int i = s_DoPositionHandle_Internal_AxisDrawOrder[ii];
        
            if (!param.ShouldShow(i))
                continue;
        
            if (!currentlyDragging)
            {
                switch (param.axesOrientation)
                {
                    case PositionHandleParam.Orientation.Camera:
                        s_DoPositionHandle_AxisHandlesOctant[i] = viewVectorDrawSpace[i] > 0.01f ? -1 : 1;
                        break;
                    case PositionHandleParam.Orientation.Signed:
                        s_DoPositionHandle_AxisHandlesOctant[i] = 1;
                        break;
                }
            }
        
            var isThisAxisHot = isHot && ids[i] == GUIUtility.hotControl;
        
            var axisColor = (Color)HandleUtils.GetColorByAxis.Invoke(null, new object[] {i});
            Handles.color = isDisabled ? Color.Lerp(axisColor, HandleUtils.staticColor, HandleUtils.staticBlend) : axisColor;
            GUI.SetNextControlName(s_DoPositionHandle_Internal_AxisNames[i]);
        
            // if we are hot here, the hot handle must be opaque
            var cameraLerp = isThisAxisHot ? 0 : s_DoPositionHandle_Internal_CameraViewLerp[i];

            if (cameraLerp <= kCameraViewThreshold)
            {
                Handles.color =
                    (Color) HandleUtils.GetFadedAxisColor.Invoke(null, new object[] {Handles.color, cameraLerp, ids[i]});
                var axisVector = GetAxisVector(i);
                var dir = rotation * axisVector;
                var offset = dir * axisOffset[i] * size;

                dir *= s_DoPositionHandle_AxisHandlesOctant[i];
                offset *= s_DoPositionHandle_AxisHandlesOctant[i];

                if (isHot && !isThisAxisHot)
                    Handles.color = HandleUtils.s_DisabledHandleColor;

                // A plane with this axis is hot
                if (isHot && (ids[s_DoPositionHandle_Internal_PrevPlaneIndex[i]] == GUIUtility.hotControl ||
                              ids[i + 3] == GUIUtility.hotControl))
                    Handles.color = GizmoExtensions.Styles.activeAxisColor;

                if(isThisAxisHot)
                    Handles.color = GizmoExtensions.Styles.activeAxisColor;
                
                Handles.color = (Color) HandleUtils.ToActiveColorSpace.Invoke(null, new object[] {Handles.color});
        
                s_DoPositionHandle_ArrowCapConeOffset = isHot
                    ? rotation * Vector3.Scale(Vector3.Scale(axisVector, param.axisOffset), s_DoPositionHandle_AxisHandlesOctant)
                    : Vector3.zero;

                if (isHot && isThisAxisHot)
                {
                    Handles.color = GizmoExtensions.Styles.activeAxisColor;
                    dir = direction;
                }
                
                if(!isHot || (isHot && isThisAxisHot))
                    position = Handles.Slider(ids[i], position, offset, dir, size * param.axisSize[i], DoPositionHandle_ArrowCap, EditorSnapSettings.move[i]);
            }
        }
        
        // IGNORE SNAPPING FOR NOW
        //VertexSnapping.HandleMouseMove(ids.xyz);
        if(Event.current.shift)
            param = PositionHandleParam.DefaultFreeMoveHandle;
        
        HandleUtils.HandleMouseMove.Invoke(null, new object[]{ids.xyz});
        if (param.ShouldShow(PositionHandleParam.Handle.XYZ) && (ids.xyz == GUIUtility.hotControl || !isHot))
        {
            Handles.color = (Color)HandleUtils.ToActiveColorSpace.Invoke(null, new object[]{centerColor});
            GUI.SetNextControlName("FreeMoveAxis");
            position = Handles.FreeMoveHandle(ids.xyz, position, rotation, size * kFreeMoveHandleSizeFactor, EditorSnapSettings.move, RectangleHandleCap);
        }
        //
        // if (GridSnapping.active)
        //     position = GridSnapping.Snap(position);
        
        Handles.color = temp;
        
        return position;
    }
    
    static void DoPositionHandle_ArrowCap(int controlId, Vector3 position, Quaternion rotation, float size, EventType eventType)
    {
        Handles.ArrowHandleCap(controlId, position, rotation, size, eventType);
    }
    
    static Vector3[] verts = {Vector3.zero, Vector3.zero, Vector3.zero, Vector3.zero};
    
    static Vector3 DoPlanarHandle(
    int id,
    int planePrimaryAxis,
    Vector3 position,
    Vector3 offset,
    Quaternion rotation,
    float handleSize,
    float cameraLerp,
    Vector3 viewVectorDrawSpace,
    PositionHandleParam.Orientation orientation)
    {
        var positionOffset = offset;

        var axis1index = planePrimaryAxis;
        var axis2index = (axis1index + 1) % 3;
        var axisNormalIndex = (axis1index  + 2) % 3;

        Color prevColor = Handles.color;

        bool isDisabled = !GUI.enabled;
        Handles.color = isDisabled ? HandleUtils.staticColor : (Color)HandleUtils.GetColorByAxis.Invoke(null, new object[] {axisNormalIndex});//GetColorByAxis(axisNormalIndex);
        Handles.color = (Color)HandleUtils.GetFadedAxisColor.Invoke(null, new object[] {Handles.color, cameraLerp, id});//GetFadedAxisColor(Handles.color, cameraLerp, id);

        float faceOpacity = 0.8f;
        if (GUIUtility.hotControl == id)
            Handles.color = Handles.selectedColor;
        else if ((bool)HandleUtils.IsHovering.Invoke(null, new object[]{id, Event.current}))//(Handles.IsHovering(id, Event.current))
            faceOpacity = 0.4f;
        else
            faceOpacity = 0.1f;

        Handles.color = (Color)HandleUtils.ToActiveColorSpace.Invoke(null, new object[] {Handles.color});//ToActiveColorSpace(Handles.color);
        
        // NOTE: The planar transform handles always face toward the camera so they won't
        // obscure each other (unlike the X, Y, and Z axis handles which always face in the
        // positive axis directions). Whenever the octant that the camera is in (relative to
        // to the transform tool) changes, we need to move the planar transform handle
        // positions to the correct octant.

        // Comments below assume axis1 is X and axis2 is Z to make it easier to visualize things.

        // Shift the planar transform handle in the positive direction by half its
        // handleSize so that it doesn't overlap in the center of the transform gizmo,
        // and also move the handle origin into the octant that the camera is in.
        // Don't update the actant while dragging to avoid too much distraction.
        if (!currentlyDragging)
        {
            switch (orientation)
            {
                case PositionHandleParam.Orientation.Camera:
                    // Offset the X position of the handle in negative direction if camera is in the -X octants; otherwise positive.
                    // Test against -0.01 instead of 0 to give a little bias to the positive quadrants. This looks better in axis views.
                    s_PlanarHandlesOctant[axis1index] = (viewVectorDrawSpace[axis1index] > 0.01f ? -1 : 1);
                    // Likewise with the other axis.
                    s_PlanarHandlesOctant[axis2index] = (viewVectorDrawSpace[axis2index] > 0.01f ? -1 : 1);
                    break;
                case PositionHandleParam.Orientation.Signed:
                    s_PlanarHandlesOctant[axis1index] = 1;
                    s_PlanarHandlesOctant[axis2index] = 1;
                    break;
            }
        }
        Vector3 handleOffset = s_PlanarHandlesOctant;
        // Zero out the offset along the normal axis.
        handleOffset[axisNormalIndex] = 0;
        positionOffset = rotation * Vector3.Scale(positionOffset, handleOffset);
        // Rotate and scale the offset
        handleOffset = rotation * (handleOffset * handleSize * 0.5f);

        // Calculate 3 axes
        Vector3 axis1 = Vector3.zero;
        Vector3 axis2 = Vector3.zero;
        Vector3 axisNormal = Vector3.zero;
        axis1[axis1index] = 1;
        axis2[axis2index] = 1;
        axisNormal[axisNormalIndex] = 1;
        axis1 = rotation * axis1;
        axis2 = rotation * axis2;
        axisNormal = rotation * axisNormal;

        // Draw the "filler" color for the handle
        verts[0] = position + positionOffset + handleOffset + (axis1 + axis2) * handleSize * 0.5f;
        verts[1] = position + positionOffset + handleOffset + (-axis1 + axis2) * handleSize * 0.5f;
        verts[2] = position + positionOffset + handleOffset + (-axis1 - axis2) * handleSize * 0.5f;
        verts[3] = position + positionOffset + handleOffset + (axis1 - axis2) * handleSize * 0.5f;
        Color faceColor = new Color(Handles.color.r, Handles.color.g, Handles.color.b, Handles.color.a * faceOpacity);
        Handles.DrawSolidRectangleWithOutline(verts, faceColor, Color.clear);

        // And then render the handle itself (this is the colored outline)
        position = Handles.Slider2D(id,
            position,
            handleOffset + positionOffset,
            axisNormal,
            axis1, axis2,
            handleSize * 0.5f,
            Handles.RectangleHandleCap, new Vector2(EditorSnapSettings.move[axis1index], EditorSnapSettings.move[axis2index]),
            false);

        Handles.color = prevColor;

        return position;
    }
    
    // Draw a camera-facing Rectangle. Pass this into handle functions.
    static Vector3[] s_RectangleHandlePointsCache = new Vector3[5];
    public static void RectangleHandleCap(int controlID, Vector3 position, Quaternion rotation, float size, EventType eventType)
    {
        RectangleHandleCap(controlID, position, rotation, new Vector2(size, size), eventType);
    }

    internal static void RectangleHandleCap(int controlID, Vector3 position, Quaternion rotation, Vector2 size, EventType eventType)
    {
        switch (eventType)
        {
            case EventType.Layout:
            case EventType.MouseMove:
                // TODO: Create DistanceToRectangle
                HandleUtility.AddControl(controlID, (float)HandleUtils.DistanceToRectangleInternal.Invoke(null, new object[]{position, rotation, size}));
                break;
            case (EventType.Repaint):
                Vector3 sideways = rotation * new Vector3(size.x, 0, 0);
                Vector3 up = rotation * new Vector3(0, size.y, 0);
                s_RectangleHandlePointsCache[0] = position + sideways + up;
                s_RectangleHandlePointsCache[1] = position + sideways - up;
                s_RectangleHandlePointsCache[2] = position - sideways - up;
                s_RectangleHandlePointsCache[3] = position - sideways + up;
                s_RectangleHandlePointsCache[4] = position + sideways + up;
                Handles.DrawPolyLine(s_RectangleHandlePointsCache);
                break;
        }
    }
}
