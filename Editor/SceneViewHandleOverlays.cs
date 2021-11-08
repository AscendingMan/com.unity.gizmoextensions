using System;
using System.Numerics;
using UnityEngine;
using UnityEditor.Overlays;
using UnityEngine.UIElements;
using UnityEditor.EditorTools;
using Vector3 = UnityEngine.Vector3;
using Unity.VisualScripting;
using Quaternion = UnityEngine.Quaternion;
using Vector2 = UnityEngine.Vector2;

namespace UnityEditor
{
    // An EditorTool that shows an Overlay in the active scene view while enabled.
    [EditorTool("Transform Field Overlay", typeof(Transform))]
    class TransformOverlayTool : EditorTool
    {
        SceneViewHandleOverlays m_Overlay;

        void OnEnable()
        {
            if (Selection.count > 1)
            {
                s_TargetTransform = target as Transform;
                m_Overlay = new SceneViewHandleOverlays(s_TargetTransform);
                try
                {
                    m_Overlay.floatingPosition = Vector2.zero;
                }
                catch
                {
                    // TODO: Find why does m_Overlay.containerWindow magically become null at random times
                }
                SceneView.RemoveOverlayFromActiveView(m_Overlay);
                return;
            }
            
            s_TargetTransform = target as Transform;
            m_Overlay = new SceneViewHandleOverlays(s_TargetTransform);
            m_Overlay.SetActiveAxis(SceneViewHandleOverlays.ActiveAxes.None);
            s_Overlay = m_Overlay;
            SceneView.AddOverlayToActiveView(m_Overlay);
            m_Overlay.Undock();
        }

        void OnDisable()
        {
            try
            {
                m_Overlay.floatingPosition = Vector2.zero;
            }
            catch
            {
                // TODO: Find why does TransformOverlayTool.s_Overlay.containerWindow magically become null at random times
            }
            m_Overlay.Close();
        }

        internal static SceneViewHandleOverlays s_Overlay;
        internal static Transform s_TargetTransform;
    }

    internal class SceneViewHandleOverlays : Overlay, ITransientOverlay
    {
        const string k_StyleCommon = "Packages/com.unity.gizmoextensions/Editor/StyleSheets/TransformOverlay.uss";
        const string k_InputTextField = "unity-transform-input-overlay-textfield";
        const string k_InputField = "unity-transform-input-overlay-inputfield";
        const string k_TextFieldLabel = "unity-transform-input-overlay-label";

        private StyleSheet m_TransformOverlayStyleSheet;
        
        internal enum ActiveAxes
        {
            X,
            Y,
            Z,
            XY,
            XZ,
            YZ,
            XYZ,
            None
        }

        private ActiveAxes currentAxis;
        private ActiveAxes lastAxis;

        internal void SetActiveAxis(ActiveAxes axis)
        {
            currentAxis = axis;
        }

        private static Vector3 s_Deltas;

        private VisualElement m_GizmoSettings;
        private TextField m_InputFieldX;
        private TextField m_InputFieldY;
        private TextField m_InputFieldZ;

        private Transform m_TargetTransform;
        
        public bool visible => true;

        public override void OnWillBeDestroyed()
        {
            SceneView.duringSceneGui -= UpdateDisplayedValuesFromTransform;
            Undo.undoRedoPerformed -= OnUndo;
        }

        public SceneViewHandleOverlays(Transform transform)
        {
            m_TargetTransform = transform;
            collapsedChanged += OnCollapsedChanged;
            s_Deltas = new Vector3();
            m_TransformOverlayStyleSheet = AssetDatabase.LoadAssetAtPath(k_StyleCommon, typeof(StyleSheet)) as StyleSheet;
            Undo.undoRedoPerformed += OnUndo;
        }

        public static void OnUndo()
        {
            TransformOverlayTool.s_Overlay.SetActiveAxis(ActiveAxes.None);
        }

        public override VisualElement CreatePanelContent()
        {
            m_GizmoSettings = new VisualElement();

            if (currentAxis == ActiveAxes.X || currentAxis == ActiveAxes.XY || currentAxis == ActiveAxes.XZ ||
                currentAxis == ActiveAxes.XYZ)
            {
                m_InputFieldX = MakeTextField("X");
                m_InputFieldX.RegisterValueChangedCallback(DoTextField);
                m_InputFieldX.userData = 0;
                m_GizmoSettings.Add(m_InputFieldX);
            }
            if (currentAxis == ActiveAxes.Y || currentAxis == ActiveAxes.XY || currentAxis == ActiveAxes.YZ ||
                currentAxis == ActiveAxes.XYZ)
            {
                m_InputFieldY = MakeTextField("Y");
                m_InputFieldY.RegisterValueChangedCallback(DoTextField);
                m_InputFieldY.userData = 1;
                m_GizmoSettings.Add(m_InputFieldY);
            }
            if (currentAxis == ActiveAxes.Z || currentAxis == ActiveAxes.XZ || currentAxis == ActiveAxes.YZ ||
                currentAxis == ActiveAxes.XYZ)
            {
                m_InputFieldZ = MakeTextField("Z");
                m_InputFieldZ.RegisterValueChangedCallback(DoTextField);
                m_InputFieldZ.userData = 2;
                m_GizmoSettings.Add(m_InputFieldZ);
            }

            lastAxis = currentAxis;

            s_Deltas = GetCurrentTransformValueForTool();
            SceneView.duringSceneGui += UpdateDisplayedValuesFromTransform;
            return m_GizmoSettings;
        }

        TextField MakeTextField(string label)
        {
            var textField = new TextField(label);
            textField.isDelayed = true;
            textField.name = $"Label{label}-Root";
            textField.styleSheets.Add(m_TransformOverlayStyleSheet);
            textField.AddToClassList(k_InputTextField);

            var labelElement = textField.Q<Label>();
            labelElement.AddToClassList(k_TextFieldLabel);
            labelElement.AddToClassList($"{k_TextFieldLabel}-{label.ToLower()}");

            var inputField = textField.Q<VisualElement>(name: "unity-text-input");
            inputField.AddToClassList(k_InputField);

            return textField;
        }

        public override void OnCreated()
        {
            UpdateHeaderAndBackground();
        }

        void OnCollapsedChanged(bool collapsed)
        {
            UpdateHeaderAndBackground();
        }

        void UpdateHeaderAndBackground()
        {
            var rootElement = (VisualElement)HandleUtils.rootVisualElement.GetValue(this);
            var headerElementImage = rootElement.Q(className: "overlay-header").Q(name: "unity-overlay-collapse__dragger");
            var headerElementLabel = rootElement.Q(className: "overlay-header").Q<Label>(name: "overlay-header__title");
            var overlayBackground = rootElement.Q(name: "unity-overlay");
            if (overlayBackground != null)
            {
                overlayBackground.focusable = false;
                overlayBackground.style.backgroundColor = new Color(1, 1, 1, 0);
            }
            
            if (headerElementImage != null)
            {
                headerElementImage.style.backgroundImage = null;
                headerElementImage.focusable = false;
            }
            
            if (headerElementLabel != null)
            {
                headerElementLabel.style.backgroundImage = null;
                headerElementLabel.text = "";
            }
        }

        void DoTextField(ChangeEvent<string> evt)
        {
            float updatedValue;
            var exprOk = ExpressionEvaluator.Evaluate(evt.newValue, out updatedValue);
            var inputField = evt.target as TextField;
            var axisIndex = (int)inputField.userData;

            if (!exprOk)
            {
                inputField.value = 0.ToString();
                return;
            }

            inputField.value = updatedValue.ToString();
            UpdateTargetObjectTransform(updatedValue, axisIndex);
        }

        void UpdateTargetObjectTransform(float newValue, int axis)
        {
            var dragLockState = (int) HandleUtils.draggingLocked.GetValue(SceneView.lastActiveSceneView);
            if (dragLockState == (int)HandleUtils.DraggingLockedState.Dragging)
                 return;

            switch (Tools.current)
            {
                case (Tool.Move):
                    if (m_TargetTransform == null)
                        break;
                    m_TargetTransform.position = GetNewTransformValueBasedOnAxis(m_TargetTransform.position, newValue, axis);
                    break;
                case (Tool.Rotate):
                    m_TargetTransform.rotation = Quaternion.Euler(GetNewTransformValueBasedOnAxis(m_TargetTransform.rotation.eulerAngles, newValue, axis));
                    break;
                case (Tool.Scale):
                    if (m_TargetTransform == null)
                        break;
                    m_TargetTransform.localScale = GetNewTransformValueBasedOnAxis(m_TargetTransform.localScale, newValue, axis);
                    break;
            }
        }

        Vector3 GetCurrentTransformValueForTool()
        {
            if (m_TargetTransform == null)
                return Vector3.zero;

            switch (Tools.current)
            {
                case (Tool.Move):
                    return m_TargetTransform.position;
                case (Tool.Rotate):
                    return m_TargetTransform.rotation.eulerAngles;
                case (Tool.Scale):
                    return m_TargetTransform.localScale;
                default:
                    return Vector3.zero;
            }
        }

        internal Vector3 GetAxisVector()
        {
            var index = (int) currentAxis;
            if (index < 3)
                return (Vector3)HandleUtils.GetAxisVector.Invoke(null, new object[] {index});
            else
            {
                switch (index)
                {
                    case(3):
                        return (Vector3)HandleUtils.GetAxisVector.Invoke(null, new object[] {1});
                    case(4):
                        return (Vector3)HandleUtils.GetAxisVector.Invoke(null, new object[] {2});
                    case(5):
                        return (Vector3)HandleUtils.GetAxisVector.Invoke(null, new object[] {2});
                    case(6):
                        return (Vector3)HandleUtils.GetAxisVector.Invoke(null, new object[] {1});
                    default:
                        return Vector3.zero;
                }
            }
        }

        internal static void SetFloatingPosition()
        {
            var camera = SceneView.lastActiveSceneView?.camera;
            if (camera == null || TransformOverlayTool.s_Overlay == null)
                return;
            
            var cameraToObjectAngle = Vector3.Dot(Tools.handlePosition, camera.transform.position - Tools.handlePosition);

            var hotHandleDirection = TransformOverlayTool.s_Overlay.GetAxisVector() * HandleUtility.GetHandleSize(Tools.handlePosition);
            var offset = cameraToObjectAngle < 0 ? hotHandleDirection * .5f : hotHandleDirection;
            
            // figure out why WorldToScreenPoint returns a value that's too large on OSX
            // there should be some screen-related variable that elimites the need to hard code this
            var worldToScreenMult = Application.platform == RuntimePlatform.OSXEditor ? 0.5f : 1f;
            var floatingPos = camera.WorldToScreenPoint(Tools.handlePosition + offset);
            try
            {
                TransformOverlayTool.s_Overlay.floatingPosition =
                    new Vector2(floatingPos.x, camera.pixelHeight - floatingPos.y) * worldToScreenMult;
            }
            catch
            {
                // TODO: Find why does TransformOverlayTool.s_Overlay.containerWindow magically become null at random times
            }
        }
        
        static void UpdateDisplayedValuesFromTransform(SceneView sceneView)
        {
            if (TransformOverlayTool.s_Overlay == null)
                return;
            
            switch (Tools.current)
            {
                case (Tool.Move):
                    SetHotAxis();
                    break;
                case (Tool.Rotate):
                    SetHotAxis();
                    break;
                case (Tool.Scale):
                    SetHotAxis();
                    break;
                default:
                    break;
            }
            
            var draggingLocked = (int)HandleUtils.draggingLocked.GetValue(sceneView);
            if (draggingLocked != (int)HandleUtils.DraggingLockedState.Dragging)
                return;

            if (TransformOverlayTool.s_TargetTransform == null)
                return;
            
            SetFloatingPosition();
            
            Vector3 newValue = Vector3.zero;
            switch (Tools.current)
            {
                case (Tool.Move):
                    newValue = TransformOverlayTool.s_TargetTransform.position;
                    break;
                case (Tool.Rotate):
                    newValue = TransformOverlayTool.s_TargetTransform.rotation.eulerAngles;
                    break;
                case (Tool.Scale):
                    newValue = TransformOverlayTool.s_TargetTransform.localScale;
                    break;
            }

            if (TransformOverlayTool.s_Overlay.m_InputFieldX != null)
                TransformOverlayTool.s_Overlay.m_InputFieldX.value = newValue.x.ToString();
            if (TransformOverlayTool.s_Overlay.m_InputFieldY != null)
                TransformOverlayTool.s_Overlay.m_InputFieldY.value = newValue.y.ToString();
            if (TransformOverlayTool.s_Overlay.m_InputFieldZ != null)
                TransformOverlayTool.s_Overlay.m_InputFieldZ.value = newValue.z.ToString();

            if (TransformOverlayTool.s_Overlay.currentAxis != TransformOverlayTool.s_Overlay.lastAxis)
                s_Deltas = newValue;
        }

        Vector3 GetNewTransformValueBasedOnAxis(Vector3 currentValue, float newValue, int axis)
        {
            if (axis == 0)
                return new Vector3(newValue, currentValue.y, currentValue.z);
            if (axis == 1)
                return new Vector3(currentValue.x, newValue, currentValue.z);
            if (axis == 2)
                return new Vector3(currentValue.x, currentValue.y, newValue);

            return currentValue;
        }

        internal static void SetHotAxis(Vector3 start, Vector3 current)
        {
            bool changedX = Math.Abs(start.x - current.x) > 0.0001f;
            bool changedY = Math.Abs(start.y - current.y) > 0.0001f;
            bool changedZ = Math.Abs(start.z - current.z) > 0.0001f;
            
            if(changedX && !changedY && !changedZ)
                TransformOverlayTool.s_Overlay.currentAxis = ActiveAxes.X;
            else if(!changedX && changedY && !changedZ)
                TransformOverlayTool.s_Overlay.currentAxis = ActiveAxes.Y;
            else if(!changedX && !changedY && changedZ)
                TransformOverlayTool.s_Overlay.currentAxis = ActiveAxes.Z;
            else if (changedX && changedY && !changedZ)
                TransformOverlayTool.s_Overlay.currentAxis = ActiveAxes.XY;
            else if (changedX && !changedY && changedZ)
                TransformOverlayTool.s_Overlay.currentAxis = ActiveAxes.XZ;
            else if (!changedX && changedY && changedZ)
                TransformOverlayTool.s_Overlay.currentAxis = ActiveAxes.YZ;
            else if (changedX && changedY && changedZ)
                TransformOverlayTool.s_Overlay.currentAxis = ActiveAxes.XYZ;
        }
        
        internal static void SetHotAxis()
        {
            var handleHasMoved = (bool)HandleUtils.HandleHasMoved.Invoke(null, new object[]{TransformOverlayTool.s_TargetTransform.position});
            var sceneLockState = (int) HandleUtils.draggingLocked.GetValue(SceneView.lastActiveSceneView);
            if(sceneLockState == (int)HandleUtils.DraggingLockedState.Dragging && handleHasMoved && s_Deltas != TransformOverlayTool.s_TargetTransform.position)
                SetHotAxis(s_Deltas, TransformOverlayTool.s_TargetTransform.position);
            
            if (TransformOverlayTool.s_Overlay.currentAxis != TransformOverlayTool.s_Overlay.lastAxis)
            {
                HandleUtils.RebuildContent.Invoke(TransformOverlayTool.s_Overlay, new object[] { });
                TransformOverlayTool.s_Overlay.lastAxis = TransformOverlayTool.s_Overlay.currentAxis;
            }
        }
    }
}
