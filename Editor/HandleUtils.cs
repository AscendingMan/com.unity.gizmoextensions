using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;

public class HandleUtils
{
    public static MethodInfo GetCameraViewFrom;
    public static MethodInfo IsHovering;
    //public static MethodInfo DoPlanarHandle;
    public static MethodInfo CalcDrawOrder;
    public static MethodInfo GetColorByAxis;
    public static MethodInfo GetFadedAxisColor;
    public static MethodInfo ToActiveColorSpace;
    public static MethodInfo HandleMouseMove;
    public static MethodInfo DistanceToRectangleInternal;
    
    public static MethodInfo SetupHandleColor;

    
    // internal color for static handles
    internal static Color staticColor = new Color(.5f, .5f, .5f, 0f);
    // internal blend ratio for static colors
    internal static float staticBlend = 0.6f;
    internal static Color s_DisabledHandleColor = new Color(0.5f, 0.5f, 0.5f, 0.5f);
    public static Color centerColor = new Color(.8f, .8f, .8f, .93f);

    static HandleUtils()
    {
        GetCameraViewFrom = typeof(Handles).GetMethod("GetCameraViewFrom", BindingFlags.NonPublic | BindingFlags.Static); 
        IsHovering = typeof(Handles).GetMethod("IsHovering", BindingFlags.NonPublic | BindingFlags.Static);
        //DoPlanarHandle = typeof(Handles).GetMethod("DoPlanarHandle", BindingFlags.NonPublic | BindingFlags.Static);
        
        CalcDrawOrder = typeof(Handles).GetMethod("CalcDrawOrder", BindingFlags.NonPublic | BindingFlags.Static); 
        GetColorByAxis = typeof(Handles).GetMethod("GetColorByAxis", BindingFlags.NonPublic | BindingFlags.Static);
        GetFadedAxisColor = typeof(Handles).GetMethod("GetFadedAxisColor", BindingFlags.NonPublic | BindingFlags.Static);
        ToActiveColorSpace = typeof(Handles).GetMethod("ToActiveColorSpace", BindingFlags.NonPublic | BindingFlags.Static); 
        //DoPositionHandle_ArrowCap = typeof(Handles).GetMethod("DoPositionHandle_ArrowCap", BindingFlags.NonPublic | BindingFlags.Static);
        DistanceToRectangleInternal = typeof(HandleUtility).GetMethod("DistanceToRectangleInternal", BindingFlags.NonPublic | BindingFlags.Static);
        
        SetupHandleColor = typeof(Handles).GetMethod("SetupHandleColor", BindingFlags.NonPublic | BindingFlags.Static);

        
        var path = EditorApplication.applicationContentsPath;
        path = $"{path}/Managed/UnityEditor.dll";
        Assembly assembly = Assembly.LoadFile(path);
        Type type = assembly.GetType("UnityEditor.VertexSnapping");
        HandleMouseMove = type.GetMethod("HandleMouseMove", BindingFlags.Public | BindingFlags.Static);
    }
}
