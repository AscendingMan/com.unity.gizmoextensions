using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Numerics;
using UnityEngine;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine.Animations;
using UnityEngine.SceneManagement;
using UnityEngine.XR;
using Quaternion = UnityEngine.Quaternion;
using Vector3 = UnityEngine.Vector3;

[CustomEditor(typeof(GameObject))]
public class GizmoExtensions : UnityEditor.Editor
{
    internal enum ActiveTool
    {
        Translate,
        Rotate,
        Scale
    }
    
    internal enum OffsetLocation
    {
        Floating,
        OnGizmo
    }

    internal static ActiveTool m_ActiveTool = ActiveTool.Translate;
    internal static int m_FontSize = 2;
    internal static float m_UnitSnapSpacing = 4; // this is a multiplier used with EditorSnapSettings.move[x, y, z]
    
    internal Transform targetTransform;
    public static class Styles
    {
        public static Color activeAxisColor = new Color(0.44f, 0.737f, 0.83f);
        public static Color planeColor = new Color(0.44f, 0.737f, 0.83f, 0.3f);
        public static Color dottedAxisColor = new Color(0.44f, 0.737f, 0.83f, 0.5f);
        public static Color textColor = Color.white;
    }
    
    float size = 0.5f;

    private Vector3 positionOnMouseDown;
    private Quaternion rotationOnMouseDown;
    bool enteredDrag = false;

    private PositionHandle handle;

    public void OnEnable()
    {
        targetTransform = (target as GameObject).transform;
        handle = new PositionHandle();
    }
    
    public void OnSceneGUI()
    {
        if (targetTransform == null)
            return;
        
        if (m_ActiveTool == ActiveTool.Translate)
            DoTranslate();
        if (m_ActiveTool == ActiveTool.Rotate)
            DoRotate();
        if (m_ActiveTool == ActiveTool.Scale)
            DoScale();
    }
   
    void DoScale()
    {
        var evtType = Event.current.type;
        if (evtType == EventType.MouseDown || evtType == EventType.MouseDrag)
            EditorGUI.BeginChangeCheck();
        
        var previousRotation = targetTransform.rotation;
        var newScale = ScaleHandle.DoScaleHandle(ScaleHandle.ScaleHandleIds.@default, targetTransform.localScale, targetTransform.position, targetTransform.rotation, HandleUtility.GetHandleSize(targetTransform.position),
            ScaleHandle.ScaleHandleParam.Default);
        
        if (evtType == EventType.MouseDown || evtType == EventType.MouseDrag)
        {
            if (!enteredDrag)
            {
                enteredDrag = true;
                rotationOnMouseDown = previousRotation;
            }

            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(targetTransform, "Change rotation");
                targetTransform.localScale = newScale;
            }
        }
        else if(evtType == EventType.MouseUp)
        {
            enteredDrag = false;
            positionOnMouseDown = Vector3.zero;
            EditorGUI.EndChangeCheck();
        }

        var controlID = GUIUtility.GetControlID(FocusType.Passive);
        
        if (enteredDrag)
        {
            DoTextLabel(targetTransform.position, targetTransform.rotation.eulerAngles, ScaleHandle.s_CurrentMultiplier.ToString("f2"), GizmoExtensions.Styles.textColor);
        }

        var newSize = TryGetBoundsOffsetFromCenter(targetTransform.gameObject).magnitude / 2;
        var arcSpacing = ScaleHandle.s_InitialScale.magnitude;
        var arcCount = (int)ScaleHandle.s_CurrentMultiplier * 2;
        for (int i = 1; i < arcCount; i++)
        {
            Handles.color = Styles.activeAxisColor;
            var radius = arcSpacing * i;
            //if (radius > newSize * arcSpacing - 0.5f && radius <= newSize * arcSpacing + 0.5f)
            if (radius > newSize - 0.2f && radius <= newSize + 0.8f)
                Handles.color = Color.white;
            Handles.DrawWireArc(targetTransform.position, Camera.current.transform.forward, Camera.current.transform.right, 30 + i * 5, radius, 3f);
        }

    }
    
    void DoRotate()
    {
        var evtType = Event.current.type;
        if (evtType == EventType.MouseDown || evtType == EventType.MouseDrag)
            EditorGUI.BeginChangeCheck();
        
        var previousRotation = targetTransform.rotation;
        var newRot = RotationHandle.DoRotationHandle(RotationHandle.RotationHandleIds.@default, targetTransform.rotation, targetTransform.position, RotationHandle.RotationHandleParam.Default);
        
        if (evtType == EventType.MouseDown || evtType == EventType.MouseDrag)
        {
            if (!enteredDrag)
            {
                enteredDrag = true;
                rotationOnMouseDown = previousRotation;
            }

            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(targetTransform, "Change rotation");
                targetTransform.rotation = newRot;
            }
        }
        else if(evtType == EventType.MouseUp)
        {
            enteredDrag = false;
            positionOnMouseDown = Vector3.zero;
            EditorGUI.EndChangeCheck();
        }

        var controlID = GUIUtility.GetControlID(FocusType.Passive);
        
        if (enteredDrag) 
        {
            Handles.color = Styles.activeAxisColor;
            Vector3 from = (Disc.s_StartPosition - targetTransform.position);
            
            var d = -Mathf.Sign(Disc.s_RotationDist) * Mathf.Repeat(Mathf.Abs(Disc.s_RotationDist), 360);
            Vector3 to = Quaternion.AngleAxis(d, targetTransform.rotation * RotationHandle.s_AxisVector[RotationHandle.activeIndex]) * from;
            Color prevColor = Handles.color;
            Handles.color = Color.white;
            Handles.DrawLine(targetTransform.position, targetTransform.position + to, Handles.lineThickness);
            Handles.color = prevColor;
            
            DoTextLabel(targetTransform.position + to, targetTransform.rotation.eulerAngles, Disc.s_RotationDist.ToString("f2"), GizmoExtensions.Styles.textColor);
        }
    }
    
    void DoTranslate()
    {
        var evtType = Event.current.type;
        if (evtType == EventType.MouseDown || evtType == EventType.MouseDrag)
            EditorGUI.BeginChangeCheck();
        
        var previousPosition = targetTransform.position;
        var newPos = PositionHandle.DoPositionHandle_Internal(targetTransform.position, targetTransform.rotation, targetTransform.position - positionOnMouseDown);
        
        if (evtType == EventType.MouseDown || evtType == EventType.MouseDrag)
        {
            if (!enteredDrag)
            {
                enteredDrag = true;
                positionOnMouseDown = previousPosition;
            }

            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(targetTransform, "Change position");
                targetTransform.position = newPos;
            }
        }
        else if(evtType == EventType.MouseUp)
        {
            enteredDrag = false;
            positionOnMouseDown = Vector3.zero;
            EditorGUI.EndChangeCheck();
        }

        var controlID = GUIUtility.GetControlID(FocusType.Passive);

        GetChangedAxes(out var axes, positionOnMouseDown, targetTransform.position);

        if (enteredDrag) 
        {
            Handles.color = Styles.activeAxisColor;

            var dir = targetTransform.position - positionOnMouseDown;
            bool[] changedAxes;
            GetChangedAxes(out changedAxes, positionOnMouseDown, targetTransform.position);
            var changedAxisCount = changedAxes.Where(ax => ax == true).Count();

            if (changedAxisCount == 1)
            {
                int activeAxis = 0;
                for (int i = 0; i < 3; i++)
                {
                    activeAxis = changedAxes[i] ? i : 0;
                }
                DoSingleAxisGraphics(targetTransform.position, dir, positionOnMouseDown, controlID, targetTransform.gameObject, activeAxis);
            }
            if (changedAxisCount == 2)
                DoPlanarGraphics(targetTransform.position, dir, positionOnMouseDown, controlID, changedAxes, targetTransform.gameObject);
            if (changedAxisCount == 3)
                DoFreeHandleGraphics(targetTransform.position, dir, positionOnMouseDown, controlID, changedAxes,
                    targetTransform.gameObject);
        }
    }
    
    static void DoPlanarGraphics(Vector3 position, Vector3 direction, Vector3 positionOnMouseDown, int controlID, bool[] axes, GameObject go)
    {
        bool snappingActive = (bool) Disc.incrementalSnapActive.GetValue(typeof(EditorSnapSettings));
        // draw line
        Handles.DrawLine(positionOnMouseDown, position, Handles.lineThickness);
        var rotation = direction.magnitude == 0 ? Quaternion.identity : Quaternion.LookRotation(positionOnMouseDown);
        Handles.SphereHandleCap(controlID, positionOnMouseDown, rotation, HandleUtility.GetHandleSize(positionOnMouseDown) * 0.15f, EventType.Repaint);
        Handles.SphereHandleCap(controlID, position, rotation, HandleUtility.GetHandleSize(positionOnMouseDown) * 0.15f, EventType.Repaint);

        Vector3 p2 = Vector3.zero, p4 = Vector3.zero;
        // draw rectangle
        var p1 = positionOnMouseDown;
        var p3 = position;

        var offsetZ = m_UnitSnapSpacing * EditorSnapSettings.move[2];
        var offsetY = m_UnitSnapSpacing * EditorSnapSettings.move[1];
        var offsetX = m_UnitSnapSpacing * EditorSnapSettings.move[0];

        var capSize = HandleUtility.GetHandleSize(positionOnMouseDown) * 0.15f;
        
        if (axes[0] && axes[2])
        {
            p2 = new Vector3(position.x, positionOnMouseDown.y, positionOnMouseDown.z);
            p4 = new Vector3(positionOnMouseDown.x, positionOnMouseDown.y, position.z);
            
            if (snappingActive)
            {
                var xDif = position.x - positionOnMouseDown.x;
                var zDif = position.z - positionOnMouseDown.z;
                int xSign = (int)(xDif / Math.Abs(xDif));
                int zSign = (int)(zDif / Math.Abs(zDif));
                
                var x1 = positionOnMouseDown.x - xSign * offsetX;
                var z1 = positionOnMouseDown.z - zSign * offsetZ;
                
                Handles.color = Styles.planeColor;
                var countX = Math.Abs((position.x - positionOnMouseDown.x) / offsetX + 2 * offsetX * xSign);
                var countZ = Math.Abs((position.z - positionOnMouseDown.z) / offsetZ + 2 * offsetZ * zSign);

                for (int x = 0; x < countX; x++)
                {
                    for (int z = 0; z < countZ; z++)
                    {
                        var pos = new Vector3(x1 + xSign * offsetX * x, positionOnMouseDown.y, z1 + zSign * offsetZ * z);
                        Handles.SphereHandleCap(controlID, pos, rotation, capSize, EventType.Repaint);
                    }
                }
            }
        }
        else if (axes[0] && axes[1])
        {
            p2 = new Vector3(position.x, positionOnMouseDown.y, positionOnMouseDown.z);
            p4 = new Vector3(positionOnMouseDown.x, position.y, positionOnMouseDown.z);
            
            if (snappingActive)
            {
                var xDif = position.x - positionOnMouseDown.x;
                var yDif = position.y - positionOnMouseDown.y;
                int xSign = (int)(xDif / Math.Abs(xDif));
                int ySign = (int)(yDif / Math.Abs(yDif));
                
                var x1 = positionOnMouseDown.x - xSign * offsetX;
                var y1 = positionOnMouseDown.y - ySign * offsetY;
                
                Handles.color = Styles.planeColor;
                var countX = Math.Abs((position.x - positionOnMouseDown.x) / offsetX + 2 * offsetX * xSign);
                var countY = Math.Abs((position.y - positionOnMouseDown.y) / offsetY + 2 * offsetY * ySign);

                for (int x = 0; x < countX; x++)
                {
                    for (int y = 0; y < countY; y++)
                    {
                        var pos = new Vector3(x1 + xSign * offsetX * x, y1 + ySign * offsetY * y, positionOnMouseDown.z);
                        Handles.SphereHandleCap(controlID, pos, rotation, capSize, EventType.Repaint);
                    }
                }
            }
        }
        else if (axes[1] && axes[2])
        {
            p2 = new Vector3(positionOnMouseDown.x, positionOnMouseDown.y, position.z);
            p4 = new Vector3(positionOnMouseDown.x, position.y, positionOnMouseDown.z);
            
            if (snappingActive)
            {
                var zDif = position.z - positionOnMouseDown.z;
                var yDif = position.y - positionOnMouseDown.y;
                int zSign = (int)(zDif / Math.Abs(zDif));
                int ySign = (int)(yDif / Math.Abs(yDif));
                
                var z1 = positionOnMouseDown.z - zSign * offsetZ;
                var y1 = positionOnMouseDown.y - ySign * offsetY;
                
                Handles.color = Styles.planeColor;
                var countZ = Math.Abs((position.z - positionOnMouseDown.z) / offsetZ + 2 * offsetZ * zSign);
                var countY = Math.Abs((position.y - positionOnMouseDown.y) / offsetY + 2 * offsetY * ySign);
                
                Debug.Log($"X {zSign}");
                Debug.Log($"Z {ySign}");
                
                for (int z = 0; z < countZ; z++)
                {
                    for (int y = 0; y < countY; y++)
                    {
                        var pos = new Vector3(positionOnMouseDown.x, y1 + ySign * offsetY * y, z1 + zSign * offsetZ * z);
                        Handles.SphereHandleCap(controlID, pos, rotation, capSize, EventType.Repaint);
                    }
                }
            }
        }

        Handles.color = Styles.planeColor;
        var color = Styles.planeColor;
        Handles.DrawSolidRectangleWithOutline(new Vector3[]{p1, p2, p3, p4}, color, color);
        var offset = GetChangedOffset(positionOnMouseDown, position);
        DoTextLabel(position, direction, offset.ToString("f2"), Styles.textColor);
    }

    static void DoFreeHandleGraphics(Vector3 position, Vector3 direction, Vector3 positionOnMouseDown, int controlID, bool[] axes, GameObject go)
    {
        Handles.DrawLine(positionOnMouseDown, position, Handles.lineThickness);

        var rotation = direction.magnitude == 0 ? Quaternion.identity : Quaternion.LookRotation(positionOnMouseDown);
        Handles.SphereHandleCap(controlID, positionOnMouseDown, rotation, HandleUtility.GetHandleSize(positionOnMouseDown) * 0.15f, EventType.Repaint);
        Handles.SphereHandleCap(controlID, position, rotation, HandleUtility.GetHandleSize(positionOnMouseDown) * 0.15f, EventType.Repaint);
        
        // Draw XZ plane
        var p1 = positionOnMouseDown;
        var p3 = new Vector3(position.x, positionOnMouseDown.y, position.z);
        var p2 = new Vector3(position.x, positionOnMouseDown.y, positionOnMouseDown.z);
        var p4 = new Vector3(positionOnMouseDown.x, positionOnMouseDown.y, position.z);

        var color = Styles.planeColor;
        Handles.DrawSolidRectangleWithOutline(new Vector3[]{p1, p2, p3, p4}, color, color);
        var offset = GetChangedOffset(positionOnMouseDown, position);
        DoTextLabel(position, direction, offset.ToString("f2"), Styles.textColor);
        
        // DRAW XY plane
        p1 = new Vector3(position.x, positionOnMouseDown.y, position.z);
        p2 = new Vector3(position.x, position.y, position.z);
        p3 = new Vector3(positionOnMouseDown.x, position.y, position.z);
        p4 = new Vector3(positionOnMouseDown.x, positionOnMouseDown.y, position.z);
        Handles.DrawSolidRectangleWithOutline(new Vector3[]{p1, p2, p3, p4}, color, color);
    }
    
    static void DoSingleAxisGraphics(Vector3 position, Vector3 direction, Vector3 positionOnMouseDown, int controlID, GameObject go, int activeAxis)
    {
        bool snappingActive = (bool) Disc.incrementalSnapActive.GetValue(typeof(EditorSnapSettings));

        var prevColor = Handles.color;

        Handles.DrawLine(positionOnMouseDown, position, Handles.lineThickness);

        var rotation = direction.magnitude == 0 ? Quaternion.identity : Quaternion.LookRotation(positionOnMouseDown);
        Handles.SphereHandleCap(controlID, positionOnMouseDown, rotation, HandleUtility.GetHandleSize(positionOnMouseDown) * 0.15f, EventType.Repaint);
        Handles.SphereHandleCap(controlID, position, rotation, HandleUtility.GetHandleSize(positionOnMouseDown) * 0.15f, EventType.Repaint);

        Handles.color = Styles.dottedAxisColor;

        Handles.DrawDottedLine(positionOnMouseDown, direction.normalized * 50f + positionOnMouseDown, 5f);
        Handles.color = prevColor;

        if (snappingActive)
        {
            var snapUnitOffset = EditorSnapSettings.move[activeAxis] * m_UnitSnapSpacing;
            var dist = (positionOnMouseDown - position).magnitude;
            var unitSnapCount = (int)(dist / snapUnitOffset);
            for (int i = 1; i <= unitSnapCount; i++)
            {
                var pos = direction.normalized * i * snapUnitOffset + positionOnMouseDown;
                var unitSnapDir = activeAxis == 0 ? go.transform.forward : go.transform.right;
                var p1 = pos + unitSnapDir * HandleUtility.GetHandleSize(positionOnMouseDown) * 0.1f;
                var p2 = pos - unitSnapDir * HandleUtility.GetHandleSize(positionOnMouseDown) * 0.1f;
                Handles.DrawLine(p1, p2, HandleUtility.GetHandleSize(positionOnMouseDown) * 0.15f);
            }
        }
        
        var offset = GetChangedOffset(positionOnMouseDown, position);
        DoTextLabel(position, direction, offset.ToString("f2"), Styles.textColor);
    }
    
    static void DoTextLabel(Vector3 position, Vector3 rotation, string text, Color color)
    {
        var style = new GUIStyle(EditorStyles.label);
        style.normal.textColor = color;
        style.onNormal.textColor = color;
        style.active.textColor = color;
        style.onActive.textColor = color;
        style.onFocused.textColor = color;
        style.focused.textColor = color;
        style.hover.textColor = color;
        style.onHover.textColor = color;
        
        style.fontSize *= m_FontSize;
        
        var offsetContent = new GUIContent(text);
        var screenPos = HandleUtility.WorldPointToSizedRect(position, offsetContent, style);
        screenPos.size = style.CalcSize(offsetContent);

        Handles.BeginGUI();
        GUI.Box(screenPos, "");
        GUI.Label(screenPos, text, style);
        Handles.EndGUI();
    }
    
    static Vector3 TryGetBoundsOffsetFromCenter(GameObject go) 
    {
        go.TryGetComponent(out Renderer renderer);
        if(renderer != null)
            return renderer.bounds.size;
        go.TryGetComponent(out Collider collider);
        if (collider != null)
            return collider.bounds.size;

        return Vector3.zero;
    }

    static void GetChangedAxes(out bool[] axes, Vector3 pos1, Vector3 pos2) 
    {
        axes = new bool[] { false, false, false };
        if(Mathf.Abs(pos1.x - pos2.x) > 0) 
        {
            axes[0] = true;
        }
        if (Mathf.Abs(pos1.y - pos2.y) > 0)
        {
            axes[1] = true;
        }
        if (Mathf.Abs(pos1.z - pos2.z) > 0)
        {
            axes[2] = true;
        }
    }
    
    static float GetChangedOffset(Vector3 pos1, Vector3 pos2)
    {
        var offset = 0.0f;
        var axes = new bool[] { false, false, false };
        GetChangedAxes(out axes, pos1, pos2);
        if (axes[0] && !axes[1] && !axes[2])
            return Mathf.Abs(pos1.x - pos2.x);
        if (!axes[0] && axes[1] && !axes[2])
            return Mathf.Abs(pos1.y- pos2.y);
        if (!axes[0] && !axes[1] && axes[2])
            return Mathf.Abs(pos1.z - pos2.z);

        return (pos1 - pos2).magnitude;
    }
}
