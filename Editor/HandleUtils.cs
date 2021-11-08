using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using Unity.VisualScripting;
using UnityEditor;
using UnityEditor.Overlays;
using UnityEngine;

public class HandleUtils
{
    internal enum DraggingLockedState
    {
        NotDragging,
        Dragging,
        LookAt,
    }
    
    public static PropertyInfo draggingLocked;
    public static PropertyInfo s_StartHandlePosition;
    public static PropertyInfo rootVisualElement;
    public static MethodInfo RebuildContent;
    public static MethodInfo HandleHasMoved;
    public static MethodInfo GetAxisVector;
    
    static HandleUtils()
    {
        draggingLocked = typeof(SceneView).GetProperty("draggingLocked", BindingFlags.NonPublic | BindingFlags.Instance); 
        rootVisualElement = typeof(Overlay).GetProperty("rootVisualElement", BindingFlags.NonPublic | BindingFlags.Instance);
        RebuildContent = typeof(Overlay).GetMethod("RebuildContent", BindingFlags.NonPublic | BindingFlags.Instance);
        GetAxisVector = typeof(Handles).GetMethod("GetAxisVector", BindingFlags.NonPublic | BindingFlags.Static);

        var path = EditorApplication.applicationContentsPath;
        path = $"{path}/Managed/UnityEditor.dll";
        Assembly assembly = Assembly.LoadFile(path);
        Type type = assembly.GetType("UnityEditor.TransformManipulator");
        s_StartHandlePosition = type.GetProperty("mouseDownHandlePosition", BindingFlags.Public | BindingFlags.Static);
        HandleHasMoved = type.GetMethod("HandleHasMoved", BindingFlags.Public | BindingFlags.Static);
    }
}
