using UnityEditor;
using UnityEditor.Graphs;
using UnityEditor.Overlays;
using UnityEditor.Toolbars;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

// Same as above, except this time we'll create a toggle + dropdown toolbar item.
[EditorToolbarElement(id, typeof(SceneView))]
class DropdownToggleExample : EditorToolbarDropdownToggle, IAccessContainerWindow
{
    public const string id = "ExampleToolbar/DropdownToggle";

    // This property is specified by IAccessContainerWindow and is used to access the Overlay's EditorWindow.
    public EditorWindow containerWindow { get; set; }
    static int colorIndex = 0;
    static readonly Color[] colors = new Color[] { Color.red, Color.green, Color.cyan };
    
    DropdownToggleExample()
    {
        text = "Color Bar";
        tooltip = "Display a color swatch in the top left of the scene view. Toggle on or off, and open the dropdown" +
            "to change the color.";

        // When the dropdown is opened, ShowColorMenu is invoked and we can create a popup menu.
        dropdownClicked += ShowColorMenu;

        // Subscribe to the Scene View OnGUI callback so that we can draw our color swatch.
        SceneView.duringSceneGui += DrawColorSwatch;
    }

    void DrawColorSwatch(SceneView view)
    {
        // Test that this callback is for the Scene View that we're interested in, and also check if the toggle is on
        // or off (value).
        if (view != containerWindow || !value)
            return;

        Handles.BeginGUI();
        GUI.color = colors[colorIndex];
        GUI.DrawTexture(new Rect(8, 8, 120, 24), Texture2D.whiteTexture);
        GUI.color = Color.white;
        Handles.EndGUI();
    }

    // When the dropdown button is clicked, this method will create a popup menu at the mouse cursor position.
    void ShowColorMenu()
    {
        var menu = new GenericMenu();
        menu.AddItem(new GUIContent("Red"), colorIndex == 0, () => colorIndex = 0);
        menu.AddItem(new GUIContent("Green"), colorIndex == 1, () => colorIndex = 1);
        menu.AddItem(new GUIContent("Blue"), colorIndex == 2, () => colorIndex = 2);
        menu.ShowAsContext();
    }
}

[Overlay(typeof(SceneView), "Placement Tools")]
public class GizmoToolOverlay : Overlay
{ 
    internal static class Styles
    {
        public static StyleSheet buttonStyleSheet;

        static Styles()
        {
            buttonStyleSheet = AssetDatabase.LoadAssetAtPath<StyleSheet>("Packages/com.unity.gizmoextensions/Editor/Styles/buttonStyleSheet.uss");
        }
    }
    
    private ColorField m_TextColorField;
    private ColorField m_GizmoLineColor;
    private ColorField m_GizmoPlaneColor;
    
    private VisualElement m_GizmoSettings;
    
    public override VisualElement CreatePanelContent()
    {
        m_GizmoSettings = new VisualElement();
         
        var toolContainer = new VisualElement();
        var translateButton = new Button(SetTransformTool);
        var rotateButton = new Button(SetRotateTool);
        var scaleButton = new Button(SetScaleTool);
        
        translateButton.styleSheets.Add(Styles.buttonStyleSheet);
        rotateButton.styleSheets.Add(Styles.buttonStyleSheet);
        scaleButton.styleSheets.Add(Styles.buttonStyleSheet);
        translateButton.AddToClassList("move-button");
        rotateButton.AddToClassList("rotate-button");
        scaleButton.AddToClassList("scale-button");

        toolContainer.styleSheets.Add(Styles.buttonStyleSheet);
        toolContainer.AddToClassList("Button"); 
        toolContainer.style.flexWrap = Wrap.Wrap;
        toolContainer.style.minHeight = 32;
        toolContainer.style.flexDirection = FlexDirection.Row;
        
        toolContainer.Add(translateButton);
        toolContainer.Add(rotateButton);
        toolContainer.Add(scaleButton);
        
        m_GizmoSettings.Add(toolContainer);
        
        m_TextColorField = new ColorField("Gizmo Text Color");
        m_TextColorField.value = GizmoExtensions.Styles.textColor;
        m_TextColorField.RegisterCallback<ChangeEvent<Color>>(ChangeGizmoTextColor);
        
        m_GizmoLineColor = new ColorField("Gizmo Line Color");
        m_GizmoLineColor.value = GizmoExtensions.Styles.activeAxisColor;
        m_GizmoLineColor.RegisterCallback<ChangeEvent<Color>>(ChangeGizmoLineColor);
        
        m_GizmoPlaneColor = new ColorField("Gizmo Plane Color");
        m_GizmoPlaneColor.value = GizmoExtensions.Styles.planeColor;
        m_GizmoPlaneColor.RegisterCallback<ChangeEvent<Color>>(ChangeGizmoPlaneColor);
        
        m_GizmoSettings.Add(m_TextColorField);
        m_GizmoSettings.Add(m_GizmoLineColor);
        m_GizmoSettings.Add(m_GizmoPlaneColor);

        IntegerField sizeField = new IntegerField("Text Size");
        sizeField.value = GizmoExtensions.m_FontSize;
        sizeField.RegisterCallback<ChangeEvent<int>>(ChangeTextSize);
        m_GizmoSettings.Add(sizeField);

        FloatField snapUnitMultiplier = new FloatField("Unit Snap Multiplier (Move)");
        snapUnitMultiplier.value = GizmoExtensions.m_UnitSnapSpacing;
        snapUnitMultiplier.RegisterCallback<ChangeEvent<float>>(ChangeUnitSnap);
        m_GizmoSettings.Add(snapUnitMultiplier);
        
        return m_GizmoSettings;
    }

    void ChangeTextSize(ChangeEvent<int> evt)
    {
        GizmoExtensions.m_FontSize = evt.newValue;
    }
    
    void ChangeUnitSnap(ChangeEvent<float> evt)
    {
        GizmoExtensions.m_UnitSnapSpacing = evt.newValue;
    }
    
    void ChangeGizmoTextColor(ChangeEvent<Color> evt)
    {
        GizmoExtensions.Styles.textColor = evt.newValue;
    }
    
    void ChangeGizmoLineColor(ChangeEvent<Color> evt)
    {
        GizmoExtensions.Styles.activeAxisColor = evt.newValue;
    }
    
    void ChangeGizmoPlaneColor(ChangeEvent<Color> evt)
    {
        GizmoExtensions.Styles.planeColor = evt.newValue;
        RotationHandle.k_RotationPieColor = new Color(GizmoExtensions.Styles.planeColor.r, GizmoExtensions.Styles.planeColor.g, GizmoExtensions.Styles.planeColor.b, 1f);
    }

    void SetTransformTool()
    {
        GizmoExtensions.m_ActiveTool = GizmoExtensions.ActiveTool.Translate;
    }
    
    void SetRotateTool()
    {
        GizmoExtensions.m_ActiveTool = GizmoExtensions.ActiveTool.Rotate;
    }
    
    void SetScaleTool()
    {
        GizmoExtensions.m_ActiveTool = GizmoExtensions.ActiveTool.Scale;
    }
}